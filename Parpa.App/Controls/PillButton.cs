using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Parpa.Controls;

/// <summary>
/// Botón píldora del diseño: Geist Mono 500 10 px, tracking .06em, padding 6×10.
/// <see cref="Primary"/>: fondo claro y texto oscuro (hover opacidad .85).
/// Si no: borde #2a2a2e y texto <see cref="Foreground"/> (hover fondo #1d1d20).
/// </summary>
public sealed partial class PillButton : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PillButton), new PropertyMetadata(string.Empty, (d, e) => ((PillButton)d)._label.Text = (string)e.NewValue));

    public static readonly DependencyProperty PrimaryProperty = DependencyProperty.Register(
        nameof(Primary), typeof(bool), typeof(PillButton), new PropertyMetadata(false, (d, _) => ((PillButton)d).ApplyStyle()));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(PillButton), new PropertyMetadata(null, (d, _) => ((PillButton)d).ApplyStyle()));

    private readonly TextBlock _label = new()
    {
        FontFamily = (FontFamily)Application.Current.Resources["FontMono"],
        FontSize = 10,
        FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        CharacterSpacing = 60,
        TextWrapping = TextWrapping.NoWrap,
    };

    private bool _hover;

    public event EventHandler? Click;

    public PillButton()
    {
        Padding = new Thickness(10, 6, 10, 6);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_label);
        Pill.SetIsPill(this, true);
        PointerEntered += (_, _) => { _hover = true; ApplyStyle(); };
        PointerExited += (_, _) => { _hover = false; ApplyStyle(); };
        Tapped += OnTapped;
        ApplyStyle();
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool Primary { get => (bool)GetValue(PrimaryProperty); set => SetValue(PrimaryProperty, value); }

    /// <summary>Color del texto en la variante con borde (por defecto #f2f2f0).</summary>
    public Brush? Foreground { get => (Brush?)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        Click?.Invoke(this, EventArgs.Empty);
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private void ApplyStyle()
    {
        if (Primary)
        {
            Background = Res("PrimaryBrush");
            BorderThickness = new Thickness(0);
            _label.Foreground = Res("BgBrush");
            Opacity = _hover ? 0.85 : 1;
        }
        else
        {
            Background = _hover ? Res("GlyphOffBrush") : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            BorderBrush = Res("BorderBrush");
            BorderThickness = new Thickness(1);
            _label.Foreground = Foreground ?? Res("PrimaryBrush");
            Opacity = 1;
        }
    }
}
