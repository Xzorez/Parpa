using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parpa.Controls;
using Parpa.Services;
using Parpa.ViewModels;

namespace Parpa.Views;

public sealed partial class CompactView : UserControl
{
    private const int EqColumns = 5;
    private const int EqRows = 4;

    private readonly DispatcherQueueTimer _eqTimer;
    private int _tick;
    private bool _visible;
    private bool _playing;

    public CompactView()
    {
        InitializeComponent();
        _eqTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _eqTimer.Interval = TimeSpan.FromMilliseconds(100);
        _eqTimer.Tick += (_, _) => { _tick++; DrawEqualizer(); };
        DrawEqualizer();
    }

    public void Show(NotchActivity activity, bool doNotDisturb)
    {
        Moon.Visibility = doNotDisturb ? Visibility.Visible : Visibility.Collapsed;
        Width = Tokens.Current.NotchSize(activity.SizeToken).W;
        ActivityGlyph.Glyph = activity.Glyph;
        // Canción nueva (u otra actividad): el título entra deslizándose un poco.
        if (LabelMain.Text.Length > 0 && LabelMain.Text != activity.Label) Motion.FadeUp(Label, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(260), fromX: 8);
        LabelMain.Text = activity.Label;
        LabelSub.Text = activity.Sub.Length > 0 ? " " + activity.Sub : string.Empty;
        ToolTipService.SetToolTip(this, string.IsNullOrEmpty(activity.Sub) ? activity.Label : $"{activity.Label} · {activity.Sub}");
        RightText.Text = activity.RightText ?? string.Empty;
        RightText.Visibility = activity.RightText is null ? Visibility.Collapsed : Visibility.Visible;
        Equalizer.Visibility = activity.RightText is null ? Visibility.Visible : Visibility.Collapsed;
        _playing = activity.IsPlaying && activity.RightText is null;
        UpdateTimer();
    }

    /// <summary>El ecualizador solo corre mientras la vista se ve y la música suena (0 % CPU en reposo).</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        UpdateTimer();
    }

    private void UpdateTimer()
    {
        if (_visible && _playing) _eqTimer.Start();
        else _eqTimer.Stop();
    }

    /// <summary>Alturas 1–4 por columna: 1 + ⌊3·|sin(t/3 + i·1,3)|⌋ (como el prototipo).</summary>
    private void DrawEqualizer()
    {
        var cells = new bool[EqColumns * EqRows];
        for (var c = 0; c < EqColumns; c++)
        {
            var height = 1 + (int)Math.Floor(3 * Math.Abs(Math.Sin(_tick / 3.0 + c * 1.3)));
            for (var r = 0; r < EqRows; r++)
                cells[r * EqColumns + c] = EqRows - r <= height;
        }
        Equalizer.Cells = cells;
    }
}
