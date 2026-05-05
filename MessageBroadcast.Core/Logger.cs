using System.Diagnostics;
using System.IO;

namespace MessageBroadcast.Core
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.LogPath)!);
                var line = $"[{DateTime.Now:HH:mm:ss}] {message}";

                lock (_lock)
                    File.AppendAllText(Paths.LogPath, line + Environment.NewLine);

                Debug.WriteLine(line);
            }
            catch { }
        }

        public static void Exception(Exception ex, bool showInner = false)
        {
            Log($"Unhandled Exception: {ex.GetType().Name} - {ex.Message}");
            if (showInner)
            {
                var inner = ex.InnerException;
                if (inner != null)
                {
                    Log($"Inner: {inner.GetType().Name} - {inner.Message}");
                }
            }
        }

        public static void Exception(Exception ex, string message, bool showInner = false)
        {
            Log($"{message}: {ex.GetType().Name} - {ex.Message}");
            if (showInner)
            {
                var inner = ex.InnerException;
                if (inner != null)
                {
                    Log($"Inner: {inner.GetType().Name} - {inner.Message}");
                }

            }
        }

        public static void Clear()
        {
            try { File.Delete(Paths.LogPath); } catch { }
        }
    }
}