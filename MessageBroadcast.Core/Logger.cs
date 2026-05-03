using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static void Clear()
        {
            try { File.Delete(Paths.LogPath); } catch { }
        }
    }
}
