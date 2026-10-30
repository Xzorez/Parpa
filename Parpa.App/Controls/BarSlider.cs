using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Parpa.Interop;
using Parpa.Services;

namespace Parpa.Controls;

/// <summary>
/// Volumen en barras (Audio, Música, Ajustes rápidos): cada barra vale 100 / n %. Pulsar fija el valor de la barra
/// que hay bajo el ratón y, sin soltar, se arrastra a izquierda o derecha (a la izquierda de la primera = 0 %);
/// la rueda sube o baja de 5 en 5. Las llenas crecen de <c>min</c> a <c>max</c> px.
/// </summary>
public sealed partial class BarSlider : Grid
{
    private const int WheelStep = 5;

    private readonly StackPanel _bars;
    private readonly double _min;
    private readonly double _max;
    private readonly DispatcherQueueTimer _follow;
    private double _pressX;
    private int _pressCursorX;
    private int _percent = -1;

    /// <summary>El usuario ha cambiado el valor (pulsando, arrastrando o con la rueda).</summary>
    public event Action<int>? ValueChanged;

    public BarSlider(int count, double barWidth, double min, double max, double spacing = 3)
    {
        _min = min;
        _max = max;
        // Fondo transparente y margen a la izquierda (pulsar ahí = 0 %). No ocupa más alto que la barra más alta.
        Background = Ui.Clear;
        Padding = new Thickness(6, 0, 0, 0);
        Height = max;
        HorizontalAlignment = HorizontalAlignment.Left;
        _bars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < count; i++)
            _bars.Children.Add(new Border { Width = barWidth, Height = min, CornerRadius = new CornerRadius(barWidth / 2), VerticalAlignment = VerticalAlignment.Center, Background = Theme.Brush("border") });
        Children.Add(_bars);

        // El arrastre sigue al cursor de Windows y no a los eventos de XAML: el notch no se activa al pulsarlo
        // (no quita el foco) y así no depende de que la captura del puntero llegue.
        _follow = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _follow.Interval = TimeSpan.FromMilliseconds(16);
        _follow.Tick += (_, _) => Follow();

        PointerPressed += OnPressed;
        PointerMoved += (_, e) => { if (IsDragging) Pick(e.GetCurrentPoint(_bars).Position.X); };
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
        var n = _bars.Children.Count;
        var filledCount = (int)Math.Round(percent * n / 100.0, MidpointRounding.AwayFromZero);
        for (var i = 0; i < n; i++)
        {
            var bar = (Border)_bars.Children[i];
            var filled = i < filledCount;
            Motion.AnimateSize(bar, null, filled ? _min + Math.Round(i / (double)n * (_max - _min)) : _min, TimeSpan.FromMilliseconds(120));
            bar.Background = Theme.Brush(filled ? "primary" : "border");
        }
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
        var point = e.GetCurrentPoint(_bars);
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

    /// <summary>x (relativo a las barras) → la barra que hay debajo; a la izquierda de la primera, 0 %.</summary>
    private void Pick(double x)
    {
        var n = _bars.Children.Count;
        var pitch = ((Border)_bars.Children[0]).Width + _bars.Spacing;
        var index = x < 0 ? -1 : Math.Min(n - 1, (int)(x / pitch));
        Set((int)Math.Round((index + 1) * 100.0 / n));
    }

    private void Set(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent == _percent) return;
        Show(percent);
        ValueChanged?.Invoke(percent);
    }
}
