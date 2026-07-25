using SkiaSharp;

namespace Glint.Models;

public class RectangleAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }
    public bool IsFilled { get; set; }

    public override void Render(SKCanvas canvas)
    {
        var rect = SKRect.Create(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Abs(End.X - Start.X), Math.Abs(End.Y - Start.Y)
        );

        using var paint = new SKPaint
        {
            Color = Color,
            StrokeWidth = StrokeWidth,
            Style = IsFilled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            IsAntialias = true
        };

        canvas.DrawRect(rect, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        var rect = GetBounds();
        var inflated = SKRect.Inflate(rect, tolerance, tolerance);
        if (!inflated.Contains(point)) return false;
        if (IsFilled) return true;
        var deflated = SKRect.Inflate(rect, -tolerance - StrokeWidth, -tolerance - StrokeWidth);
        return !deflated.Contains(point);
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
        return new RectangleAnnotation
        {
            Start = Start, End = End,
            Color = Color, StrokeWidth = StrokeWidth,
            IsFilled = IsFilled
        };
    }
}
