using Glint.Models;
using SkiaSharp;

namespace Glint.Services;

public static class ImageExportService
{
    /// <summary>
    /// Composites the screenshot with all annotations and returns the final bitmap.
    /// </summary>
    public static SKBitmap Composite(SKBitmap screenshot, IReadOnlyList<AnnotationBase> annotations)
    {
        var result = screenshot.Copy();
        
        // First pass: apply blur annotations to the bitmap
        foreach (var annotation in annotations)
        {
            if (annotation is BlurAnnotation blur)
            {
                blur.ApplyBlur(result);
            }
        }

        // Second pass: render all other annotations
        using var canvas = new SKCanvas(result);
        foreach (var annotation in annotations)
        {
            if (annotation is not BlurAnnotation)
            {
                annotation.Render(canvas);
            }
        }

        return result;
    }

    /// <summary>
    /// Saves the composited image to a file.
    /// </summary>
    public static async Task SaveAsync(SKBitmap bitmap, string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        await using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Encodes the bitmap to bytes for clipboard.
    /// </summary>
    public static byte[] EncodeToBytes(SKBitmap bitmap, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        return data.ToArray();
    }
}
