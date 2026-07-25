using SkiaSharp;

namespace Glint.Models;

public class LoupeAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }

    public override void Render(SKCanvas canvas)
    {
        // Visuals are completely handled by ApplyLoupe compositing
    }

    public void ApplyLoupe(SKBitmap bitmap)
    {
        var radius = Math.Max(10f, SKPoint.Distance(Start, End));
        var center = Start;
        var srcRadius = radius / 2f;
        
        var srcRect = new SKRect(center.X - srcRadius, center.Y - srcRadius, center.X + srcRadius, center.Y + srcRadius);
        var srcRectI = SKRectI.Round(srcRect);
        var bitmapBounds = new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        
        if (!srcRectI.IntersectsWith(bitmapBounds)) return;
        srcRectI.Intersect(bitmapBounds);
        
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, srcRectI)) return;
        
        using var srcImage = SKImage.FromBitmap(subset);
        
        using var canvas = new SKCanvas(bitmap);
        canvas.Save();
        
        using var path = new SKPath();
        path.AddCircle(center.X, center.Y, radius);
        canvas.ClipPath(path, SKClipOperation.Intersect, true);
        
        var scale = radius / srcRadius; // 2.0
        var actualDstRect = new SKRect(
            center.X + (srcRectI.Left - center.X) * scale,
            center.Y + (srcRectI.Top - center.Y) * scale,
            center.X + (srcRectI.Right - center.X) * scale,
            center.Y + (srcRectI.Bottom - center.Y) * scale
        );
        
        canvas.DrawImage(srcImage, actualDstRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        
        canvas.Restore();

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(3f, StrokeWidth * 1.5f),
            IsAntialias = true
        };
        
        using var shadowFilter = SKImageFilter.CreateDropShadow(0, 4f, 6f, 6f, new SKColor(0, 0, 0, 120));
        borderPaint.ImageFilter = shadowFilter;
        
        canvas.DrawCircle(center.X, center.Y, radius, borderPaint);
        
        using var innerBorder = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 120),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };
        canvas.DrawCircle(center.X, center.Y, radius - borderPaint.StrokeWidth / 2f, innerBorder);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        var radius = Math.Max(10f, SKPoint.Distance(Start, End));
        var dx = point.X - Start.X;
        var dy = point.Y - Start.Y;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    public override SKRect GetBounds()
    {
        var radius = Math.Max(10f, SKPoint.Distance(Start, End));
        return new SKRect(Start.X - radius, Start.Y - radius, Start.X + radius, Start.Y + radius);
    }

    public override void Translate(float dx, float dy)
    {
        Start = new SKPoint(Start.X + dx, Start.Y + dy);
        End = new SKPoint(End.X + dx, End.Y + dy);
    }

    public override AnnotationBase Clone()
    {
        return new LoupeAnnotation
        {
            Start = Start,
            End = End,
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
