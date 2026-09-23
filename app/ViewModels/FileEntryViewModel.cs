using System;
using CommunityToolkit.Mvvm.ComponentModel;
using VeFoldery.Core.Models;

namespace VeFoldery.Avalonia.ViewModels;

public partial class FileEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _include = true;

    public FileItem Item { get; }

    public string Name => Item.Name;

    public string TypeDisplay => Item.TypeDisplay;

    public string TargetFolder => Item.TargetFolder;

    public string SizeDisplay => Item.IsDirectory
        ? "—"
        : Item.Size switch
        {
            >= 1_073_741_824 => $"{Item.Size / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{Item.Size / 1_048_576.0:F1} MB",
            >= 1_024 => $"{Item.Size / 1_024.0:F1} KB",
            _ => $"{Item.Size} B"
        };

    public string ModifiedDisplay => Item.ModifiedDate.ToString("yyyy-MM-dd HH:mm");

    public FileEntryViewModel(FileItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }
}