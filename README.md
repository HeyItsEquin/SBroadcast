# SBroadcast

A lightweight .NET application that lets you send messages directly to your friends' screens over a local network.

> ⚠️ This project is a work in progress from a beginner developer. Expect bugs.

## Features

- Send messages that pop up as an on-screen overlay on any connected machine
- Works over LAN and virtual LAN networks (ZeroTier, Hamachi, etc.)
- Minimal setup. No server required

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (to build)
- Windows

## Usage
   1. **Run: `MessageBroadcast.Sender.exe`** - This starts both the sender application and the overlay in the background. 
   2. Select a friend's machine from the list, write a message, and send.
   > Note: If your friend's machine broadcasted multiple IPs, you can set your 'preferred' IP address for that machine.
   > If your friend isn't receiving your message, or your message is being displayed on your own screen, this can probably help.

## Building

1. Clone the repository:
```bash
   git clone https://github.com/HeyItsEquin/SBroadcast.git
   cd SBroadcast
```

2. Build the solution:
```bash
   dotnet build MessageBroadcast.sln
```

## Connectivity

SBroadcast works on any network where machines can reach each other directly by IP, including:

- Local area networks (LAN)
- Virtual LAN tools like [ZeroTier](https://www.zerotier.com/) or [Hamachi](https://vpn.net/)

It does **not** support connections over the open internet without a VPN bridge.

## Notes

### For .NET developers
Not that I know for sure, but I imagine looking at this project might anger a few real .NET developers.
I'm probably not doing things very "C#" like, especially regarding .NET's build system. Please keep in mind that I am a beginner developer,
infact most of my programming experience is in pure C, some Python, and some JS. Basically everything I know about OOP comes from those languages,
and I know C# is very different from them.

If any .NET devs decide to take the time to look at this codebase, I'd really appreciate it.
If you find anything at all that could be improved, even if it's minor changes (things that may seem 'nitpicky'), I'd love to hear your feedback.
Create an issue or a PR, I'd really love to improve in C#/.NET.

As a final note, I am absolutely god-awful at frontend development, so a lot of my XAML is going to be either stolen or just plain bad.
I especially don't like WPF's `<Template />` pattern. Writing decent CSS is hard enough, so unpacking a control's template and seeing 100 inner elements and 100 getters/setters is pretty scary.
Once again though, any feedback is GREATLY appreciated.

## License

This project is licensed under the [MIT License](LICENSE.txt).
