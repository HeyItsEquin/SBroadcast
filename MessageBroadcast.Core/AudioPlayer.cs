using NAudio.Wave;
using System.IO;

namespace MessageBroadcast.Core
{
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent? _output;
        private AudioFileReader? _reader;
        private string? _tempPath;

        public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

        public async Task PlayAsync(byte[] soundData, string? format)
        {
            Stop();

            string? tempPath = null;
            try
            {
                // Write sound data to temp file
                tempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"mbsound_{Guid.NewGuid()}.{format ?? "mp3"}");

                await Task.Run(() => File.WriteAllBytes(tempPath, soundData));
                Logger.Log($"[MB] Sound written to {tempPath}");

                var reader = new AudioFileReader(tempPath);
                var output = new WaveOutEvent();
                output.Init(reader);

                output.PlaybackStopped += (_, _) =>
                {
                    if (_output == output)
                    {
                        Cleanup(output, reader, tempPath);
                        _output = null;
                        _reader = null;
                        _tempPath = null;
                    }
                };

                _output = output;
                _reader = reader;
                _tempPath = tempPath;

                output.Play();
                Logger.Log("[MB] Playback started");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] AudioPlayer.PlayAsync error: {ex.Message}");
                if (tempPath != null)
                    try { File.Delete(tempPath); } catch { }
            }
        }

        // Audio stops automatically, but for larger audios, let the user stop it manually
        public void Stop()
        {
            // Return if not currently playing
            if (_output == null) return;

            // Store these temporarily so we can still clean them up
            var output = _output;
            var reader = _reader;
            var tempPath = _tempPath;

            _output = null;
            _reader = null;
            _tempPath = null;

            Task.Run(() => Cleanup(output, reader, tempPath));
            Logger.Log("[MB] Audio stopped");
        }

        private void Cleanup(WaveOutEvent? output, AudioFileReader? reader, string? tempPath)
        {
            try
            {
                output?.Stop();
                output?.Dispose();
                reader?.Dispose();
                if (tempPath != null)
                    try { File.Delete(tempPath); } catch { }
                Logger.Log("[MB] Audio cleaned up");
            }
            catch (Exception ex)
            {
                Logger.Log($"[MB] Audio cleanup error: {ex.Message}");
            }
        }

        // Required for IDisposable, just stop audio
        public void Dispose()
        {
            Stop();
        }
    }
}