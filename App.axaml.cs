using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Glint.ViewModels;
using Glint.Views;
using System;

namespace Glint;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainViewModel = new MainViewModel();
            _mainWindow = new MainWindow
            {
                DataContext = _mainViewModel,
            };

            desktop.MainWindow = _mainWindow;

            // Load from stdin if piped (e.g. grim -g "$(slurp)" - | glint)
            if (Console.IsInputRedirected)
            {
                var ms = new System.IO.MemoryStream();
                using (var stdin = Console.OpenStandardInput())
                {
                    stdin.CopyTo(ms);
                }
                ms.Position = 0;
                
                if (ms.Length > 0)
                {
                    _mainViewModel.LoadFromStream(ms);
                }
                else
                {
                    Console.WriteLine("No image data received from stdin.");
                    Environment.Exit(1);
                }
            }
            else
            {
                // In Sway/piped-only mode, if no input is redirected, we just exit.
                Console.WriteLine("Glint must be launched with an image piped to stdin. Example: grim -g \"$(slurp)\" - | glint");
                Environment.Exit(1);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}