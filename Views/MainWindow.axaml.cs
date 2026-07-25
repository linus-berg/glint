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
        // Ctrl/Cmd+N = New capture (Shift for region)
        else if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            e.Handled = true;
            bool isRegion = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            ExecuteCapture(isRegion);
        }
        // Ctrl/Cmd+O = Open file
        else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            _viewModel.OpenFileCommand.Execute(null);
            e.Handled = true;
        }
        // Tool shortcuts (single key, no modifier)
        else if (e.Key == Key.D && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Freehand");
            e.Handled = true;
        }
        else if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Arrow");
            e.Handled = true;
        }
        else if (e.Key == Key.T && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Text");
            e.Handled = true;
        }
        else if (e.Key == Key.B && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Blur");
            e.Handled = true;
        }
        else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Rectangle");
            e.Handled = true;
        }
        else if (e.Key == Key.E && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Ellipse");
            e.Handled = true;
        }
        else if (e.Key == Key.V && e.KeyModifiers == KeyModifiers.None)
        {
            _viewModel.Editor.SelectToolCommand.Execute("Select");
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
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
        Hide();
    }

    private async void ExecuteCapture(bool isRegion)
    {
        if (_viewModel == null) return;
        
        Hide();
        await Task.Delay(100);
        
        if (isRegion)
            await _viewModel.CaptureRegionCommand.ExecuteAsync(null);
        else
            await _viewModel.CaptureScreenCommand.ExecuteAsync(null);
            
        Show();
        Activate();
        WindowState = WindowState.Normal;
        
        // On macOS, bring app to front
        Topmost = true;
        await Task.Delay(100);
        Topmost = false;
    }

    private async void OnClipboardCopyRequested()
    {
        if (_viewModel?.ClipboardData == null) return;
        try
        {
            // Save the composited image to temp and report the path
            var tempPath = Path.Combine(Path.GetTempPath(), $"glint_clipboard_{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(tempPath, _viewModel.ClipboardData);
            _viewModel.Editor.StatusText = $"Image saved to: {tempPath}";
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
}