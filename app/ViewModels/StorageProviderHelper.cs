using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace VeFoldery.Avalonia.ViewModels;

/// <summary>
/// Bridges view-model commands to Avalonia storage/window APIs that need a
/// visual window or top-level. The views register themselves here on init.
/// </summary>
public static class StorageProviderHelper
{
    private static Func<TopLevel?>? _topLevelProvider;

    public static void Register(Func<TopLevel?> provider) => _topLevelProvider = provider;

    public static async Task<string> PickFolderAsync()
    {
        var topLevel = _topLevelProvider?.Invoke();
        if (topLevel is null) return string.Empty;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder to organize",
            AllowMultiple = false
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() ?? string.Empty : string.Empty;
    }

    public static void RevealInFileManager(string path)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", $"-R \"{path}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Reveal is not standardized; open the containing folder instead.
                var target = System.IO.Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path) ?? path;
                System.Diagnostics.Process.Start("xdg-open", $"\"{target}\"");
            }
            else if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Best-effort: no-op when no file manager is available.
        }
    }
}