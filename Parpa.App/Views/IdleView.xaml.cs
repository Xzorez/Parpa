using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parpa.Controls;
using Parpa.Services;

namespace Parpa.Views;

public sealed partial class IdleView : UserControl
{
    public IdleView()
    {
        InitializeComponent();
        Mascot.Cells = MascotFrames.Awake();
    }

    public void Apply(ParpaSettings s)
    {
        Mascot.Visibility = s.Mascot ? Visibility.Visible : Visibility.Collapsed;
        Clock.Visibility = s.Mascot ? Visibility.Collapsed : Visibility.Visible;
        Clock.Text = s.Clock ? ClockService.Format(DateTime.Now, s.H24) : string.Empty;
    }
}
