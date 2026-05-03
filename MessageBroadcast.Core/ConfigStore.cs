using System.Text.Json;
using System.IO;

namespace MessageBroadcast.Core
{
    public class AppConfig
    {
        public int DefaultFontSize { get; set; } = 36;
        public string DefaultFontFamily { get; set; } = "Segoe UI";
        public string DefaultFontColor { get; set; } = "#FFFFFF";
        public int DefaultDisplaySeconds { get; set; } = 5;
        public MessagePosition DefaultPosition { get; set; } = MessagePosition.Center;
    }

    public class DeviceConfig
    {
        public string? Nickname { get; set; }
        public bool Blocked { get; set; } = false;
        public bool Favorite { get; set; } = false;
        public string? PreferredIp { get; set; }
    }

    public class ConfigStore
    {
        public static ConfigStore Instance { get; } = new();

        private AppConfig _appConfig = new();
        private Dictionary<Guid, DeviceConfig> _deviceConfigs = new();

        public void Load()
        {
            LoadPreferences();
            LoadDeviceConfigs();
        }

        public void Save()
        {
            SavePreferences();
            SaveDeviceConfigs();
        }

        public void LoadPreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.PreferencesPath)!);

                if (!File.Exists(Paths.PreferencesPath))
                {
                    _appConfig = new AppConfig();
                    Logger.Log("[MB] No preferences file found, using defaults");
                    return;
                }

                var json = File.ReadAllText(Paths.PreferencesPath);
                _appConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                Logger.Log("[MB] Preferences loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Failed to load preferences: {ex.Message}");
            }
        }

        public void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.PreferencesPath)!);

                var json = JsonSerializer.Serialize(_appConfig, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(Paths.PreferencesPath, json);
                Logger.Log("[MB] Preferences saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Failed to save preferences: {ex.Message}");
            }
        }

        public void LoadDeviceConfigs()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.DeviceConfigsPath)!);

                if (!File.Exists(Paths.DeviceConfigsPath))
                {
                    _deviceConfigs = new Dictionary<Guid, DeviceConfig>();
                    Logger.Log("[MB] No device config file found, using defaults");
                    return;
                }

                var json = File.ReadAllText(Paths.DeviceConfigsPath);
                _deviceConfigs = JsonSerializer.Deserialize<Dictionary<Guid, DeviceConfig>>(json)
                    ?? new Dictionary<Guid, DeviceConfig>();
                Logger.Log("[MB] Device configs loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Failed to load device configs: {ex.Message}");
            }
        }

        public void SaveDeviceConfigs()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.DeviceConfigsPath)!);

                var json = JsonSerializer.Serialize(_deviceConfigs, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(Paths.DeviceConfigsPath, json);
                Logger.Log("[MB] Device configs saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Failed to save device configs: {ex.Message}");
            }
        }

        public DeviceConfig GetDeviceConfig(Guid deviceId)
        {
            return _deviceConfigs.TryGetValue(deviceId, out var config)
                ? config
                : new DeviceConfig();
        }

        public void SetDeviceConfig(Guid deviceId, DeviceConfig config)
        {
            _deviceConfigs[deviceId] = config;
            SaveDeviceConfigs();
        }

        public void RemoveDeviceConfig(Guid deviceId)
        {
            _deviceConfigs.Remove(deviceId);
            SaveDeviceConfigs();
        }

        public void UpdateDeviceConfig(Guid deviceId, string property, object value)
        {
            var prop = typeof(DeviceConfig).GetProperty(property)
                ?? throw new ArgumentException($"DeviceConfig has no such property '{property}'");

            if (!prop.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Expected value of type {prop.PropertyType.Name}, got {value.GetType().Name}");

            var config = GetDeviceConfig(deviceId);
            prop.SetValue(config, value);
            SetDeviceConfig(deviceId, config);
        }

        public AppConfig GetAppConfig()
        {
            return _appConfig;
        }

        public void SetAppConfig(AppConfig config)
        {
            _appConfig = config;
            SavePreferences();
        }

        public void UpdateAppConfig(string property, object value)
        {
            var prop = typeof(AppConfig).GetProperty(property)
                ?? throw new ArgumentException($"AppConfig has no such property '{property}'");

            if (!prop.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Expected value of type {prop.PropertyType.Name}, got {value.GetType().Name}");

            var config = GetAppConfig();
            prop.SetValue(config, value);
            SetAppConfig(config);
        }
    }
}