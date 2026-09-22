using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileOrganizer.Models;
using FileOrganizer.Services;

namespace TimeFold.Avalonia.ViewModels;

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

    public bool HasFolder => !string.IsNullOrWhiteSpace(FolderPath) && Directory.Exists(FolderPath);

    public bool CanOrganize => Files.Count > 0 && !IsLoading;

    /// <summary>
    /// View-provided confirmation gate for the Organize action.
    /// Receives a human summary; returns true to proceed.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmOrganize { get; set; }

    public MainViewModel()
    {
        // Restore persisted org mode. The startup folder is restored via
        // RestoreStartupFolder() so it always goes through the public
        // property and triggers the scan, even when it equals the
        // most-recent folder persisted from a previous session.
        _selectedMode = Settings.OrgMode;
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
    private async Task OrganizeAsync()
    {
        if (!CanOrganize || _service is null) return;

        var toOrganize = Files.Where(f => f.Include).Select(f => f.Item).ToList();
        if (toOrganize.Count == 0)
        {
            StatusText = "Nothing selected to organize.";
            return;
        }

        // Safety gate: moving files is destructive-ish; confirm first.
        var summary = $"{toOrganize.Count} item(s) in \"{FolderPath}\" will be moved into " +
                      $"\"{FileOrganizer.Config.AppConstants.SortedFolderPrefix}…\" subfolders " +
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
                toOrganize, progress, CancellationToken.None);

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
                executablePath: Environment.ProcessPath ?? "TimeFold",
                workingDirectory: FolderPath,
                outputDirectory: FolderPath);

            // Reuse the same naming/mode settings the WinForms app persists,
            // so both frontends behave identically for the same user config.
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