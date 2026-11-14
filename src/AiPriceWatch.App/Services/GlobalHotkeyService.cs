using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AiPriceWatch.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4150; // "AP"
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;

    private HwndSource? _hwndSource;
    private nint _windowHandle;
    private bool _isRegistered;
    private readonly Action _onHotkeyPressed;

    public GlobalHotkeyService(Action onHotkeyPressed)
    {
        ArgumentNullException.ThrowIfNull(onHotkeyPressed);
        _onHotkeyPressed = onHotkeyPressed;
    }

    public bool Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        _isRegistered = RegisterHotKey(
            _windowHandle,
            HotkeyId,
            ModControl | ModShift | ModNoRepeat,
            VkSpace);

        return _isRegistered;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _onHotkeyPressed();
            handled = true;
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        if (_isRegistered && _windowHandle != nint.Zero)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
