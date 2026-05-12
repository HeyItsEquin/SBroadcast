using FileTypeChecker;
using MessageBroadcast.Core;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Message = MessageBroadcast.Core.Message;
using VerticalAlignment = System.Windows.VerticalAlignment;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using NAudio.CoreAudioApi;
using System.Windows.Controls;

namespace MessageBroadcast.Overlay
{
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (double)value * double.Parse((string)parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class OverlayWindow : Window
    {
        private Message? _currentMessage;

        private CancellationTokenSource? _hideCts;
        private readonly AudioPlayer _audioPlayer = new();
        private string? _tempMediaPath;

        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private Media? _currentMedia;

        public OverlayWindow()
        {
            InitializeComponent();
            Icon = IconLoader.LoadIcon() ?? Icon;

            // Initialize LibVLCSharp
            try
            {
                LibVLCSharp.Shared.Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                VideoDisplay.MediaPlayer = _mediaPlayer;

                // Add event handlers for debugging
                //_mediaPlayer.Playing += (s, e) => Logger.Log("[OVR] Video playing");
                //_mediaPlayer.EncounteredError += (s, e) => Logger.Log("[OVR] Video error!");
                //_mediaPlayer.EndReached += (s, e) => Logger.Log("[OVR] Video ended");
            }
            catch (Exception ex)
            {
                Logger.Log($"[OVR] LibVLC init error: {ex}");
            }
        }

        // This method is a fucking messss :( <- Not anymore! :3
        public async void ShowMessage(Message message)
        {
            _currentMessage = message;

            // Text doesn't render correctly with video, simply disable that functionality
            // quick fix for now, make better later
            if (message.ShouldRenderVideo())
                message.Text = "";

            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            ResetUIState();

            // Stop and clean up previous video
            DestroyMedia();

            if (message.SoundData != null)
                await _audioPlayer.PlayAsync(message.SoundData, message.SoundFormat);

            if (message.UseAnchoredText())
                ShowText(message, AnchoredMessageText);
            else
                ShowText(message, MessageText);

            if (message.ShouldRenderVideo())
                await HandleVideo(message);
            else if (message.ShouldRenderImage())
                HandleImage(message);

            try
            {
                // Message fades out smoothly after the allotted time
                var displayMs = GetDisplayTime(message);
                await Task.Delay(TimeSpan.FromMilliseconds(displayMs), token);

                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(message.FadeoutTimeSeconds),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var tcs = new TaskCompletionSource();
                fadeOut.Completed += (_, _) => tcs.TrySetResult();

                if (message.UseAnchoredText())
                    AnchoredMessageText.BeginAnimation(OpacityProperty, fadeOut);
                else
                    MessageText.BeginAnimation(OpacityProperty, fadeOut);

                if (message.ShouldRenderImage())
                    ImageDisplay.BeginAnimation(OpacityProperty, fadeOut);

                if (message.ShouldRenderVideo())
                    VideoDisplay.BeginAnimation(OpacityProperty, fadeOut);

                await tcs.Task;

                DestroyMedia();
                Close();
            }
            catch (OperationCanceledException)
            {
                // A new message showed up while one was being shown
                ResetUIState();
            }
        }

        private void ResetUIState()
        {
            MessageText.BeginAnimation(OpacityProperty, null);
            MessageText.Opacity = 1;
            MessageText.Visibility = Visibility.Hidden;
            AnchoredMessageText.BeginAnimation(OpacityProperty, null);
            AnchoredMessageText.Opacity = 1;
            AnchoredMessageText.Visibility = Visibility.Hidden;
            ImageDisplay.BeginAnimation(OpacityProperty, null);
            ImageDisplay.Opacity = 1;
            ImageDisplay.Visibility = Visibility.Hidden;
            VideoDisplay.BeginAnimation(OpacityProperty, null);
            VideoDisplay.Opacity = 1;
            VideoDisplay.Visibility = Visibility.Hidden;
            _mediaPlayer?.Stop();
        }

        private double GetDisplayTime(Message message)
        {
            if (message.UseVideoLengthAsDisplayTime
                && message.ShouldRenderVideo()
                && _currentMedia?.Duration != null)
            {
                return _currentMedia.Duration;
            }
            return message.DisplaySeconds * 1000;
        }

        private void ShowText(Message message, TextBlock tb)
        {
            tb.Text = message.Text;
            tb.FontSize = message.FontSize;
            tb.FontFamily = new FontFamily(message.FontFamily);
            tb.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(message.FontColor));

            ApplyPosition(tb, message.Position);

            tb.Visibility = Visibility.Visible;
            tb.Opacity = 1;
        }

        private void HandleImage(Message message)
        {
            ApplyPosition(ImageContainer, message.ImagePosition);

            ImageDisplay.Source = BytesToBitmapImage(message.ImageData!);
            ImageDisplay.Visibility = Visibility.Visible;
            ImageDisplay.Opacity = 1;
        }

        private async Task HandleVideo(Message message)
        {
            try
            {
                ApplyPosition(ImageContainer, message.ImagePosition);
                await WriteTempVideo(message.VideoFormat!, message.VideoData!);

                Logger.Log($"[OVR] Playing video: {_tempMediaPath}");

                await CreateMedia();

                VideoDisplay.Visibility = Visibility.Visible;
                VideoDisplay.Opacity = 1;

                PlayVideo(message.MuteVideo);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[OVR] Video playback error");
            }
        }

        private async Task WriteTempVideo(string fmt, byte[] data)
        {
            _tempMediaPath = Path.Combine(Path.GetTempPath(),
                $"overlay_video_{Guid.NewGuid()}{fmt}");
            await File.WriteAllBytesAsync(_tempMediaPath, data);
        }

        private async Task CreateMedia()
        {
            _currentMedia = new Media(_libVLC!, _tempMediaPath!, FromType.FromPath);
            await _currentMedia.Parse(MediaParseOptions.ParseNetwork);
        }

        private void PlayVideo(bool mute)
        {
            if (mute == true)
                _currentMedia!.AddOption(":no-audio");
            _mediaPlayer!.EndReached += OnVideoComplete;
            _mediaPlayer!.Play(_currentMedia!);
        }
        
        private void DestroyMedia()
        {
            _mediaPlayer?.Stop();
            _currentMedia?.Dispose();
            _currentMedia = null;
            CleanupTempMedia();
        }

        private void OnVideoComplete(object? sender, EventArgs e)
        {
            if (_currentMessage == null) return;

            Dispatcher.BeginInvoke(() =>
            {
                if (_currentMessage.HideVideoWhenDone)
                {
                    VideoDisplay.BeginAnimation(OpacityProperty, null);
                    VideoDisplay.Visibility = Visibility.Hidden;
                    VideoDisplay.Opacity = 1;
                }
                _mediaPlayer!.EndReached -= OnVideoComplete;
            });
        }

        private async void CleanupTempMedia()
        {
            if (_tempMediaPath != null && File.Exists(_tempMediaPath))
            {
                try
                {
                    // Small delay to ensure LibVLC releases the file
                    await Task.Delay(100);
                    File.Delete(_tempMediaPath);
                }
                catch { }
                _tempMediaPath = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _mediaPlayer?.Stop();
            _currentMedia?.Dispose();
            _currentMedia = null;
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            CleanupTempMedia();
            base.OnClosed(e);
        }

        public void StopAudio() => _audioPlayer.Stop();

        private BitmapImage BytesToBitmapImage(byte[] imageData)
        {
            using var ms = new MemoryStream(imageData);
            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private void ApplyPosition(FrameworkElement element, MessagePosition position)
        {
            (element.HorizontalAlignment, element.VerticalAlignment) = position switch
            {
                MessagePosition.TopLeft => (HorizontalAlignment.Left, VerticalAlignment.Top),
                MessagePosition.TopCenter => (HorizontalAlignment.Center, VerticalAlignment.Top),
                MessagePosition.TopRight => (HorizontalAlignment.Right, VerticalAlignment.Top),
                MessagePosition.MiddleLeft => (HorizontalAlignment.Left, VerticalAlignment.Center),
                MessagePosition.Center => (HorizontalAlignment.Center, VerticalAlignment.Center),
                MessagePosition.MiddleRight => (HorizontalAlignment.Right, VerticalAlignment.Center),
                MessagePosition.BottomLeft => (HorizontalAlignment.Left, VerticalAlignment.Bottom),
                MessagePosition.BottomCenter => (HorizontalAlignment.Center, VerticalAlignment.Bottom),
                MessagePosition.BottomRight => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
                _ => (HorizontalAlignment.Center, VerticalAlignment.Center)
            };
        }

        private void VideoDisplay_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Log($"[OVR] VideoDisplay loaded - Size: {VideoDisplay.ActualWidth}x{VideoDisplay.ActualHeight}");
            Logger.Log($"[OVR] MediaPlayer assigned: {VideoDisplay.MediaPlayer != null}");
        }
    }
}