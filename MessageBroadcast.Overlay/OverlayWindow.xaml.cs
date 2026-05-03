using MessageBroadcast.Core;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Message = MessageBroadcast.Core.Message;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace MessageBroadcast.Overlay
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private CancellationTokenSource? _hideCts;
        private readonly AudioPlayer _audioPlayer = new();

        public OverlayWindow()
        {
            InitializeComponent();

            // Turn icon into Bitmap because .NET doesn't like .ico files
            if (File.Exists(Paths.IconPath))
            {
                using var stream = new FileStream(Paths.IconPath, FileMode.Open, FileAccess.Read);
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                // Icon has multiple frames, use highest resolution
                Icon = decoder.Frames
                    .OrderByDescending(f => f.Width)
                    .First();
            }
        }

        public async void ShowMessage(Message message)
        {
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            if (message.SoundData != null)
                await _audioPlayer.PlayAsync(message.SoundData, message.SoundFormat);

            if (message.ContentType != MessageContentType.Sound)
            {
                MessageText.Text = message.Text;
                MessageText.FontSize = message.FontSize;
                MessageText.FontFamily = new FontFamily(message.FontFamily);
                MessageText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(message.FontColor));

                ApplyPosition(message.Position);

                MessageText.Visibility = Visibility.Visible;
                MessageText.Opacity = 1;
            }

            try
            {
                await Task.Delay(message.DisplaySeconds * 1000, token);

                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(1),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var tcs = new TaskCompletionSource();
                fadeOut.Completed += (_, _) => tcs.TrySetResult();

                MessageText.BeginAnimation(OpacityProperty, fadeOut);
                await tcs.Task;

                Close();
            }
            catch (OperationCanceledException)
            {
                MessageText.BeginAnimation(OpacityProperty, null);
                MessageText.Opacity = 1;
            }
        }

        public void StopAudio() => _audioPlayer.Stop();

        private void ApplyPosition(MessagePosition position)
        {
            (MessageText.HorizontalAlignment, MessageText.VerticalAlignment) = position switch
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
    }
}
