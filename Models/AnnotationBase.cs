using SkiaSharp;

namespace Glint.Models;

public abstract class AnnotationBase
{
    public SKColor Color { get; set; } = SKColors.Red;
    public float StrokeWidth { get; set; } = 3f;
    public bool IsSelected { get; set; }
    
    public abstract void Render(SKCanvas canvas);
    public abstract bool HitTest(SKPoint point, float tolerance = 8f);
    public abstract SKRect GetBounds();
    public abstract AnnotationBase Clone();
}
