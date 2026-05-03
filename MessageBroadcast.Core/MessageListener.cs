using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace MessageBroadcast.Core
{
    public class MessageListener : IDisposable
    {
        private readonly int _port;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public event Action<Message>? MessageReceived;

        public MessageListener(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync(ct);
                    Logger.Log($"Incoming connection from {client.Client.RemoteEndPoint}");
                    Task.Run(() => HandleClientAsync(client, ct));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Logger.Log($"Accept error: {ex.Message}"); }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var lengthBuffer = new byte[4];
                    await ReadExactAsync(stream, lengthBuffer, ct);
                    var messageLength = BitConverter.ToInt32(lengthBuffer);

                    if (messageLength <= 0 || messageLength > 8_000_000 * 1.4) // 1.4x for B64 overhead
                        return;

                    var messageBuffer = new byte[messageLength];
                    await ReadExactAsync(stream, messageBuffer, ct);

                    var json = Encoding.UTF8.GetString(messageBuffer);
                    var message = JsonSerializer.Deserialize<Message>(json);

                    if (message != null)
                        MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Handle client error: {ex.Message}");
            }
        }

        private async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var bytesRead = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);

                if (bytesRead == 0)
                    throw new EndOfStreamException("Connection closed before message was complete");

                totalRead += bytesRead;
            }
        }

        public void Dispose()
        {
            Stop();
            _listener?.Server.Dispose();
            _cts?.Dispose();
        }
    }
}
