using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Parpa.Controls;

/// <summary>
/// Píldora: equivalente a <c>border-radius: 999px</c> de CSS.
/// En XAML un CornerRadius mayor que la mitad del alto se reparte en ambos ejes y sale un óvalo;
/// esto fija el radio a la mitad del alto real (extremos semicirculares, lados rectos).
/// Uso: <c>&lt;Border c:Pill.IsPill="True"&gt;</c> (también en Grid).
/// </summary>
public static class Pill
{
    public static readonly DependencyProperty IsPillProperty = DependencyProperty.RegisterAttached(
        "IsPill", typeof(bool), typeof(Pill), new PropertyMetadata(false, OnIsPillChanged));

    public static bool GetIsPill(FrameworkElement element) => (bool)element.GetValue(IsPillProperty);

    public static void SetIsPill(FrameworkElement element, bool value) => element.SetValue(IsPillProperty, value);

    private static void OnIsPillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.SizeChanged -= OnSizeChanged;
        if (e.NewValue is true)
        {
            element.SizeChanged += OnSizeChanged;
            Apply(element);
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Apply((FrameworkElement)sender);

    private static void Apply(FrameworkElement element)
    {
        var radius = new CornerRadius(element.ActualHeight / 2);
        switch (element)
        {
            case Border border: border.CornerRadius = radius; break;
            case Grid grid: grid.CornerRadius = radius; break;
        }
    }
}
