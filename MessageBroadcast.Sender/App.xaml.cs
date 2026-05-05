using MessageBroadcast.Core;
using MessageBroadcast.Overlay;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;

using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace MessageBroadcast.Sender
{
    public partial class App : Application
    {
        private MessageListener? _listener;
        private DeviceDiscovery? _discovery;
        private OverlayWindow? _overlay;
        private NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private readonly AudioPlayer _audioPlayer = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigStore.Instance.Load();

            var localDevice = new DeviceInfo
            {
                Id = DeviceIdentity.LoadOrCreateUuid(),
                Name = Environment.MachineName,
                Port = 41235
            };

            _listener = new MessageListener(localDevice.Port);
            _listener.MessageReceived += OnMessageReceived;
            _listener.Start();

            _discovery = new DeviceDiscovery(localDevice);
            _discovery.Start();

            _mainWindow = new MainWindow();
            if (!e.Args.Contains("--hidden"))
                _mainWindow.Show();

            SetupTrayIcon();

            Dispatcher.InvokeAsync(async () =>
            {
                var update = await VersionCheck.CheckForUpdates();
                
                // App is outdated, show update prompt
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

        private void OnMessageReceived(Message message)
        {
            ConfigStore.Instance.LoadDeviceConfigs();

            Dispatcher.Invoke(async () =>
            {
                var config = ConfigStore.Instance.GetDeviceConfig(message.SenderId);
                // Don't show blocked messages
                if (config.Blocked)
                {
                    Logger.Log($"[SND] Message received from blocked user ({message.DeviceName}) was not shown");
                    return;
                }

                // Messages that contain only sound don't create an overlay
                if (message.ContentType == MessageContentType.Sound)
                {
                    await _audioPlayer.PlayAsync(message.SoundData!, message.SoundFormat);
                    return;
                }

                if (_overlay == null)
                {
                    _overlay = new OverlayWindow();
                    _overlay.Closed += (_, _) => _overlay = null;
                    _overlay.Show();
                }

                _overlay.ShowMessage(message);
            });
        }

        private void SetupTrayIcon()
        {
            // Create an icon in the system tray with various options
            _trayIcon = new NotifyIcon
            {
                Icon = File.Exists(Paths.IconPath) ? new Icon(Paths.IconPath) : SystemIcons.Application,
                Visible = true,
                Text = "SBroadcast"
            };
            _trayIcon.MouseDoubleClick += (_, _) =>
            {
                _mainWindow!.Show();
                _mainWindow!.WindowState = WindowState.Normal;
                _mainWindow!.Activate();
            };

            var menu = new ContextMenuStrip();

            var stopAudioItem = new ToolStripMenuItem("Stop Audio");
            stopAudioItem.Click += (_, _) =>
            {
                StopCurrentAudio();
                Logger.Log("[SND] Audio stopped via tray item");
            };

            var startupItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = IsRegisteredForStartup(),
                CheckOnClick = true
            };
            startupItem.CheckedChanged += (_, _) =>
            {
                if (startupItem.Checked) RegisterStartup();
                else UnregisterStartup();
            };

            var openSenderItem = new ToolStripMenuItem("Open");
            openSenderItem.Click += (_, _) =>
            {
                _mainWindow!.Show();
                _mainWindow!.WindowState = WindowState.Normal;
                _mainWindow!.Activate();
            };

            menu.Items.Add(openSenderItem);
            menu.Items.Add(stopAudioItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) =>
            {
                StopCurrentAudio();
                _trayIcon!.Visible = false;
                Shutdown();
            });

            _trayIcon.ContextMenuStrip = menu;
        }

        private void StopCurrentAudio()
        {
            Logger.Log("[SND] StopCurrentAudio called");
            Dispatcher.Invoke(() =>
            {
                _overlay?.StopAudio();
                _audioPlayer.Stop();
            });
        }

        // Show update prompt as modal window
        private UpdatePromptResult ShowUpdateDialog(VersionCheck.UpdateInfo update)
        {
            var window = new UpdatePrompt();
            window.NewVersionLabel.Text = $"v{update.NewVersion.ToString(3)}";
            window.CurrentVersionLabel.Text = $"v{update.CurrentVersion.ToString(3)}";

            window.ShowDialog();
            return window.Result;
        }

        // Launch auto-updater process
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

        private bool IsRegisteredForStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("MessageSender") != null;
        }

        // Register sender to start with Windows
        private void RegisterStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue("MessageSender", $"\"{Environment.ProcessPath}\" --hidden");
        }

        // Unregister sender, don't start with Windows
        private void UnregisterStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("MessageSender", throwOnMissingValue: false);
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

        // Run netsh process for firewall rules
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