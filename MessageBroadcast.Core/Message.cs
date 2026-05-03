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
        TextWithSound
    }

    public class Message
    {
        public Guid SenderId { get; set; }
        public string? DeviceName { get; set; }
        public MessageContentType ContentType { get; set; } = MessageContentType.Text;
        public string Text { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; }
        public byte[]? SoundData { get; set; }
        public string? SoundFormat { get; set; }
        public int FontSize { get; set; } = 36;
        public string FontFamily { get; set; } = "Segoe UI";
        public string FontColor { get; set; } = "#FFFFFF";
        public int DisplaySeconds { get; set; } = 5;
        public MessagePosition Position { get; set; } = MessagePosition.Center;
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
