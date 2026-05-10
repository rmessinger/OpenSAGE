namespace OpenSage.Tools.ReplaySketch.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new MainPage());
    }
}
