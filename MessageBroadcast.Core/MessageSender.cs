using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MessageBroadcast.Core
{
    public class MessageSender
    {
        private const int TimeoutMs = 5000;

        public async Task<bool> SendMessageAsync(DeviceInfo target, Message message)
        {
            try
            {
                using var client = new TcpClient();

                var connectTask = client.ConnectAsync(target.IpAddress, target.Port);
                if (await Task.WhenAny(connectTask, Task.Delay(TimeoutMs)) != connectTask)
                {
                    Logger.Log($"Connection to {target.Name} timed out");
                    return false;
                }

                using var stream = client.GetStream();

                var json = JsonSerializer.Serialize(message);
                var data = Encoding.UTF8.GetBytes(json);

                var lengthPrefix = BitConverter.GetBytes(data.Length);
                await stream.WriteAsync(lengthPrefix);
                await stream.WriteAsync(data);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Send error: {ex.Message}");
                return false;
            }
        }
    }
    }
