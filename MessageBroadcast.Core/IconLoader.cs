using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MessageBroadcast.Core
{
    public class IconLoader
    {
        // XAML doesn't like my icon so load it programatically
        public static ImageSource? LoadIcon()
        {
            if (!File.Exists(Paths.IconPath)) return null;

            using var stream = new FileStream(Paths.IconPath, FileMode.Open, FileAccess.Read);
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            
            // If icon has multiple frames, use highest res
            var icon = decoder.Frames
                .OrderByDescending(f => f.Width)
                .First();

            return icon;
        }
    }
}
