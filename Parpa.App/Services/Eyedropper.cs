using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Parpa.Interop;
using Windows.UI;

namespace Parpa.Services;

/// <summary>
/// Cuentagotas: mientras está activo, el color bajo el cursor se lee cada 50 ms y el siguiente clic izquierdo en
/// cualquier parte lo elige (ese clic no llega a la app de debajo). Esc o clic derecho cancelan; a los 30 s se rinde.
/// Los ganchos de ratón y teclado solo existen mientras se elige.
/// </summary>
public sealed class Eyedropper : IDisposable
{
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(30);

    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _sample;
    private readonly Win32.HookProc _mouseProc;
    private readonly Win32.HookProc _keyProc;
    private IntPtr _mouseHook;
    private IntPtr _keyHook;
    private Color? _picked;
    private DateTime _deadline;
    private Color _color;

    public bool IsActive => _mouseHook != IntPtr.Zero;

    /// <summary>Color bajo el cursor (en el hilo de la interfaz).</summary>
    public event Action<Color>? Hover;

    /// <summary>Color elegido, o null si se canceló (en el hilo de la interfaz).</summary>
    public event Action<Color?>? Finished;

    public Eyedropper()
    {
        _mouseProc = MouseHook;
        _keyProc = KeyHook;
        _sample = _dispatcher.CreateTimer();
        _sample.Interval = TimeSpan.FromMilliseconds(50);
        _sample.Tick += (_, _) =>
        {
            if (DateTime.UtcNow > _deadline)
            {
                Stop(null);
                return;
            }
            var color = Sample();
            if (color == _color) return;
            _color = color;
            Hover?.Invoke(color);
        };
    }

    public void Start()
    {
        if (IsActive) return;
        var module = Win32.GetModuleHandle(null);
        _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, module, 0);
        _keyHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyProc, module, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            Log.Info($"Cuentagotas: sin gancho de ratón ({Marshal.GetLastWin32Error()})");
            Unhook();
            Finished?.Invoke(null);
            return;
        }
        _picked = null;
        _deadline = DateTime.UtcNow + GiveUpAfter;
        _color = Sample();
        Hover?.Invoke(_color);
        _sample.Start();
    }

    public void Cancel() => Stop(null);

    public void Dispose() => Unhook();

    private void Stop(Color? picked)
    {
        if (!IsActive) return;
        _sample.Stop();
        Unhook();
        Finished?.Invoke(picked);
    }

    private void Unhook()
    {
        if (_mouseHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mouseHook);
        if (_keyHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_keyHook);
        _mouseHook = _keyHook = IntPtr.Zero;
    }

    /// <summary>Píxel de la pantalla bajo el cursor (coordenadas físicas: la app declara DPI por monitor).</summary>
    private static Color Sample()
    {
        Win32.GetCursorPos(out var point);
        var dc = Win32.GetDC(IntPtr.Zero);
        try
        {
            var bgr = Win32.GetPixel(dc, point.X, point.Y);
            return Color.FromArgb(255, (byte)bgr, (byte)(bgr >> 8), (byte)(bgr >> 16));
        }
        finally
        {
            Win32.ReleaseDC(IntPtr.Zero, dc);
        }
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            // El clic se queda en el cuentagotas; se termina al soltar, para que la app de debajo no reciba
            // un "soltar" suelto (el derecho abriría su menú).
            switch ((uint)wParam)
            {
                case Win32.WM_LBUTTONDOWN:
                    _picked = Sample();
                    return 1;
                case Win32.WM_LBUTTONUP:
                    var color = _picked ?? Sample();
                    _dispatcher.TryEnqueue(() => Stop(color));
                    return 1;
                case Win32.WM_RBUTTONDOWN:
                    return 1;
                case Win32.WM_RBUTTONUP:
                    _dispatcher.TryEnqueue(() => Stop(null));
                    return 1;
            }
        }
        return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (uint)wParam == Win32.WM_KEYDOWN)
        {
            var key = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            if (key.vkCode == Win32.VK_ESCAPE)
            {
                _dispatcher.TryEnqueue(() => Stop(null));
                return 1;
            }
        }
        return Win32.CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }
}
