using Parpa.Interop;

namespace Parpa.Services;

/// <summary>Atajo global (RegisterHotKey) a partir del ajuste <c>keys</c>, p. ej. ["WIN","ALT","N"].</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x5041; // "PA"
    private readonly IntPtr _hwnd;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyService(IntPtr hwnd) => _hwnd = hwnd;

    /// <returns><c>false</c> si otra app ya usa la combinación.</returns>
    public bool Register(IReadOnlyList<string> keys)
    {
        Unregister();
        uint mods = Win32.MOD_NOREPEAT, vk = 0;
        foreach (var key in keys)
        {
            switch (key.ToUpperInvariant())
            {
                case "WIN": mods |= Win32.MOD_WIN; break;
                case "ALT": mods |= Win32.MOD_ALT; break;
                case "CTRL": mods |= Win32.MOD_CONTROL; break;
                case "SHIFT": mods |= Win32.MOD_SHIFT; break;
                case var k when k.Length == 1 && char.IsAsciiLetterOrDigit(k[0]): vk = k[0]; break;
                case var k when k.StartsWith('F') && int.TryParse(k[1..], out var f) && f is >= 1 and <= 24: vk = (uint)(0x70 + f - 1); break;
            }
        }
        if (vk == 0) return false;
        _registered = Win32.RegisterHotKey(_hwnd, HotkeyId, mods, vk);
        return _registered;
    }

    /// <summary>Llamado desde el WndProc de la ventana del notch.</summary>
    public bool HandleMessage(uint msg, IntPtr wParam)
    {
        if (msg != Win32.WM_HOTKEY || wParam != HotkeyId) return false;
        Pressed?.Invoke();
        return true;
    }

    public void Unregister()
    {
        if (_registered) Win32.UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }

    public void Dispose() => Unregister();
}
