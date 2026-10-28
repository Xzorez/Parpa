using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class PeekView : UserControl
{
    private const int BarDots = 30;

    /// <summary>Verde de "aceptar" en llamadas (prototipo: oklch(0.72 0.17 150)).</summary>
    private static readonly Color CallGreen = Tokens.ParseOklch("oklch(0.72 0.17 150)");

    private readonly DispatcherQueueTimer _barTimer;
    private readonly SolidColorBrush _hoverBrush = Theme.Brush("glyphOff");
    private NotchViewModel? _vm;
    private int _filled = -1;

    public PeekView()
    {
        InitializeComponent();
        Motion.SmoothBackground(SecondaryButton);
        Motion.PressFeedback(SecondaryButton);
        Motion.PressFeedback(PrimaryButton);
        _barTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _barTimer.Interval = TimeSpan.FromMilliseconds(100);
        _barTimer.Tick += (_, _) => DrawBar();
    }

    public void Show(NotchViewModel vm, Color accent)
    {
        _vm = vm;
        var peek = vm.Peek!;
        var primary = Theme.Get("primary");

        PeekGlyph.Glyph = peek.Glyph;
        PeekGlyph.OnColor = peek.IsCall ? accent : primary;
        Head.Text = peek.Head;
        ToolTipService.SetToolTip(Head, peek.Head);
        When.Text = peek.When;
        Body.Text = peek.Body;

        PrimaryLabel.Text = peek.PrimaryLabel;
        PrimaryButton.Background = new SolidColorBrush(peek.IsCall ? CallGreen : primary);
        SecondaryLabel.Text = peek.SecondaryLabel;
        SecondaryLabel.Foreground = new SolidColorBrush(peek.IsCall ? accent : Theme.Get("secondary"));

        _filled = -1;
        DrawBar();
    }

    /// <summary>La barra solo se actualiza mientras el aviso se ve.</summary>
    public void SetVisible(bool visible)
    {
        if (visible) _barTimer.Start();
        else _barTimer.Stop();
    }

    private void DrawBar()
    {
        if (_vm is null) return;
        var filled = (int)Math.Ceiling(_vm.PeekRemainingFraction * BarDots);
        if (filled == _filled) return;
        _filled = filled;
        TimeBar.Cells = Enumerable.Range(0, BarDots).Select(i => i < filled).ToArray();
    }

    private void PrimaryButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _vm?.PeekPrimary();
    }

    private void SecondaryButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _vm?.PeekSecondary();
    }

    private void PrimaryButton_PointerEntered(object sender, PointerRoutedEventArgs e) => Motion.SetOpacity(PrimaryButton, 0.85f);

    private void PrimaryButton_PointerExited(object sender, PointerRoutedEventArgs e) => Motion.SetOpacity(PrimaryButton, 1f);

    private void SecondaryButton_PointerEntered(object sender, PointerRoutedEventArgs e) => SecondaryButton.Background = _hoverBrush;

    private void SecondaryButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        SecondaryButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
