using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Parpa.AppWindows;
using Parpa.Services;

namespace Parpa;

public partial class App : Application
{
    private NotchWindow? _notch;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>Arranque oculto: solo aparece el notch, sin ventana principal ni foco.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Tokens.Load();
        Glyphs.Load();
        SettingsService.Load();
        Loc.SetLanguage(SettingsService.Current.S.Lang);
        ApplyColorResources(Tokens.Current.Dark);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Styles.xaml") });

        _notch = new NotchWindow(SettingsService.Current.S);
        _notch.ShowWithoutActivation();
    }

    /// <summary>Cada color de tokens.json como <c>{Nombre}Color</c> y <c>{Nombre}Brush</c> (p. ej. PrimaryBrush).</summary>
    private void ApplyColorResources(IReadOnlyDictionary<string, Windows.UI.Color> palette)
    {
        foreach (var (name, color) in palette)
        {
            var key = char.ToUpperInvariant(name[0]) + name[1..];
            Resources[key + "Color"] = color;
            Resources[key + "Brush"] = new SolidColorBrush(color);
        }
    }
}
