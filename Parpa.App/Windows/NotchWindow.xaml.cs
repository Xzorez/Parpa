using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Parpa.Interop;
using Parpa.Services;
using Parpa.ViewModels;
using Parpa.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Parpa.AppWindows;

/// <summary>
/// Ventana del notch: sin borde, siempre encima, no activable y transparente.
/// Tamaño fijo = mayor estado + orejas + margen de sombra; la forma se dibuja dentro.
/// El ratón solo se captura dentro de la región (forma actual + <c>hoverSens</c>), fuera pasa a las apps de debajo.
/// Solo se activa cuando hace falta escribir (nota, nueva tarea) y devuelve el foco al terminar.
/// </summary>
public sealed partial class NotchWindow : Window, IKeyboardHost
{
    private const double ShadowMargin = 24;
    private static readonly TimeSpan SnoozeDelay = TimeSpan.FromMinutes(10);

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
    private readonly ClipboardService _clipboard;
    private readonly ShelfService _shelf;
    private readonly TasksService _tasks = new();
    private readonly NotesService _notes = new();
    private readonly FocusService _focus;
    private readonly IdleView _idle = new();
    private readonly CompactView _compact = new();
    private readonly OsdView _osd = new();
    private readonly BannerView _banner = new();
    private readonly PeekView _peek = new();
    private readonly DropView _drop = new();
    private readonly OpenView _open;

    private readonly double _ear = Tokens.Current.Radius("ear");
    private readonly double _logicalWidth;
    private readonly double _logicalHeight;
    private double _scale = 1;          // píxeles físicos por unidad de diseño (DPI × size)
    private Win32.RECT _region;         // región actual, en coordenadas de la ventana
    private NotchGeometry _geometry;
    private FrameworkElement? _shownView;
    private Win32.RECT _monitorBounds;  // monitor donde está el notch, en píxeles físicos
    private bool _hiddenForFullscreen;
    private bool _typing;
    private IntPtr _previousForeground;
    private DropKind? _dragKind;
    private string? _dragText;
    private readonly MinimalView _minimal = new();
    private bool _wasOpen;
    private NotchPage? _lastPage;
    private NotchPeek? _lastPeek;
    private string _registeredKeys = string.Empty;

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

        _vm = new NotchViewModel(settings, SettingsService.Current.Wins);
        _system = new SystemMonitor(_vm, _s);
        _notices = new NoticeCenter(_vm, _s);
        _clipboard = new ClipboardService(_hwnd, _s);
        _shelf = new ShelfService(_s, _clipboard);
        _focus = new FocusService(_s);
        _open = new OpenView(new NotchServices(_vm, _s, this, _system, _notices, _clipboard, _shelf, _tasks, _notes, _focus));
        _vm.Changed += OnStateChanged;

        OnThemeChanged();
        Theme.Changed += OnThemeChanged;
        SettingsService.Current.Changed += OnSettingsChanged;
        Shape.AddView(_idle, anchorLeft: false);
        Shape.AddView(_minimal, anchorLeft: false);
        Shape.AddView(_compact, anchorLeft: false);
        Shape.AddView(_osd, anchorLeft: false);
        Shape.AddView(_banner, anchorLeft: false);
        Shape.AddView(_peek, anchorLeft: true);
        Shape.AddView(_drop, anchorLeft: true);
        Shape.AddView(_open, anchorLeft: true);
        Shape.TransitionCompleted += () => UpdateRegion(_geometry, null);
        _idle.Apply(_s, ReducedMotion);

        _clock.MinuteChanged += () => { _idle.Apply(_s, ReducedMotion); _open.Apply(_s); };

        _hotkey = new HotkeyService(_hwnd);
        _hotkey.Pressed += _vm.OnHotkey;

        _outside = new OutsideClickWatcher(ScreenRegion);
        _outside.ClickedOutside += () =>
        {
            EndTextInput(restoreFocus: false); // el clic ya lleva el foco a la otra app
            _vm.OnClickedOutside();
        };

        _wndProc = WndProc;
        Win32.SetWindowSubclass(_hwnd, _wndProc, 1, IntPtr.Zero);

        _geometry = _vm.Geometry;
        Reposition();
        Shape.Show(_idle, _geometry, motion: null);
        _shownView = _idle;
        _idle.SetAnimating(true);

        _system.Start();
        _ = _notices.StartAsync();
        WireWindows();

        // Pantalla completa en el monitor del notch + hideFull: se aparta (sin tapar ni capturar clics) y los avisos esperan.
        _fullscreen = new FullscreenWatcher(_hwnd, () => _monitorBounds);
        _fullscreen.Changed += fullscreen =>
        {
            _hiddenForFullscreen = fullscreen && _s.HideFull;
            Log.Info($"Pantalla completa: {fullscreen} → notch {(_hiddenForFullscreen ? "oculto" : "visible")}");
            _vm.SetSuppressed(_hiddenForFullscreen);
            SoundService.Suppressed = _hiddenForFullscreen;
            if (_hiddenForFullscreen) Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
            else ShowWithoutActivation();
        };
        _fullscreen.Start();

        // Atajo ocupado: error breve una vez al arrancar, con enlace a Ajustes.
        _registeredKeys = string.Join("+", _s.Keys);
        if (!_hotkey.Register(_s.Keys))
        {
            Log.Info($"Atajo {string.Join("+", _s.Keys)} en uso por otra app");
            _vm.ShowIndicator(new NotchIndicator(string.Empty, Loc.Get("empty_errors_hotkey_taken"), string.Empty, 0,
                Loc.Get("empty_errors_settings_link"), Banner: true));
        }

        Closed += (_, _) =>
        {
            _hotkey.Dispose();
            _outside.Dispose();
            _system.Dispose();
            _notices.Dispose();
        };
    }

    /// <summary>Portapapeles, tareas y foco: lo que aparece en el notch cerrado.</summary>
    private void WireWindows()
    {
        _clipboard.Start();
        // Ctrl+C en cualquier app → aviso breve "COPIADO" (VER lleva al Portapapeles).
        _clipboard.Copied += entry =>
        {
            if (_vm.IsOpen) return;
            _vm.EnqueuePeek(new NotchPeek(
                Glyph: "copias",
                Head: $"{Loc.Get("windows_clip")} · {Loc.Get("peek_copied")}",
                When: string.Empty,
                Body: entry.Text.ReplaceLineEndings(" "),
                PrimaryLabel: Loc.Get("peek_view"),
                SecondaryLabel: Loc.Get("peek_ok"),
                Priority: NotchPeek.PriorityInfo,
                IsCall: false,
                OnPrimary: () => _vm.ShowPage(NotchPage.Clip),
                Key: "copied"));
        };

        // Tarea a su hora: HECHO / POSPONER (10 min). No molestar la silencia.
        _tasks.Due += task =>
        {
            if (_s.DndActive) return;
            _vm.EnqueuePeek(new NotchPeek(
                Glyph: "tareas",
                Head: $"{Loc.Get("peek_task_due")} · {task.Time}",
                When: string.Empty,
                Body: task.Text,
                PrimaryLabel: Loc.Get("home_done"),
                SecondaryLabel: Loc.Get("peek_snooze"),
                Priority: NotchPeek.PriorityMessage,
                IsCall: false,
                OnPrimary: () => _tasks.Toggle(task.Id),
                OnSecondary: () => _tasks.Snooze(task.Id, SnoozeDelay)));
        };

        // Sesión de foco en marcha o en pausa → actividad "FOCO" con el tiempo en Doto.
        _focus.Changed += () => _vm.SetActivity(NotchViewModel.ActivityFocus,
            _focus.State == FocusService.FocusState.Idle ? null : new NotchActivity(
                SizeToken: "focus",
                Glyph: "foco",
                Label: Loc.Get("compact_focus"),
                Sub: string.Empty,
                IsPlaying: _focus.State == FocusService.FocusState.Running,
                RightText: FocusService.Format(_focus.Remaining)));
    }

    /// <summary>Muestra la ventana sin quitar el foco a la app activa (salvo que esté apartada por pantalla completa).</summary>
    public void ShowWithoutActivation()
    {
        if (!_hiddenForFullscreen) Win32.ShowWindow(_hwnd, Win32.SW_SHOWNOACTIVATE);
    }

    // ---- Teclado (IKeyboardHost) ----

    /// <summary>
    /// Solo aquí se activa la ventana: hay un campo de texto que necesita el teclado.
    /// Se recuerda la app que tenía el foco para devolvérselo al terminar.
    /// </summary>
    public void BeginTextInput(Control control)
    {
        if (!_typing)
        {
            var foreground = Win32.GetForegroundWindow();
            if (foreground != _hwnd) _previousForeground = foreground;
            _typing = true;
            _vm.SetHoldOpen(true);
            Win32.SetForegroundWindow(_hwnd);
            Activate();
        }
        DispatcherQueue.TryEnqueue(() => control.Focus(FocusState.Programmatic));
    }

    public void EndTextInput() => EndTextInput(restoreFocus: true);

    private void EndTextInput(bool restoreFocus)
    {
        if (!_typing) return;
        _typing = false;
        _vm.SetHoldOpen(false);
        if (restoreFocus && _previousForeground != IntPtr.Zero && Win32.GetForegroundWindow() == _hwnd)
            Win32.SetForegroundWindow(_previousForeground);
        _previousForeground = IntPtr.Zero;
    }

    // ---- Ventana ----

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
        var accent = Tokens.Current.Accent(_s.Accent);

        FrameworkElement view;
        switch (_vm.State)
        {
            case NotchState.Drop:
                _drop.Show(_vm.Drop!.Value);
                view = _drop;
                break;
            case NotchState.Open:
                _open.Height = _geometry.Height;
                _open.Apply(_s);
                _open.Show(_vm.Page, _vm.IsLauncher);
                view = _open;
                break;
            case NotchState.Peek:
                _peek.Show(_vm, accent);
                view = _peek;
                break;
            case NotchState.Osd when _vm.Indicator!.Banner:
                _banner.Show(_vm.Indicator, accent);
                view = _banner;
                break;
            case NotchState.Osd:
                _osd.Show(_vm.Indicator!, accent);
                view = _osd;
                break;
            case NotchState.Activity:
                _compact.Show(_vm.Activity!, _s.DndActive);
                view = _compact;
                break;
            case NotchState.Minimal:
                _minimal.Show(_vm.Activity, _geometry);
                view = _minimal;
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
        if (view != _open && _shownView == _open)
        {
            _open.Hide();
            EndTextInput();
        }
        if (_vm.IsOpen) _outside.Start();
        else _outside.Stop();

        // "Ocultar si no hay actividad": en reposo el notch es invisible (se puede seguir abriendo con el ratón).
        Shape.Opacity = _s.HideIdle && _vm.State == NotchState.Idle ? 0 : 1;
        PlayTransitionSounds(view);

        if (from == _geometry && view == _shownView) return; // solo cambió el contenido (p. ej. otro volumen)
        _shownView = view;
        UpdateRegion(from, _geometry);
        Shape.Show(view, _geometry, ReducedMotion ? Tokens.Current.ReducedMotion : Tokens.Current.ShapeMotion);
    }

    /// <summary>open/close al abrir y cerrar, page al cambiar de ventana, notify con un aviso nuevo, ring en llamadas.</summary>
    private void PlayTransitionSounds(FrameworkElement view)
    {
        var page = _vm.IsLauncher ? null : _vm.Page;
        if (_vm.IsOpen && !_wasOpen) SoundService.Play("open");
        else if (!_vm.IsOpen && _wasOpen) SoundService.Play("close");
        else if (_vm.IsOpen && page != _lastPage) SoundService.Play("page");

        var peek = _vm.Peek;
        if (peek is not null && peek != _lastPeek) SoundService.Play(peek.IsCall ? "ring" : "notify");
        SoundService.SetRinging(peek?.IsCall == true);

        _wasOpen = _vm.IsOpen;
        _lastPage = page;
        _lastPeek = peek;
    }

    /// <summary>Menú de bandeja › Abrir Parpa.</summary>
    public void OpenFromTray()
    {
        ShowWithoutActivation();
        if (!_vm.IsOpen) _vm.OnHotkey();
    }

    /// <summary>Ajustes cambiados (ventana de Ajustes, rápidos, bandeja): se aplican al instante.</summary>
    private void OnSettingsChanged()
    {
        Reposition();
        var keys = string.Join("+", _s.Keys);
        if (keys != _registeredKeys)
        {
            _registeredKeys = keys;
            if (!_hotkey.Register(_s.Keys)) Log.Info($"Atajo {keys} en uso por otra app");
        }
        _idle.Apply(_s, ReducedMotion);
        _open.Apply(_s);
        _vm.Refresh();
    }

    private void OnThemeChanged()
    {
        Shape.Fill = Theme.Get("bg");
        Shape.SetShadow(Theme.IsLight);
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
            case ClipboardService.WM_CLIPBOARDUPDATE:
                DispatcherQueue.TryEnqueue(_clipboard.OnClipboardUpdate);
                return IntPtr.Zero;
            case Win32.WM_DPICHANGED:
            case Win32.WM_DISPLAYCHANGE:
                var result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
                DispatcherQueue.TryEnqueue(Reposition);
                return result;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    // ---- Ratón y teclado ----

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs e) => _vm.OnPointerEntered();

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e) => _vm.OnPointerExited();

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed) _vm.OnClicked();
    }

    private void Root_PointerWheelChanged(object sender, PointerRoutedEventArgs e) =>
        _vm.OnWheel(e.GetCurrentPoint(Root).Properties.MouseWheelDelta);

    /// <summary>Esc: deja de escribir; si no se estaba escribiendo, cierra.</summary>
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape) return;
        e.Handled = true;
        if (_typing) EndTextInput();
        else _vm.RequestClose();
    }

    // ---- Arrastrar y soltar: archivo → Bandeja, texto → nota, enlace → Portapapeles ----

    private async void Root_DragEnter(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            (_dragKind, _dragText) = await ClassifyAsync(e.DataView);
            e.AcceptedOperation = _dragKind is null ? DataPackageOperation.None : DataPackageOperation.Copy;
            if (_dragKind is { } kind) _vm.DragEntered(kind);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = _dragKind is null ? DataPackageOperation.None : DataPackageOperation.Copy;
        e.DragUIOverride.IsCaptionVisible = false; // la etiqueta ya la dibuja el notch
    }

    private void Root_DragLeave(object sender, DragEventArgs e)
    {
        _dragKind = null;
        _vm.DragEnded(null);
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        var kind = _dragKind;
        var text = _dragText;
        _dragKind = null;
        var deferral = e.GetDeferral();
        try
        {
            switch (kind)
            {
                case DropKind.File:
                    var items = await e.DataView.GetStorageItemsAsync();
                    var first = _shelf.Add(items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)));
                    _open.Shelf.Select(first);
                    SoundService.Play("confirm");
                    _vm.DragEnded(NotchPage.Shelf);
                    break;
                case DropKind.Text when text is not null:
                    _notes.Add(text);
                    _vm.DragEnded(NotchPage.Notes);
                    break;
                case DropKind.Link when text is not null:
                    _clipboard.Add(text, announce: false);
                    _vm.DragEnded(NotchPage.Clip);
                    break;
                default:
                    _vm.DragEnded(null);
                    break;
            }
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        catch (Exception ex)
        {
            Log.Error("Soltar: no se pudo leer lo soltado", ex);
            _vm.DragEnded(null);
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>Qué se arrastra: archivos, enlace (web o texto que es una URL) o texto.</summary>
    private static async Task<(DropKind?, string?)> ClassifyAsync(DataPackageView data)
    {
        try
        {
            if (data.Contains(StandardDataFormats.StorageItems)) return (DropKind.File, null);
            if (data.Contains(StandardDataFormats.WebLink)) return (DropKind.Link, (await data.GetWebLinkAsync()).ToString());
            if (data.Contains(StandardDataFormats.Text))
            {
                var text = (await data.GetTextAsync()).Trim();
                if (text.Length == 0) return (null, null);
                var isLink = Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !text.Contains(' ');
                return (isLink ? DropKind.Link : DropKind.Text, text);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Soltar: formato no legible", ex);
        }
        return (null, null);
    }
}
