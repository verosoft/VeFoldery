using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FileOrganizer.Models;

namespace TimeFold.Avalonia.Views;

public static class Dialogs
{
    /// <summary>
    /// Minimal modal confirm dialog (Avalonia has no built-in MessageBox).
    /// Returns true only when the user clicks the confirm button.
    /// </summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message,
        string confirmLabel = "OK", string cancelLabel = "Cancel")
    {
        var text = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var confirmButton = new Button
        {
            Content = confirmLabel,
            Padding = new Thickness(20, 6),
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = cancelLabel,
            Padding = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancelButton, confirmButton }
        };

        var panel = new StackPanel { Margin = new Thickness(20), Children = { text, buttons } };
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = panel
        };

        var tcs = new TaskCompletionSource<bool>();

        confirmButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    /// <summary>
    /// Row model for the conflicts list.
    /// </summary>
    public class ConflictRow
    {
        public string ItemName { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Target { get; init; } = "";
        public string Conflict { get; init; } = "";
        public string Proposed { get; init; } = "";
    }

    /// <summary>
    /// Modal conflict dialog, equivalent of the WinForms ConflictDialog:
    /// lists detected collisions and asks for a resolution strategy.
    /// Returns null when cancelled.
    /// </summary>
    public static async Task<ConflictResolutionStrategy?> ShowConflictsAsync(
        Window owner, IReadOnlyList<ConflictInfo> conflicts)
    {
        var title = new TextBlock
        {
            Text = $"⚠ {conflicts.Count} conflict{(conflicts.Count == 1 ? "" : "s")} detected",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        var subtitle = new TextBlock
        {
            Text = "TimeFold will never touch or overwrite existing folders/files. " +
                   "Colliding items can be auto-renamed or skipped.",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 680,
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var rows = conflicts.Select(c => new ConflictRow
        {
            ItemName = c.Item?.Name ?? "",
            Kind = c.Item?.IsDirectory == true ? "Folder" : "File",
            Target = c.TargetFolder,
            Conflict = c.Description,
            Proposed = c.ProposedAction
        }).ToList();

        // Compact height: header row + one row per conflict, capped.
        var listHeight = Math.Min(34 + rows.Count * 28, 280);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Height = listHeight,
            ItemsSource = new DataGridCollectionView(rows)
        };
        grid.Columns.Add(new DataGridTextColumn
            { Header = "Item", Binding = new global::Avalonia.Data.Binding("ItemName"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn
            { Header = "Kind", Binding = new global::Avalonia.Data.Binding("Kind"), Width = new DataGridLength(60) });
        grid.Columns.Add(new DataGridTextColumn
            { Header = "Target", Binding = new global::Avalonia.Data.Binding("Target"), Width = new DataGridLength(130) });
        grid.Columns.Add(new DataGridTextColumn
            { Header = "Conflict", Binding = new global::Avalonia.Data.Binding("Conflict"), Width = new DataGridLength(220) });
        grid.Columns.Add(new DataGridTextColumn
            { Header = "Proposed", Binding = new global::Avalonia.Data.Binding("Proposed"), Width = new DataGridLength(240) });

        var renameButton = new Button
        {
            Content = "🛡 Auto-rename colliding items",
            Padding = new Thickness(16, 6),
            FontWeight = FontWeight.SemiBold,
            Classes = { "primary" }
        };
        var skipButton = new Button
        {
            Content = "⏭ Skip colliding items",
            Padding = new Thickness(16, 6)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 6)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 14, 0, 0),
            Children = { cancelButton, skipButton, renameButton }
        };

        var panel = new StackPanel { Margin = new Thickness(20), Children = { title, subtitle, grid, buttons } };

        var dialog = new Window
        {
            Title = "Potential conflicts & collisions",
            Width = 720,
            Height = listHeight + 190,
            SizeToContent = SizeToContent.Manual,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = panel
        };

        var tcs = new TaskCompletionSource<ConflictResolutionStrategy?>();
        renameButton.Click += (_, _) => { tcs.TrySetResult(ConflictResolutionStrategy.AutoRename); dialog.Close(); };
        skipButton.Click += (_, _) => { tcs.TrySetResult(ConflictResolutionStrategy.Skip); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(null); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(null);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}