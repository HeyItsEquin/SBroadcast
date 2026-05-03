using MessageBroadcast.Core;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Message = MessageBroadcast.Core.Message;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using SystemIcons = System.Drawing.SystemIcons;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace MessageBroadcast.Overlay
{
    public partial class App : System.Windows.Application
    {
        private MessageListener? _listener;
        private DeviceDiscovery? _discovery;
        private OverlayWindow? _overlay;
        private NotifyIcon? _trayIcon;
        private readonly AudioPlayer _audioPlayer = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigStore.Instance.Load();
            Logger.Clear();
            Logger.Log("[OVR] Overlay starting up");

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

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

            SetupTrayIcon();
        }

        private void OnMessageReceived(Message message)
        {
            ConfigStore.Instance.LoadDeviceConfigs();

            Dispatcher.Invoke(async () =>
            {
                var config = ConfigStore.Instance.GetDeviceConfig(message.SenderId);
                if(config.Blocked)
                {
                    Logger.Log($"[OVR] Message received from blocked user ({message.DeviceName}) was not shown");
                    return;
                }

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
            _trayIcon = new NotifyIcon
            {
                Icon = File.Exists(Paths.IconPath) ? new Icon(Paths.IconPath) : SystemIcons.Application,
                Visible = true,
                Text = "SBroadcast"
            };

            var menu = new ContextMenuStrip();

            var stopAudioItem = new ToolStripMenuItem("Stop Audio");
            stopAudioItem.Click += (_, _) =>
            {
                StopCurrentAudio();
                Logger.Log("[OVR] Audio stopped via tray menu");
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
                Logger.Log("[OVR] Opening sender from tray");
                var fullPath = Path.GetFullPath(Paths.SenderPath);

                if (File.Exists(fullPath))
                {
                    var processName = "MessageBroadcast.Sender";
                    if (Process.GetProcessesByName(processName).Length > 0)
                    {
                        Logger.Log("[OVR] Sender already running");
                        return;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fullPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Logger.Log($"[OVR] Sender not found at {fullPath}");
                    System.Windows.Forms.MessageBox.Show(
                        "Could not find MessageBroadcast.Sender.exe.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
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
            Logger.Log("[OVR] StopCurrentAudio called");
            Dispatcher.Invoke(() =>
            {
                _overlay?.StopAudio();
                _audioPlayer.Stop();
            });
        }

        private bool IsRegisteredForStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("MessageOverlay") != null;
        }

        private void RegisterStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.SetValue("MessageOverlay", $"\"{Environment.ProcessPath}\"");
        }

        private void UnregisterStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("MessageOverlay", throwOnMissingValue: false);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _audioPlayer.Dispose();
            _trayIcon?.Dispose();
            _listener?.Dispose();
            _discovery?.Dispose();
            base.OnExit(e);
        }
    }
}