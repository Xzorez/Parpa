using Microsoft.UI.Dispatching;
using Parpa.Interop;

namespace Parpa.Services;

/// <summary>
/// Detecta clics fuera del notch mientras está abierto (gancho WH_MOUSE_LL).
/// Solo se instala con el notch abierto: en reposo no hay gancho ni coste.
/// </summary>
public sealed class OutsideClickWatcher : IDisposable
{
    private readonly Func<Win32.RECT> _getBounds;
    private readonly DispatcherQueue _dispatcher;
    private readonly Win32.HookProc _proc;
    private IntPtr _hook;

    public event Action? ClickedOutside;

    public OutsideClickWatcher(Func<Win32.RECT> getBounds)
    {
        _getBounds = getBounds;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _proc = HookCallback;
    }

    public bool IsActive => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsActive) return;
        _hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (!IsActive) return;
        Win32.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (uint)wParam;
            if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                if (!_getBounds().Contains(data.pt))
                    _dispatcher.TryEnqueue(() => ClickedOutside?.Invoke());
            }
        }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
