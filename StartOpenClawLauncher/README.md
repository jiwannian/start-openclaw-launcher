# StartOpenClawLauncher

A lightweight Windows launcher for OpenClaw with a Chinese UI.

## Features

- One-click start for OpenClaw
- One-click stop for OpenClaw
- Detects Clash and waits for the proxy port to become ready
- Opens the official OpenClaw install page if OpenClaw is not found
- Auto-enables the Windows system proxy when available
- Auto-opens the OpenClaw access URL after startup
- Tracks runtime duration

## Development

```powershell
dotnet build
```

## Publish a single-file executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The published output is generated in:

`bin\Release\net8.0-windows\win-x64\publish\`
