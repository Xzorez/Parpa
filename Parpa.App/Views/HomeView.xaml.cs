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
    private readonly List<TextBlock> _systemValues = new();
    private readonly List<FrameworkElement> _systemChips = new();
    private readonly BarSlider _musicVolume = new(10, 4, 4, 5, spacing: 2);
    private AppVolume? _musicApp;
    private object? _artworkFor;
    private bool _shown;
    private bool _onArt;

    public HomeView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        foreach (var control in new FrameworkElement[] { PreviousButton, PlayPauseButton, NextButton }) Motion.PressFeedback(control);
        if (!Motion.Reduced)
        {
            ArtBackdrop.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(450) };
            ArtShade.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(450) };
        }
        TintMusic();
        Theme.Changed += TintMusic;

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

        // Píldoras de CPU (verde), GPU (azul) y RAM (ámbar): punto de color, nombre y porcentaje.
        for (var i = 0; i < SystemKeys.Length; i++)
        {
            var value = new TextBlock
            {
                FontFamily = Res<FontFamily>("FontUi"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Res<Brush>("PrimaryBrush"), VerticalAlignment = VerticalAlignment.Center,
            };
            var chip = new Grid
            {
                Padding = new Thickness(12, 0, 12, 0),
                Background = Res<Brush>("CardBrush"),
                CornerRadius = new CornerRadius(14),
                ColumnSpacing = 7,
            };
            chip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chip.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            chip.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(AccentByName(SystemColors[i])),
            });
            var name = new TextBlock
            {
                Text = SystemKeys[i], FontFamily = Res<FontFamily>("FontUi"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Res<Brush>("SecondaryBrush"), VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(value, 2);
            chip.Children.Add(name);
            chip.Children.Add(value);
            Grid.SetColumn(chip, i);
            Chips.Children.Add(chip);
            _systemValues.Add(value);
            _systemChips.Add(chip);
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
        DeniedPanel.Visibility == Visibility.Visible
            ? DeniedPanel.Children
            : new UIElement[] { MusicCard }.Concat(Chips.Children).Where(c => c.Visibility == Visibility.Visible);

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
        ToolTipService.SetToolTip(TaskDone, Loc.Get("home_done"));
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
        MusicCard.Visibility = cards.GetValueOrDefault("MÚSICA", true) ? Visibility.Visible : Visibility.Collapsed;
        var system = cards.GetValueOrDefault("SISTEMA", true);
        foreach (var chip in _systemChips) chip.Visibility = system ? Visibility.Visible : Visibility.Collapsed;
        foreach (var column in new[] { CpuColumn, GpuColumn, RamColumn })
            column.Width = system ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        var task = cards.GetValueOrDefault("PRÓXIMA TAREA", false);
        TaskCard.Visibility = task ? Visibility.Visible : Visibility.Collapsed;
        TaskColumn.Width = task ? new GridLength(2.2, GridUnitType.Star) : new GridLength(0);
        Chips.Visibility = system || task || MusicVolume.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
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
            RenderMusicVolume();
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
            HideBackdrop();
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
            var image = new BitmapImage { DecodePixelWidth = 152 };
            await image.SetSourceAsync(memory);
            // Fondo: la misma carátula a 12 px; al estirarla se ve difuminada (sin Win2D ni efectos).
            memory.Seek(0);
            var backdrop = new BitmapImage { DecodePixelWidth = 12 };
            await backdrop.SetSourceAsync(memory);
            if (!ReferenceEquals(_artworkFor, card.Thumbnail)) return;
            Artwork.Source = image;
            ArtBackdrop.Source = backdrop;
            ArtBackdrop.Opacity = 1;
            ArtShade.Opacity = 1;
            _onArt = true;
            TintMusic();
        }
        catch (Exception)
        {
            Artwork.Source = null; // carátula no disponible: queda el icono de nota
            HideBackdrop();
        }
    }

    private void HideBackdrop()
    {
        ArtBackdrop.Opacity = 0;
        ArtShade.Opacity = 0;
        _onArt = false;
        TintMusic();
    }

    /// <summary>
    /// Sobre la carátula (o en tema oscuro) el texto de la tarjeta es blanco; en tema claro sin carátula
    /// la tarjeta es clara y el texto pasa a los colores de la paleta.
    /// </summary>
    private void TintMusic()
    {
        var white = _onArt || !Theme.IsLight;
        Brush primary = white ? new SolidColorBrush(Microsoft.UI.Colors.White) : Theme.Brush("primary");
        Brush secondary = white ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xB3, 255, 255, 255)) : Theme.Brush("secondary");
        var ink = white ? Microsoft.UI.Colors.White : Theme.Get("primary");
        MusicTitle.Foreground = primary;
        foreach (var control in new[] { PreviousButton, PlayPauseButton, NextButton }) control.Foreground = primary;
        MusicProgressFill.Background = primary;
        MusicApp.Foreground = MusicPosition.Foreground = MusicDuration.Foreground = ArtIcon.Foreground = secondary;
        MusicTrack.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, ink.R, ink.G, ink.B));
        ArtTile.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, ink.R, ink.G, ink.B));
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
        var visible = _musicApp is not null && MusicCard.Visibility == Visibility.Visible;
        if ((MusicVolume.Visibility == Visibility.Visible) != visible)
        {
            MusicVolume.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            VolumeColumn.Width = visible ? new GridLength(1.7, GridUnitType.Star) : new GridLength(0);
            if (visible) Chips.Visibility = Visibility.Visible;
        }
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

    private void SetRow(int index, int? percent) => _systemValues[index].Text = percent is { } p ? $"{p}%" : "—";

    // ---- PRÓXIMA TAREA ----

    private void RenderTask()
    {
        var next = _app.Tasks.Next;
        TaskText.Text = next?.Text ?? Loc.Get("todo_all_done_2");
        TaskTime.Text = next?.Time ?? string.Empty;
        TaskTime.Foreground = new SolidColorBrush(Tokens.Current.Accent(_app.Settings.Accent));
        TaskDone.Visibility = next is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TaskDone_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
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
