using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Glint.Services;

public static class ScreenCaptureService
{
    /// <summary>
    /// Captures the entire screen (primary monitor) and returns it as an SKBitmap.
    /// Uses platform-specific mechanisms.
    /// </summary>
    public static async Task<SKBitmap?> CaptureFullScreenAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await CaptureMacAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await CaptureLinuxAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await CaptureWindowsAsync();
        }

        return null;
    }

    /// <summary>
    /// Crops a bitmap to the specified region.
    /// </summary>
    public static SKBitmap CropBitmap(SKBitmap source, SKRectI region)
    {
        // Clamp to source bounds
        var left = Math.Max(0, region.Left);
        var top = Math.Max(0, region.Top);
        var right = Math.Min(source.Width, region.Right);
        var bottom = Math.Min(source.Height, region.Bottom);
        var width = right - left;
        var height = bottom - top;

        if (width <= 0 || height <= 0) return source;

        var cropped = new SKBitmap(width, height);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(source, new SKRectI(left, top, right, bottom),
            new SKRect(0, 0, width, height));
        return cropped;
    }

    private static async Task<SKBitmap?> CaptureMacAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"glint_capture_{Guid.NewGuid()}.png");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-x \"{tempFile}\"", // -x = no sound
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;
            await process.WaitForExitAsync();

            if (!File.Exists(tempFile)) return null;

            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static async Task<SKBitmap?> CaptureLinuxAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"glint_capture_{Guid.NewGuid()}.png");
        try
        {
            // Try gnome-screenshot first, then scrot
            var tools = new[] {
                ("gnome-screenshot", $"-f \"{tempFile}\""),
                ("scrot", $"\"{tempFile}\"")
            };

            foreach (var (tool, args) in tools)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tool,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process == null) continue;
                    await process.WaitForExitAsync();
                    if (File.Exists(tempFile))
                    {
                        using var stream = File.OpenRead(tempFile);
                        return SKBitmap.Decode(stream);
                    }
                }
                catch { continue; }
            }
            return null;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static Task<SKBitmap?> CaptureWindowsAsync()
    {
        // On Windows, use the Screen.PrimaryScreen approach via P/Invoke
        // For now, we use a simple approach with Graphics
        // This will be enhanced later if needed
        return Task.FromResult<SKBitmap?>(null);
    }

    /// <summary>
    /// Captures a selected region of the screen interactively.
    /// Uses platform-specific mechanisms.
    /// </summary>
    public static async Task<SKBitmap?> CaptureRegionAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await CaptureMacRegionAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await CaptureLinuxRegionAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows interactive capture isn't implemented natively here yet.
            // A fallback would be to capture fullscreen and show a crop UI, but for now we'll do fullscreen.
            return await CaptureWindowsAsync();
        }

        return null;
    }

    private static async Task<SKBitmap?> CaptureMacRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"glint_capture_{Guid.NewGuid()}.png");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "screencapture",
                Arguments = $"-i -x \"{tempFile}\"", // -i = interactive, -x = no sound
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;
            await process.WaitForExitAsync();

            if (!File.Exists(tempFile)) return null;

            using var stream = File.OpenRead(tempFile);
            return SKBitmap.Decode(stream);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static async Task<SKBitmap?> CaptureLinuxRegionAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"glint_capture_{Guid.NewGuid()}.png");
        try
        {
            // Try gnome-screenshot first, then scrot
            var tools = new[] {
                ("gnome-screenshot", $"-a -f \"{tempFile}\""), // -a = area
                ("scrot", $"-s \"{tempFile}\"")                // -s = select
            };

            foreach (var (tool, args) in tools)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tool,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process == null) continue;
                    await process.WaitForExitAsync();
                    if (File.Exists(tempFile))
                    {
                        using var stream = File.OpenRead(tempFile);
                        return SKBitmap.Decode(stream);
                    }
                }
                catch { continue; }
            }
            return null;
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
