using System.Reflection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.IO;

namespace MessageBroadcast.Core
{
    public class VersionCheck
    {
        private static readonly string GithubLatestReleaseUrl =
            "https://api.github.com/repos/HeyItsEquin/SBroadcast/releases/latest";

        public record UpdateInfo(string TagName, string DownloadUrl,
                                 Version CurrentVersion, Version NewVersion);

        public static async Task<UpdateInfo?> CheckForUpdates()
        {
            var current = Assembly.GetEntryAssembly()!.GetName().Version!;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SBroadcast", current.ToString()));

            var response = await client.GetStringAsync(GithubLatestReleaseUrl);
            var json = JsonDocument.Parse(response);
            var root = json.RootElement;

            var tagName = root.GetProperty("tag_name").GetString()!;
            var latest = Version.Parse(tagName.TrimStart('v'));

            if (latest <= current) return null;

            // Find the release's download asset
            var assets = root.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var url = asset.GetProperty("browser_download_url").GetString()!;
                if (url.EndsWith(".zip"))
                    return new UpdateInfo(tagName, url, current, latest);
            }

            return null;
        }

        // Updater creates a batch file to copy files over, remove it
        public static void RemoveUpdateArtifacts()
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "SBroadcast-update.bat");
            if (File.Exists(updaterPath))
                File.Delete(updaterPath);
        }
    }
}
