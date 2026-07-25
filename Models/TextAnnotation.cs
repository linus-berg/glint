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

        using var font = new SKFont(SKTypeface.FromFamilyName("Inter", SKFontStyle.Normal), FontSize);
        using var paint = new SKPaint
        {
            Color = Color,
            IsAntialias = true
        };

        font.GetFontMetrics(out var metrics);

        // Draw background for readability
        var displayText = Text;
        if (IsEditing && string.IsNullOrEmpty(displayText))
            displayText = " "; // Use a space to measure height if empty

        float advance = font.MeasureText(displayText, out _, paint);
        
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 80),
            Style = SKPaintStyle.Fill
        };
        
        var bgRect = SKRect.Create(
            Position.X - 4,
            Position.Y + metrics.Ascent - 2,
            advance + 8,
            (metrics.Descent - metrics.Ascent) + 4
        );
        canvas.DrawRoundRect(bgRect, 3, 3, bgPaint);

        canvas.DrawText(Text, Position.X, Position.Y, font, paint);

        // Draw cursor if editing
        if (IsEditing)
        {
            float textAdvance = font.MeasureText(Text, out _, paint);
            var cursorX = Position.X + textAdvance;
            using var cursorPaint = new SKPaint
            {
                Color = Color,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
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
        using var font = new SKFont(SKTypeface.FromFamilyName("Inter", SKFontStyle.Normal), FontSize);
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
