using MessageBroadcast.Core;
using System.Diagnostics;
using System.Windows;
using System.IO;

namespace MessageBroadcast.Sender
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigStore.Instance.Load();

            EnsureFirewallRule();

            DispatcherUnhandledException += (s, ex) =>
            {
                Logger.Log($"Unhandled Exception: {ex.Exception.GetType().ToString()} - {ex.Exception.Message}");
                ex.Handled = true;
                Application.Current.Shutdown();
            };
        }

        private void EnsureFirewallRule()
        {
            var senderExe = Paths.SenderPath;
            var overlayExe = Paths.OverlayPath;

            var ruleName = "MessageBroadcast";
            var checkResult = RunNetsh($"advfirewall firewall show rule name=\"{ruleName}\"");

            if (checkResult.Contains("No rules match"))
            {
                RunNetsh($"advfirewall firewall add rule name=\"{ruleName} Sender\" dir=in action=allow program=\"{senderExe}\"");

                if (File.Exists(overlayExe))
                    RunNetsh($"advfirewall firewall add rule name=\"{ruleName} Overlay\" dir=in action=allow program=\"{overlayExe}\"");

                Logger.Log("[MB] Firewall rules added");
            }
        }

        private string RunNetsh(string args)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;

            return process.StandardOutput.ReadToEnd();
        }
    }
}