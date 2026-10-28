using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Parpa.Services;
using Parpa.ViewModels;

namespace Parpa.Views;

public sealed partial class OpenView : UserControl
{
    private NotchPage _page = NotchPage.Home;

    public OpenView()
    {
        InitializeComponent();
        Loc.LanguageChanged += () => SetPage(_page);
    }

    public void SetPage(NotchPage page)
    {
        _page = page;
        PageGlyph.Glyph = page.Glyph;
        PageTitle.Text = Loc.Get(page.TitleKey);
    }

    public void Apply(ParpaSettings s) =>
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;

    private void MenuButton_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        MenuButton.Background = (Brush)Application.Current.Resources["BorderAltBrush"];

    private void MenuButton_PointerExited(object sender, PointerRoutedEventArgs e) =>
        MenuButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
}
