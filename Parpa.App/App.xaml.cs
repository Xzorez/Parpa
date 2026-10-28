using Microsoft.UI.Xaml;
using Parpa.AppWindows;
using Parpa.Services;

namespace Parpa;

public partial class App : Application
{
    private NotchWindow? _notch;
    private TrayService? _tray;
    private SettingsWindow? _settingsWindow;
    private WelcomeWindow? _welcome;
    private string _lang = string.Empty;
    private string _theme = string.Empty;
    private bool? _startup;

    public App()
    {
        InitializeComponent();
    }

    public static new App Current => (App)Application.Current;

    /// <summary>Arranque oculto: solo aparece el notch, sin ventana principal ni foco.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Tokens.Load();
        Glyphs.Load();
        SettingsService.Load();
        var s = SettingsService.Current.S;

        Theme.Initialize(Resources);
        ApplySettings();
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Styles.xaml") });
        SettingsService.Current.Changed += ApplySettings;

        SoundService.Initialize(s);
        _notch = new NotchWindow(s);
        _notch.ShowWithoutActivation();
        _tray = new TrayService(s);

        // Primer uso → bienvenida. "--welcome" / "--settings" abren esas ventanas (accesos directos del instalador).
        var arguments = Environment.GetCommandLineArgs();
        if (!s.WelcomeDone || arguments.Contains("--welcome")) ShowWelcome();
        if (arguments.Contains("--settings")) OpenSettings();
    }

    /// <summary>Idioma, tema e inicio con Windows: se aplican al instante al cambiar un ajuste.</summary>
    private void ApplySettings()
    {
        var s = SettingsService.Current.S;
        if (s.Lang != _lang)
        {
            _lang = s.Lang;
            Loc.SetLanguage(s.Lang);
        }
        if (s.Theme != _theme)
        {
            _theme = s.Theme;
            Theme.Apply(s.Theme);
        }
        if (s.Startup != _startup)
        {
            _startup = s.Startup;
            StartupService.Apply(s.Startup);
        }
    }

    /// <summary>Ventana de Ajustes completa (una sola instancia).</summary>
    public void OpenSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
    }

    public void ShowWelcome()
    {
        if (_welcome is null)
        {
            _welcome = new WelcomeWindow();
            _welcome.Closed += (_, _) => _welcome = null;
        }
        _welcome.Activate();
    }

    /// <summary>Abre el notch desde el menú de bandeja.</summary>
    public void OpenNotch() => _notch?.OpenFromTray();

    public void Quit()
    {
        _tray?.Dispose();
        SoundService.Dispose();
        _settingsWindow?.Close();
        _welcome?.Close();
        _notch?.Close();
        Exit();
    }
}
