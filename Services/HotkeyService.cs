using SharpHook;
using SharpHook.Native;

namespace Glint.Services;

/// <summary>
/// Registers global hotkeys using SharpHook (libuiohook).
/// Listens for PrintScreen to trigger screen capture.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly SimpleGlobalHook _hook;
    private bool _disposed;

    /// <summary>
    /// Raised on a background thread when the capture hotkey is pressed.
    /// </summary>
    public event Action? CaptureRequested;

    public HotkeyService()
    {
        _hook = new SimpleGlobalHook(globalHookType: GlobalHookType.Keyboard);
        _hook.KeyPressed += OnKeyPressed;
    }

    /// <summary>
    /// Starts listening for global hotkeys. Call once at app startup.
    /// Runs the hook on a background thread so it doesn't block the UI.
    /// </summary>
    public void Start()
    {
        Task.Run(async () =>
        {
            try
            {
                await _hook.RunAsync();
            }
            catch (HookException ex)
            {
                // On macOS this fails if Accessibility permission is not granted.
                Console.Error.WriteLine($"[Glint] Global hotkey hook failed: {ex.Message}");
                Console.Error.WriteLine("[Glint] On macOS, grant Accessibility permission in System Settings → Privacy & Security → Accessibility.");
            }
        });
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcPrintScreen)
        {
            CaptureRequested?.Invoke();
            // Suppress the default PrintScreen behaviour so the OS doesn't also handle it
            e.SuppressEvent = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hook.KeyPressed -= OnKeyPressed;

        // Dispose will also stop the hook
        _hook.Dispose();
    }
}
