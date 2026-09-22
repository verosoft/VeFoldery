using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using FileOrganizer.Models;

namespace TimeFold.Avalonia;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Swaps the app-wide Linear theme at runtime and persists the choice.
/// FluentTheme stays loaded underneath (it styles control templates);
/// the Linear theme file overrides its colors on top.
/// </summary>
public static class ThemeManager
{
    private const string DarkUri = "avares://TimeFold.Avalonia/Themes/LinearDark.axaml";
    private const string LightUri = "avares://TimeFold.Avalonia/Themes/LinearLight.axaml";

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void Initialize(AppSettings settings)
    {
        Current = Enum.TryParse<AppTheme>(settings.UiTheme, ignoreCase: true, out var t)
            ? t : AppTheme.Dark;
        Apply(Current);
    }

    public static void SetTheme(AppTheme theme, AppSettings settings)
    {
        if (theme == Current) return;
        Current = theme;
        settings.UiTheme = theme.ToString();
        settings.SaveToFile();
        Apply(theme);
    }

    private static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        // Remove previously loaded Linear theme includes (if any).
        var stale = app.Styles.OfType<StyleInclude>()
            .Where(si => si.Source?.ToString()?.Contains("/Themes/Linear") == true)
            .ToList();
        foreach (var si in stale)
            app.Styles.Remove(si);

        var uri = theme == AppTheme.Light ? LightUri : DarkUri;
        app.Styles.Add(new StyleInclude(new Uri("avares://TimeFold.Avalonia/"))
        {
            Source = new Uri(uri)
        });

        // Flip Avalonia's own variant so Fluent-drawn pieces (popups, focus
        // rings, scrollbars) follow the same look.
        app.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}