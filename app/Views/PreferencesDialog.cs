using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VeFoldery.Avalonia.ViewModels;
using VeFoldery.Core.Models;

namespace VeFoldery.Avalonia.Views;

/// <summary>
/// Modal Preferences window: date format/source, toggles and naming options.
/// Built in code (like Dialogs) so it inherits the active theme automatically.
/// </summary>
public static class PreferencesDialog
{
    /// <summary>
    /// Shows the preferences editor. Returns true when the user saved changes.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, AppSettings settings)
    {
        var vm = new PreferencesViewModel(settings);

        var title = new TextBlock
        {
            Text = "Preferences",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // --- Section: date organization ---
        var formatLabel = new TextBlock { Text = "Date folder format:", VerticalAlignment = VerticalAlignment.Center };
        var formatCombo = new ComboBox
        {
            MinWidth = 300,
            ItemsSource = vm.FolderFormats,
            DisplayMemberBinding = new global::Avalonia.Data.Binding("Label"),
            SelectedItem = vm.SelectedFormat
        };
        formatCombo.SelectionChanged += (_, _) => vm.SelectedFormat = (FolderFormatItem)formatCombo.SelectedItem!;

        var fileDateLabel = new TextBlock { Text = "File date source:", VerticalAlignment = VerticalAlignment.Center };
        var fileDateCombo = new ComboBox
        {
            MinWidth = 180,
            ItemsSource = vm.DateSources,
            DisplayMemberBinding = new global::Avalonia.Data.Binding("Label"),
            SelectedItem = vm.SelectedFileDateSource
        };
        fileDateCombo.SelectionChanged += (_, _) => vm.SelectedFileDateSource = (DateSourceItem)fileDateCombo.SelectedItem!;

        var folderDateLabel = new TextBlock { Text = "Folder date source:", VerticalAlignment = VerticalAlignment.Center };
        var folderDateCombo = new ComboBox
        {
            MinWidth = 180,
            ItemsSource = vm.DateSources,
            DisplayMemberBinding = new global::Avalonia.Data.Binding("Label"),
            SelectedItem = vm.SelectedFolderDateSource
        };
        folderDateCombo.SelectionChanged += (_, _) => vm.SelectedFolderDateSource = (DateSourceItem)folderDateCombo.SelectedItem!;

        var chk24h = new CheckBox { Content = "Use 24-hour timestamps", IsChecked = vm.Use24HourTimestamp };
        chk24h.IsCheckedChanged += (_, _) => vm.Use24HourTimestamp = chk24h.IsChecked == true;

        var chkSorted = new CheckBox { Content = "Create a Sorted_ subfolder for each run", IsChecked = vm.CreateSortedSubfolder };
        chkSorted.IsCheckedChanged += (_, _) => vm.CreateSortedSubfolder = chkSorted.IsChecked == true;

        var dateSection = MakeSection("Date organization",
            Row(formatLabel, formatCombo),
            Row(fileDateLabel, fileDateCombo),
            Row(folderDateLabel, folderDateCombo),
            chk24h,
            chkSorted);

        // --- Section: naming ---
        var prefixBox = new TextBox { PlaceholderText = "e.g. IMG_", Text = vm.FolderPrefix, MinWidth = 200 };
        prefixBox.TextChanged += (_, _) => vm.FolderPrefix = prefixBox.Text ?? string.Empty;
        var suffixBox = new TextBox { PlaceholderText = "e.g. _archive", Text = vm.FolderSuffix, MinWidth = 200 };
        suffixBox.TextChanged += (_, _) => vm.FolderSuffix = suffixBox.Text ?? string.Empty;
        var catPrefixBox = new TextBox { PlaceholderText = "e.g. My ", Text = vm.CategoryPrefix, MinWidth = 200 };
        catPrefixBox.TextChanged += (_, _) => vm.CategoryPrefix = catPrefixBox.Text ?? string.Empty;
        var catSuffixBox = new TextBox { PlaceholderText = "e.g. files", Text = vm.CategorySuffix, MinWidth = 200 };
        catSuffixBox.TextChanged += (_, _) => vm.CategorySuffix = catSuffixBox.Text ?? string.Empty;

        var namingSection = MakeSection("Naming (Category mode)",
            Row(new TextBlock { Text = "Date folder prefix:", VerticalAlignment = VerticalAlignment.Center }, prefixBox),
            Row(new TextBlock { Text = "Date folder suffix:", VerticalAlignment = VerticalAlignment.Center }, suffixBox),
            Row(new TextBlock { Text = "Category prefix:", VerticalAlignment = VerticalAlignment.Center }, catPrefixBox),
            Row(new TextBlock { Text = "Category suffix:", VerticalAlignment = VerticalAlignment.Center }, catSuffixBox));

        // --- Section: file handling ---
        var chkHtml = new CheckBox
        {
            Content = "Keep HTML companions together (webpage.html + _files folder)",
            IsChecked = vm.KeepHtmlCompanionsTogether
        };
        chkHtml.IsCheckedChanged += (_, _) => vm.KeepHtmlCompanionsTogether = chkHtml.IsChecked == true;

        var chkSubs = new CheckBox
        {
            Content = "Keep subtitle companions together (movie.mkv + movie.srt)",
            IsChecked = vm.KeepSubtitleCompanionsTogether
        };
        chkSubs.IsCheckedChanged += (_, _) => vm.KeepSubtitleCompanionsTogether = chkSubs.IsChecked == true;

        var chkSys = new CheckBox { Content = "Ignore system files (.DS_Store, Thumbs.db…)", IsChecked = vm.IgnoreSystemFiles };
        chkSys.IsCheckedChanged += (_, _) => vm.IgnoreSystemFiles = chkSys.IsChecked == true;

        var handlingSection = MakeSection("File handling", chkHtml, chkSubs, chkSys);

        // --- Buttons ---
        var saveButton = new Button
        {
            Content = "Save",
            Padding = new global::Avalonia.Thickness(22, 6),
            FontWeight = FontWeight.SemiBold,
            Classes = { "primary" }
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new global::Avalonia.Thickness(16, 6)
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(0, 16, 0, 0),
            Children = { cancelButton, saveButton }
        };

        var scroll = new ScrollViewer
        {
            MaxHeight = 480,
            Content = new StackPanel { Spacing = 14, Children = { dateSection, namingSection, handlingSection } }
        };

        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(22), Children = { title, scroll, buttons } };

        var dialog = new Window
        {
            Title = "Preferences",
            Width = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Content = panel
        };

        var tcs = new TaskCompletionSource<bool>();
        saveButton.Click += (_, _) => { vm.Save(); tcs.TrySetResult(true); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }

    private static StackPanel Row(Control left, Control right) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 12,
        Children = { left, right }
    };

    private static StackPanel MakeSection(string header, params Control[] rows)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = header,
            FontWeight = FontWeight.Medium,
            Opacity = 0.85,
            Margin = new global::Avalonia.Thickness(0, 4, 0, 2)
        });
        foreach (var row in rows) panel.Children.Add(row);
        return panel;
    }
}