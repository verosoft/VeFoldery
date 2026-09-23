using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VeFoldery.Avalonia.ViewModels;
using VeFoldery.Avalonia.Views;

namespace VeFoldery.Avalonia;

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
            // (dotnet run -- /some/folder); falls back to most recent.
            var arg = desktop.Args?.FirstOrDefault(a => Directory.Exists(a));
            vm.RestoreStartupFolder(arg);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}