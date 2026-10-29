using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;

namespace Parpa.Views;

/// <summary>
/// Modo minimal (24 px): una línea fina con el glifo de la actividad en puntos de 1,5 px (#8d8d92).
/// En reposo queda vacía.
/// </summary>
public sealed partial class MinimalView : Grid
{
    private readonly DotMatrix _glyph = new()
    {
        DotSize = 1.5,
        Gap = 1,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        OnBrush = Theme.Brush("secondary"),
    };

    /// <summary>Micro/cámara en uso: en minimal se ve el punto rojo en vez del glifo.</summary>
    private readonly Ellipse _liveDot = new()
    {
        Width = 6,
        Height = 6,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    public MinimalView()
    {
        Height = 24;
        Children.Add(_glyph);
        Children.Add(_liveDot);
    }

    public void Show(NotchActivity? activity, NotchGeometry geometry, Windows.UI.Color accent)
    {
        Width = geometry.Width;
        var live = activity?.Live == true;
        _glyph.Visibility = activity is null || live ? Visibility.Collapsed : Visibility.Visible;
        _liveDot.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
        _liveDot.Fill = new SolidColorBrush(accent);
        if (activity is not null) _glyph.Glyph = activity.Glyph;
    }
}
