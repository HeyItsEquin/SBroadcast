@echo off
for /f "delims=" %%i in ('powershell -command "([xml](Get-Content Directory.build.props)).Project.PropertyGroup.Version"') do set VERSION=%%i

set PUBLISH_DIR=Publish\SBroadcast-%VERSION%

dotnet publish MessageBroadcast.Core\MessageBroadcast.Core.csproj -c Release -p:Version=%VERSION%
dotnet publish MessageBroadcast.Sender\MessageBroadcast.Sender.csproj -c Release -p:Version=%VERSION%
dotnet publish MessageBroadcast.Updater\MessageBroadcast.Updater.csproj -c Release -p:Version=%VERSION%

msbuild Launcher\Launcher.vcxproj -p:Configuration=Release

copy Release\SBroadcast-%VERSION%\SBroadcast.exe %PUBLISH_DIR%\SBroadcast.exe