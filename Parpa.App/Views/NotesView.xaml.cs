using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Services;
using Windows.System;
using Windows.UI.Core;

namespace Parpa.Views;

public sealed partial class NotesView : UserControl, INotchPageView
{
    private readonly NotchServices _app;
    private Note? _open;
    private bool _shown;

    public NotesView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        // El cuadro de texto pide el teclado al pulsarlo (la ventana del notch no se activa sola).
        Draft.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _app.Keyboard.BeginTextInput(Draft)), handledEventsToo: true);
        Draft.LostFocus += (_, _) => _app.Keyboard.EndTextInput();
        app.Notes.Changed += () => { if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    public void OnShown()
    {
        _shown = true;
        Draft.PlaceholderText = Loc.Get("notes_ph");
        SaveHint.Text = Loc.Get("notes_save_hint");
        SaveButton.Text = Loc.Get("notes_save");
        SavedLabel.Text = Loc.Get("notes_saved_list");
        BackButton.Text = "‹ " + Loc.Get("notes_back");
        CopyButton.Text = Loc.Get("clip_copy");
        DeleteButton.Text = Loc.Get("notes_delete");
        DeleteButton.Foreground = new SolidColorBrush(Tokens.Current.Accent(_app.Settings.Accent));
        EmptyTitle.Text = Loc.Get("notes_empty");
        EmptyText.Text = Loc.Get("notes_empty_2");
        Render();
    }

    public void OnHidden()
    {
        _shown = false;
        _open = null;
    }

    private void Render()
    {
        var notes = _app.Notes.Items;
        if (_open is not null && !notes.Contains(_open)) _open = null;

        EmptyPanel.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListPanel.Visibility = notes.Count > 0 && _open is null ? Visibility.Visible : Visibility.Collapsed;
        NotePanel.Visibility = _open is not null ? Visibility.Visible : Visibility.Collapsed;
        SavedCount.Text = notes.Count.ToString();

        if (_open is not null)
        {
            var number = notes.Count - notes.ToList().IndexOf(_open);
            NoteTitle.Text = Loc.Format("notes_note_n", ("n", number.ToString("00")));
            NoteTime.Text = ClockService.Format(_open.Created, _app.Settings.H24);
            NoteText.Text = _open.Text;
            CopyButton.Text = Loc.Get("clip_copy");
            return;
        }

        Rows.Children.Clear();
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var row = new Grid
            {
                Padding = new Thickness(4, 7, 4, 7),
                BorderBrush = Res<Brush>("DividerBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                ColumnSpacing = 10,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var index = new TextBlock { Style = Res<Style>("RowIndex"), FontSize = 12, Text = (notes.Count - i).ToString("00") };
            var text = new TextBlock { Style = Res<Style>("RowText"), Text = note.Text.ReplaceLineEndings(" ") };
            var time = new TextBlock
            {
                Style = Res<Style>("RowMeta"),
                FontSize = 9,
                Foreground = Res<Brush>("QuaternaryBrush"),
                Text = ClockService.Format(note.Created, _app.Settings.H24),
            };
            var delete = new TextBlock
            {
                Style = Res<Style>("RowMeta"),
                FontSize = 11,
                Foreground = Res<Brush>("TertiaryBrush"),
                Text = "×",
            };
            delete.PointerEntered += (_, _) => delete.Foreground = Res<Brush>("PrimaryBrush");
            delete.PointerExited += (_, _) => delete.Foreground = Res<Brush>("TertiaryBrush");
            delete.Tapped += (_, e) =>
            {
                e.Handled = true;
                _app.Notes.Remove(note.Id);
            };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(time, 2);
            Grid.SetColumn(delete, 3);
            row.Children.Add(index);
            row.Children.Add(text);
            row.Children.Add(time);
            row.Children.Add(delete);

            row.PointerEntered += (_, _) => row.Background = Res<Brush>("CardBrush");
            row.PointerExited += (_, _) => row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.Tapped += (_, e) =>
            {
                e.Handled = true;
                _open = note;
                Render();
            };
            Rows.Children.Add(row);
        }
    }

    private void Save()
    {
        if (Draft.Text.Trim().Length == 0) return;
        _app.Notes.Add(Draft.Text);
        SoundService.Play("confirm");
        Draft.Text = string.Empty;
    }

    /// <summary>Ctrl + Enter guarda.</summary>
    private void Draft_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.Enter && ctrl)
        {
            e.Handled = true;
            Save();
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            _app.Keyboard.EndTextInput();
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e) => Save();

    private void BackButton_Click(object? sender, EventArgs e)
    {
        _open = null;
        Render();
    }

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (_open is not null && _app.Clipboard.Copy(_open.Text)) CopyButton.Text = Loc.Get("notes_copied");
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_open is null) return;
        var id = _open.Id;
        _open = null;
        _app.Notes.Remove(id);
    }
}
