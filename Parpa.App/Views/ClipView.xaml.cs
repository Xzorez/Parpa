using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            };
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

            row.Tapped += (_, e) =>
            {
                e.Handled = true;
                if (_app.Clipboard.Copy(entry.Text)) time.Text = Loc.Get("notes_copied");
            };
            Rows.Children.Add(row);
        }
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
        Text = text,
        FontFamily = Res<FontFamily>("FontMono"),
        FontSize = 10,
        FontWeight = Microsoft.UI.Text.FontWeights.Medium,
        CharacterSpacing = 80,
        Foreground = Res<Brush>("SoftBrush"),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border KeyCap(string key) => new()
    {
        Padding = new Thickness(6, 3, 6, 3),
        BorderBrush = Res<Brush>("BorderBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Child = new TextBlock
        {
            Text = key,
            FontFamily = Res<FontFamily>("FontMono"),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = Res<Brush>("PrimaryBrush"),
        },
    };
}
