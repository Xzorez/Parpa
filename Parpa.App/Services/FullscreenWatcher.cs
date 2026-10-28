using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Parpa.Interop;

namespace Parpa.Services;

/// <summary>
/// Pantalla completa en el monitor del notch (sondeo de 1 s, sin coste apreciable).
/// Cuenta como pantalla completa:
/// <list type="bullet">
/// <item>la ventana más alta de la pila en ese monitor lo cubre entero (vídeo, juego sin bordes, navegador F11);</item>
/// <item>un juego D3D exclusivo o el modo presentación (SHQueryUserNotificationState).</item>
/// </list>
/// QUNS_BUSY no se usa: es global (salta con un vídeo en otro monitor) y da falsos positivos.
/// Con <c>hideFull</c>, el notch se oculta y los avisos esperan en la cola.
/// </summary>
public sealed class FullscreenWatcher
{
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const int QUNS_PRESENTATION_MODE = 4;
    private const int DWMWA_CLOAKED = 14;
    private const long WS_EX_TRANSPARENT = 0x00000020L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_NOACTIVATE = 0x08000000L;

    /// <summary>Ventanas del escritorio/shell que cubren la pantalla pero no son "pantalla completa".</summary>
    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly IntPtr _ownWindow;
    private readonly Func<Win32.RECT> _monitorBounds;
    private readonly DispatcherQueueTimer _timer;
    private readonly EnumWindowsProc _enumProc;
    private Win32.RECT _bounds;
    private bool _found;

    public bool IsFullscreen { get; private set; }

    public event Action<bool>? Changed;

    /// <param name="ownWindow">Ventana del notch (se ignora).</param>
    /// <param name="monitorBounds">Rectángulo del monitor donde está el notch, en píxeles físicos.</param>
    public FullscreenWatcher(IntPtr ownWindow, Func<Win32.RECT> monitorBounds)
    {
        _ownWindow = ownWindow;
        _monitorBounds = monitorBounds;
        _enumProc = OnWindow;
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        Check();
        _timer.Start();
    }

    private void Check()
    {
        var fullscreen = (SHQueryUserNotificationState(out var state) == 0
                && state is QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE)
            || TopWindowCoversMonitor();
        if (fullscreen == IsFullscreen) return;
        IsFullscreen = fullscreen;
        Changed?.Invoke(fullscreen);
    }

    private bool TopWindowCoversMonitor()
    {
        _bounds = _monitorBounds();
        _found = false;
        EnumWindows(_enumProc, IntPtr.Zero); // de arriba abajo en la pila
        return _found;
    }

    /// <returns><c>false</c> para parar en la primera ventana de app visible en el monitor.</returns>
    private bool OnWindow(IntPtr hwnd, IntPtr _)
    {
        if (hwnd == _ownWindow || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
        var ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
        if ((ex & (WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)) != 0) return true; // superposiciones
        if (!Win32.GetWindowRect(hwnd, out var rect) || rect.Width <= 0 || rect.Height <= 0) return true;

        // ¿Toca el monitor del notch?
        if (rect.Right <= _bounds.Left || rect.Left >= _bounds.Right || rect.Bottom <= _bounds.Top || rect.Top >= _bounds.Bottom) return true;

        var cls = new StringBuilder(64);
        GetClassName(hwnd, cls, cls.Capacity);
        if (ShellClasses.Contains(cls.ToString())) return true;

        _found = rect.Left <= _bounds.Left && rect.Top <= _bounds.Top && rect.Right >= _bounds.Right && rect.Bottom >= _bounds.Bottom;
        return false;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
}
