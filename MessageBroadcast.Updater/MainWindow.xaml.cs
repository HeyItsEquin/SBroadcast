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

        public MainWindow()
        {
            InitializeComponent();
            Icon = IconLoader.LoadIcon() ?? Icon;
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            var args = Environment.GetCommandLineArgs();
            var downloadUrl = args[1];
            var senderPid = int.Parse(args[2]);
            var appDir = AppContext.BaseDirectory;

            try
            {
                await CloseProcesses(senderPid);
                await DownloadUpdateFiles(downloadUrl);
                await InstallUpdateFiles(appDir);

                Process.Start(Path.Combine(appDir, "MessageBroadcast.Sender.exe"));
                Application.Current.Shutdown();
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

            StatusLabel.Text = "Waiting for Overlay to close...";
            foreach (var overlay in Process.GetProcessesByName("MessageBroadcast.Overlay"))
            {
                overlay.Kill();
                await Task.Run(() => overlay.WaitForExit());
            }

            UpdateProgressBar(1, durationSeconds: 0.1);
        }

        // Read file-by-file so that the progress bar can be smooth
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

        private async Task InstallUpdateFiles(string outputDir)
        {
            StatusLabel.Text = "Extracting files...";
            var tempExtract = Path.Combine(Path.GetTempPath(), "SBroadcast-update");

            UpdateProgressBar(3.8, durationSeconds: 2.0);

            await Task.Run(() =>
            {
                if (Directory.Exists(tempExtract))
                    Directory.Delete(tempExtract, recursive: true);

                ZipFile.ExtractToDirectory(UpdateZipPath, tempExtract);

                UpdateStatusLabelAsync("Installing...");
                UpdateProgressBarAsync(3.9, durationSeconds: 0.1);

                foreach (var file in Directory.GetFiles(tempExtract))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName == "MessageBroadcast.Updater.exe") continue;

                    File.Copy(file, Path.Combine(outputDir, fileName), overwrite: true);
                }

                UpdateStatusLabelAsync("Cleaning up...");
                UpdateProgressBarAsync(4.9, durationSeconds: 0.5);

                Directory.Delete(tempExtract, recursive: true);
            });

            File.Delete(UpdateZipPath);
            UpdateProgressBar(5);
        }

        private void UpdateStatusLabelAsync(string txt)
        {
            Dispatcher.Invoke(() => { StatusLabel.Text = txt; });
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

        private void UpdateProgressBarAsync(double value, double durationSeconds = 0.3)
        {
            Dispatcher.Invoke(() => UpdateProgressBar(value, durationSeconds));
        }
    }
}