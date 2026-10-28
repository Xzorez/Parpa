using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Parpa.Interop;
using Parpa.Services;
using Parpa.ViewModels;
using Parpa.Views;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Parpa.AppWindows;

/// <summary>
/// Ventana del notch: sin borde, siempre encima, no activable y transparente.
/// Tamaño fijo = mayor estado + orejas + margen de sombra; la forma se dibuja dentro.
/// El ratón solo se captura dentro de la región (forma actual + <c>hoverSens</c>), fuera pasa a las apps de debajo.
/// </summary>
public sealed partial class NotchWindow : Window
{
    private const double ShadowMargin = 24;

    private readonly IntPtr _hwnd;
    private readonly ParpaSettings _s;
    private readonly NotchViewModel _vm;
    private readonly HotkeyService _hotkey;
    private readonly OutsideClickWatcher _outside;
    private readonly ClockService _clock = new();
    private readonly UISettings _uiSettings = new();
    private readonly Win32.SubclassProc _wndProc;
    private readonly SystemMonitor _system;
    private readonly NoticeCenter _notices;
    private readonly FullscreenWatcher _fullscreen;
    private readonly IdleView _idle = new();
    private readonly CompactView _compact = new();
    private readonly OsdView _osd = new();
    private readonly PeekView _peek = new();
    private readonly OpenView _open = new();

    private readonly double _ear = Tokens.Current.Radius("ear");
    private readonly double _logicalWidth;
    private readonly double _logicalHeight;
    private double _scale = 1;          // píxeles físicos por unidad de diseño (DPI × size)
    private Win32.RECT _region;         // región actual, en coordenadas de la ventana
    private NotchGeometry _geometry;
    private FrameworkElement? _shownView;
    private Win32.RECT _monitorBounds;  // monitor donde está el notch, en píxeles físicos
    private bool _hiddenForFullscreen;

    public NotchWindow(ParpaSettings settings)
    {
        InitializeComponent();
        _s = settings;
        _hwnd = WindowNative.GetWindowHandle(this);
        Title = Loc.Get("app_name");

        var largest = NotchViewModel.Largest;
        _logicalWidth = largest.Width + 2 * _ear + 2 * ShadowMargin;
        _logicalHeight = largest.Height + ShadowMargin;
        Root.Width = _logicalWidth;
        Root.Height = _logicalHeight;

        ConfigureWindow();
        SystemBackdrop = new WinUIEx.TransparentTintBackdrop();

        _vm = new NotchViewModel(settings);
        _vm.Changed += OnStateChanged;

        Shape.Fill = Tokens.Current.Dark["bg"];
        Shape.AddView(_idle, anchorLeft: false);
        Shape.AddView(_compact, anchorLeft: false);
        Shape.AddView(_osd, anchorLeft: false);
        Shape.AddView(_peek, anchorLeft: true);
        Shape.AddView(_open, anchorLeft: true);
        Shape.TransitionCompleted += () => UpdateRegion(_geometry, null);
        _idle.Apply(_s, ReducedMotion);
        _open.SetPage(_vm.Page);

        _clock.MinuteChanged += () => { _idle.Apply(_s, ReducedMotion); _open.Apply(_s); };

        _hotkey = new HotkeyService(_hwnd);
        _hotkey.Pressed += _vm.OnHotkey;
        if (!_hotkey.Register(_s.Keys))
            Debug.WriteLine($"Parpa: atajo {string.Join("+", _s.Keys)} en uso por otra app");

        _outside = new OutsideClickWatcher(ScreenRegion);
        _outside.ClickedOutside += _vm.OnClickedOutside;

        _wndProc = WndProc;
        Win32.SetWindowSubclass(_hwnd, _wndProc, 1, IntPtr.Zero);

        _geometry = _vm.Geometry;
        Reposition();
        Shape.Show(_idle, _geometry, motion: null);
        _shownView = _idle;
        _idle.SetAnimating(true);

        _system = new SystemMonitor(_vm, _s);
        _system.Start();

        _notices = new NoticeCenter(_vm, _s);
        _ = _notices.StartAsync();

        // Pantalla completa en el monitor del notch + hideFull: se aparta (sin tapar ni capturar clics) y los avisos esperan.
        _fullscreen = new FullscreenWatcher(_hwnd, () => _monitorBounds);
        _fullscreen.Changed += fullscreen =>
        {
            _hiddenForFullscreen = fullscreen && _s.HideFull;
            Log.Info($"Pantalla completa: {fullscreen} → notch {(_hiddenForFullscreen ? "oculto" : "visible")}");
            _vm.SetSuppressed(_hiddenForFullscreen);
            if (_hiddenForFullscreen) Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
            else ShowWithoutActivation();
        };
        _fullscreen.Start();

        Closed += (_, _) => { _hotkey.Dispose(); _outside.Dispose(); _system.Dispose(); _notices.Dispose(); };
    }

    /// <summary>Muestra la ventana sin quitar el foco a la app activa (salvo que esté apartada por pantalla completa).</summary>
    public void ShowWithoutActivation()
    {
        if (!_hiddenForFullscreen) Win32.ShowWindow(_hwnd, Win32.SW_SHOWNOACTIVATE);
    }

    private void ConfigureWindow()
    {
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_STYLE, new IntPtr(Win32.WS_POPUP | Win32.WS_CLIPCHILDREN));
        var ex = Win32.GetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE).ToInt64();
        ex |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST;
        Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));

        // Windows 11: sin esquinas redondeadas ni borde de DWM.
        var corner = Win32.DWMWCP_DONOTROUND;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        var border = Win32.DWMWA_COLOR_NONE;
        Win32.DwmSetWindowAttribute(_hwnd, Win32.DWMWA_BORDER_COLOR, ref border, sizeof(uint));

        // Alfa por píxel: DWM compone la ventana con transparencia (región de desenfoque vacía = sin desenfoque).
        var emptyRgn = Win32.CreateRectRgn(-2, -2, -1, -1);
        var blur = new Win32.DWM_BLURBEHIND { dwFlags = Win32.DWM_BB_ENABLE | Win32.DWM_BB_BLURREGION, fEnable = true, hRgnBlur = emptyRgn };
        Win32.DwmEnableBlurBehindWindow(_hwnd, ref blur);
        Win32.DeleteObject(emptyRgn);
        var margins = new Win32.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        Win32.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Borde superior del monitor elegido; horizontal = 22 / 50 / 78 % del ancho según <c>pos</c> + <c>offset</c>.
    /// Escala <c>size</c> con origen centro-superior.
    /// </summary>
    private void Reposition()
    {
        var monitor = MonitorService.Get(_s.Monitor);
        _monitorBounds = monitor.Bounds;
        var size = Math.Clamp(_s.Size, 80, 120) / 100;
        _scale = monitor.Scale * size;

        var width = (int)Math.Round(_logicalWidth * _scale);
        var height = (int)Math.Round(_logicalHeight * _scale);
        var pct = _s.Pos switch { "left" => 0.22, "right" => 0.78, _ => 0.5 };
        var anchorX = monitor.Bounds.Left + monitor.Bounds.Width * pct + _s.Offset * monitor.Scale;
        var x = (int)Math.Round(anchorX - width / 2.0);

        Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, x, monitor.Bounds.Top, width, height, Win32.SWP_NOACTIVATE);
        RootScale.ScaleX = RootScale.ScaleY = size;
        UpdateRegion(_geometry, null);
    }

    /// <summary>
    /// La ventana solo existe (para el ratón y el dibujo) dentro de esta región: forma + orejas + <c>hoverSens</c>.
    /// Mientras se anima, cubre la forma de origen y la de destino (más el pequeño rebote de la curva).
    /// </summary>
    private void UpdateRegion(NotchGeometry a, NotchGeometry? b)
    {
        var width = a.Width;
        var height = a.Height;
        if (b is { } t)
        {
            width = Math.Max(width, t.Width) + Math.Abs(a.Width - t.Width) * 0.02 + 2;
            height = Math.Max(height, t.Height) + Math.Abs(a.Height - t.Height) * 0.02 + 2;
        }

        var half = width / 2 + _ear + _s.HoverSens;
        var cx = _logicalWidth / 2;
        _region = new Win32.RECT
        {
            Left = (int)Math.Floor((cx - half) * _scale),
            Top = 0,
            Right = (int)Math.Ceiling((cx + half) * _scale),
            Bottom = (int)Math.Ceiling((height + _s.HoverSens) * _scale),
        };
        var rgn = Win32.CreateRectRgn(_region.Left, _region.Top, _region.Right, _region.Bottom);
        Win32.SetWindowRgn(_hwnd, rgn, true); // el sistema se queda con la región
    }

    private Win32.RECT ScreenRegion()
    {
        Win32.GetWindowRect(_hwnd, out var window);
        return new Win32.RECT
        {
            Left = window.Left + _region.Left,
            Top = window.Top + _region.Top,
            Right = window.Left + _region.Right,
            Bottom = window.Top + _region.Bottom,
        };
    }

    private void OnStateChanged()
    {
        var from = _geometry;
        _geometry = _vm.Geometry;

        FrameworkElement view;
        switch (_vm.State)
        {
            case NotchState.Open:
                _open.SetPage(_vm.Page);
                _open.Height = _geometry.Height;
                _open.Apply(_s);
                view = _open;
                break;
            case NotchState.Peek:
                _peek.Show(_vm, Tokens.Current.Accent(_s.Accent));
                view = _peek;
                break;
            case NotchState.Osd:
                _osd.Show(_vm.Indicator!, Tokens.Current.Accent(_s.Accent));
                view = _osd;
                break;
            case NotchState.Activity:
                _compact.Show(_vm.Activity!, _s.Dnd);
                view = _compact;
                break;
            default:
                _idle.Apply(_s, ReducedMotion);
                view = _idle;
                break;
        }

        // Solo se anima lo que se ve: en reposo no corre ningún temporizador innecesario.
        _idle.SetAnimating(view == _idle);
        _compact.SetVisible(view == _compact);
        _peek.SetVisible(view == _peek);
        if (_vm.IsOpen) _outside.Start();
        else _outside.Stop();

        if (from == _geometry && view == _shownView) return; // solo cambió el contenido (p. ej. otro volumen)
        _shownView = view;
        UpdateRegion(from, _geometry);
        Shape.Show(view, _geometry, ReducedMotion ? Tokens.Current.ReducedMotion : Tokens.Current.ShapeMotion);
    }

    /// <summary>Animaciones reducidas por el ajuste <c>anim</c> o por Windows ("Efectos de animación").</summary>
    private bool ReducedMotion => _s.Anim == "reduced" || !_uiSettings.AnimationsEnabled;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr id, IntPtr data)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEACTIVATE:
                return new IntPtr(Win32.MA_NOACTIVATE);
            case Win32.WM_NCCALCSIZE when wParam != IntPtr.Zero:
                return IntPtr.Zero; // toda la ventana es área cliente: sin marco ni franja superior
            case Win32.WM_ERASEBKGND:
                // Con blur-behind activo, el negro GDI se compone como transparente.
                Win32.GetClientRect(hWnd, out var client);
                Win32.FillRect(wParam, ref client, Win32.GetStockObject(Win32.BLACK_BRUSH));
                return new IntPtr(1);
            case Win32.WM_HOTKEY when _hotkey.HandleMessage(msg, wParam):
                return IntPtr.Zero;
            case Win32.WM_DPICHANGED:
            case Win32.WM_DISPLAYCHANGE:
                var result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                DispatcherQueue.TryEnqueue(Reposition);
                return result;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs e) => _vm.OnPointerEntered();

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e) => _vm.OnPointerExited();

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed) _vm.OnClicked();
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => _vm.OnWheel();
}
