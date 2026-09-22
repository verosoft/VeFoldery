using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TimeFold.Avalonia.Views;

public static class Dialogs
{
    /// <summary>
    /// Minimal modal confirm dialog (Avalonia has no built-in MessageBox).
    /// Returns true only when the user clicks "Organize".
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
}