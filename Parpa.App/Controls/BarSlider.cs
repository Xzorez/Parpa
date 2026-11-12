using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Parpa.Interop;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Volumen en barra continua estilo Centro de control (Audio, Música, Ajustes rápidos): pista redondeada con el
/// relleno claro. Pulsar fija el valor bajo el ratón y, sin soltar, se arrastra; la rueda sube o baja de 5 en 5.
/// El constructor conserva la firma de las antiguas barras: el ancho mínimo sale de <c>count</c>, <c>barWidth</c> y
/// <c>spacing</c>, y el grosor es <c>max</c>.
/// </summary>
public sealed partial class BarSlider : Grid
{
    private const int WheelStep = 5;

    private readonly Border _track;
    private readonly Border _fill;
    private readonly DispatcherQueueTimer _follow;
    private double _pressX;
    private int _pressCursorX;
    private int _percent = -1;

    /// <summary>El usuario ha cambiado el valor (pulsando, arrastrando o con la rueda).</summary>
    public event Action<int>? ValueChanged;

    public BarSlider(int count, double barWidth, double min, double max, double spacing = 3)
    {
        var thickness = max;
        Background = Ui.Clear;
        Height = thickness;
        MinWidth = count * barWidth + (count - 1) * spacing;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _track = new Border
        {
            Height = thickness,
            CornerRadius = new CornerRadius(thickness / 2),
            Background = Theme.Brush("border"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _fill = new Border
        {
            Height = thickness,
            Width = 0,
            CornerRadius = new CornerRadius(thickness / 2),
            Background = Theme.Brush("primary"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Children.Add(_track);
        Children.Add(_fill);
        SizeChanged += (_, _) => Layout();

        // El arrastre sigue al cursor de Windows y no a los eventos de XAML: el notch no se activa al pulsarlo
        // (no quita el foco) y así no depende de que la captura del puntero llegue.
        _follow = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _follow.Interval = TimeSpan.FromMilliseconds(16);
        _follow.Tick += (_, _) => Follow();

        PointerPressed += OnPressed;
        PointerMoved += (_, e) => { if (IsDragging) Pick(e.GetCurrentPoint(this).Position.X); };
        PointerReleased += (_, _) => _follow.Stop();
        PointerWheelChanged += (_, e) => Wheel(e);
        Tapped += (_, e) => e.Handled = true; // que el toque no llegue a la fila de debajo (silenciar…)
    }

    public int Value => Math.Max(0, _percent);

    public bool IsDragging => _follow.IsRunning;

    /// <summary>Muestra un valor que llega de fuera (no avisa con <see cref="ValueChanged"/>).</summary>
    public void Show(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _percent) return;
        _percent = percent;
        Layout();
    }

    /// <summary>Relleno = valor × ancho; con algo de valor nunca baja del grosor (se ve como una píldora).</summary>
    private void Layout()
    {
        var width = ActualWidth;
        if (width <= 0) return;
        var value = Math.Max(0, _percent) / 100.0 * width;
        _fill.Width = _percent <= 0 ? 0 : Math.Max(Height, value);
    }

    /// <summary>Rueda sobre el control o sobre algo más grande que lo contiene (la tarjeta, la fila).</summary>
    public void Wheel(PointerRoutedEventArgs e)
    {
        e.Handled = true; // la rueda no cambia de ventana mientras está encima
        var delta = Math.Sign(e.GetCurrentPoint(this).Properties.MouseWheelDelta) * WheelStep;
        if (delta != 0) Set((int)Math.Round(Value / (double)WheelStep) * WheelStep + delta);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        CapturePointer(e.Pointer);
        _pressX = point.Position.X;
        Win32.GetCursorPos(out var cursor);
        _pressCursorX = cursor.X;
        Pick(_pressX);
        SoundService.Play("tick");
        _follow.Start();
    }

    private void Follow()
    {
        var button = Win32.GetSystemMetrics(Win32.SM_SWAPBUTTON) != 0 ? Win32.VK_RBUTTON : Win32.VK_LBUTTON;
        if ((Win32.GetAsyncKeyState(button) & 0x8000) == 0 || XamlRoot is null)
        {
            _follow.Stop();
            return;
        }
        Win32.GetCursorPos(out var cursor);
        Pick(_pressX + (cursor.X - _pressCursorX) / XamlRoot.RasterizationScale);
    }

    /// <summary>x (relativo al control) → porcentaje.</summary>
    private void Pick(double x)
    {
        var width = ActualWidth;
        if (width <= 0) return;
        Set((int)Math.Round(Math.Clamp(x / width, 0, 1) * 100));
    }

    private void Set(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _percent) return;
        Show(percent);
        ValueChanged?.Invoke(percent);
    }
}
