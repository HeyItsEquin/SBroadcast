using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;
using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace MessageBroadcast.Core
{
    public class AppConfig
    {
        // Message option defaults
        public int DefaultFontSize { get; set; } = 36;
        public string DefaultFontFamily { get; set; } = "Segoe UI";
        public string DefaultFontColor { get; set; } = "#FFFFFF";
        public double DefaultDisplaySeconds { get; set; } = 5.0;
        public double FadeoutTimeSeconds { get; set; } = 1.0;
        public MessagePosition DefaultPosition { get; set; } = MessagePosition.Center;
        public MessagePosition DefaultImagePosition { get; set; } = MessagePosition.Center;
        public bool AnchorTextToImage { get; set; } = false;


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
        private List<GroupInfo> _groups = new();

        // After this is called, ConfigStore will be ready to pull from
        public void Load()
        {
            LoadPreferences();
            LoadDeviceConfigs();
            LoadGroups();
        }

        // Write updated configs
        public void Save()
        {
            SavePreferences();
            SaveDeviceConfigs();
            SaveGroups();
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
                    SavePreferences(); // Create file
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

        // Load groups list from groups.json
        public void LoadGroups()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.GroupsPath)!);
                
                if (!File.Exists(Paths.GroupsPath))
                {
                    _groups = [];
                    SaveGroups();
                    Logger.Log($"[MB] No group configs found, using defaults");
                    return;
                }

                var json = File.ReadAllText(Paths.GroupsPath);
                var groupsDict = JsonSerializer.Deserialize<Dictionary<string, List<DeviceInfo>>>(json);
                _groups = GroupsFromDict(groupsDict);
                UpdateGroupMembers();
                Logger.Log("[MB] Group configs loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[MB] Failed to load group configs");
            }
        }

        public void SaveGroups()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.GroupsPath)!);

                var json = JsonSerializer.Serialize(GroupsDict(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(Paths.GroupsPath, json);
                Logger.Log("[MB] Group configs saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[MB] Failed to save group configs");
            }
        }

        // Updates group member's info with updated values from _deviceConfigs
        private void UpdateGroupMembers()
        {
            _groups.ForEach(g => g.GroupMembers.ForEach(m =>
            {
                if (_deviceConfigs.TryGetValue(m.Id, out var config))
                    m.PreferredName = config.Nickname;
            })); // Gotta love nested ForEach...
        }

        // Turn groups into a dictionary
        private Dictionary<string, List<DeviceInfo>> GroupsDict()
        {
            return _groups.ToDictionary(g => g.GroupName, g => g.GroupMembers);
        }

        // Turn dictionary of group name and group member's IDs into list of GroupInfo objects
        private List<GroupInfo> GroupsFromDict(Dictionary<string, List<DeviceInfo>>? groupsDict)
        {
            return [..groupsDict?.Select(kvp => new GroupInfo
            {
                GroupName = kvp.Key,
                GroupMembers = kvp.Value
            }) ?? []]; // I hate C# sometimes because what is thisss
        }

        // Load per-device settings from device_configs.json
        public void LoadDeviceConfigs()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.DeviceConfigsPath)!);

                if (!File.Exists(Paths.DeviceConfigsPath))
                {
                    _deviceConfigs = new Dictionary<Guid, DeviceConfig>();
                    Logger.Log("[MB] No device configs found, using defaults");
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
            UpdateGroupMembers();
            SaveDeviceConfigs();
        }

        // I don't think this is even used
        public void RemoveDeviceConfig(Guid deviceId)
        {
            _deviceConfigs.Remove(deviceId);
            UpdateGroupMembers();
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

        public List<GroupInfo> GetGroups()
        {
            return _groups;
        }

        public void SetGroups(List<GroupInfo> groups)
        {
            _groups = groups;
            SaveGroups();
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