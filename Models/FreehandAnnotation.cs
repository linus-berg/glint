using SkiaSharp;

namespace Glint.Models;

public class FreehandAnnotation : AnnotationBase
{
    public List<SKPoint> Points { get; set; } = new();

    public override void Render(SKCanvas canvas)
    {
        if (Points.Count < 2) return;

        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(Points[0]);

        for (int i = 1; i < Points.Count; i++)
        {
            pathBuilder.LineTo(Points[i]);
        }

        using var paint = new SKPaint
        {
            Color = Color,
            StrokeWidth = StrokeWidth,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        for (int i = 1; i < Points.Count; i++)
        {
            var dist = DistanceToSegment(point, Points[i - 1], Points[i]);
            if (dist <= tolerance + StrokeWidth / 2) return true;
        }
        return false;
    }

    public override SKRect GetBounds()
    {
        if (Points.Count == 0) return SKRect.Empty;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in Points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }
        return new SKRect(minX, minY, maxX, maxY);
    }

    public override void Translate(float dx, float dy)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = new SKPoint(Points[i].X + dx, Points[i].Y + dy);
    }

    public override AnnotationBase Clone()
    {
        return new FreehandAnnotation
        {
            Points = new List<SKPoint>(Points),
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }

    private static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        if (lenSq == 0) return SKPoint.Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var proj = new SKPoint(a.X + t * dx, a.Y + t * dy);
        return SKPoint.Distance(p, proj);
    }
}
