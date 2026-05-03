using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroadcast.Core
{
    public static class Paths
    {
        public static string AppData => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MessageBroadcast");

        public static string IconPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets", "app.ico");

        public static string SenderPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "MessageBroadcast.Sender.exe");

        public static string OverlayPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "MessageBroadcast.Overlay.exe");

        public static string DeviceUuidPath => Path.Combine(AppData, "device.uuid");
        public static string PreferencesPath => Path.Combine(AppData, "preferences.json");
        public static string DeviceConfigsPath => Path.Combine(AppData, "device_configs.json");
        public static string LogPath => Path.Combine(AppData, "log.txt");
    }
}
