using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VeFoldery.Core.Models;

namespace VeFoldery.Avalonia.ViewModels;

/// <summary>
/// Editable mirror of AppSettings for the Preferences window.
/// On Save, values are written back to the shared settings.json and
/// applied to the live MainViewModel where relevant.
/// </summary>
public partial class PreferencesViewModel : ViewModelBase
{
    private readonly AppSettings _settings;

    // --- Date format ---
    public IReadOnlyList<FolderFormatItem> FolderFormats { get; } =
        Enum.GetValues<FolderFormat>().Select(f => new FolderFormatItem(f)).ToList();

    [ObservableProperty]
    private FolderFormatItem _selectedFormat;

    // --- Date source ---
    public IReadOnlyList<DateSourceItem> DateSources { get; } =
        new[]
        {
            new DateSourceItem(DateSource.Modified, "Date Modified"),
            new DateSourceItem(DateSource.Created, "Date Created"),
            new DateSourceItem(DateSource.Earliest, "Earliest date")
        };

    [ObservableProperty]
    private DateSourceItem _selectedFileDateSource;

    [ObservableProperty]
    private DateSourceItem _selectedFolderDateSource;

    // --- Toggles ---
    [ObservableProperty]
    private bool _use24HourTimestamp;

    [ObservableProperty]
    private bool _createSortedSubfolder;

    [ObservableProperty]
    private bool _keepHtmlCompanionsTogether;

    [ObservableProperty]
    private bool _keepSubtitleCompanionsTogether;

    [ObservableProperty]
    private bool _ignoreSystemFiles;

    // --- Naming ---
    [ObservableProperty]
    private string _folderPrefix = string.Empty;

    [ObservableProperty]
    private string _folderSuffix = string.Empty;

    [ObservableProperty]
    private string _categoryPrefix = string.Empty;

    [ObservableProperty]
    private string _categorySuffix = string.Empty;

    // --- Misc ---
    [ObservableProperty]
    private int _maxPreviewItems;

    public PreferencesViewModel(AppSettings settings)
    {
        _settings = settings;
        _selectedFormat = FolderFormats.FirstOrDefault(f => f.Value == settings.FolderFormat)
                          ?? FolderFormats[0];
        _selectedFileDateSource = DateSources.FirstOrDefault(d => d.Value == settings.FileDateSource)
                                  ?? DateSources[0];
        _selectedFolderDateSource = DateSources.FirstOrDefault(d => d.Value == settings.FolderDateSource)
                                    ?? DateSources[0];
        _use24HourTimestamp = settings.Use24HourTimestamp;
        _createSortedSubfolder = settings.CreateSortedSubfolder;
        _keepHtmlCompanionsTogether = settings.KeepHtmlCompanionsTogether;
        _keepSubtitleCompanionsTogether = settings.KeepSubtitleCompanionsTogether;
        _ignoreSystemFiles = settings.IgnoreSystemFiles;
        _folderPrefix = settings.FolderPrefix;
        _folderSuffix = settings.FolderSuffix;
        _categoryPrefix = settings.CategoryPrefix;
        _categorySuffix = settings.CategorySuffix;
        _maxPreviewItems = settings.MaxPreviewItems;
    }

    public void Save()
    {
        _settings.FolderFormat = SelectedFormat.Value;
        _settings.FileDateSource = SelectedFileDateSource.Value;
        _settings.FolderDateSource = SelectedFolderDateSource.Value;
        _settings.Use24HourTimestamp = Use24HourTimestamp;
        _settings.CreateSortedSubfolder = CreateSortedSubfolder;
        _settings.KeepHtmlCompanionsTogether = KeepHtmlCompanionsTogether;
        _settings.KeepSubtitleCompanionsTogether = KeepSubtitleCompanionsTogether;
        _settings.IgnoreSystemFiles = IgnoreSystemFiles;
        _settings.FolderPrefix = FolderPrefix?.Trim() ?? string.Empty;
        _settings.FolderSuffix = FolderSuffix?.Trim() ?? string.Empty;
        _settings.CategoryPrefix = CategoryPrefix?.Trim() ?? string.Empty;
        _settings.CategorySuffix = CategorySuffix?.Trim() ?? string.Empty;
        _settings.MaxPreviewItems = Math.Clamp(MaxPreviewItems, 10, 10000);
        _settings.SaveToFile();
    }
}

/// <summary>Combo item for FolderFormat (enum + human label).</summary>
public class FolderFormatItem
{
    public FolderFormat Value { get; }
    public string Label { get; }

    public FolderFormatItem(FolderFormat value)
    {
        Value = value;
        Label = Value switch
        {
            FolderFormat.YearMonth => "2026 September",
            FolderFormat.MonthYear => "September 2026",
            FolderFormat.YearShortMonth => "2026 Sep",
            FolderFormat.ShortMonthYear => "Sep 2026",
            FolderFormat.IsoMonth => "2026-09",
            FolderFormat.MonthIso => "09-2026",
            FolderFormat.IsoDate => "2026-09-23",
            FolderFormat.YearMonthDay => "2026 September 23",
            FolderFormat.DayMonthYear => "23 September 2026",
            FolderFormat.YearShortMonthDay => "2026 Sep 23",
            FolderFormat.DayShortMonthYear => "23 Sep 2026",
            FolderFormat.YearQuarter => "2026 Q3",
            FolderFormat.QuarterYear => "Q3 2026",
            FolderFormat.YearQuarterMonths => "2026 Q3 (July, August & September)",
            FolderFormat.YearQuarterShortMonths => "2026 Q3 (Jul, Aug & Sep)",
            FolderFormat.YearHalf => "2026 H2",
            FolderFormat.HalfYear => "H2 2026",
            FolderFormat.YearOnly => "2026",
            FolderFormat.YearWithMonth => "2026 / 2026 September",
            FolderFormat.YearWithShortMonth => "2026 / 2026 Sep",
            FolderFormat.YearWithMonthFlipped => "2026 / September 2026",
            FolderFormat.YearWithShortMonthFlipped => "2026 / Sep 2026",
            FolderFormat.YearWithMonthOnly => "2026 / September",
            FolderFormat.YearWithShortMonthOnly => "2026 / Sep",
            FolderFormat.YearWithIsoMonth => "2026 / 2026-09",
            FolderFormat.YearWithIsoMonthFlipped => "2026 / 09-2026",
            FolderFormat.YearWithQuarter => "2026 / 2026 Q3",
            FolderFormat.YearWithQuarterFlipped => "2026 / Q3 2026",
            FolderFormat.YearWithHalf => "2026 / 2026 H2",
            FolderFormat.YearWithHalfFlipped => "2026 / H2 2026",
            _ => Value.ToString()
        };
    }
}

public class DateSourceItem
{
    public DateSource Value { get; }
    public string Label { get; }

    public DateSourceItem(DateSource value, string label)
    {
        Value = value;
        Label = label;
    }
}