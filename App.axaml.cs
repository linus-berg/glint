using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Glint.Services;
using Glint.ViewModels;
using Glint.Views;

namespace Glint;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private HotkeyService? _hotkeyService;
    private bool _isReallyQuitting;

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

            // Prevent closing — hide to tray instead
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += OnMainWindowClosing;

            // Start global hotkey service
            _hotkeyService = new HotkeyService();
            _hotkeyService.CaptureRequested += OnHotkeyCaptureRequested;
            _hotkeyService.Start();

            // Load from stdin if piped (e.g. grim -g "$(slurp)" - | glint)
            if (Console.IsInputRedirected)
            {
                // We use a MemoryStream to copy the input, as some streams don't support seeking/rewinding which SKBitmap might want
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
            }

            _mainWindow.Opened += (s, e) =>
            {
                if (!_mainViewModel.Editor.HasScreenshot)
                {
                    _mainWindow.Hide();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// When the user closes the window, hide it to the tray instead of quitting.
    /// </summary>
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isReallyQuitting)
        {
            e.Cancel = true;
            _mainWindow?.Hide();
        }
    }

    /// <summary>
    /// Global PrintScreen hotkey was pressed — capture the screen.
    /// </summary>
    private void OnHotkeyCaptureRequested()
    {
        // SharpHook fires on a background thread — dispatch to UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (_mainViewModel == null) return;

            // Hide the window so it doesn't block the screen
            _mainWindow?.Hide();
            await Task.Delay(100);

            // Trigger screen capture
            await _mainViewModel.CaptureScreenCommand.ExecuteAsync(null);

            ShowAndActivateWindow();
        });
    }

    // ── Tray icon event handlers ──────────────────────────────────────

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        ShowAndActivateWindow();
    }

    private void ShowWindow_Click(object? sender, EventArgs e)
    {
        ShowAndActivateWindow();
    }

    private void CaptureScreen_Click(object? sender, EventArgs e)
    {
        OnHotkeyCaptureRequested();
    }

    private void CaptureRegion_Click(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (_mainViewModel == null) return;

            // Hide the window so it doesn't block the screen
            _mainWindow?.Hide();
            await Task.Delay(100);

            await _mainViewModel.CaptureRegionCommand.ExecuteAsync(null);

            ShowAndActivateWindow();
        });
    }

    private void QuitApp_Click(object? sender, EventArgs e)
    {
        _isReallyQuitting = true;
        _hotkeyService?.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ShowAndActivateWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();

        // Briefly set topmost to ensure it comes to front on macOS
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            _mainWindow.Topmost = true;
            await Task.Delay(100);
            _mainWindow.Topmost = false;
        });
    }
}