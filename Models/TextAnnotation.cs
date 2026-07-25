using SkiaSharp;

namespace Glint.Models;

public class TextAnnotation : AnnotationBase
{
    public SKPoint Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; } = 20f;
    public bool IsEditing { get; set; }

    public override void Render(SKCanvas canvas)
    {
        if (string.IsNullOrEmpty(Text) && !IsEditing) return;

        using var typeface = SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold);
        using var font = new SKFont(typeface, FontSize);
        
        // Stylish outline and shadow
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 160),
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = Math.Max(2f, FontSize * 0.15f),
            StrokeJoin = SKStrokeJoin.Round,
            ImageFilter = SKImageFilter.CreateDropShadow(0, 3f, 4f, 4f, new SKColor(0, 0, 0, 120))
        };

        // Main text color
        using var paint = new SKPaint
        {
            Color = Color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        font.GetFontMetrics(out var metrics);

        // Draw the text stroke/shadow first
        canvas.DrawText(Text, Position.X, Position.Y, SKTextAlign.Left, font, shadowPaint);

        // Draw the main text color over it
        canvas.DrawText(Text, Position.X, Position.Y, SKTextAlign.Left, font, paint);

        // Draw cursor if editing
        if (IsEditing)
        {
            float textAdvance = font.MeasureText(Text, out _, paint);
            var cursorX = Position.X + textAdvance + 2;
            using var cursorPaint = new SKPaint
            {
                Color = Color,
                StrokeWidth = Math.Max(2, FontSize * 0.1f),
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(cursorX, Position.Y + metrics.Ascent, cursorX, Position.Y + metrics.Descent, cursorPaint);
        }
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        var b = GetBounds();
        return SKRect.Inflate(b, tolerance, tolerance).Contains(point);
    }

    public override SKRect GetBounds()
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Inter", SKFontStyle.Bold), FontSize);
        font.GetFontMetrics(out var metrics);
        
        var displayText = string.IsNullOrEmpty(Text) ? "A" : Text;
        float advance = font.MeasureText(displayText, out _);
        
        return SKRect.Create(
            Position.X,
            Position.Y + metrics.Ascent,
            advance,
            metrics.Descent - metrics.Ascent
        );
    }

    public override void Translate(float dx, float dy)
    {
        Position = new SKPoint(Position.X + dx, Position.Y + dy);
    }

    public override AnnotationBase Clone()
    {
        return new TextAnnotation
        {
            Position = Position,
            Text = Text,
            FontSize = FontSize,
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
