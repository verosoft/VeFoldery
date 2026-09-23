using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeFoldery.Core.Models;
using VeFoldery.Core.Services;

namespace VeFoldery.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private FileOrganizerService? _service;
    private AppSettings Settings { get; } = AppSettings.LoadFromFile();

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private OrganizationMode _selectedMode = OrganizationMode.Date;

    [ObservableProperty]
    private bool _includeFolders = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "Choose a folder to preview its organization.";

    [ObservableProperty]
    private string _organizeButtonText = "Organize";

    [ObservableProperty]
    private ObservableCollection<FileEntryViewModel> _files = new();

    public System.Collections.Generic.IEnumerable<string> ThemeOptions { get; } =
        new[] { "Dark", "Light" };

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    partial void OnSelectedThemeChanged(string value)
    {
        if (Enum.TryParse<AppTheme>(value, ignoreCase: true, out var theme))
            ThemeManager.SetTheme(theme, Settings);
    }

    public bool HasFolder => !string.IsNullOrWhiteSpace(FolderPath) && Directory.Exists(FolderPath);

    public bool CanOrganize => Files.Count > 0 && !IsLoading;

    /// <summary>
    /// View-provided confirmation gate for the Organize action.
    /// Receives a human summary; returns true to proceed.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmOrganize { get; set; }

    /// <summary>
    /// View-provided conflict dialog gate. Receives detected conflicts;
    /// returns the chosen strategy, or null to cancel the run.
    /// </summary>
    public Func<System.Collections.Generic.IReadOnlyList<ConflictInfo>,
        Task<ConflictResolutionStrategy?>>? ConfirmConflicts { get; set; }

    public MainViewModel()
    {
        // Restore persisted org mode. The startup folder is restored via
        // RestoreStartupFolder() so it always goes through the public
        // property and triggers the scan, even when it equals the
        // most-recent folder persisted from a previous session.
        _selectedMode = Settings.OrgMode;
        _selectedTheme = Settings.UiTheme;
        ThemeManager.Initialize(Settings);
    }

    /// <summary>
    /// Opens the CLI-provided folder, falling back to the most recent one.
    /// Must run after construction so FolderPath change handlers fire.
    /// </summary>
    public void RestoreStartupFolder(string? cliArg)
    {
        var candidate = cliArg;
        if (string.IsNullOrWhiteSpace(candidate) && Settings.RecentFolders.Count > 0)
            candidate = Settings.RecentFolders[0];
        if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            FolderPath = Path.GetFullPath(candidate);
    }

    partial void OnFolderPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasFolder));
        if (HasFolder)
        {
            Settings.AddRecentFolder(value);
            _ = ScanCoreAsync();
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOrganize));
        OrganizeButtonText = value ? "Working…" : "Organize";
    }

    partial void OnSelectedModeChanged(OrganizationMode value)
    {
        Settings.OrgMode = value;
        Settings.SaveToFile();
        if (HasFolder && Files.Count > 0) _ = ScanCoreAsync();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await StorageProviderHelper.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
            FolderPath = path;
    }

    [RelayCommand]
    private void RevealFolder()
    {
        if (HasFolder) StorageProviderHelper.RevealInFileManager(FolderPath);
    }

    [RelayCommand]
    private Task RescanAsync() => ScanCoreAsync();

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (IsLoading || !HasFolder) return;

        var logs = UndoService.GetUndoableLogs(FolderPath);
        if (logs.Count == 0)
        {
            StatusText = "Nothing to undo: no audit logs in this folder.";
            return;
        }

        string logPath = logs[0]; // newest run

        // Safety gate: restoring moves files too; confirm first.
        var summary = $"Undo the last organization run in \"{FolderPath}\"?\n\n" +
                      "Every file listed in the newest audit log will be moved " +
                      "back to its original location. Existing files are never overwritten.";
        if (ConfirmOrganize is not null && !await ConfirmOrganize(summary))
        {
            StatusText = "Undo cancelled.";
            return;
        }

        IsLoading = true;
        StatusText = "Undoing…";

        try
        {
            var progress = new Progress<(int current, int total, string name)>(p =>
                StatusText = $"Restoring {p.current}/{p.total}: {p.name}");

            var result = await UndoService.UndoFromLogAsync(logPath, progress);

            await ScanCoreAsync(preserveSelection: false);

            StatusText = $"Undo done: {result.FilesRestored} restored, {result.FilesSkipped} skipped" +
                         (result.Errors > 0 ? $", {result.Errors} errors" : "") +
                         (result.RemovedFolders.Count > 0 ? $" — removed {result.RemovedFolders.Count} empty Sorted folder(s)" : "");
        }
        catch (Exception ex)
        {
            StatusText = $"Undo error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OrganizeAsync()
    {
        if (!CanOrganize || _service is null) return;

        var toOrganize = Files.Where(f => f.Include).Select(f => f.Item).ToList();
        if (toOrganize.Count == 0)
        {
            StatusText = "Nothing selected to organize.";
            return;
        }

        // Detect collisions before touching anything: dialog when starting a run.
        var grouped = _service.GroupByMonthYear(toOrganize);
        var conflicts = ConflictDetector.Detect(
            toOrganize, grouped, FolderPath,
            Settings.CreateSortedSubfolder, Settings.Use24HourTimestamp);

        var strategy = ConflictResolutionStrategy.AutoRename;
        if (conflicts.Count > 0 && ConfirmConflicts is not null)
        {
            var chosen = await ConfirmConflicts(conflicts);
            if (chosen is null)
            {
                StatusText = "Organize cancelled (conflicts).";
                return;
            }
            strategy = chosen.Value;
        }

        // Safety gate: moving files is destructive-ish; confirm first.
        var summary = $"{toOrganize.Count} item(s) in \"{FolderPath}\" will be moved into " +
                      $"\"{VeFoldery.Core.Config.AppConstants.SortedFolderPrefix}…\" subfolders " +
                      $"(mode: {SelectedMode}). A CSV audit log will be written.";
        if (ConfirmOrganize is not null && !await ConfirmOrganize(summary))
        {
            StatusText = "Organize cancelled.";
            return;
        }

        IsLoading = true;
        StatusText = "Organizing…";

        try
        {
            var progress = new Progress<(int current, int total, string currentFile)>(p =>
                StatusText = $"Moving {p.current}/{p.total}: {p.currentFile}");

            var result = await _service.OrganizeFilesAsync(
                toOrganize, progress, CancellationToken.None,
                generateCsvLog: true,
                conflictStrategy: strategy,
                collidingFilePaths: new System.Collections.Generic.HashSet<string>(
                    conflicts.Select(c => c.Item.FullPath),
                    System.StringComparer.OrdinalIgnoreCase));

            await ScanCoreAsync(preserveSelection: false);

            StatusText = $"Done: {result.FilesMoved} moved, {result.FilesSkipped} skipped" +
                         (result.Errors > 0 ? $", {result.Errors} errors" : "") +
                         $" → {result.SortedFolderPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ScanCoreAsync(bool preserveSelection = true)
    {
        if (!HasFolder) return;

        IsLoading = true;
        StatusText = "Scanning…";

        try
        {
            var keep = preserveSelection
                ? Files.Where(f => f.Include).Select(f => f.Name).ToHashSet()
                : new HashSet<string>();

            _service = new FileOrganizerService(
                executablePath: Environment.ProcessPath ?? "VeFoldery",
                workingDirectory: FolderPath,
                outputDirectory: FolderPath);

            // Persisted naming/mode settings shared across versions of the app.
            _service.ApplyNamingSettings(
                format: Settings.FolderFormat,
                prefix: Settings.FolderPrefix,
                suffix: Settings.FolderSuffix,
                use24Hour: Settings.Use24HourTimestamp,
                mode: SelectedMode,
                keepHtmlCompanions: Settings.KeepHtmlCompanionsTogether,
                categoryPrefix: Settings.CategoryPrefix,
                categorySuffix: Settings.CategorySuffix,
                keepSubtitleCompanions: Settings.KeepSubtitleCompanionsTogether,
                createSortedSubfolder: Settings.CreateSortedSubfolder);

            // Offload the directory scan to a worker thread to keep the UI free.
            var scanned = await Task.Run(() =>
                _service.ScanFiles(
                    includeTopLevelFolders: IncludeFolders,
                    ignoreSystemFiles: Settings.IgnoreSystemFiles,
                    fileDateSource: Settings.FileDateSource,
                    folderDateSource: Settings.FolderDateSource));

            Files = new ObservableCollection<FileEntryViewModel>(
                scanned.Select(f => new FileEntryViewModel(f)
                {
                    Include = keep.Count == 0 || keep.Contains(f.Name)
                }));

            OnPropertyChanged(nameof(CanOrganize));
            StatusText = $"{scanned.Count} item(s) — {Files.Count(f => f.Include)} selected for organizing";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}