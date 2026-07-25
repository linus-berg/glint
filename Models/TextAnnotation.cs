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

        // Draw background for readability
        var displayText = Text;
        if (IsEditing && string.IsNullOrEmpty(displayText))
            displayText = "|";

        var bounds = new SKRect();
        font.MeasureText(displayText, out bounds, paint);
        
        using var bgPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 80),
            Style = SKPaintStyle.Fill
        };
        
        var bgRect = SKRect.Create(
            Position.X + bounds.Left - 4,
            Position.Y + bounds.Top - 2,
            bounds.Width + 8,
            bounds.Height + 4
        );
        canvas.DrawRoundRect(bgRect, 3, 3, bgPaint);

        canvas.DrawText(displayText, Position.X, Position.Y, font, paint);

        // Draw cursor if editing
        if (IsEditing)
        {
            font.MeasureText(Text, out var textBounds, paint);
            var cursorX = Position.X + textBounds.Width;
            if (string.IsNullOrEmpty(Text)) cursorX = Position.X;
            using var cursorPaint = new SKPaint
            {
                Color = Color,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawLine(cursorX, Position.Y - FontSize + 4, cursorX, Position.Y + 4, cursorPaint);
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
        var bounds = new SKRect();
        var displayText = string.IsNullOrEmpty(Text) ? "A" : Text;
        font.MeasureText(displayText, out bounds);
        return SKRect.Create(
            Position.X + bounds.Left,
            Position.Y + bounds.Top,
            bounds.Width,
            bounds.Height
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
