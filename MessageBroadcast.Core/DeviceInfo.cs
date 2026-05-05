using System.ComponentModel;

namespace MessageBroadcast.Core
{
    public class DeviceInfo : INotifyPropertyChanged
    {
        public Guid Id { get; set; } // Device UUID
        public string Name { get; set; } = string.Empty; // Device name

        public event PropertyChangedEventHandler? PropertyChanged;

        private string? _preferredName; // Either nickname or device name

        public string? PreferredName
        {
            get => _preferredName ?? Name;
            set
            {
                _preferredName = value;
                // Notify whenever nickname changes
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreferredName)));
            }
        }

        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public DateTime LastSeen { get; set; } // Timestamp of when the device was last seen
        public List<string> AdvertisedIps { get; set; } = new(); // All of the IPs that the device has advertised
        public bool Blocked { get; set; } = false; // Whether this device has been blocked
        public bool IsFavorite { get; set; } = false; // Whether this device has been favorited

        // Display certain properties in a nice string format for XAML Bindings
        public string LastSeenDisplay => LastSeen.ToLocalTime().ToString("HH:mm:ss");
        public string StatusIcon => Blocked ? "\uF140" : IsFavorite ? "\uE735" : "";
        public string StatusToolTip => Blocked ? "Blocked" : IsFavorite ? "Favorite" : "";

        // The order in which devices appear in the device list
        public int SortOrder
        {
            get
            {
                if (IsFavorite) return 0; // Favorited devices go on the top
                if (Blocked) return 2;    // Blocked devices go on the bottom
                return 1;                 // Other devices go in the middle
            }
        }

        // Load configs for this device
        public void LoadConfigs()
        {
            var config = ConfigStore.Instance.GetDeviceConfig(Id);
            PreferredName = config.Nickname;
            Blocked = config.Blocked;
            IsFavorite = config.Favorite;
            if (config.PreferredIp != null && AdvertisedIps.Contains(config.PreferredIp))
            {
                IpAddress = config.PreferredIp;
            }
        }

        // I don't think this is used anymore?
        public string ToPayload()
        {
            return $"{Id}|{Name}|{Port}";
        }

        // Also unsure if this is used
        public static DeviceInfo? FromPayload(string payload, string senderIp)
        {
            var parts = payload.Split('|');
            if (parts.Length != 3) return null;

            return new DeviceInfo
            {
                Id = Guid.Parse(parts[0]),
                Name = parts[1],
                IpAddress = senderIp,
                Port = int.Parse(parts[2]),
                LastSeen = DateTime.UtcNow
            };
        }
    }
}

