using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TimeFold.Avalonia.ViewModels;
using TimeFold.Avalonia.Views;

namespace TimeFold.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();

            // Optional: pre-load a folder passed on the command line
            // (dotnet run -- /some/folder) for quick testing.
            var arg = desktop.Args?.FirstOrDefault(a => Directory.Exists(a));
            if (arg is not null) vm.FolderPath = Path.GetFullPath(arg);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}