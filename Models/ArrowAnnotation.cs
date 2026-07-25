using SkiaSharp;

namespace Glint.Models;

public class ArrowAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }

    public override void Render(SKCanvas canvas)
    {
        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return;
        
        var angle = (float)Math.Atan2(dy, dx);
        
        // Arrowhead geometry
        var headLength = Math.Max(StrokeWidth * 3.5f, 18f);
        var headAngle = (float)(Math.PI / 6.5); // Slightly sharper angle

        var p1 = new SKPoint(
            End.X - headLength * (float)Math.Cos(angle - headAngle),
            End.Y - headLength * (float)Math.Sin(angle - headAngle)
        );
        var p2 = new SKPoint(
            End.X - headLength * (float)Math.Cos(angle + headAngle),
            End.Y - headLength * (float)Math.Sin(angle + headAngle)
        );
        
        // The "notch" in the back of the arrowhead (makes it a chevron)
        var notchDistance = headLength * 0.6f;
        var notch = new SKPoint(
            End.X - notchDistance * (float)Math.Cos(angle),
            End.Y - notchDistance * (float)Math.Sin(angle)
        );

        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(End);
        pathBuilder.LineTo(p1);
        pathBuilder.LineTo(notch);
        pathBuilder.LineTo(p2);
        pathBuilder.Close();

        using var headPath = pathBuilder.Detach();

        // Check luminance for border color
        var luminance = 0.299 * Color.Red + 0.587 * Color.Green + 0.114 * Color.Blue;
        var isLight = luminance > 160;
        var borderColor = isLight ? new SKColor(0, 0, 0, 100) : SKColors.White.WithAlpha(220);
        var borderWidth = 2f;

        // Group the arrow drawing into a layer to apply a single unified drop shadow
        using var shadowFilter = SKImageFilter.CreateDropShadow(0, 3f, 5f, 5f, new SKColor(0, 0, 0, 100));
        using var layerPaint = new SKPaint { ImageFilter = shadowFilter };
        
        canvas.SaveLayer(GetBounds(), layerPaint);

        // 1. Draw Outlines (Borders)
        using var lineBorderPaint = new SKPaint
        {
            Color = borderColor,
            StrokeWidth = StrokeWidth + borderWidth * 2,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        // Draw the line up to the notch
        canvas.DrawLine(Start, notch, lineBorderPaint);

        using var headBorderPaint = new SKPaint
        {
            Color = borderColor,
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = borderWidth * 2,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        canvas.DrawPath(headPath, headBorderPaint);

        // 2. Draw Fills (Inner color)
        using var lineFillPaint = new SKPaint
        {
            Color = Color,
            StrokeWidth = StrokeWidth,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };
        canvas.DrawLine(Start, notch, lineFillPaint);

        using var headFillPaint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawPath(headPath, headFillPaint);

        canvas.Restore();
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        return DistanceToSegment(point, Start, End) <= tolerance + StrokeWidth / 2;
    }

    public override SKRect GetBounds()
    {
        var rect = new SKRect(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y)
        );
        rect.Inflate(StrokeWidth * 4 + 20f, StrokeWidth * 4 + 20f); // Account for arrowhead and shadow
        return rect;
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
