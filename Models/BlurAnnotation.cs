using SkiaSharp;

namespace Glint.Models;

public class BlurAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }
    public float BlurRadius { get; set; } = 25f;

    public override void Render(SKCanvas canvas)
    {
        // This is only called when the annotation is actively being dragged
        var rect = GetBounds();

        // Draw a subtle overlay and dashed border while dragging
        using var overlayPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 255, 40),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(rect, overlayPaint);

        using var borderPaint = new SKPaint
        {
            Color = new SKColor(100, 100, 255, 150),
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0)
        };
        canvas.DrawRect(rect, borderPaint);
    }

    /// <summary>
    /// Applies the actual blur effect to the given bitmap within this annotation's bounds.
    /// Call this before rendering other annotations.
    /// </summary>
    public void ApplyBlur(SKBitmap bitmap)
    {
        var rect = GetBounds();
        var clipRect = SKRectI.Create(
            Math.Max(0, (int)rect.Left),
            Math.Max(0, (int)rect.Top),
            Math.Min(bitmap.Width - (int)rect.Left, (int)rect.Width),
            Math.Min(bitmap.Height - (int)rect.Top, (int)rect.Height)
        );

        if (clipRect.Width <= 0 || clipRect.Height <= 0) return;

        // Extract the region
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, clipRect)) return;

        // Apply blur
        using var surface = SKSurface.Create(new SKImageInfo(clipRect.Width, clipRect.Height));
        var canvas = surface.Canvas;
        
        using var paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateBlur(BlurRadius, BlurRadius, SKShaderTileMode.Clamp)
        };
        
        canvas.DrawBitmap(subset, 0, 0, paint);
        
        // Copy back
        using var blurredImage = surface.Snapshot();
        using var blurredBitmap = SKBitmap.FromImage(blurredImage);
        
        using var targetCanvas = new SKCanvas(bitmap);
        targetCanvas.DrawBitmap(blurredBitmap, clipRect.Left, clipRect.Top);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        return SKRect.Inflate(GetBounds(), tolerance, tolerance).Contains(point);
    }

    public override SKRect GetBounds()
    {
        return SKRect.Create(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Abs(End.X - Start.X), Math.Abs(End.Y - Start.Y)
        );
    }

    public override AnnotationBase Clone()
    {
        return new BlurAnnotation
        {
            Start = Start, End = End,
            BlurRadius = BlurRadius,
            Color = Color, StrokeWidth = StrokeWidth
        };
    }
}
