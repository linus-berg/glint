using SkiaSharp;

namespace Glint.Models;

public class RedactionAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var paint = new SKPaint
        {
            Color = Color,
            IsAntialias = false, // Crisp edges for redaction
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(rect, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        return GetBounds().Contains(point);
    }

    public override SKRect GetBounds()
    {
        var left = Math.Min(Start.X, End.X);
        var right = Math.Max(Start.X, End.X);
        var top = Math.Min(Start.Y, End.Y);
        var bottom = Math.Max(Start.Y, End.Y);
        return new SKRect(left, top, right, bottom);
    }

    public override AnnotationBase Clone()
    {
        return new RedactionAnnotation
        {
            Start = Start,
            End = End,
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
