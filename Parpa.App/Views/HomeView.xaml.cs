using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Parpa.Controls;
using Parpa.Services;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class HomeView : UserControl, INotchPageView
{
    private const int SystemDots = 12;

    private readonly NotchServices _app;
    private readonly DispatcherQueueTimer _progressTimer;
    private readonly List<(TextBlock Label, DotMatrix Dots, TextBlock Value)> _systemRows = new();
    private object? _artworkFor;
    private bool _shown;

    public HomeView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        Motion.PressFeedback(FocusCard);
        foreach (var control in new FrameworkElement[] { PreviousButton, PlayPauseButton, NextButton }) Motion.PressFeedback(control);

        _progressTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += (_, _) => DrawProgress();

        foreach (var key in new[] { "home_bat", "home_vol", "home_wifi" })
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Style = (Style)Application.Current.Resources["RowMeta"], Text = Loc.Get(key) };
            var dots = new DotMatrix
            {
                Columns = SystemDots, Rows = 1, DotSize = 3, Gap = 2, VerticalAlignment = VerticalAlignment.Center,
                OnBrush = Theme.Brush("primary"), OffBrush = Theme.Brush("border"),
            };
            var value = new TextBlock
            {
                FontFamily = Res<FontFamily>("FontDisplay"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = Res<Brush>("PrimaryBrush"), VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(dots, 1);
            Grid.SetColumn(value, 2);
            row.Children.Add(label);
            row.Children.Add(dots);
            row.Children.Add(value);
            SystemRows.Children.Add(row);
            _systemRows.Add((label, dots, value));
        }

        MusicProgress.SizeChanged += (_, _) => FitProgress();
        Cards.SizeChanged += (_, _) => FitProgress();

        app.System.Media.CardChanged += () => { if (_shown) RenderMusic(); };
        app.System.SystemChanged += () => { if (_shown) RenderSystem(); };
        app.Focus.Changed += () => { if (_shown) RenderFocus(); };
        app.Tasks.Changed += () => { if (_shown) RenderTask(); };
        Loc.LanguageChanged += () => { if (_shown) OnShown(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    /// <summary>Tarjetas (o la tarjeta de permiso) entran escalonadas.</summary>
    public IEnumerable<UIElement> EntranceItems =>
        DeniedPanel.Visibility == Visibility.Visible ? DeniedPanel.Children : Cards.Children.Where(c => c.Visibility == Visibility.Visible);

    public void OnShown()
    {
        _shown = true;
        ApplyTexts();
        ApplyLayout();
        RenderMusic();
        RenderFocus();
        RenderSystem();
        RenderTask();
        _ = RecheckPermissionAsync();
    }

    public void OnHidden()
    {
        _shown = false;
        _progressTimer.Stop();
    }

    private void ApplyTexts()
    {
        MusicEmptyLabel.Text = Loc.Get("home_music");
        NoMusicTitle.Text = Loc.Get("empty_errors_no_music");
        NoMusicText.Text = Loc.Get("empty_errors_no_music_2");
        FocusLabel.Text = Loc.Get("home_focus");
        FocusStart.Text = Loc.Get("home_start");
        SystemLabel.Text = Loc.Get("home_system");
        TaskLabel.Text = Loc.Get("home_next_task");
        TaskDone.Text = Loc.Get("home_done");
        DeniedTitle.Text = Loc.Get("empty_errors_notif_denied");
        DeniedText.Text = Loc.Get("empty_errors_notif_denied_2");
        DeniedEnable.Text = Loc.Get("empty_errors_enable");
        DeniedHint.Text = Loc.Get("empty_errors_notif_path");
        string[] keys = ["home_bat", "home_vol", "home_wifi"];
        for (var i = 0; i < _systemRows.Count; i++) _systemRows[i].Label.Text = Loc.Get(keys[i]);
    }

    /// <summary>Tarjetas visibles según Ajustes (homeCards) o la tarjeta de permiso denegado.</summary>
    private void ApplyLayout()
    {
        var denied = _app.Notices.Access == UserNotificationListenerAccess.Denied && !_app.Settings.NotifCardHidden;
        DeniedPanel.Visibility = denied ? Visibility.Visible : Visibility.Collapsed;
        Cards.Visibility = denied ? Visibility.Collapsed : Visibility.Visible;
        DeniedMascot.Cells = MascotFrames.Off();

        var cards = _app.Settings.HomeCards;
        Show(MusicColumn, MusicCard, cards.GetValueOrDefault("MÚSICA", true), 1.5);
        Show(FocusColumn, FocusCard, cards.GetValueOrDefault("FOCO", true), 1);
        Show(SystemColumn, SystemCard, cards.GetValueOrDefault("SISTEMA", true), 1);
        Show(TaskColumn, TaskCard, cards.GetValueOrDefault("PRÓXIMA TAREA", false), 1);
    }

    private static void Show(ColumnDefinition column, FrameworkElement card, bool visible, double stars)
    {
        column.Width = visible ? new GridLength(stars, GridUnitType.Star) : new GridLength(0);
        card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- MÚSICA ----

    private void RenderMusic()
    {
        var card = _app.System.Media.Card;
        MusicFilled.Visibility = card is null ? Visibility.Collapsed : Visibility.Visible;
        MusicEmpty.Visibility = card is null ? Visibility.Visible : Visibility.Collapsed;
        if (card is null)
        {
            _progressTimer.Stop();
            return;
        }

        MusicTitle.Text = card.Title.Length > 0 ? card.Title : Loc.Get("home_music");
        MusicApp.Text = Loc.Upper(card.App);
        ToolTipService.SetToolTip(MusicTitle, string.IsNullOrEmpty(card.Artist) ? card.Title : $"{card.Title} · {card.Artist}");
        PlayPauseButton.Text = card.IsPlaying ? "▮▮" : "▶";
        _ = LoadArtworkAsync(card);
        DrawProgress();
        if (card.IsPlaying) _progressTimer.Start();
        else _progressTimer.Stop();
    }

    private async Task LoadArtworkAsync(MediaService.CardInfo card)
    {
        if (ReferenceEquals(_artworkFor, card.Thumbnail)) return;
        _artworkFor = card.Thumbnail;
        if (card.Thumbnail is null)
        {
            Artwork.Source = null;
            return;
        }
        try
        {
            using var stream = await card.Thumbnail.OpenReadAsync();
            var image = new BitmapImage { DecodePixelWidth = 96 };
            await image.SetSourceAsync(stream);
            if (ReferenceEquals(_artworkFor, card.Thumbnail)) Artwork.Source = image;
        }
        catch (Exception)
        {
            Artwork.Source = null; // carátula no disponible: queda el fondo de puntos
        }
    }

    /// <summary>Tantos puntos de 4 px (gap 3) como quepan en el ancho de la tarjeta.</summary>
    private void FitProgress()
    {
        var width = MusicFilled.ActualWidth - MusicFilled.Padding.Left - MusicFilled.Padding.Right;
        if (width <= 0) return;
        var columns = Math.Max(4, (int)((width + 3) / 7));
        if (MusicProgress.Columns != columns) MusicProgress.Columns = columns;
        DrawProgress();
    }

    private void DrawProgress()
    {
        if (_app.System.Media.Card is not { } card) return;
        var position = card.PositionNow;
        var fraction = card.Duration > TimeSpan.Zero ? position / card.Duration : 0;
        var filled = (int)Math.Round(Math.Clamp(fraction, 0, 1) * MusicProgress.Columns);
        MusicProgress.Cells = Enumerable.Range(0, MusicProgress.Columns).Select(i => i < filled).ToArray();
        MusicPosition.Text = card.Duration > TimeSpan.Zero ? FormatTime(position) : string.Empty;
        MusicDuration.Text = card.Duration > TimeSpan.Zero ? FormatTime(card.Duration) : string.Empty;
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    private void PreviousButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _ = _app.System.Media.PreviousAsync();
    }

    private void PlayPauseButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _ = _app.System.Media.PlayPauseAsync();
    }

    private void NextButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _ = _app.System.Media.NextAsync();
    }

    // ---- FOCO ----

    private void RenderFocus()
    {
        var focus = _app.Focus;
        var accent = Tokens.Current.Accent(_app.Settings.Accent);
        var running = focus.State == FocusService.FocusState.Running;
        var idle = focus.State == FocusService.FocusState.Idle;

        FocusLive.Fill = new SolidColorBrush(accent);
        FocusLive.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        FocusTime.Text = FocusService.Format(focus.Remaining);
        FocusTime.Foreground = Res<Brush>(idle ? "TertiaryBrush" : "PrimaryBrush");

        // Sin empezar o en pausa: botón EMPEZAR. En marcha: un punto por minuto que queda.
        FocusStart.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        FocusDots.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        var minutes = Math.Min(30, (int)focus.Length.TotalMinutes);
        if (FocusDots.Count != minutes)
        {
            FocusDots.Count = minutes;
            FocusDots.Rows = (int)Math.Ceiling(minutes / (double)FocusDots.Columns);
        }
        FocusDots.OnColor = accent;
        var left = (int)Math.Ceiling(focus.Remaining.TotalMinutes);
        FocusDots.Cells = Enumerable.Range(0, minutes).Select(i => i < left).ToArray();
    }

    private void FocusStart_Click(object? sender, EventArgs e) => _app.Focus.Toggle();

    private void FocusCard_Tapped(object sender, TappedRoutedEventArgs e) => _app.Focus.Toggle();

    // ---- SISTEMA ----

    private void RenderSystem()
    {
        var (battery, volume, wifi) = _app.System.Snapshot();
        SetRow(0, battery);
        SetRow(1, volume);
        SetRow(2, wifi);
    }

    private void SetRow(int index, int? percent)
    {
        var (_, dots, value) = _systemRows[index];
        var filled = percent is { } p ? (int)Math.Round(p / 100.0 * SystemDots) : 0;
        dots.Cells = Enumerable.Range(0, SystemDots).Select(i => i < filled).ToArray();
        value.Text = percent is { } v ? $"{v}%" : "—";
    }

    // ---- PRÓXIMA TAREA ----

    private void RenderTask()
    {
        var next = _app.Tasks.Next;
        TaskText.Text = next?.Text ?? Loc.Get("todo_all_done_2");
        TaskTime.Text = next?.Time ?? string.Empty;
        TaskTime.Foreground = new SolidColorBrush(Tokens.Current.Accent(_app.Settings.Accent));
        TaskDone.Visibility = next is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TaskDone_Click(object? sender, EventArgs e)
    {
        if (_app.Tasks.Next is { } next) _app.Tasks.Toggle(next.Id);
    }

    // ---- Permiso de avisos ----

    private async Task RecheckPermissionAsync()
    {
        if (_app.Notices.Access != UserNotificationListenerAccess.Denied) return;
        if (await _app.Notices.RetryAsync()) ApplyLayout();
    }

    private async void DeniedEnable_Click(object? sender, EventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-notifications"));

    private void DeniedHint_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        _app.Settings.NotifCardHidden = true;
        SettingsService.Current.Save();
        ApplyLayout();
    }
}
