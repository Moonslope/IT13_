namespace HestiaIT13Final
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage()) { Title = "HestiaIT13Final" };

#if WINDOWS
            window.Created += (sender, args) =>
            {
                var mauiWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (mauiWindow == null) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mauiWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            };
#endif

            return window;
        }
    }
}
