using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glint.Models;
using Glint.Services;
using SkiaSharp;

namespace Glint.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial EditorViewModel Editor { get; set; }

    [ObservableProperty]
    public partial bool IsCapturing { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Glint — Screenshot Tool";

    public MainViewModel()
    {
        Editor = new EditorViewModel();
    }

    [RelayCommand]
    private async Task CaptureScreenAsync()
    {
        IsCapturing = true;
        try
        {
            var bitmap = await ScreenCaptureService.CaptureFullScreenAsync();
            if (bitmap != null)
            {
                Editor.SetScreenshot(bitmap);
            }
            else
            {
                Editor.StatusText = "Screen capture failed. Check permissions.";
            }
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    private async Task CaptureRegionAsync()
    {
        IsCapturing = true;
        try
        {
            var bitmap = await ScreenCaptureService.CaptureRegionAsync();
            if (bitmap != null)
            {
                Editor.SetScreenshot(bitmap);
            }
            else
            {
                Editor.StatusText = "Region capture failed or was cancelled.";
            }
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    private async Task SaveScreenshotAsync()
    {
        if (Editor.Screenshot == null) return;

        var composite = ImageExportService.Composite(Editor.Screenshot, Editor.Annotations.ToList());
        
        // Default save location
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"Glint_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var filePath = Path.Combine(desktopPath, fileName);

        await ImageExportService.SaveAsync(composite, filePath);
        Editor.StatusText = $"Saved to {filePath}";
        composite.Dispose();
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (Editor.Screenshot == null) return;

        var composite = ImageExportService.Composite(Editor.Screenshot, Editor.Annotations.ToList());
        // We'll store the bytes - the view will handle clipboard
        var bytes = ImageExportService.EncodeToBytes(composite);
        composite.Dispose();

        // Store for the view to pick up via event
        ClipboardData = bytes;
        ClipboardCopyRequested?.Invoke();
        Editor.StatusText = "Copied to clipboard!";
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        // The view will handle file dialog - we emit an event
        OpenFileRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        SaveAsRequested?.Invoke();
    }

    public async Task SaveToFileAsync(string filePath)
    {
        if (Editor.Screenshot == null) return;
        var composite = ImageExportService.Composite(Editor.Screenshot, Editor.Annotations.ToList());
        await ImageExportService.SaveAsync(composite, filePath);
        Editor.StatusText = $"Saved to {filePath}";
        composite.Dispose();
    }

    public byte[]? ClipboardData { get; set; }
    public event Action? ClipboardCopyRequested;
    public event Action? OpenFileRequested;
    public event Action? SaveAsRequested;

    public void LoadFromFile(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Editor.SetScreenshot(bitmap);
                Editor.StatusText = $"Loaded: {Path.GetFileName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            Editor.StatusText = $"Error loading file: {ex.Message}";
        }
    }

    public void LoadFromStream(Stream stream, string sourceName = "stdin")
    {
        try
        {
            var bitmap = SKBitmap.Decode(stream);
            if (bitmap != null)
            {
                Editor.SetScreenshot(bitmap);
                Editor.StatusText = $"Loaded: {sourceName}";
            }
        }
        catch (Exception ex)
        {
            Editor.StatusText = $"Error loading from {sourceName}: {ex.Message}";
        }
    }
}
