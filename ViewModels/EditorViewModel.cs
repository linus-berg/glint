using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glint.Models;
using SkiaSharp;

namespace Glint.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ToolType CurrentTool { get; set; } = ToolType.Freehand;

    [ObservableProperty]
    public partial SKColor CurrentColor { get; set; } = SKColors.Red;

    [ObservableProperty]
    public partial float CurrentStrokeWidth { get; set; } = 3f;

    [ObservableProperty]
    public partial float CurrentFontSize { get; set; } = 20f;

    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    public partial SKBitmap? Screenshot { get; set; }

    [ObservableProperty]
    public partial bool HasScreenshot { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial int SelectedColorIndex { get; set; } = 0;

    [ObservableProperty]
    public partial int SelectedStrokeIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int SelectedFontSizeIndex { get; set; } = 1;

    public ObservableCollection<AnnotationBase> Annotations { get; } = new();
    public UndoRedoManager UndoRedo { get; } = new();

    // Preset colors
    public SKColor[] PresetColors { get; } = new[]
    {
        SKColors.Red,
        new SKColor(255, 87, 34),   // Deep Orange
        new SKColor(255, 193, 7),   // Amber
        new SKColor(76, 175, 80),   // Green
        new SKColor(33, 150, 243),  // Blue
        new SKColor(156, 39, 176),  // Purple
        SKColors.White,
        SKColors.Black
    };

    // Preset stroke widths
    public float[] PresetStrokes { get; } = new[] { 1f, 3f, 5f, 8f };

    // Preset font sizes
    public float[] PresetFontSizes { get; } = new[] { 14f, 20f, 32f, 48f };

    /// <summary>
    /// Event raised when the canvas should be redrawn.
    /// </summary>
    public event Action? InvalidateCanvas;

    public EditorViewModel()
    {
        UndoRedo.StateChanged += () =>
        {
            CanUndo = UndoRedo.CanUndo;
            CanRedo = UndoRedo.CanRedo;
        };
    }

    public void SetScreenshot(SKBitmap bitmap)
    {
        Screenshot = bitmap;
        HasScreenshot = true;
        Annotations.Clear();
        UndoRedo.Clear();
        StatusText = $"Screenshot captured ({bitmap.Width}×{bitmap.Height})";
        RequestInvalidate();
    }

    public void AddAnnotation(AnnotationBase annotation)
    {
        var a = annotation;
        UndoRedo.AddWithoutExecuting(new UndoableAction
        {
            Undo = () => { Annotations.Remove(a); RequestInvalidate(); },
            Redo = () => { Annotations.Add(a); RequestInvalidate(); },
            Description = $"Add {a.GetType().Name}"
        });
        Annotations.Add(annotation);
        RequestInvalidate();
    }

    public void RemoveAnnotation(AnnotationBase annotation)
    {
        var a = annotation;
        var index = Annotations.IndexOf(a);
        UndoRedo.AddWithoutExecuting(new UndoableAction
        {
            Undo = () => { Annotations.Insert(Math.Min(index, Annotations.Count), a); RequestInvalidate(); },
            Redo = () => { Annotations.Remove(a); RequestInvalidate(); },
            Description = $"Remove {a.GetType().Name}"
        });
        Annotations.Remove(annotation);
        RequestInvalidate();
    }

    [RelayCommand]
    private void SelectTool(string toolName)
    {
        if (Enum.TryParse<ToolType>(toolName, out var tool))
        {
            CurrentTool = tool;
            StatusText = $"Tool: {tool}";
        }
    }

    [RelayCommand]
    private void SelectColor(int index)
    {
        if (index >= 0 && index < PresetColors.Length)
        {
            SelectedColorIndex = index;
            CurrentColor = PresetColors[index];
        }
    }

    [RelayCommand]
    private void SelectStroke(int index)
    {
        if (index >= 0 && index < PresetStrokes.Length)
        {
            SelectedStrokeIndex = index;
            CurrentStrokeWidth = PresetStrokes[index];
        }
    }

    [RelayCommand]
    private void SelectFontSize(int index)
    {
        if (index >= 0 && index < PresetFontSizes.Length)
        {
            SelectedFontSizeIndex = index;
            CurrentFontSize = PresetFontSizes[index];
        }
    }

    [RelayCommand]
    private void PerformUndo()
    {
        UndoRedo.Undo();
        RequestInvalidate();
    }

    [RelayCommand]
    private void PerformRedo()
    {
        UndoRedo.Redo();
        RequestInvalidate();
    }

    [RelayCommand]
    private void ClearAnnotations()
    {
        var snapshot = Annotations.ToList();
        UndoRedo.AddWithoutExecuting(new UndoableAction
        {
            Undo = () => { foreach (var a in snapshot) Annotations.Add(a); RequestInvalidate(); },
            Redo = () => { Annotations.Clear(); RequestInvalidate(); },
            Description = "Clear all annotations"
        });
        Annotations.Clear();
        RequestInvalidate();
    }

    public void RequestInvalidate()
    {
        InvalidateCanvas?.Invoke();
    }
}
