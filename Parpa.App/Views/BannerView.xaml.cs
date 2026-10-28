using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Parpa.ViewModels;
using Windows.UI;

namespace Parpa.Views;

public sealed partial class BannerView : UserControl
{
    public BannerView() => InitializeComponent();

    public void Show(NotchIndicator banner, Color accent)
    {
        Dot.Fill = new SolidColorBrush(accent);
        Label.Text = banner.Label;
        Link.Text = banner.Value;
    }
}
