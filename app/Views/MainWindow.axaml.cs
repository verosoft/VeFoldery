using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileOrganizer.Models;
using TimeFold.Avalonia.ViewModels;

namespace TimeFold.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Register this window so view-model helpers can open pickers.
        StorageProviderHelper.Register(() => this);

        ModeSelector.SelectionChanged += (_, _) => OnModeChanged();

        // Drop a folder anywhere on the window to open it (parity with the
        // WinForms PnlSourceDrop drag & drop).
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is not { } items) return;
        var path = items.FirstOrDefault()?.Path.LocalPath;
        if (path is null || Vm is null || !System.IO.Directory.Exists(path)) return;
        Vm.FolderPath = path;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnModeChanged()
    {
        if (Vm is null) return;

        Vm.SelectedMode = ModeSelector.SelectedIndex switch
        {
            1 => OrganizationMode.Category,
            2 => OrganizationMode.Extension,
            3 => OrganizationMode.CategoryAndDate,
            4 => OrganizationMode.DateAndCategory,
            _ => OrganizationMode.Date
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        OnModeChanged();

        // DataContext is assigned after the constructor runs (object
        // initializer), so wire the confirm gate here, once the VM exists.
        if (DataContext is MainViewModel vm && vm.ConfirmOrganize is null)
        {
            vm.ConfirmOrganize = summary =>
                Dialogs.ConfirmAsync(this, "Confirm organize", summary, "Organize", "Cancel");

            vm.ConfirmConflicts = conflicts =>
                Dialogs.ShowConflictsAsync(this, conflicts);
        }
    }
}