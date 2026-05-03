using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroadcast.Core
{
    public class DeviceInfo : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string? _preferredName;

        public string? PreferredName
        {
            get => _preferredName ?? Name;
            set
            {
                _preferredName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreferredName)));
            }
        }

        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }
        public List<string> AdvertisedIps { get; set; } = new();
        public bool Blocked { get; set; } = false;
        public bool IsFavorite { get; set; } = false;

        public string LastSeenDisplay => LastSeen.ToLocalTime().ToString("HH:mm:ss");

        public string StatusIcon => Blocked ? "\uF140" : IsFavorite ? "\uE735" : "";
        public string StatusToolTip => Blocked ? "Blocked" : IsFavorite ? "Favorite" : "";

        public int SortOrder
        {
            get
            {
                if (IsFavorite) return 0;
                if (Blocked) return 2;
                return 1;
            }
        }

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

        public string ToPayload()
        {
            return $"{Id}|{Name}|{Port}";
        }

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

