# SBroadcast

A lightweight .NET application that lets you send messages directly to your friends' screens over a local network.

> ⚠️ This project is a work in progress. Expect bugs.

## Features

- Send messages that pop up as an on-screen overlay on any connected machine
- Works over LAN and virtual LAN networks (ZeroTier, Hamachi, etc.)
- Minimal setup. No server required

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (to run)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (to build)
- Windows

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

## Usage
   1. **Run: `MessageBroadcast.Sender.exe`** - This starts both the sender application and the overlay in the background. 
   2. Select a friend's machine from the list, write a message, and send.
   > Note: If your friend's machine broadcasted multiple IPs, you can set your 'preferred' IP address for that machine.
   > If your friend isn't receiving your message, or your message is being displayed on your own screen, this can probably help.

## Connectivity

SBroadcast works on any network where machines can reach each other directly by IP, including:

- Local area networks (LAN)
- Virtual LAN tools like [ZeroTier](https://www.zerotier.com/) or [Hamachi](https://vpn.net/)

It does **not** support connections over the open internet without a VPN bridge.

## License

This project is licensed under the [MIT License](LICENSE.txt).
