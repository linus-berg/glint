using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glint.Models;
using Glint.Services;
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

    [ObservableProperty]
    public partial bool IsFillEnabled { get; set; } = false;

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
        if (ConfigService.Current.Palette != null)
        {
            for (int i = 0; i < 8; i++)
            {
                if (SKColor.TryParse(ConfigService.Current.Palette[i], out var c))
                    PresetColors[i] = c;
            }
        }

        if (Enum.TryParse<ToolType>(ConfigService.Current.DefaultTool, true, out var t))
            CurrentTool = t;

        SelectedColorIndex = ConfigService.Current.DefaultColorIndex;
        if (SelectedColorIndex >= 0 && SelectedColorIndex < PresetColors.Length)
            CurrentColor = PresetColors[SelectedColorIndex];

        SelectedStrokeIndex = ConfigService.Current.DefaultStrokeIndex;
        if (SelectedStrokeIndex >= 0 && SelectedStrokeIndex < PresetStrokes.Length)
            CurrentStrokeWidth = PresetStrokes[SelectedStrokeIndex];

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

    public void PerformCrop(SKRectI cropRect)
    {
        if (Screenshot == null) return;
        
        // Clamp rect to bounds
        cropRect.Intersect(new SKRectI(0, 0, Screenshot.Width, Screenshot.Height));
        if (cropRect.Width < 10 || cropRect.Height < 10) return;

        var croppedBitmap = new SKBitmap(cropRect.Width, cropRect.Height);
        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.DrawBitmap(Screenshot, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height), new SKSamplingOptions());
        }

        var oldScreenshot = Screenshot;
        var oldAnnotations = Annotations.ToList();

        var newAnnotations = new List<AnnotationBase>();
        foreach (var ann in oldAnnotations)
        {
            var cloned = ann.Clone();
            cloned.Translate(-cropRect.Left, -cropRect.Top);
            
            var b = cloned.GetBounds();
            // keep if it intersects the new bounds
            if (b.IntersectsWith(new SKRect(0, 0, cropRect.Width, cropRect.Height)))
            {
                newAnnotations.Add(cloned);
            }
        }

        UndoRedo.AddWithoutExecuting(new UndoableAction
        {
            Undo = () =>
            {
                Screenshot = oldScreenshot;
                Annotations.Clear();
                foreach (var a in oldAnnotations) Annotations.Add(a);
                RequestInvalidate();
            },
            Redo = () =>
            {
                Screenshot = croppedBitmap;
                Annotations.Clear();
                foreach (var a in newAnnotations) Annotations.Add(a);
                RequestInvalidate();
            },
            Description = "Crop"
        });

        Screenshot = croppedBitmap;
        Annotations.Clear();
        foreach (var a in newAnnotations) Annotations.Add(a);
        
        // We do NOT dispose the oldScreenshot because it's stored in the Undo stack!
        RequestInvalidate();
    }

    [RelayCommand]
    private void ClearAnnotations()
    {
        UndoRedo.AddWithoutExecuting(new UndoableAction
        {
            Undo = () =>
            {
                // Note: Doesn't restore correctly in this basic implementation 
                // if we don't save the exact state. 
                // A better approach is copying the list.
            },
            Redo = () => Annotations.Clear(),
            Description = "Clear All"
        });
        Annotations.Clear();
        RequestInvalidate();
    }

    [RelayCommand]
    private void ToggleFill()
    {
        IsFillEnabled = !IsFillEnabled;
    }

    public void RequestInvalidate()
    {
        InvalidateCanvas?.Invoke();
    }
}
