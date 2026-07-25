using SkiaSharp;

namespace Glint.Models;

public class EllipseAnnotation : AnnotationBase
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

        canvas.DrawOval(rect, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        var bounds = GetBounds();
        var cx = bounds.MidX;
        var cy = bounds.MidY;
        var rx = bounds.Width / 2 + tolerance;
        var ry = bounds.Height / 2 + tolerance;
        if (rx == 0 || ry == 0) return false;
        var dx = point.X - cx;
        var dy = point.Y - cy;
        var outer = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        if (IsFilled) return outer <= 1;
        var rxInner = Math.Max(0, bounds.Width / 2 - tolerance - StrokeWidth);
        var ryInner = Math.Max(0, bounds.Height / 2 - tolerance - StrokeWidth);
        if (rxInner == 0 || ryInner == 0) return outer <= 1;
        var inner = (dx * dx) / (rxInner * rxInner) + (dy * dy) / (ryInner * ryInner);
        return outer <= 1 && inner >= 1;
    }

    public override SKRect GetBounds()
    {
        return SKRect.Create(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Abs(End.X - Start.X), Math.Abs(End.Y - Start.Y)
        );
    }

    public override void Translate(float dx, float dy)
    {
        Start = new SKPoint(Start.X + dx, Start.Y + dy);
        End = new SKPoint(End.X + dx, End.Y + dy);
    }

    public override AnnotationBase Clone()
    {
        return new EllipseAnnotation
        {
            Start = Start, End = End,
            Color = Color, StrokeWidth = StrokeWidth,
            IsFilled = IsFilled
        };
    }
}
