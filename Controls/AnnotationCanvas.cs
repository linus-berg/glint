using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Glint.Models;
using Glint.ViewModels;
using SkiaSharp;

namespace Glint.Controls;

/// <summary>
/// Custom control that renders the screenshot and annotations, and handles
/// pointer input for drawing tools.
/// </summary>
public class AnnotationCanvas : Control
{
    private EditorViewModel? _editor;
    private WriteableBitmap? _renderTarget;
    private SKBitmap? _blurredScreenshot;

    // Drawing state
    private bool _isDrawing;
    private SKPoint _dragStart;
    private AnnotationBase? _currentAnnotation;
    private TextAnnotation? _activeTextAnnotation;
    private SKPoint? _previewPos;
    
    public bool IsEditingText => _activeTextAnnotation != null && _activeTextAnnotation.IsEditing;

    // Zoom state
    private double _zoom = 1.0;
    private Point _offset;

    public AnnotationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        LostFocus += (s, e) => CommitActiveText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateEditor();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateEditor();
    }

    private void UpdateEditor()
    {
        if (_editor != null)
        {
            _editor.InvalidateCanvas -= OnInvalidateCanvas;
            _editor.PropertyChanged -= OnEditorPropertyChanged;
        }

        // Walk up to find MainViewModel
        var dc = DataContext;
        if (dc is MainViewModel mainVm)
        {
            _editor = mainVm.Editor;
        }
        else if (dc is EditorViewModel edVm)
        {
            _editor = edVm;
        }

        if (_editor != null)
        {
            _editor.InvalidateCanvas += OnInvalidateCanvas;
            _editor.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.Screenshot))
        {
            _blurredScreenshot?.Dispose();
            _blurredScreenshot = null;
            _renderTarget?.Dispose();
            _renderTarget = null;
            FitToView();
            InvalidateVisual();
        }
    }

    private void OnInvalidateCanvas()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private void FitToView()
    {
        if (_editor?.Screenshot == null) return;
        var bw = _editor.Screenshot.Width;
        var bh = _editor.Screenshot.Height;
        var cw = Bounds.Width;
        var ch = Bounds.Height;
        if (cw <= 0 || ch <= 0) return;

        _zoom = Math.Min(cw / bw, ch / bh);
        _offset = new Point(
            (cw - bw * _zoom) / 2,
            (ch - bh * _zoom) / 2
        );
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_editor?.Screenshot != null)
        {
            var w = Math.Min(_editor.Screenshot.Width, availableSize.Width);
            var h = Math.Min(_editor.Screenshot.Height, availableSize.Height);
            
            // Preserve aspect ratio when scaling down to available size
            double scale = Math.Min(w / _editor.Screenshot.Width, h / _editor.Screenshot.Height);
            return new Size(_editor.Screenshot.Width * scale, _editor.Screenshot.Height * scale);
        }
        return new Size(800, 600);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        FitToView();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Ensure hit testing works across the entire bounds even if the image is scaled
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), null, new Rect(Bounds.Size));

        if (_editor?.Screenshot == null) return;

        var screenshot = _editor.Screenshot;
        var width = screenshot.Width;
        var height = screenshot.Height;

        // Create/resize render target
        if (_renderTarget == null || _renderTarget.PixelSize.Width != width || _renderTarget.PixelSize.Height != height)
        {
            _renderTarget?.Dispose();
            _renderTarget = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);
        }

        // Render to SKCanvas via WriteableBitmap
        using (var fb = _renderTarget.Lock())
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes);
            var canvas = surface.Canvas;

            // Draw screenshot
            canvas.Clear(SKColors.Transparent);

            // Create a copy for layer rendering
            using var workBitmap = screenshot.Copy();
            using var workCanvas = new SKCanvas(workBitmap);

            // Apply all committed annotations in chronological order
            foreach (var annotation in _editor.Annotations)
            {
                if (annotation is BlurAnnotation blur)
                {
                    blur.ApplyBlur(workBitmap);
                }
                else if (annotation is LoupeAnnotation loupe)
                {
                    loupe.ApplyLoupe(workBitmap);
                }
                else if (annotation is RedactionAnnotation redaction)
                {
                    redaction.ApplyRedaction(workBitmap);
                }
                else
                {
                    annotation.Render(workCanvas);
                }
            }

            // Apply live blur effect if currently dragging a blur
            if (_currentAnnotation is BlurAnnotation currentBlur)
            {
                currentBlur.ApplyBlur(workBitmap);
            }
            else if (_currentAnnotation is LoupeAnnotation currentLoupe)
            {
                currentLoupe.ApplyLoupe(workBitmap);
            }
            else if (_currentAnnotation is RedactionAnnotation currentRedaction)
            {
                currentRedaction.ApplyRedaction(workBitmap);
            }

            // Draw the fully composited layered image to the screen
            canvas.DrawBitmap(workBitmap, 0, 0);

            // Draw the live annotation (stroke, shape, or blur UI guide) on top
            if (_currentAnnotation != null)
            {
                _currentAnnotation.Render(canvas);
            }
            // Draw active text annotation
            _activeTextAnnotation?.Render(canvas);

            // Draw Step preview
            if (!_isDrawing && _previewPos.HasValue && _editor?.CurrentTool == ToolType.Step)
            {
                var previewStep = new StepAnnotation
                {
                    Position = _previewPos.Value,
                    Number = _editor.Annotations.OfType<StepAnnotation>().Count() + 1,
                    Color = _editor.CurrentColor.WithAlpha(128), // 50% opacity preview
                    StrokeWidth = _editor.CurrentStrokeWidth
                };
                previewStep.Render(canvas);
            }

            surface.Flush();
        }

        // Draw the render target to the Avalonia DrawingContext with zoom/pan
        var destRect = new Rect(
            _offset.X, _offset.Y,
            width * _zoom, height * _zoom
        );

        context.DrawImage(_renderTarget, new Rect(0, 0, width, height), destRect);
    }

    #region Pointer Input

    private SKPoint ScreenToImage(Point screenPoint)
    {
        return new SKPoint(
            (float)((screenPoint.X - _offset.X) / _zoom),
            (float)((screenPoint.Y - _offset.Y) / _zoom)
        );
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_editor?.Screenshot == null) return;

        var pos = e.GetPosition(this);
        var imgPos = ScreenToImage(pos);
        var props = e.GetCurrentPoint(this).Properties;

        if (!props.IsLeftButtonPressed) return;

        Focus();

        switch (_editor.CurrentTool)
        {
            case ToolType.Freehand:
                StartFreehand(imgPos);
                break;
            case ToolType.Arrow:
                StartArrow(imgPos);
                break;
            case ToolType.Rectangle:
                StartRectangle(imgPos);
                break;
            case ToolType.Ellipse:
                StartEllipse(imgPos);
                break;
            case ToolType.Blur:
                StartBlur(imgPos);
                break;
            case ToolType.Text:
                StartOrEditText(imgPos);
                break;
            case ToolType.Highlighter:
                StartHighlighter(imgPos);
                break;
            case ToolType.Step:
                AddStep(imgPos);
                break;
            case ToolType.Redaction:
                StartRedaction(imgPos);
                break;
            case ToolType.Loupe:
                StartLoupe(imgPos);
                break;
            case ToolType.Crop:
                StartCrop(imgPos);
                break;
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_editor?.Screenshot == null) return;

        var pos = e.GetPosition(this);
        var imgPos = ScreenToImage(pos);

        if (!_isDrawing)
        {
            if (_editor.CurrentTool == ToolType.Step)
            {
                _previewPos = imgPos;
                InvalidateVisual();
            }
            return;
        }

        switch (_currentAnnotation)
        {
            case FreehandAnnotation freehand:
                freehand.Points.Add(imgPos);
                break;
            case ArrowAnnotation arrow:
                arrow.End = imgPos;
                break;
            case RectangleAnnotation rect:
                rect.End = imgPos;
                break;
            case EllipseAnnotation ellipse:
                ellipse.End = imgPos;
                break;
            case BlurAnnotation blur:
                blur.End = imgPos;
                InvalidateVisual();
                break;
            case RedactionAnnotation redaction:
                redaction.End = imgPos;
                InvalidateVisual();
                break;
            case LoupeAnnotation loupe:
                loupe.End = imgPos;
                InvalidateVisual();
                break;
            case HighlighterAnnotation highlighter:
                highlighter.Path.LineTo(imgPos);
                break;
            case CropAnnotation crop:
                crop.End = imgPos;
                InvalidateVisual();
                break;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDrawing || _currentAnnotation == null || _editor == null) return;

        _isDrawing = false;

        if (_currentAnnotation is CropAnnotation ca)
        {
            var r = ca.GetCropRect();
            if (r.Width >= 10 && r.Height >= 10)
            {
                _editor.PerformCrop(new SKRectI((int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom));
            }
        }
        else
        {
            // Only add if the annotation has meaningful content
            bool shouldAdd = _currentAnnotation switch
            {
                FreehandAnnotation fh => fh.Points.Count > 2,
                ArrowAnnotation ar => SKPoint.Distance(ar.Start, ar.End) > 5,
                RectangleAnnotation re => Math.Abs(re.End.X - re.Start.X) > 3 && Math.Abs(re.End.Y - re.Start.Y) > 3,
                EllipseAnnotation el => Math.Abs(el.End.X - el.Start.X) > 3 && Math.Abs(el.End.Y - el.Start.Y) > 3,
                BlurAnnotation bl => Math.Abs(bl.End.X - bl.Start.X) > 5 && Math.Abs(bl.End.Y - bl.Start.Y) > 5,
                RedactionAnnotation re => Math.Abs(re.End.X - re.Start.X) > 3 && Math.Abs(re.End.Y - re.Start.Y) > 3,
                LoupeAnnotation lo => Math.Abs(lo.End.X - lo.Start.X) > 5 && Math.Abs(lo.End.Y - lo.Start.Y) > 5,
                HighlighterAnnotation hl => !hl.Path.IsEmpty,
                StepAnnotation => true,
                _ => true
            };

            if (shouldAdd)
            {
                _editor.AddAnnotation(_currentAnnotation);
            }
        }

        _currentAnnotation = null;
        InvalidateVisual();
    }

    #endregion

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _previewPos = null;
        if (_editor?.CurrentTool == ToolType.Step)
        {
            InvalidateVisual();
        }
    }

    #region Drawing Tool Implementations

    private void StartFreehand(SKPoint pos)
    {
        var annotation = new FreehandAnnotation
        {
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        annotation.Points.Add(pos);
        _currentAnnotation = annotation;
        _isDrawing = true;
    }

    private void StartHighlighter(SKPoint pos)
    {
        var annotation = new HighlighterAnnotation
        {
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        annotation.Path.MoveTo(pos);
        _currentAnnotation = annotation;
        _isDrawing = true;
    }

    private void AddStep(SKPoint pos)
    {
        var number = _editor!.Annotations.OfType<StepAnnotation>().Count() + 1;
        var step = new StepAnnotation
        {
            Position = pos,
            Number = number,
            Color = _editor.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        _editor.AddAnnotation(step);
        InvalidateVisual();
    }

    private void StartArrow(SKPoint pos)
    {
        _currentAnnotation = new ArrowAnnotation
        {
            Start = pos,
            End = pos,
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        _isDrawing = true;
    }

    private void StartRectangle(SKPoint pos)
    {
        _currentAnnotation = new RectangleAnnotation
        {
            Start = pos,
            End = pos,
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth,
            IsFilled = _editor.IsFillEnabled
        };
        _isDrawing = true;
    }

    private void StartEllipse(SKPoint pos)
    {
        _currentAnnotation = new EllipseAnnotation
        {
            Start = pos,
            End = pos,
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth,
            IsFilled = _editor.IsFillEnabled
        };
        _isDrawing = true;
    }

    private void StartRedaction(SKPoint pos)
    {
        _currentAnnotation = new RedactionAnnotation
        {
            Start = pos,
            End = pos,
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        _isDrawing = true;
    }

    private void StartLoupe(SKPoint pos)
    {
        _currentAnnotation = new LoupeAnnotation
        {
            Start = pos,
            End = pos,
            Color = _editor!.CurrentColor,
            StrokeWidth = _editor.CurrentStrokeWidth
        };
        _isDrawing = true;
    }

    private void StartBlur(SKPoint pos)
    {
        _currentAnnotation = new BlurAnnotation
        {
            Start = pos,
            End = pos
        };
        _isDrawing = true;
    }

    private void StartCrop(SKPoint pos)
    {
        _currentAnnotation = new CropAnnotation
        {
            Start = pos,
            End = pos,
            FullImageBounds = new SKRect(0, 0, _editor!.Screenshot.Width, _editor.Screenshot.Height)
        };
        _isDrawing = true;
    }

    private void StartOrEditText(SKPoint pos)
    {
        // Finish any existing text editing
        CommitActiveText();

        // Check if clicked on existing text annotation
        foreach (var annotation in _editor!.Annotations)
        {
            if (annotation is TextAnnotation text && text.HitTest(pos))
            {
                text.IsEditing = true;
                _activeTextAnnotation = text;
                InvalidateVisual();
                return;
            }
        }

        // Create new text annotation
        _activeTextAnnotation = new TextAnnotation
        {
            Position = pos,
            Color = _editor.CurrentColor,
            FontSize = _editor.CurrentFontSize,
            IsEditing = true
        };
        InvalidateVisual();
    }

    public void CommitActiveText()
    {
        if (_activeTextAnnotation != null)
        {
            _activeTextAnnotation.IsEditing = false;
            if (!string.IsNullOrWhiteSpace(_activeTextAnnotation.Text))
            {
                if (_editor != null && !_editor.Annotations.Contains(_activeTextAnnotation))
                {
                    _editor.AddAnnotation(_activeTextAnnotation);
                }
            }
            _activeTextAnnotation = null;
            InvalidateVisual();
        }
    }

    #endregion

    #region Text Input

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_activeTextAnnotation != null && _activeTextAnnotation.IsEditing)
        {
            if (e.Key == Key.Escape)
            {
                // Cancel text editing (discard if new)
                if (_editor != null && !_editor.Annotations.Contains(_activeTextAnnotation))
                {
                    _activeTextAnnotation = null;
                }
                else
                {
                    _activeTextAnnotation.IsEditing = false;
                    _activeTextAnnotation = null;
                }
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                CommitActiveText();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
                if (_activeTextAnnotation.Text.Length > 0)
                {
                    _activeTextAnnotation.Text = _activeTextAnnotation.Text[..^1];
                    InvalidateVisual();
                }
                e.Handled = true;
                return;
            }

            e.Handled = false;
            return;
        }

        // Delete selected annotations
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            var selected = _editor?.Annotations.Where(a => a.IsSelected).ToList();
            if (selected != null)
            {
                foreach (var a in selected)
                    _editor!.RemoveAnnotation(a);
                InvalidateVisual();
            }
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (_activeTextAnnotation != null && _activeTextAnnotation.IsEditing && !string.IsNullOrEmpty(e.Text))
        {
            _activeTextAnnotation.Text += e.Text;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    #endregion
}
