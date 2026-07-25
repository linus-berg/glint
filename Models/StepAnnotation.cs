using SkiaSharp;

namespace Glint.Models;

public class StepAnnotation : AnnotationBase
{
    public SKPoint Position { get; set; }
    public int Number { get; set; }

    public override void Render(SKCanvas canvas)
    {
        var radius = Math.Max(14f, StrokeWidth * 3f);
        
        // Draw circle background
        using var bgPaint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(Position, radius, bgPaint);

        var luminance = 0.299 * Color.Red + 0.587 * Color.Green + 0.114 * Color.Blue;
        var contrastColor = luminance > 160 ? SKColors.Black : SKColors.White;

        // Draw circle border (optional, but adds contrast)
        using var borderPaint = new SKPaint
        {
            Color = contrastColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            IsAntialias = true
        };
        canvas.DrawCircle(Position, radius, borderPaint);

        // Draw text
        using var typeface = SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold);
        using var font = new SKFont(typeface, radius * 1.2f);
        
        using var textPaint = new SKPaint
        {
            Color = contrastColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Calculate vertical centering
        font.GetFontMetrics(out var metrics);
        var textY = Position.Y - (metrics.Ascent + metrics.Descent) / 2f;

        canvas.DrawText(Number.ToString(), Position.X, textY, SKTextAlign.Center, font, textPaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        var radius = Math.Max(14f, StrokeWidth * 3f);
        var dx = point.X - Position.X;
        var dy = point.Y - Position.Y;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    public override SKRect GetBounds()
    {
        var radius = Math.Max(14f, StrokeWidth * 3f);
        return SKRect.Create(Position.X - radius, Position.Y - radius, radius * 2, radius * 2);
    }

    public override AnnotationBase Clone()
    {
        return new StepAnnotation
        {
            Position = Position,
            Number = Number,
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
