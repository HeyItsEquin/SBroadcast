using Makaretu.Dns;
using MessageBroadcast.Core;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MessageBroadcast.Core
{
    public class DeviceDiscovery : IDisposable
    {
        private const string ServiceType = "_msgbroadcast._udp.local.";
        private readonly DeviceInfo _localDevice;
        private MulticastService? _mdns;
        private ServiceDiscovery? _sd;
        private CancellationTokenSource? _cts;

        public event Action<DeviceInfo>? DeviceDiscovered;

        private string? _interfaceFilter;

        private readonly Dictionary<Guid, List<IPAddress>> _pendingAddresses = new();
        private readonly Dictionary<Guid, CancellationTokenSource> _pendingTimers = new();

        public DeviceDiscovery(DeviceInfo localDevice)
        {
            _localDevice = localDevice;
        }

        public void Start(string? interfaceFilter = null)
        {
            _interfaceFilter = interfaceFilter;

            _cts = new CancellationTokenSource();

            _mdns = new MulticastService(interfaces => GetUsableInterfaces());
            _sd = new ServiceDiscovery(_mdns);

            _mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var iface in e.NetworkInterfaces)
                    Logger.Log($"[MB] Interface discovered: {iface.Name}");

                if (interfaceFilter != null)
                {
                    var match = e.NetworkInterfaces.Any(i =>
                        i.Name.Contains(interfaceFilter, StringComparison.OrdinalIgnoreCase));

                    if (match)
                        _sd!.QueryServiceInstances(ServiceType);
                }
                else
                {
                    _sd!.QueryServiceInstances(ServiceType);
                }
            };

            _sd.ServiceInstanceDiscovered += OnServiceInstanceDiscovered;

            var profile = new ServiceProfile(
                _localDevice.Name,
                ServiceType,
                (ushort)_localDevice.Port);

            var validAddresses = GetUsableInterfaces()
                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address)
                .ToList();

            foreach (var address in validAddresses)
            {
                Logger.Log($"[MB] Advertising IP: {address}");
                profile.Resources.Add(new ARecord
                {
                    Name = profile.FullyQualifiedName,
                    Address = address
                });
            }

            foreach (var record in profile.Resources)
            {
                Logger.Log($"[MB] Profile resource: {record}");
            }

            profile.AddProperty("uuid", _localDevice.Id.ToString());
            profile.AddProperty("name", _localDevice.Name);
            profile.AddProperty("port", _localDevice.Port.ToString());

            _sd.Advertise(profile);
            _mdns.Start();
        }

        private IEnumerable<NetworkInterface> GetUsableInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(i =>
                {
                    if (i.OperationalStatus != OperationalStatus.Up) return false;
                    if (i.NetworkInterfaceType == NetworkInterfaceType.Loopback) return false;

                    var allowedTypes = new[]
                    {
                        NetworkInterfaceType.Ethernet,
                        NetworkInterfaceType.Wireless80211,
                        NetworkInterfaceType.GigabitEthernet,
                        NetworkInterfaceType.FastEthernetT,
                        NetworkInterfaceType.FastEthernetFx
                    };

                    if (allowedTypes.Contains(i.NetworkInterfaceType)) return true;
                    if (i.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase)) return true;
                    if (i.Description.Contains("Radmin", StringComparison.OrdinalIgnoreCase)) return true;
                    if (i.Description.Contains("Hamachi", StringComparison.OrdinalIgnoreCase)) return true;
                    if (i.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase)) return true;
                    if (i.Description.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase)) return true;

                    return false;
                });
        }

        private void OnServiceInstanceDiscovered(object? sender, ServiceInstanceDiscoveryEventArgs e)
        {
            try
            {
                var txtRecord = e.Message.AdditionalRecords.OfType<TXTRecord>().FirstOrDefault();
                var aRecords = e.Message.AdditionalRecords.OfType<ARecord>()
                    .Select(a => a.Address)
                    .ToList();

                //foreach (var a in aRecords)
                //    Logger.Log($"[MB] A record in response: {a}");

                if (txtRecord == null || !aRecords.Any()) return;

                var props = txtRecord.Strings
                    .Select(s => s.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => p[1]);

                if (!props.TryGetValue("uuid", out var uuidStr)) return;
                if (!Guid.TryParse(uuidStr, out var uuid)) return;
                if (uuid == _localDevice.Id) return;

                props.TryGetValue("name", out var deviceName);
                props.TryGetValue("port", out var portStr);
                int.TryParse(portStr, out var port);

                lock (_pendingAddresses)
                {
                    if (!_pendingAddresses.ContainsKey(uuid))
                    {
                        _pendingAddresses[uuid] = new List<IPAddress>();

                        var cts = new CancellationTokenSource();
                        _pendingTimers[uuid] = cts;

                        Task.Delay(2000, cts.Token).ContinueWith(t =>
                        {
                            if (t.IsCanceled) return;

                            List<IPAddress> addressSnapshot;
                            lock (_pendingAddresses)
                            {
                                if (!_pendingAddresses.TryGetValue(uuid, out var allAddresses)) return;
                                addressSnapshot = allAddresses.Distinct().ToList();
                                _pendingAddresses.Remove(uuid);
                                _pendingTimers.Remove(uuid);
                            }

                            _ = Task.Run(async () =>
                            {
                                var ip = await PickBestReachableAddress(addressSnapshot);
                                if (ip == null)
                                {
                                    Logger.Log($"[MB] No reachable address found for {deviceName}");
                                    return;
                                }

                                var device = new DeviceInfo
                                {
                                    Id = uuid,
                                    Name = deviceName ?? "Unknown",
                                    IpAddress = ip,
                                    Port = port,
                                    LastSeen = DateTime.UtcNow,
                                    AdvertisedIps = addressSnapshot.Select(a => a.ToString()).ToList()
                                };

                                Logger.Log($"[MB] Device found: {device.Name} at {device.IpAddress}:{device.Port}");
                                DeviceDiscovered?.Invoke(device);
                            });
                        }, TaskScheduler.Default);
                    }

                    _pendingAddresses[uuid].AddRange(aRecords);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Discovery error: {ex.Message}");
            }
        }

        private async Task<string?> PickBestReachableAddress(List<IPAddress> addresses)
        {
            var localIps = GetUsableInterfaces()
                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToHashSet();

            Logger.Log($"[MB] Local IPs to exclude: {string.Join(", ", localIps)}");

            var candidates = addresses
                .Distinct()
                .Where(a => !localIps.Contains(a.ToString()))
                .Select(a => (Address: a, Score: ScoreAddress(a, localIps.Select(IPAddress.Parse).ToList())))
                .OrderBy(x => x.Score)
                .ToList();

            Logger.Log($"[MB] Candidates after filtering own IPs: {string.Join(", ", candidates.Select(c => $"{c.Address}={c.Score}"))}");

            if (!candidates.Any())
            {
                Logger.Log("[MB] No candidates remaining after filtering");
                return null;
            }

            var tasks = candidates.Select(async candidate =>
            {
                try
                {
                    using var client = new TcpClient();
                    using var connectCts = new CancellationTokenSource(500);
                    await client.ConnectAsync(candidate.Address.ToString(), _localDevice.Port, connectCts.Token);
                    Logger.Log($"[MB] Reachability success: {candidate.Address}");
                    return candidate;
                }
                catch
                {
                    Logger.Log($"[MB] Reachability failed: {candidate.Address}");
                    return (Address: (IPAddress)null!, Score: int.MaxValue);
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);

            var best = results
                .Where(r => r.Address != null)
                .OrderBy(r => r.Score)
                .FirstOrDefault();

            if (best.Address != null)
            {
                Logger.Log($"[MB] Best reachable address: {best.Address}");
                return best.Address.ToString();
            }

            Logger.Log("[MB] No reachable address found");
            return null;
        }

        private int ScoreAddress(IPAddress candidate, List<IPAddress> localIps)
        {
            var cb = candidate.GetAddressBytes();

            foreach (var local in localIps)
            {
                var lb = local.GetAddressBytes();

                if (cb[0] == lb[0] && cb[1] == lb[1] && cb[2] == lb[2])
                    return 0;

                if (cb[0] == lb[0] && cb[1] == lb[1])
                    return 1;

                if (cb[0] == lb[0])
                    return 2;
            }

            return 3;
        }

        public async Task<List<DeviceInfo>> ScanOnceAsync(CancellationToken ct)
        {
            var discovered = new List<DeviceInfo>();

            void OnDiscovered(DeviceInfo device)
            {
                lock (discovered)
                    discovered.Add(device);
            }

            DeviceDiscovered += OnDiscovered;

            try
            {
                _sd?.QueryServiceInstances(ServiceType);
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                DeviceDiscovered -= OnDiscovered;
            }

            return discovered;
        }

        public void Broadcast()
        {
            // Artifact. Keep for compatibility
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            lock (_pendingAddresses)
            {
                foreach (var cts in _pendingTimers.Values)
                    cts.Cancel();
                _pendingTimers.Clear();
                _pendingAddresses.Clear();
            }

            _sd?.Dispose();
            _mdns?.Stop();
            _mdns?.Dispose();
        }
    }
}