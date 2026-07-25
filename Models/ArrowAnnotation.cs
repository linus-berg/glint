using SkiaSharp;

namespace Glint.Models;

public class ArrowAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }

    public override void Render(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = Color,
            StrokeWidth = StrokeWidth,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        
        var angle = (float)Math.Atan2(dy, dx);
        
        if (length > 0)
        {
            // Shorten the line by StrokeWidth so the round cap doesn't poke out the tip of the arrowhead
            var shortenBy = Math.Min(length, StrokeWidth * 1.2f);
            var lineEnd = new SKPoint(
                End.X - shortenBy * (dx / length),
                End.Y - shortenBy * (dy / length)
            );
            canvas.DrawLine(Start, lineEnd, paint);
        }

        // Draw arrowhead
        var headLength = Math.Max(StrokeWidth * 4, 15f);
        var headAngle = (float)(Math.PI / 6); // 30 degrees

        var p1 = new SKPoint(
            End.X - headLength * (float)Math.Cos(angle - headAngle),
            End.Y - headLength * (float)Math.Sin(angle - headAngle)
        );
        var p2 = new SKPoint(
            End.X - headLength * (float)Math.Cos(angle + headAngle),
            End.Y - headLength * (float)Math.Sin(angle + headAngle)
        );

        using var fillPaint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(End);
        pathBuilder.LineTo(p1);
        pathBuilder.LineTo(p2);
        pathBuilder.Close();
        
        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, fillPaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        return DistanceToSegment(point, Start, End) <= tolerance + StrokeWidth / 2;
    }

    public override SKRect GetBounds()
    {
        return new SKRect(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)
        );
    }

    public override AnnotationBase Clone()
    {
        return new ArrowAnnotation
        {
            Start = Start,
            End = End,
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
