using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Controls;
using Parpa.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class ShelfView : UserControl, INotchPageView
{
    private const double TileHeight = 78;

    private readonly NotchServices _app;
    private ShelfItem? _selected;
    private (ShelfAction Action, ShelfItem Item)? _failed;
    private bool _shown;

    public ShelfView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        EmptyMascot.Cells = MascotFrames.HappyStill();
        Retry.Tapped += (_, e) => e.Handled = true;
        app.Shelf.Changed += () => { if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    public IEnumerable<UIElement> EntranceItems =>
        EmptyPanel.Visibility == Visibility.Visible ? [EmptyPanel] : Tiles.Children.Append(Detail.Visibility == Visibility.Visible ? Detail : PickHint);

    public void OnShown()
    {
        _shown = true;
        PickTitle.Text = Loc.Get("shelf_empty_1");
        PickText.Text = Loc.Get("shelf_pick_hint");
        EmptyTitle.Text = Loc.Get("shelf_empty_1");
        EmptyText.Text = Loc.Get("shelf_empty_2");
        Retry.Text = Loc.Get("empty_errors_retry");
        Render();
    }

    public void OnHidden()
    {
        _shown = false;
        _selected = null;
        ClearResult();
    }

    /// <summary>Tras soltar archivos: se elige el primero nuevo.</summary>
    public void Select(int index)
    {
        _selected = index >= 0 && index < _app.Shelf.Items.Count ? _app.Shelf.Items[index] : null;
        ClearResult();
        if (_shown) Render();
    }

    private void Render()
    {
        var items = _app.Shelf.Items;
        if (_selected is not null && !items.Contains(_selected)) _selected = null;
        EmptyPanel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilledPanel.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        Tiles.Children.Clear();
        Tiles.RowDefinitions.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            if (i % 2 == 0) Tiles.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeight) });
            var tile = CreateTile(items[i]);
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            Tiles.Children.Add(tile);
        }

        PickHint.Visibility = _selected is null ? Visibility.Visible : Visibility.Collapsed;
        Detail.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible;
        if (_selected is not null) RenderDetail(_selected);
    }

    /// <summary>Tarjeta: icono de puntos 22 px + tipo arriba; nombre y tamaño abajo. Anillo 1,5 px si está elegida.</summary>
    private Border CreateTile(ShelfItem item)
    {
        var icon = new Grid { Width = 22, Height = 22, Background = Res<Brush>("DividerBrush"), CornerRadius = new CornerRadius(5) };
        icon.Children.Add(new DotPattern { DotColor = Color.FromArgb(255, 0x4a, 0x4a, 0x50), Spacing = 4, CornerRadius = new CornerRadius(5) });

        var kind = new TextBlock
        {
            Text = item.Kind,
            FontFamily = Res<FontFamily>("FontMono"),
            FontSize = 9,
            CharacterSpacing = 80,
            Foreground = Res<Brush>("QuaternaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var top = new Grid();
        top.Children.Add(icon);
        top.Children.Add(kind);
        icon.HorizontalAlignment = HorizontalAlignment.Left;

        var name = new TextBlock { Style = Res<Style>("RowText"), Text = item.Name };
        ToolTipService.SetToolTip(name, item.Path);
        var size = new TextBlock
        {
            Text = ShelfService.SizeText(item),
            FontFamily = Res<FontFamily>("FontMono"),
            FontSize = 9,
            Foreground = Res<Brush>("SecondaryBrush"),
        };
        var bottom = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Bottom };
        bottom.Children.Add(name);
        bottom.Children.Add(size);

        var content = new Grid();
        content.Children.Add(top);
        content.Children.Add(bottom);

        var tile = new Border
        {
            Padding = new Thickness(10),
            Background = Res<Brush>("CardBrush"),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1.5),
            BorderBrush = item == _selected ? Res<Brush>("PrimaryBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = content,
            CanDrag = true,
        };
        Motion.PressFeedback(tile);
        tile.Tapped += (_, e) =>
        {
            e.Handled = true;
            _selected = item == _selected ? null : item;
            ClearResult();
            Render();
        };
        // Arrastrar fuera saca el archivo de la bandeja.
        tile.DragStarting += async (_, e) =>
        {
            var deferral = e.GetDeferral();
            try
            {
                IStorageItem storage = item.IsFolder
                    ? await StorageFolder.GetFolderFromPathAsync(item.Path)
                    : await StorageFile.GetFileFromPathAsync(item.Path);
                e.Data.SetStorageItems([storage]);
                e.Data.RequestedOperation = DataPackageOperation.Copy | DataPackageOperation.Move;
            }
            catch (Exception ex)
            {
                Log.Error("Bandeja: no se pudo arrastrar", ex);
                e.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        };
        tile.DropCompleted += (_, e) =>
        {
            if (e.DropResult != DataPackageOperation.None) _app.Shelf.Remove(item);
        };
        return tile;
    }

    private void RenderDetail(ShelfItem item)
    {
        DetailName.Text = item.Name;
        DetailPath.Text = item.Path;
        ToolTipService.SetToolTip(DetailPath, item.Path);

        Actions.Children.Clear();
        Actions.RowDefinitions.Clear();
        var actions = _app.Shelf.ActionsFor(item);
        for (var i = 0; i < actions.Count; i++)
        {
            if (i % 2 == 0) Actions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var button = CreateAction(actions[i], item);
            Grid.SetRow(button, i / 2);
            Grid.SetColumn(button, i % 2);
            Actions.Children.Add(button);
        }
    }

    /// <summary>Acción: radio 8, fondo #1d1d20 (hover #26262a), mono 500 10 px.</summary>
    private Border CreateAction(ShelfAction action, ShelfItem item)
    {
        var button = new Border
        {
            Padding = new Thickness(8),
            Background = Res<Brush>("GlyphOffBrush"),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = Loc.Get(ShelfService.LabelKey(action)),
                FontFamily = Res<FontFamily>("FontMono"),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                CharacterSpacing = 60,
                Foreground = Res<Brush>("PrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
        Motion.SmoothBackground(button);
        Motion.PressFeedback(button);
        button.PointerEntered += (_, _) => button.Background = Res<Brush>("BorderAltBrush");
        button.PointerExited += (_, _) => button.Background = Res<Brush>("GlyphOffBrush");
        button.Tapped += async (_, e) =>
        {
            e.Handled = true;
            await RunAsync(action, item);
        };
        return button;
    }

    private async Task RunAsync(ShelfAction action, ShelfItem item)
    {
        ClearResult();
        try
        {
            var message = await _app.Shelf.RunAsync(action, item);
            if (message.Length > 0) ShowResult(message, success: true);
            SoundService.Play(action == ShelfAction.Remove ? "click" : "confirm");
        }
        catch (Exception ex)
        {
            Log.Error($"Bandeja: {action} falló", ex);
            // "No pude {acción} «archivo»: otra app lo tiene abierto." (o genérico si no es un bloqueo).
            var key = ex is IOException or UnauthorizedAccessException ? "empty_errors_action_failed" : "empty_errors_action_failed_generic";
            ShowResult(Loc.Format(key, ("action", Loc.Get(ShelfService.VerbKey(action))), ("file", item.Name)), success: false);
            _failed = (action, item);
            SoundService.Play("decline");
            Retry.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Verde para lo hecho; punto rojo de acento + REINTENTAR si falla.</summary>
    private void ShowResult(string message, bool success)
    {
        var accent = Tokens.Current.Accent(_app.Settings.Accent);
        Result.Text = message;
        ToolTipService.SetToolTip(Result, message);
        Result.Foreground = success ? new SolidColorBrush(Tokens.Current.Success) : Res<Brush>("PrimaryBrush");
        ResultDot.Fill = new SolidColorBrush(accent);
        ResultDot.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearResult()
    {
        _failed = null;
        Result.Text = string.Empty;
        ResultDot.Visibility = Visibility.Collapsed;
        Retry.Visibility = Visibility.Collapsed;
    }

    private async void Retry_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_failed is { } failed) await RunAsync(failed.Action, failed.Item);
    }
}
