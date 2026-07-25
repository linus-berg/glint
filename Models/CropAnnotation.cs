using SkiaSharp;

namespace Glint.Models;

/// <summary>
/// A transient annotation used to visualize the crop area.
/// It is never saved to the final annotation list.
/// </summary>
public class CropAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }
    public SKRect FullImageBounds { get; set; }

    public SKRect GetCropRect()
    {
        return new SKRect(
            Math.Min(Start.X, End.X),
            Math.Min(Start.Y, End.Y),
            Math.Max(Start.X, End.X),
            Math.Max(Start.Y, End.Y)
        );
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetCropRect();

        // Draw dimming overlay over the whole image EXCEPT the crop area
        using var dimPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 150),
            Style = SKPaintStyle.Fill
        };

        // Top
        canvas.DrawRect(FullImageBounds.Left, FullImageBounds.Top, FullImageBounds.Width, rect.Top - FullImageBounds.Top, dimPaint);
        // Bottom
        canvas.DrawRect(FullImageBounds.Left, rect.Bottom, FullImageBounds.Width, FullImageBounds.Bottom - rect.Bottom, dimPaint);
        // Left
        canvas.DrawRect(FullImageBounds.Left, rect.Top, rect.Left - FullImageBounds.Left, rect.Height, dimPaint);
        // Right
        canvas.DrawRect(rect.Right, rect.Top, FullImageBounds.Right - rect.Right, rect.Height, dimPaint);

        // Draw crop border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = false
        };
        canvas.DrawRect(rect, borderPaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f) => false;
    
    public override SKRect GetBounds() => FullImageBounds;
    
    public override AnnotationBase Clone() => new CropAnnotation
    {
        Start = Start, End = End, FullImageBounds = FullImageBounds
    };

    public override void Translate(float dx, float dy) { }
}
