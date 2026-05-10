namespace OpenSage.Tools.ReplaySketch.Maui;

public partial class App : Application
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;

    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new MainPage());
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Width = DefaultWidth;
        window.Height = DefaultHeight;
        return window;
    }
}
