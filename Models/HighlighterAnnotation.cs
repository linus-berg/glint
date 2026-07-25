using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace Glint.Models;

public class HighlighterAnnotation : AnnotationBase
{
    public SKPath Path { get; set; } = new SKPath();

    public override void Render(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(Color.Red, Color.Green, Color.Blue, 120), // 50% opacity
            StrokeWidth = StrokeWidth * 3f, // Make it thicker like a marker
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
            BlendMode = SKBlendMode.Multiply // Multiply blend mode for highlighter effect
        };

        canvas.DrawPath(Path, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        Path.GetTightBounds(out var bounds);
        return bounds.Contains(point);
    }

    public override SKRect GetBounds()
    {
        Path.GetTightBounds(out var bounds);
        var expand = StrokeWidth * 3f / 2f;
        bounds.Inflate(expand, expand);
        return bounds;
    }

    public override void Translate(float dx, float dy)
    {
        Path.Transform(SKMatrix.CreateTranslation(dx, dy));
    }

    public override AnnotationBase Clone()
    {
        return new HighlighterAnnotation
        {
            Path = new SKPath(Path),
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
