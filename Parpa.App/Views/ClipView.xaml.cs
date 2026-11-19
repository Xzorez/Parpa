using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

public sealed partial class ClipView : UserControl, INotchPageView
{
    private readonly NotchServices _app;
    private bool _shown;

    public ClipView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        app.Clipboard.Changed += () => { if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    /// <summary>En la cabecera, en lugar del reloj: cómo se usa la lista.</summary>
    public (string Text, Windows.UI.Color Color)? HeaderRight => _app.Clipboard.Items.Count == 0 ? null :
        (Loc.Get("clip_hint").ToUpper(Loc.Culture), (Windows.UI.Color)Application.Current.Resources["TertiaryColor"]);

    public IEnumerable<UIElement> EntranceItems => ListScroll.Visibility == Visibility.Visible ? Rows.Children : Empty.Children;

    public void OnShown()
    {
        _shown = true;
        Render();
        ListScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    public void OnHidden() => _shown = false;

    private void Render()
    {
        var items = _app.Clipboard.Items;
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListScroll.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (items.Count == 0)
        {
            RenderHint();
            return;
        }

        Rows.Children.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            var row = new Grid
            {
                Padding = new Thickness(4, 9, 4, 9),
                BorderBrush = Res<Brush>("DividerBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                ColumnSpacing = 14,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(6),
            };
            Motion.SmoothBackground(row);
            Motion.PressFeedback(row);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var index = new TextBlock { Style = Res<Style>("RowIndex"), Text = (i + 1).ToString("00") };
            // Una sola línea: los saltos se ven como espacios.
            var text = new TextBlock { Style = Res<Style>("RowText"), Text = entry.Text.ReplaceLineEndings(" ") };
            var time = new TextBlock { Style = Res<Style>("RowMeta"), Text = ClockService.Format(entry.Time, _app.Settings.H24) };
            ToolTipService.SetToolTip(text, entry.Text.Length > 400 ? entry.Text[..400] + "…" : entry.Text);
            Grid.SetColumn(text, 1);
            Grid.SetColumn(time, 2);
            row.Children.Add(index);
            row.Children.Add(text);
            row.Children.Add(time);

            // Hover: fondo tenue. Clic: copia con un destello de tinta. Clic derecho: se va deslizando y se borra.
            var removing = false;
            row.PointerEntered += (_, _) => { if (!removing) row.Background = Res<Brush>("GlyphOffBrush"); };
            row.PointerExited += (_, _) => { if (!removing) row.Background = Ui.Clear; };
            row.Tapped += (_, e) =>
            {
                e.Handled = true;
                if (removing) return;
                SoundService.Play("click");
                if (!_app.Clipboard.Copy(entry.Text)) return;
                Flash(row, index, text, time);
            };
            row.RightTapped += (_, e) =>
            {
                e.Handled = true;
                if (removing) return;
                removing = true;
                SoundService.Play("decline");
                Remove(row, entry);
            };
            Rows.Children.Add(row);
        }
    }

    /// <summary>Copiado: la fila se rellena de tinta un instante y la hora dice COPIADO.</summary>
    private void Flash(Grid row, TextBlock index, TextBlock text, TextBlock time)
    {
        var hour = time.Text;
        row.Background = Res<Brush>("PrimaryBrush");
        foreach (var t in new[] { index, text, time }) t.Foreground = Res<Brush>("BgBrush");
        time.Text = Loc.Get("notes_copied").ToUpper(Loc.Culture);
        Motion.FadeUp(time, 4, TimeSpan.Zero, TimeSpan.FromMilliseconds(180));
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = TimeSpan.FromMilliseconds(900);
        timer.Tick += (_, _) =>
        {
            row.Background = Ui.Clear;
            index.ClearValue(TextBlock.ForegroundProperty);
            text.ClearValue(TextBlock.ForegroundProperty);
            time.ClearValue(TextBlock.ForegroundProperty);
            time.Text = hour;
        };
        timer.Start();
    }

    /// <summary>Borrar: la fila sale hacia la izquierda con fundido y se cierra su hueco; después se quita de verdad.</summary>
    private void Remove(Grid row, ClipEntry entry)
    {
        if (Motion.Reduced)
        {
            _app.Clipboard.Remove(entry);
            return;
        }
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(row);
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(row, true);
        var c = visual.Compositor;
        var ease = c.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(.4f, 0f), new System.Numerics.Vector2(.2f, 1f));
        var slide = c.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(1f, new System.Numerics.Vector3(-28, 0, 0), ease);
        slide.Duration = TimeSpan.FromMilliseconds(200);
        visual.StartAnimation("Translation", slide);
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f, ease);
        fade.Duration = TimeSpan.FromMilliseconds(180);
        visual.StartAnimation("Opacity", fade);

        row.Height = row.ActualHeight;
        row.MinHeight = 0;
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Interval = TimeSpan.FromMilliseconds(110);
        timer.Tick += (_, _) =>
        {
            row.Padding = new Thickness(4, 0, 4, 0);
            Motion.AnimateSize(row, null, 0, TimeSpan.FromMilliseconds(170));
            var done = DispatcherQueue.CreateTimer();
            done.IsRepeating = false;
            done.Interval = TimeSpan.FromMilliseconds(190);
            done.Tick += (_, _) => _app.Clipboard.Remove(entry);
            done.Start();
        };
        timer.Start();
    }

    /// <summary>"COPIA ALGO CON [CTRL] [C] Y APARECERÁ AQUÍ" con las teclas como chapas.</summary>
    private void RenderHint()
    {
        EmptyHint.Children.Clear();
        var parts = Loc.Get("clip_empty").Split("{keys}");
        EmptyHint.Children.Add(HintText(parts[0].Trim()));
        EmptyHint.Children.Add(KeyCap("CTRL"));
        EmptyHint.Children.Add(KeyCap("C"));
        if (parts.Length > 1) EmptyHint.Children.Add(HintText(parts[1].Trim()));
    }

    private static TextBlock HintText(string text) => new()
    {
        Text = text.ToUpper(Loc.Culture),
        Style = Res<Style>("Tiny"),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border KeyCap(string key) => new()
    {
        Padding = new Thickness(6, 3, 6, 3),
        BorderBrush = Res<Brush>("PrimaryBrush"),
        BorderThickness = new Thickness(1.5),
        CornerRadius = new CornerRadius(6),
        Child = new TextBlock { Text = key, Style = Res<Style>("Wide"), FontSize = 11 },
    };
}
