using MessageBroadcast.Core;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Data;
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
    public class MultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (double)value * double.Parse((string)parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class OverlayWindow : Window
    {
        private CancellationTokenSource? _hideCts;
        private readonly AudioPlayer _audioPlayer = new();

        public OverlayWindow()
        {
            InitializeComponent();
            // XAML doesn't like my icon, not sure why. Set it programatically
            Icon = IconLoader.LoadIcon() ?? Icon;
        }

        // Display a message on the screen and play audio if present
        public async void ShowMessage(Message message)
        {
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            // Reset all UI state
            MessageText.BeginAnimation(OpacityProperty, null);
            MessageText.Visibility = Visibility.Hidden;
            AnchoredMessageText.BeginAnimation(OpacityProperty, null);
            AnchoredMessageText.Visibility = Visibility.Hidden;
            ImageDisplay.BeginAnimation(OpacityProperty, null);
            ImageDisplay.Visibility = Visibility.Hidden;

            if (message.SoundData != null)
                await _audioPlayer.PlayAsync(message.SoundData, message.SoundFormat);

            // Messages that contain only sound don't create an overlay
            if (message.ContentType != MessageContentType.Sound)
            {
                if (message.AnchorTextToImage == true)
                {
                    AnchoredMessageText.Text = message.Text;
                    AnchoredMessageText.FontSize = message.FontSize;
                    AnchoredMessageText.FontFamily = new FontFamily(message.FontFamily);
                    AnchoredMessageText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(message.FontColor));

                    ApplyPosition(AnchoredMessageText, message.Position);

                    AnchoredMessageText.Visibility = Visibility.Visible;
                    AnchoredMessageText.Opacity = 1;
                }
                else
                {
                    MessageText.Text = message.Text;
                    MessageText.FontSize = message.FontSize;
                    MessageText.FontFamily = new FontFamily(message.FontFamily);
                    MessageText.Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(message.FontColor));

                    ApplyPosition(MessageText, message.Position);

                    MessageText.Visibility = Visibility.Visible;
                    MessageText.Opacity = 1;
                }

                if (message.ContentType.HasFlag(MessageContentType.Image))
                {
                    ApplyPosition(ImageContainer, message.ImagePosition);

                    ImageDisplay.Source = BytesToBitmapImage(message.ImageData!);
                    ImageDisplay.Visibility = Visibility.Visible;
                    ImageDisplay.Opacity = 1;
                }
            }

            try
            {
                // Message fades out smoothly after the allotted time
                await Task.Delay(TimeSpan.FromSeconds(message.DisplaySeconds), token);

                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(message.FadeoutTimeSeconds),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var tcs = new TaskCompletionSource();
                fadeOut.Completed += (_, _) => tcs.TrySetResult();

                if (message.AnchorTextToImage == true)
                    AnchoredMessageText.BeginAnimation(OpacityProperty, fadeOut);
                else
                    MessageText.BeginAnimation(OpacityProperty, fadeOut);

                if (message.ContentType.HasFlag(MessageContentType.Image))
                    ImageDisplay.BeginAnimation(OpacityProperty, fadeOut);

                await tcs.Task;

                Close();
            }
            catch (OperationCanceledException)
            {
                // A new message showed up while one was being shown
                // Cancel the animation and show the message
                MessageText.BeginAnimation(OpacityProperty, null);
                MessageText.Opacity = 1;
                AnchoredMessageText.BeginAnimation(OpacityProperty, null);
                AnchoredMessageText.Opacity = 1;
                ImageDisplay.BeginAnimation(OpacityProperty, null);
                ImageDisplay.Visibility = Visibility.Hidden;
                ImageDisplay.Opacity = 1;
            }
        }

        // Stop current audio from system tray
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
    }
}
