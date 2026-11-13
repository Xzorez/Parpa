using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class OsdView : UserControl
{
    private readonly SolidColorBrush _filled = new();
    private double _level;

    public OsdView()
    {
        InitializeComponent();
        BarFill.Background = _filled;
        Bars.SizeChanged += (_, _) => DrawLevel(animate: false);
    }

    /// <summary>Relleno = nivel × ancho de la línea, animado al cambiar.</summary>
    private void DrawLevel(bool animate)
    {
        var width = Bars.ActualWidth - 6;
        if (width <= 0) return;
        var target = Math.Clamp(_level / 100, 0, 1) * width;
        if (animate) Motion.AnimateSize(BarFill, target, null, TimeSpan.FromMilliseconds(140));
        else BarFill.Width = target;
    }

    /// <param name="accent">Color de acento elegido en Ajustes (glifo en alertas).</param>
    public void Show(NotchIndicator indicator, Color accent)
    {
        var primary = Theme.Get("primary");
        IndicatorGlyph.Glyph = indicator.Glyph;
        IndicatorGlyph.OnColor = indicator.Alert ? accent : primary;
        Label.Text = indicator.Label.ToUpper(Loc.Culture);
        Sub.Text = indicator.Sub.ToUpper(Loc.Culture);
        Value.Text = indicator.Value;

        _filled.Color = indicator.Success ? Tokens.Current.Success : primary;
        _level = indicator.Level;
        DrawLevel(animate: true);
    }
}
