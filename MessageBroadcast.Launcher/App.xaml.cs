using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MessageBroadcast.Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var senderPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Internal", "MessageBroadcast.Sender.exe");

            if (!File.Exists(senderPath))
            {
                Debug.WriteLine("Could not find MessageBroadcast.Sender.exe");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = senderPath,
                UseShellExecute = true
            });

            Shutdown();
        }
    }

}
