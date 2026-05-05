@echo off
dotnet publish MessageBroadcast.Core\MessageBroadcast.Core.csproj -c Release
dotnet publish MessageBroadcast.Sender\MessageBroadcast.Sender.csproj -c Release
dotnet publish MessageBroadcast.Updater\MessageBroadcast.Updater.csproj -c Release