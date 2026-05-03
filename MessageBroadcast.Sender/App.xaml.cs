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

            Dispatcher.InvokeAsync(async () =>
            {
                var update = await VersionCheck.CheckForUpdates();
                if (update != null)
                {
                    var result = ShowUpdateDialog(update);

                    if (result == UpdatePromptResult.SkipVersion)
                    {
                        Logger.Log($"[SND] Version {update.NewVersion.ToString(3)} skipped");
                        ConfigStore.Instance.UpdateAppConfig(nameof(AppConfig.SkipVersion), update.NewVersion);
                    }
                    else if (result == UpdatePromptResult.Accept)
                    {
                        LaunchUpdater(update);
                    }
                }
            });

            DispatcherUnhandledException += (s, ex) =>
            {
                var inner = ex.Exception.InnerException;
                Logger.Log($"[SND] Unhandled Exception: {ex.Exception.GetType()} - {ex.Exception.Message}");
                if (inner != null)
                    Logger.Log($"[SND] Inner Exception: {inner.GetType()} - {inner.Message}");
                ex.Handled = true;
                Application.Current.Shutdown();
            };
        }

        private UpdatePromptResult ShowUpdateDialog(VersionCheck.UpdateInfo update)
        {
            var window = new UpdatePrompt();
            window.NewVersionLabel.Text = $"v{update.NewVersion.ToString(3)}";
            window.CurrentVersionLabel.Text = $"v{update.CurrentVersion.ToString(3)}";

            window.ShowDialog();
            return window.Result;
        }

        private void LaunchUpdater(VersionCheck.UpdateInfo update)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.UpdaterPath,
                    Arguments = $"{update.DownloadUrl} {Environment.ProcessId}",
                    UseShellExecute = false // Sender already launches as admin, inherit token
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[SND] Failed to launch Updater: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                Application.Current.Shutdown();
            }
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

                Logger.Log("[SND] Firewall rules added");
            }
        }

        private string RunNetsh(string args)
        {
            try
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
            catch (Exception ex)
            {
                Logger.Log($"[SND] Failed to launch Netsh: {ex.Message}");
            }
            return string.Empty;
        }
    }
}