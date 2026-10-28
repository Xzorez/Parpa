using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class OsdView : UserControl
{
    private const int BarCount = 16;

    private readonly Border[] _bars = new Border[BarCount];
    private readonly SolidColorBrush _filled = new();
    private readonly SolidColorBrush _empty = Theme.Brush("border");

    public OsdView()
    {
        InitializeComponent();
        for (var i = 0; i < BarCount; i++)
        {
            _bars[i] = new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
            Bars.Children.Add(_bars[i]);
        }
    }

    /// <param name="accent">Color de acento elegido en Ajustes (glifo en alertas).</param>
    public void Show(NotchIndicator indicator, Color accent)
    {
        var primary = Theme.Get("primary");
        IndicatorGlyph.Glyph = indicator.Glyph;
        IndicatorGlyph.OnColor = indicator.Alert ? accent : primary;
        Label.Text = indicator.Label;
        Sub.Text = indicator.Sub;
        Value.Text = indicator.Value;

        // Barra i llena si (i + 0,5) / 16 ≤ nivel; altura 4 → 14 px de izquierda a derecha.
        _filled.Color = indicator.Success ? Tokens.Current.Success : primary;
        for (var i = 0; i < BarCount; i++)
        {
            var filled = (i + 0.5) / BarCount <= indicator.Level / 100;
            _bars[i].Background = filled ? _filled : _empty;
            _bars[i].Height = filled ? 4 + Math.Round(i / (double)BarCount * 10) : 4;
        }
    }
}
