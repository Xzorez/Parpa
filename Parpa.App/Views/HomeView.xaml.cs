using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Parpa.Controls;
using Parpa.Services;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class HomeView : UserControl, INotchPageView
{
    private static readonly string[] SystemKeys = ["CPU", "GPU", "RAM"];

    private readonly NotchServices _app;
    private readonly SystemStats _stats = new();
    private readonly DispatcherQueueTimer _progressTimer;
    private readonly List<TextBlock> _systemValues = new();
    private readonly List<TextBlock> _systemNames = new();
    private readonly List<Border> _systemChips = new();
    private readonly BarSlider _musicVolume = new(10, 4, 4, 5, spacing: 2);
    private AppVolume? _musicApp;
    private object? _artworkFor;
    private bool _shown;
    private byte[]? _artBytes;

    public HomeView(NotchServices app)
    {
        _app = app;
        InitializeComponent();
        foreach (var control in new FrameworkElement[] { PreviousButton, PlayPauseButton, NextButton }) Motion.PressFeedback(control);
        if (!Motion.Reduced) Artwork.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(350) };
        // La carátula se tiñe con la paleta: al cambiar de tema se vuelve a teñir.
        Theme.Changed += () => { if (_artBytes is { } bytes) _ = ShowDuotoneAsync(bytes, _artworkFor); RenderSystem(); };

        _progressTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += (_, _) =>
        {
            DrawProgress();
            RenderMusicVolume(); // se puede cambiar desde la propia app o desde Audio
        };

        Grid.SetColumn(_musicVolume, 1);
        _musicVolume.VerticalAlignment = VerticalAlignment.Center;
        MusicVolumeRow.Children.Add(_musicVolume);
        _musicVolume.ValueChanged += percent =>
        {
            if (_musicApp is null) return;
            _app.System.Audio.SetAppVolume(_musicApp, percent);
            MusicVolumeValue.Text = $"{percent}";
        };

        // Píldoras de CPU, GPU y RAM: borde rosa, nombre diminuto y cifra ancha. La más alta va rellena.
        for (var i = 0; i < SystemKeys.Length; i++)
        {
            var value = new TextBlock { Style = Res<Style>("Wide"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Style = Res<Style>("Tiny"), Text = SystemKeys[i] };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(value, 1);
            row.Children.Add(name);
            row.Children.Add(value);
            var chip = new Border
            {
                Padding = new Thickness(10, 0, 10, 0),
                BorderBrush = Res<Brush>("PrimaryBrush"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Child = row,
            };
            Grid.SetColumn(chip, i);
            Chips.Children.Add(chip);
            _systemValues.Add(value);
            _systemNames.Add(name);
            _systemChips.Add(chip);
        }

        MusicProgress.SizeChanged += (_, _) => DrawProgress();

        app.System.Media.CardChanged += () => { if (_shown) RenderMusic(); };
        _stats.Changed += () => { if (_shown) RenderSystem(); };
        app.Tasks.Changed += () => { if (_shown) RenderTask(); };
        Loc.LanguageChanged += () => { if (_shown) OnShown(); };
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static string Up(string text) => text.ToUpper(Loc.Culture);

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
        MusicEmptyLabel.Text = Up(Loc.Get("home_music"));
        NoMusicTitle.Text = Up(Loc.Get("empty_errors_no_music"));
        NoMusicText.Text = Up(Loc.Get("empty_errors_no_music_2"));
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

        MusicTitle.Text = Up(card.Title.Length > 0 ? card.Title : Loc.Get("home_music"));
        MusicApp.Text = Up(card.Artist is { Length: > 0 } artist ? $"{artist} — {card.App}" : card.App);
        ToolTipService.SetToolTip(MusicTitle, string.IsNullOrEmpty(card.Artist) ? card.Title : $"{card.Title} · {card.Artist}");
        PlayPauseIcon.Text = card.IsPlaying ? "" : "";
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
            ClearArtwork();
            return;
        }
        try
        {
            var art = await ArtColor.LoadAsync(card.Thumbnail);
            if (!ReferenceEquals(_artworkFor, card.Thumbnail)) return;
            if (art is null) throw new InvalidOperationException("sin carátula");
            _artBytes = art.Bytes;
            await ShowDuotoneAsync(art.Bytes, card.Thumbnail);
        }
        catch (Exception)
        {
            ClearArtwork(); // carátula no disponible: queda el icono de nota
        }
    }

    private void ClearArtwork()
    {
        _artBytes = null;
        Artwork.Source = null;
        Artwork.Opacity = 0;
    }

    private async Task ShowDuotoneAsync(byte[] bytes, object? owner)
    {
        // Sombras con el fondo y luces con la tinta: en oscuro, negro y rosa.
        var bg = Theme.Get("bg");
        var ink = Theme.Get("primary");
        var dark = Luma(bg) < Luma(ink);
        var bitmap = await DuotoneAsync(bytes, dark ? Color.FromArgb(255, 18, 14, 16) : ink, dark ? ink : bg, 320);
        if (!ReferenceEquals(_artworkFor, owner)) return;
        Artwork.Source = bitmap;
        Artwork.Opacity = 1;
    }

    private static double Luma(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    /// <summary>
    /// Carátula a dos tintas, como una serigrafía: gris → niveles automáticos → curva con contraste → grano →
    /// mezcla entre la tinta de sombra y la de luz.
    /// </summary>
    private static async Task<WriteableBitmap> DuotoneAsync(byte[] bytes, Color shadow, Color light, uint size)
    {
        using var memory = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await memory.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes));
        memory.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(memory);
        var scale = (double)size / Math.Max(1, Math.Min(decoder.PixelWidth, decoder.PixelHeight));
        var width = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * scale));
        var height = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * scale));
        var data = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = width, ScaledHeight = height, InterpolationMode = BitmapInterpolationMode.Fant },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var pixels = data.DetachPixelData();

        var count = pixels.Length / 4;
        var luma = new float[count];
        var histogram = new int[256];
        for (var i = 0; i < count; i++)
        {
            var o = i * 4;
            var l = (0.0722f * pixels[o] + 0.7152f * pixels[o + 1] + 0.2126f * pixels[o + 2]) / 255f;
            luma[i] = l;
            histogram[(int)(l * 255)]++;
        }
        // Niveles: del 2 % más oscuro al 2 % más claro, para que toda carátula use las dos tintas.
        float Percentile(double q)
        {
            var target = count * q;
            var sum = 0;
            for (var v = 0; v < 256; v++) { sum += histogram[v]; if (sum >= target) return v / 255f; }
            return 1;
        }
        var low = Percentile(0.02);
        var high = Math.Max(low + 0.05f, Percentile(0.98));

        var random = new Random(7);
        for (var i = 0; i < count; i++)
        {
            var t = Math.Clamp((luma[i] - low) / (high - low), 0f, 1f);
            t = Math.Clamp((t - 0.5f) * 1.6f + 0.5f + (float)(random.NextDouble() - 0.5) * 0.18f, 0f, 1f);
            t = t * t * (3 - 2 * t);
            var o = i * 4;
            pixels[o] = (byte)(shadow.B + (light.B - shadow.B) * t);
            pixels[o + 1] = (byte)(shadow.G + (light.G - shadow.G) * t);
            pixels[o + 2] = (byte)(shadow.R + (light.R - shadow.R) * t);
            pixels[o + 3] = 255;
        }

        var bitmap = new WriteableBitmap((int)width, (int)height);
        using (var stream = System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsStream(bitmap.PixelBuffer))
            await stream.WriteAsync(pixels);
        bitmap.Invalidate();
        return bitmap;
    }

    private void DrawProgress()
    {
        if (_app.System.Media.Card is not { } card) return;
        var position = card.PositionNow;
        var fraction = card.Duration > TimeSpan.Zero ? position / card.Duration : 0;
        MusicKnob.Margin = new Thickness(Math.Clamp(fraction, 0, 1) * Math.Max(0, MusicProgress.ActualWidth - MusicKnob.Width), 0, 0, 0);
        MusicPosition.Text = card.Duration > TimeSpan.Zero ? FormatTime(position) : string.Empty;
        MusicDuration.Text = card.Duration > TimeSpan.Zero ? "-" + FormatTime(card.Duration - position < TimeSpan.Zero ? TimeSpan.Zero : card.Duration - position) : string.Empty;
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
        MusicVolumeValue.Text = app.Muted ? Up(Loc.Get("audio_muted")) : $"{app.Volume}";
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
        int?[] values = [cpu, gpu, ram];
        // La cifra más alta va en una píldora rellena de rosa, como el bloque destacado del cartel.
        var top = -1;
        for (var i = 0; i < values.Length; i++)
            if (values[i] is { } v && (top < 0 || v > values[top]!.Value)) top = i;
        for (var i = 0; i < values.Length; i++)
        {
            _systemValues[i].Text = values[i] is { } p ? $"{p}%" : "—";
            var hot = i == top;
            _systemChips[i].Background = hot ? Res<Brush>("PrimaryBrush") : null;
            var ink = hot ? Res<Brush>("BgBrush") : Res<Brush>("PrimaryBrush");
            _systemValues[i].Foreground = ink;
            _systemNames[i].Foreground = ink;
        }
    }

    // ---- PRÓXIMA TAREA ----

    private void RenderTask()
    {
        var next = _app.Tasks.Next;
        TaskText.Text = Up(next?.Text ?? Loc.Get("todo_all_done_2"));
        TaskTime.Text = next?.Time ?? string.Empty;
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
