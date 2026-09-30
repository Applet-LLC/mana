using Mana.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mana;

public partial class App : Application
{
    private Window? _window;

    public static MainWindow? MainAppWindow { get; private set; }

    public App()
    {
        UnhandledException += App_UnhandledException;
        InitializeComponent();
        LanguageService.Initialize();
        Localization.Refresh();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupCrashLog.Write("Unhandled UI exception.", e.Exception);
        e.Handled = true;
        StartupCrashLog.ShowNativeMessage(
            $"Unhandled error:\n\n{e.Message}\n\nLog: {StartupCrashLog.LogPath}",
            "mana as setting keyboard layout");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            MainAppWindow = (MainWindow)_window;
            _window.Activate();
        }
        catch (Exception ex)
        {
            StartupCrashLog.Write("OnLaunched failed.", ex);
            StartupCrashLog.ShowNativeMessage(
                $"Failed to open window.\n\n{ex.Message}\n\nLog: {StartupCrashLog.LogPath}",
                "mana as setting keyboard layout");
            throw;
        }
    }

    public static void ReloadUiLanguage()
    {
        Localization.Refresh();
        MainAppWindow?.ReloadForLanguageChange();
    }
}
