using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Glint.ViewModels;

namespace Glint.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null)
        {
            _viewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
            _viewModel.OpenFileRequested -= OnOpenFileRequested;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel != null)
        {
            _viewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
            _viewModel.OpenFileRequested += OnOpenFileRequested;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // Always recenter the window when it resizes to fit a new screenshot
        var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
        if (screen != null)
        {
            var bounds = screen.WorkingArea;
            var scaling = screen.Scaling;
            var pw = Bounds.Width * scaling;
            var ph = Bounds.Height * scaling;
            
            Position = new PixelPoint(
                (int)(bounds.X + (bounds.Width - pw) / 2),
                (int)(bounds.Y + (bounds.Height - ph) / 2)
            );
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_viewModel == null) return;

        // Ctrl/Cmd+Z = Undo, Ctrl/Cmd+Shift+Z = Redo
        if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                _viewModel.Editor.PerformRedoCommand.Execute(null);
            else
                _viewModel.Editor.PerformUndoCommand.Execute(null);
            e.Handled = true;
        }
        // Ctrl/Cmd+S = Save
        else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            _viewModel.SaveScreenshotCommand.Execute(null);
            e.Handled = true;
        }
        // Ctrl/Cmd+C = Copy
        else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            _viewModel.CopyToClipboardCommand.Execute(null);
            e.Handled = true;
        }

        // Ctrl/Cmd+O = Open file
        else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            _viewModel.OpenFileCommand.Execute(null);
            e.Handled = true;
        }
        // Tool shortcuts (single key, no modifier)
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            if (AnnotationCanvas.IsEditingText) return; // Do not steal keys while typing text
            
            if (e.Key == Key.D)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Freehand");
                e.Handled = true;
            }
            else if (e.Key == Key.A)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Arrow");
                e.Handled = true;
            }
            else if (e.Key == Key.T)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Text");
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Blur");
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Rectangle");
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Ellipse");
                e.Handled = true;
            }
            else if (e.Key == Key.H)
            {
                _viewModel.Editor.SelectToolCommand.Execute("Highlighter");
                e.Handled = true;
            }
            else if (e.Key == Key.S) // wait, S is used for save (Ctrl+S) but standalone S is fine! Wait, actually S without modifier is fine.
            {
                _viewModel.Editor.SelectToolCommand.Execute("Step");
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            // If editing text, let AnnotationCanvas handle Escape to cancel
            if (AnnotationCanvas.IsEditingText) return;
            
            Close();
            e.Handled = true;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void CloseWindow_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }



    private async void OnClipboardCopyRequested()
    {
        if (_viewModel?.ClipboardData == null) return;
        try
        {
            // Save the composited image to temp
            var tempPath = Path.Combine(Path.GetTempPath(), $"glint_clipboard_{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(tempPath, _viewModel.ClipboardData);
            
            // OS-specific clipboard copy
            if (OperatingSystem.IsMacOS())
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "osascript",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.StartInfo.ArgumentList.Add("-e");
                process.StartInfo.ArgumentList.Add($"set the clipboard to (read (POSIX file \"{tempPath}\") as «class PNGf»)");
                process.Start();
                await process.WaitForExitAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                var isWayland = !string.IsNullOrEmpty(waylandDisplay);
                
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = isWayland 
                            ? $"-c \"wl-copy < '{tempPath}'\"" 
                            : $"-c \"xclip -selection clipboard -t image/png -i '{tempPath}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
            }
            
            // Close the window after copying
            Close();
        }
        catch (Exception ex)
        {
            _viewModel.Editor.StatusText = $"Copy error: {ex.Message}";
        }
    }

    private async void OnOpenFileRequested()
    {
        try
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
                }
            });

            if (files.Count > 0)
            {
                var path = files[0].Path.LocalPath;
                _viewModel?.LoadFromFile(path);
            }
        }
        catch (Exception ex)
        {
            if (_viewModel != null)
                _viewModel.Editor.StatusText = $"Error opening file: {ex.Message}";
        }
    }

    private void OnColorSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out var index))
        {
            _viewModel?.Editor.SelectColorCommand.Execute(index);
        }
    }

    private void OnStrokeSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out var index))
        {
            _viewModel?.Editor.SelectStrokeCommand.Execute(index);
        }
    }

    private void OnFontSizeSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out var index))
        {
            _viewModel?.Editor.SelectFontSizeCommand.Execute(index);
        }
    }
}