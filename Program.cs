using Mana.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace Mana;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ComWrappersSupport.InitializeComWrappers();
            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    _ = new App();
                }
                catch (Exception ex)
                {
                    StartupCrashLog.Write("App constructor failed.", ex);
                    StartupCrashLog.ShowNativeMessage(
                        $"Startup failed.\n\n{ex.Message}\n\nDetails: {StartupCrashLog.LogPath}",
                        "mana as setting keyboard layout");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            StartupCrashLog.Write("Application.Start failed.", ex);
            StartupCrashLog.ShowNativeMessage(
                $"Fatal startup error.\n\n{ex.Message}\n\nHRESULT may appear in Event Viewer (Application).\nLog: {StartupCrashLog.LogPath}",
                "mana as setting keyboard layout");
        }
    }
}
