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
    private static readonly string[] SystemKeys = ["CPU", "GPU", "RAM"];
    private static readonly string[] SystemColors = ["verde", "azul", "ambar"];

    private readonly NotchServices _app;
    private readonly SystemStats _stats = new();
    private readonly DispatcherQueueTimer _progressTimer;
    private readonly List<Ring> _systemRings = new();
    private readonly BarSlider _musicVolume = new(20, 4, 4, 5, spacing: 2);
    private AppVolume? _musicApp;
    private object? _artworkFor;
    private bool _shown;

    public HomeView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        foreach (var control in new FrameworkElement[] { PreviousButton, PlayPauseButton, NextButton }) Motion.PressFeedback(control);
        ArtworkGlow.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(450) };

        _progressTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += (_, _) =>
        {
            DrawProgress();
            RenderMusicVolume(); // se puede cambiar desde la propia app o desde Audio
        };

        Grid.SetColumn(_musicVolume, 1);
        MusicVolume.Children.Add(_musicVolume);
        _musicVolume.ValueChanged += percent =>
        {
            if (_musicApp is null) return;
            _app.System.Audio.SetAppVolume(_musicApp, percent);
            MusicVolumeValue.Text = $"{percent}%";
        };

        // Anillos de CPU (verde), GPU (azul) y RAM (ámbar), repartidos a lo ancho con su nombre debajo.
        for (var i = 0; i < SystemKeys.Length; i++)
        {
            SystemRows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ring = new Ring(48, 5, AccentByName(SystemColors[i]), Theme.Brush("border"));
            var item = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
            item.Children.Add(ring);
            item.Children.Add(new TextBlock
            {
                Text = SystemKeys[i], FontFamily = Res<FontFamily>("FontUi"), FontSize = 11,
                Foreground = Res<Brush>("SecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Center,
            });
            Grid.SetColumn(item, i);
            SystemRows.Children.Add(item);
            _systemRings.Add(ring);
        }

        MusicProgress.SizeChanged += (_, _) => DrawProgress();

        app.System.Media.CardChanged += () => { if (_shown) RenderMusic(); };
        _stats.Changed += () => { if (_shown) RenderSystem(); };
        app.Tasks.Changed += () => { if (_shown) RenderTask(); };
        Loc.LanguageChanged += () => { if (_shown) OnShown(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    /// <summary>Color de acento por nombre de tokens.json (rojo, ambar, verde, azul).</summary>
    private static Color AccentByName(string name) => name switch
    {
        "rojo" => Tokens.Current.Accent(0),
        "ambar" => Tokens.Current.Accent(1),
        "verde" => Tokens.Current.Accent(2),
        _ => Tokens.Current.Accent(3),
    };

    /// <summary>Tarjetas (o la tarjeta de permiso) entran escalonadas.</summary>
    public IEnumerable<UIElement> EntranceItems =>
        DeniedPanel.Visibility == Visibility.Visible ? DeniedPanel.Children : Cards.Children.Where(c => c.Visibility == Visibility.Visible);

    public void OnShown()
    {
        _shown = true;
        ApplyTexts();
        ApplyLayout();
        RenderMusic();
        RenderSystem();
        _stats.Start();
        RenderTask();
        _ = RecheckPermissionAsync();
    }

    public void OnHidden()
    {
        _shown = false;
        _progressTimer.Stop();
        _stats.Stop();
    }

    private void ApplyTexts()
    {
        MusicEmptyLabel.Text = Loc.Get("home_music");
        NoMusicTitle.Text = Loc.Get("empty_errors_no_music");
        NoMusicText.Text = Loc.Get("empty_errors_no_music_2");
        SystemLabel.Text = Loc.Get("home_system");
        TaskLabel.Text = Loc.Get("home_next_task");
        TaskDone.Text = Loc.Get("home_done");
        DeniedTitle.Text = Loc.Get("empty_errors_notif_denied");
        DeniedText.Text = Loc.Get("empty_errors_notif_denied_2");
        DeniedEnable.Text = Loc.Get("empty_errors_enable");
        DeniedHint.Text = Loc.Get("empty_errors_notif_path");
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
        MusicApp.Text = card.Artist is { Length: > 0 } artist ? $"{artist} · {card.App}" : card.App;
        ToolTipService.SetToolTip(MusicTitle, string.IsNullOrEmpty(card.Artist) ? card.Title : $"{card.Title} · {card.Artist}");
        PlayPauseButton.Text = card.IsPlaying ? "" : "";
        _ = LoadArtworkAsync(card);
        DrawProgress();
        RenderMusicVolume();
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
            ArtworkGlow.Opacity = 0;
            return;
        }
        try
        {
            var art = await ArtColor.LoadAsync(card.Thumbnail);
            if (!ReferenceEquals(_artworkFor, card.Thumbnail)) return;
            if (art is null) throw new InvalidOperationException("sin carátula");
            using var memory = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await memory.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(art.Bytes));
            memory.Seek(0);
            var image = new BitmapImage { DecodePixelWidth = 96 };
            await image.SetSourceAsync(memory);
            if (!ReferenceEquals(_artworkFor, card.Thumbnail)) return;
            Artwork.Source = image;
            // Resplandor del color de la carátula detrás (como el iPhone): da profundidad a la tarjeta.
            if (art.Color is { } color)
            {
                ArtworkGlow.Background = ArtColor.GlowBrush(color);
                ArtworkGlow.Opacity = 1;
            }
            else ArtworkGlow.Opacity = 0;
        }
        catch (Exception)
        {
            Artwork.Source = null; // carátula no disponible: queda el icono de nota
            ArtworkGlow.Opacity = 0;
        }
    }

    private void DrawProgress()
    {
        if (_app.System.Media.Card is not { } card) return;
        var position = card.PositionNow;
        var fraction = card.Duration > TimeSpan.Zero ? position / card.Duration : 0;
        MusicProgressFill.Width = Math.Clamp(fraction, 0, 1) * MusicProgress.ActualWidth;
        MusicPosition.Text = card.Duration > TimeSpan.Zero ? FormatTime(position) : string.Empty;
        MusicDuration.Text = card.Duration > TimeSpan.Zero ? "−" + FormatTime(card.Duration - position < TimeSpan.Zero ? TimeSpan.Zero : card.Duration - position) : string.Empty;
    }

    private static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    /// <summary>Volumen de la app que suena (si suena por la salida actual; si no, la fila no sale).</summary>
    private void RenderMusicVolume()
    {
        if (_musicVolume.IsDragging) return;
        var card = _app.System.Media.Card;
        _musicApp = card is null ? null : _app.System.Audio.AppFor(card.Source);
        MusicVolume.Visibility = _musicApp is null ? Visibility.Collapsed : Visibility.Visible;
        if (_musicApp is not { } app) return;
        _musicVolume.Show(app.Muted ? 0 : app.Volume);
        MusicVolumeValue.Text = app.Muted ? Loc.Get("audio_muted") : $"{app.Volume}%";
    }

    private void MusicVolume_PointerWheelChanged(object sender, PointerRoutedEventArgs e) => _musicVolume.Wheel(e);

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

    // ---- SISTEMA ----

    private void RenderSystem()
    {
        var (cpu, gpu, ram) = _stats.Current;
        SetRow(0, cpu);
        SetRow(1, gpu);
        SetRow(2, ram);
    }

    private void SetRow(int index, int? percent) => _systemRings[index].Set(percent);

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
