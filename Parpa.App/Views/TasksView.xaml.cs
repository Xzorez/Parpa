using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Parpa.Controls;
using Parpa.Services;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;

namespace Parpa.Views;

public sealed partial class TasksView : UserControl, INotchPageView
{
    private readonly NotchServices _app;
    private bool _shown;
    private bool _adding;

    public TasksView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        DoneMascot.Cells = MascotFrames.HappyStill();
        app.Tasks.Changed += () => { if (_shown) Render(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    /// <summary>Con todo hecho, el contador va a la cabecera en verde (en lugar del reloj).</summary>
    public (string Text, Color Color)? HeaderRight =>
        AllDone && _app.Tasks.Items.Count > 0 ? (CountText, Tokens.Current.Success) : null;

    /// <summary>La cabecera debe redibujarse (cambió el contador de la derecha).</summary>
    public event Action? HeaderChanged;

    private bool AllDone => _app.Tasks.Pending == 0 && !_adding;

    private string CountText =>
        Loc.Format("todo_count", ("done", _app.Tasks.Items.Count(t => t.Done)), ("total", _app.Tasks.Items.Count));

    public IEnumerable<UIElement> EntranceItems =>
        DonePanel.Visibility == Visibility.Visible ? DonePanel.Children : Rows.Children.Append(NewLabel);

    public void OnShown()
    {
        _shown = true;
        TodayLabel.Text = Loc.Get("todo_today");
        NewLabel.Text = Loc.Get("todo_new");
        NewBox.PlaceholderText = Loc.Get("todo_new_ph");
        DoneTitle.Text = Loc.Get("todo_all_done");
        DoneText.Text = Loc.Get("todo_all_done_2");
        DoneNew.Text = Loc.Get("todo_new");
        Render();
    }

    public void OnHidden()
    {
        _shown = false;
        StopAdding();
    }

    private void Render()
    {
        DonePanel.Visibility = AllDone ? Visibility.Visible : Visibility.Collapsed;
        ListPanel.Visibility = AllDone ? Visibility.Collapsed : Visibility.Visible;
        Count.Text = CountText;
        HeaderChanged?.Invoke();
        if (AllDone) return;

        Rows.Children.Clear();
        var accent = Tokens.Current.Accent(_app.Settings.Accent);
        var now = DateTime.Now.ToString("HH:mm");
        foreach (var task in _app.Tasks.Items)
        {
            var row = new Grid
            {
                Padding = new Thickness(4, 7, 4, 7),
                BorderBrush = Res<Brush>("DividerBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                ColumnSpacing = 12,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new Ellipse
            {
                Width = 10,
                Height = 10,
                Stroke = Res<Brush>("PrimaryBrush"),
                StrokeThickness = 1.5,
                Fill = task.Done ? Res<Brush>("PrimaryBrush") : null,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var text = new TextBlock
            {
                Style = Res<Style>("RowText"),
                Text = task.Text,
                Foreground = Res<Brush>(task.Done ? "TertiaryBrush" : "PrimaryBrush"),
                TextDecorations = task.Done ? TextDecorations.Strikethrough : TextDecorations.None,
            };
            // Vencida (hora pasada y sin hacer): en acento.
            var overdue = !task.Done && task.Time is { } t && string.CompareOrdinal(t, now) <= 0;
            var time = new TextBlock
            {
                Style = Res<Style>("RowMeta"),
                Text = task.Time ?? string.Empty,
                Foreground = overdue ? new SolidColorBrush(accent) : Res<Brush>("SecondaryBrush"),
            };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(time, 2);
            row.Children.Add(box);
            row.Children.Add(text);
            row.Children.Add(time);
            var id = task.Id;
            row.Tapped += (_, e) =>
            {
                e.Handled = true;
                SoundService.Play(task.Done ? "tick" : "confirm");
                _app.Tasks.Toggle(id);
            };
            Rows.Children.Add(row);
        }
    }

    // ---- Nueva tarea ----

    private void NewLabel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        StartAdding();
    }

    private void DoneNew_Click(object? sender, EventArgs e) => StartAdding();

    private void StartAdding()
    {
        _adding = true;
        Render();
        NewLabel.Visibility = Visibility.Collapsed;
        NewBox.Visibility = Visibility.Visible;
        _app.Keyboard.BeginTextInput(NewBox);
    }

    private void StopAdding()
    {
        if (!_adding) return;
        _adding = false;
        NewBox.Text = string.Empty;
        NewBox.Visibility = Visibility.Collapsed;
        NewLabel.Visibility = Visibility.Visible;
        _app.Keyboard.EndTextInput();
        if (_shown) Render();
    }

    private void NewBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _app.Tasks.Add(NewBox.Text);
            NewBox.Text = string.Empty;
            ListScroll.ChangeView(null, ListScroll.ScrollableHeight + 100, null);
        }
        else if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            StopAdding();
        }
    }

    private void NewBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (NewBox.Text.Length == 0) StopAdding();
    }
}
