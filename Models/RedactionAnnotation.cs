using SkiaSharp;

namespace Glint.Models;

public class RedactionAnnotation : AnnotationBase
{
    public SKPoint Start { get; set; }
    public SKPoint End { get; set; }

    public override void Render(SKCanvas canvas)
    {
        // Visuals are completely handled by ApplyRedaction compositing
    }

    public void ApplyRedaction(SKBitmap bitmap)
    {
        var rect = GetBounds();
        var rectI = SKRectI.Round(rect);
        var bitmapBounds = new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        
        if (!rectI.IntersectsWith(bitmapBounds)) return;
        rectI.Intersect(bitmapBounds);
        if (rectI.Width <= 0 || rectI.Height <= 0) return;
        
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, rectI)) return;
        
        // Calculate pixel block size (larger stroke width = bigger pixels)
        var pxSize = Math.Max(5, (int)(StrokeWidth * 3));
        var sw = Math.Max(1, rectI.Width / pxSize);
        var sh = Math.Max(1, rectI.Height / pxSize);
        
        var smallInfo = new SKImageInfo(sw, sh, bitmap.ColorType, bitmap.AlphaType);
        using var small = subset.Resize(smallInfo, SKSamplingOptions.Default);
        
        if (small == null) return; // Fallback if resize fails
        
        using var pixelated = small.Resize(subset.Info, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        
        using var canvas = new SKCanvas(bitmap);
        if (pixelated != null)
        {
            canvas.DrawBitmap(pixelated, rectI.Left, rectI.Top);
        }
        
        // Add a very subtle tinted overlay of the user's selected color
        // This gives the pixelation a "cool" stylized tech vibe that matches the palette
        using var tintPaint = new SKPaint
        {
            Color = Color.WithAlpha(40), // 15% opacity tint
            Style = SKPaintStyle.Fill,
            BlendMode = SKBlendMode.SrcOver
        };
        canvas.DrawRect(rectI, tintPaint);

        // Sleek outer border to frame the redaction block
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = false
        };
        canvas.DrawRect(rectI, borderPaint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 8f)
    {
        return GetBounds().Contains(point);
    }

    public override SKRect GetBounds()
    {
        var left = Math.Min(Start.X, End.X);
        var right = Math.Max(Start.X, End.X);
        var top = Math.Min(Start.Y, End.Y);
        var bottom = Math.Max(Start.Y, End.Y);
        return new SKRect(left, top, right, bottom);
    }

    public override void Translate(float dx, float dy)
    {
        Start = new SKPoint(Start.X + dx, Start.Y + dy);
        End = new SKPoint(End.X + dx, End.Y + dy);
    }

    public override AnnotationBase Clone()
    {
        return new RedactionAnnotation
        {
            Start = Start,
            End = End,
            Color = Color,
            StrokeWidth = StrokeWidth
        };
    }
}
