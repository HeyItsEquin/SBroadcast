using MessageBroadcast.Core;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace MessageBroadcast.Updater
{
    public partial class MainWindow : Window
    {
        private readonly string UpdateZipPath = Path.Combine(Path.GetTempPath(), "SBroadcast-update.zip");
        private readonly string TempExtractPath = Path.Combine(Path.GetTempPath(), "SBroadcast-update");

        public MainWindow()
        {
            InitializeComponent();
            Icon = IconLoader.LoadIcon() ?? Icon;
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // First arg is new ver. download link, second arg is pid of sender proc
            var args = Environment.GetCommandLineArgs();
            var downloadUrl = args[1];
            var senderPid = int.Parse(args[2]);
            var appDir = AppContext.BaseDirectory;

            try
            {
                await CloseProcesses(senderPid);
                await DownloadUpdateFiles(downloadUrl);
                var extractRoot = await ExtractUpdateFiles();
                CopyUpdateFiles(extractRoot, appDir);
            } 
            catch (Exception ex)
            {
                ProgressBar.IsIndeterminate = false;
                StatusLabel.Text = $"Update failed: {ex.Message}";
                Logger.Log($"[UPD] Failed to update app: {ex.GetType().Name} - {ex.Message}");

                await Task.Delay(3000);
                Application.Current.Shutdown();
            }
        }

        // Close sender and overlay processes
        private async Task CloseProcesses(int senderPid)
        {
            StatusLabel.Text = "Waiting for Sender to close...";
            try
            {
                var sender = Process.GetProcessById(senderPid);
                sender.Kill();
                await Task.Run(() => sender.WaitForExit());
            }
            catch (ArgumentException) { }

            UpdateProgressBar(1, durationSeconds: 0.1);
        }

        // Download release files from Github
        private async Task DownloadUpdateFiles(string downloadUrl)
        {
            StatusLabel.Text = "Downloading update files...";

            using var client = new HttpClient();
            
            // Github's API requires this header
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SBroadcast-updater", "1.0.0"));

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            var totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(UpdateZipPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192];
            long bytesRead = 0;
            int read;

            // Read file-by-file so that the progress bar can be smooth
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var progress = 1 + (bytesRead / (double)totalBytes);
                    UpdateProgressBar(progress, durationSeconds: 0.1);
                }
            }
        }

        // Extract the update zip downloaded in the previous zip
        private async Task<string> ExtractUpdateFiles()
        {
            StatusLabel.Text = "Extracting files...";
            UpdateProgressBar(3.8, durationSeconds: 2.0);

            // Extract zip file
            await Task.Run(() =>
            {
                if (Directory.Exists(TempExtractPath))
                    Directory.Delete(TempExtractPath, recursive: true);

                ZipFile.ExtractToDirectory(UpdateZipPath, TempExtractPath);
            });

            File.Delete(UpdateZipPath);

            // Go 1 entry deep into the extracted zip
            // Release archives follow this format: SBroadcast-vX.X.X\SBroadcast-vX.X.X
            var extractRoot = TempExtractPath;
            var entries = Directory.GetFileSystemEntries(TempExtractPath);
            if (entries.Length == 1 && Directory.Exists(entries[0]))
                extractRoot = entries[0];

            UpdateProgressBar(4);
            return extractRoot;
        }

        // Create a batch script in app directory, hand off file copying to it
        private void CopyUpdateFiles(string extractRoot, string appDir)
        {
            StatusLabel.Text = "Installing...";
            UpdateProgressBar(5);

            var scriptPath = Path.Combine(appDir, "SBroadcast-update.bat");
            var lines = new List<string>();

            lines.Add("@echo off");
            lines.Add("timeout /t 2 /nobreak > nul"); // Wait a bit to allow file locks to release

            foreach (var file in Directory.GetFiles(extractRoot))
            {
                var fileName = Path.GetFileName(file);
                lines.Add($"copy /y \"{file}\" \"{Path.Combine(appDir, fileName)}\"");
            }

            lines.Add($"rmdir /s /q \"{TempExtractPath}\"");
            lines.Add($"start \"\" \"{Path.Combine(appDir, "MessageBroadcast.Sender.exe")}\"");
            lines.Add($"del \"%~f0\"");

            // Write script to app install directory, anything else tends to trip MS Defender
            File.WriteAllLines(scriptPath, lines);

            // Start script
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Application.Current.Shutdown();
        }

        // 'Fake' progress bar
        private void UpdateProgressBar(double value, double durationSeconds = 0.3)
        {
            var animation = new DoubleAnimation
            {
                To = value,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressBar.BeginAnimation(RangeBase.ValueProperty, animation);
        }

        // UI can only be updated from the UI thread, update via Dispatcher.Invoke
        private void UpdateStatusLabelAsync(string txt)
        {
            Dispatcher.Invoke(() => { StatusLabel.Text = txt; });
        }

        private void UpdateProgressBarAsync(double value, double durationSeconds = 0.3)
        {
            Dispatcher.Invoke(() => UpdateProgressBar(value, durationSeconds));
        }
    }
}