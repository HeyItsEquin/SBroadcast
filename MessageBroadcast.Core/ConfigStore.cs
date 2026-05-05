using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;

namespace MessageBroadcast.Core
{
    public class AppConfig
    {
        // Message option defaults
        public int DefaultFontSize { get; set; } = 36;
        public string DefaultFontFamily { get; set; } = "Segoe UI";
        public string DefaultFontColor { get; set; } = "#FFFFFF";
        public int DefaultDisplaySeconds { get; set; } = 5;
        public MessagePosition DefaultPosition { get; set; } = MessagePosition.Center;

        // The version number that the user skipped
        // If this is the latest version, do not prompt to update
        [JsonConverter(typeof(VersionConverter))]
        public Version? SkipVersion { get; set; } = null;
    }

    public class DeviceConfig
    {
        public string? Nickname { get; set; }       // The nickname given to this user (or null if none has been given)
        public bool Blocked { get; set; } = false;  // Whether this device has been blocked
        public bool Favorite { get; set; } = false; // Whether this device is favorited
        public string? PreferredIp { get; set; }    // IP used for communication with this device
    }

    public class ConfigStore
    {
        // Singleton
        public static ConfigStore Instance { get; } = new();

        private AppConfig _appConfig = new();
        private Dictionary<Guid, DeviceConfig> _deviceConfigs = new();

        // After this is called, ConfigStore will be ready to pull from
        public void Load()
        {
            LoadPreferences();
            LoadDeviceConfigs();
        }

        // Write updated configs
        public void Save()
        {
            SavePreferences();
            SaveDeviceConfigs();
        }

        // Load settings from preferences.json
        public void LoadPreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.PreferencesPath)!);

                if (!File.Exists(Paths.PreferencesPath))
                {
                    _appConfig = new AppConfig();
                    // TODO: Create preferences file when missing
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
                    WriteIndented = true // Make it human-readable
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
                    WriteIndented = true // Make it human-readable
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

        // I don't think this is even used
        public void RemoveDeviceConfig(Guid deviceId)
        {
            _deviceConfigs.Remove(deviceId);
            SaveDeviceConfigs();
        }

        // Update a specific property of a device's config
        // Get property via nameof(DeviceConfig.Property)
        public void UpdateDeviceConfig(Guid deviceId, string property, object value)
        {
            var prop = typeof(DeviceConfig).GetProperty(property)
                ?? throw new ArgumentException($"DeviceConfig has no such property '{property}'");

            // Value must be the correct type
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

        // Get property name via nameof(AppConfig.Property)
        public void UpdateAppConfig(string property, object value)
        {
            var prop = typeof(AppConfig).GetProperty(property)
                ?? throw new ArgumentException($"AppConfig has no such property '{property}'");

            // Make sure value is the correct type
            if (!prop.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Expected value of type {prop.PropertyType.Name}, got {value.GetType().Name}");

            var config = GetAppConfig();
            prop.SetValue(config, value);
            SetAppConfig(config);
        }
    }

    // JsonDeserializer doesn't provide a native way to deserialize version strings to Version objects
    public class VersionConverter : JsonConverter<Version>
    {
        public override Version? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Version.Parse(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}