using System.Windows;
using MessageBroadcast.Core;

namespace MessageBroadcast.Updater
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                Logger.Log($"[UPD] Unhandled Exception: {ex.Exception.GetType().ToString()} - {ex.Exception.Message}");
                ex.Handled = true;
                Application.Current.Shutdown();
            };
        }
    }
}
