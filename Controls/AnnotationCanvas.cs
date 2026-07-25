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
    
    public bool IsEditingText => _activeTextAnnotation != null && _activeTextAnnotation.IsEditing;

    // Pan/Zoom state
    private double _zoom = 1.0;
    private Point _offset;
    private bool _isPanning;
    private Point _panStart;
    private Point _panOffsetStart;

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

        _zoom = Math.Min(cw / bw, ch / bh) * 0.95;
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
            return new Size(w, h);
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

            // Create a copy for blur rendering
            using var workBitmap = screenshot.Copy();

            // Apply blur annotations to the working copy
            foreach (var annotation in _editor.Annotations)
            {
                if (annotation is BlurAnnotation blur)
                {
                    blur.ApplyBlur(workBitmap);
                }
            }

            // Draw the (potentially blurred) screenshot
            canvas.DrawBitmap(workBitmap, 0, 0);

            // Draw non-blur annotations
            foreach (var annotation in _editor.Annotations)
            {
                if (annotation is not BlurAnnotation)
                {
                    annotation.Render(canvas);
                }
            }

            // Draw blur annotation outlines (editor-only visual)
            foreach (var annotation in _editor.Annotations)
            {
                if (annotation is BlurAnnotation blurOutline)
                {
                    blurOutline.Render(canvas);
                }
            }

            // Draw current in-progress annotation
            _currentAnnotation?.Render(canvas);
            
            // Draw active text annotation
            _activeTextAnnotation?.Render(canvas);

            surface.Flush();
        }

        // Draw the render target to the Avalonia DrawingContext with zoom/pan
        var destRect = new Rect(
            _offset.X, _offset.Y,
            width * _zoom, height * _zoom
        );

        // Draw shadow behind the screenshot
        var shadowRect = new Rect(destRect.X + 4, destRect.Y + 4, destRect.Width, destRect.Height);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), null, shadowRect, 4, 4);

        context.DrawImage(_renderTarget, new Rect(0, 0, width, height), destRect);

        // Draw border around the image
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1), destRect);
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

        // Middle button or Space+Left for panning
        if (props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = pos;
            _panOffsetStart = _offset;
            e.Handled = true;
            return;
        }

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
            case ToolType.Select:
                // Could implement selection/move logic here
                break;
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_editor?.Screenshot == null) return;

        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            var delta = pos - _panStart;
            _offset = new Point(_panOffsetStart.X + delta.X, _panOffsetStart.Y + delta.Y);
            InvalidateVisual();
            return;
        }

        if (!_isDrawing) return;

        var imgPos = ScreenToImage(pos);

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
                break;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            return;
        }

        if (!_isDrawing || _currentAnnotation == null || _editor == null) return;

        _isDrawing = false;

        // Only add if the annotation has meaningful content
        bool shouldAdd = _currentAnnotation switch
        {
            FreehandAnnotation fh => fh.Points.Count > 2,
            ArrowAnnotation ar => SKPoint.Distance(ar.Start, ar.End) > 5,
            RectangleAnnotation re => Math.Abs(re.End.X - re.Start.X) > 3 && Math.Abs(re.End.Y - re.Start.Y) > 3,
            EllipseAnnotation el => Math.Abs(el.End.X - el.Start.X) > 3 && Math.Abs(el.End.Y - el.Start.Y) > 3,
            BlurAnnotation bl => Math.Abs(bl.End.X - bl.Start.X) > 5 && Math.Abs(bl.End.Y - bl.Start.Y) > 5,
            _ => true
        };

        if (shouldAdd)
        {
            _editor.AddAnnotation(_currentAnnotation);
        }

        _currentAnnotation = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var pos = e.GetPosition(this);
        var oldZoom = _zoom;
        var zoomDelta = e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
        _zoom = Math.Clamp(_zoom * zoomDelta, 0.1, 10.0);

        // Zoom toward cursor
        _offset = new Point(
            pos.X - (pos.X - _offset.X) * (_zoom / oldZoom),
            pos.Y - (pos.Y - _offset.Y) * (_zoom / oldZoom)
        );

        InvalidateVisual();
        e.Handled = true;
    }

    #endregion

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
            StrokeWidth = _editor.CurrentStrokeWidth
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
