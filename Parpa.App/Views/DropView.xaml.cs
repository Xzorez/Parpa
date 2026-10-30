using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

/// <summary>Qué se hace con lo soltado.</summary>
public enum DropAction { Shelf, ExtractText, Resize, Note, Task, LinkWithTitle, LinkToClipboard }

public sealed partial class DropView : UserControl
{
    private readonly List<(DotPattern Dots, TextBlock Label)> _zones = new();
    private IReadOnlyList<DropAction> _actions = [];

    public DropView() => InitializeComponent();

    /// <summary>Acción bajo el ratón (la primera si solo hay una).</summary>
    public int Selected { get; private set; }

    public DropAction? SelectedAction => _actions.Count == 0 ? null : _actions[Selected];

    /// <summary>Una zona por acción: archivo → Bandeja (+ texto o redimensionar si son imágenes), texto → nota o tarea, enlace → con título o al portapapeles.</summary>
    public void Show(IReadOnlyList<DropAction> actions)
    {
        _actions = actions;
        _zones.Clear();
        Zones.Children.Clear();
        Zones.ColumnDefinitions.Clear();
        for (var i = 0; i < actions.Count; i++)
        {
            Zones.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var (glyph, key) = Look(actions[i], actions.Count);
            var dots = new DotPattern { Spacing = 7, CornerRadius = new CornerRadius(16) };
            var label = new TextBlock
            {
                Text = Loc.Get(key),
                CharacterSpacing = 120,
                FontFamily = Res<FontFamily>("FontMono"),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            };
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal, Spacing = 10 };
            content.Children.Add(new DotMatrix { Glyph = glyph, VerticalAlignment = VerticalAlignment.Center, OnBrush = Res<SolidColorBrush>("PrimaryBrush"), OffBrush = Res<SolidColorBrush>("GlyphOffBrush") });
            content.Children.Add(new Border { Padding = new Thickness(6, 3, 6, 3), Background = Res<Brush>("BgBrush"), Child = label });
            var zone = new Grid();
            zone.Children.Add(dots);
            zone.Children.Add(content);
            Grid.SetColumn(zone, i);
            Zones.Children.Add(zone);
            _zones.Add((dots, label));
        }
        Selected = -1;
        Select(0);
    }

    /// <summary>La zona bajo el ratón (x en coordenadas de esta vista).</summary>
    public void Hover(double x)
    {
        if (_zones.Count < 2) return;
        Select(x < ActualWidth / 2 ? 0 : 1);
    }

    /// <summary>Mientras se procesa (leer texto, redimensionar…): una sola zona con "UN MOMENTO…".</summary>
    public void ShowWorking()
    {
        for (var i = 0; i < _zones.Count; i++)
        {
            Zones.Children[i].Visibility = i == Selected ? Visibility.Visible : Visibility.Collapsed;
            Zones.ColumnDefinitions[i].Width = i == Selected ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        }
        if (Selected >= 0) _zones[Selected].Label.Text = Loc.Get("drop_working");
    }

    private void Select(int index)
    {
        if (index == Selected) return;
        Selected = index;
        for (var i = 0; i < _zones.Count; i++)
        {
            var on = i == index;
            _zones[i].Dots.DotBrush = Res<SolidColorBrush>(on ? "TertiaryBrush" : "BorderBrush");
            _zones[i].Label.Foreground = Res<Brush>(on ? "PrimaryBrush" : "TertiaryBrush");
        }
    }

    private static (string Glyph, string Key) Look(DropAction action, int count) => action switch
    {
        DropAction.Shelf => ("bandeja", count == 1 ? "drop_file" : "drop_shelf"),
        DropAction.ExtractText => ("copias", "drop_ocr"),
        DropAction.Resize => ("general", "drop_resize"),
        DropAction.Note => ("notas", "drop_note"),
        DropAction.Task => ("tareas", "drop_task"),
        DropAction.LinkWithTitle => ("notas", "drop_link_title"),
        _ => ("copias", "drop_link_clip"),
    };

    private static T Res<T>(string key) where T : class => (T)Application.Current.Resources[key];
}
