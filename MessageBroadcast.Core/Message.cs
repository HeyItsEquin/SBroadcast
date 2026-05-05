using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroadcast.Core
{
    public enum MessageContentType
    {
        Text,
        Image,
        Sound,
        TextWithSound,
        TextWithImage,
        ImageWithSound,
        TextWithImageWithSound
    }

    public class Message
    {
        public Guid SenderId { get; set; }
        public string? DeviceName { get; set; }
        public MessageContentType ContentType { get; set; } = MessageContentType.Text;
        public string Text { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; } // Raw image data for when the message has an image
        public byte[]? SoundData { get; set; } // Raw sound data for when the message has audio 
        public string? SoundFormat { get; set; } // Audio format (WAV, MP3, etc.)
        public int FontSize { get; set; } = 36;
        public string FontFamily { get; set; } = "Segoe UI";
        public string FontColor { get; set; } = "#FFFFFF";
        public int DisplaySeconds { get; set; } = 5; // How long the message should display for
        public MessagePosition Position { get; set; } = MessagePosition.Center; // Where should the message display on the screen
        public static int MaxLength { get; } = 32_000_000; // 32MB for messsage and any payload (image/audio)
        public static int MaxLengthDisplay => MaxLength / 1_000_000; // Bytes => MB
    }

    public enum MessagePosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
