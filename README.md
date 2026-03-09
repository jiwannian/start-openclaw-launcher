# Start OpenClaw Launcher

A Windows desktop launcher with a Chinese UI that turns the `OpenClaw` start/stop workflow, proxy setup, and access entry into a simpler one-click experience for everyday users.

This project does not aim to replace `OpenClaw` itself. Instead, it wraps the repetitive steps around it—checking the proxy, starting OpenClaw, opening the local access URL, and tracking runtime—into a more stable desktop flow.

## Highlights

- Chinese desktop UI designed for non-terminal users
- One-click start for `OpenClaw`
- One-click stop for `OpenClaw`
- Automatic detection of installed and running `Clash`
- Automatic enablement of the Windows system proxy after the proxy port is ready
- Automatic opening of the OpenClaw access URL after the gateway becomes available
- Runtime tracking with elapsed time shown in the UI
- Automatic redirect to the official OpenClaw install page when OpenClaw is not detected
- Single-file green distribution for Windows via `exe`

## Use Cases

- You do not want to manually run `openclaw gateway run` in a terminal every time
- You want `OpenClaw` and `Clash` to work together with fewer manual steps
- You want to hand OpenClaw to non-technical users
- You want a Windows launcher that can be distributed and used directly

## Current Capabilities

The current version implements the following workflow:

1. Detect whether `Clash` is installed and running
2. If installed but not running, attempt to launch `Clash`
3. Check whether the proxy port is ready, default `127.0.0.1:8090`
4. Automatically enable the Windows system proxy
5. Detect whether `OpenClaw` is installed
6. If OpenClaw is not installed, prompt the user and open the official install page
7. Start the `OpenClaw` gateway, default URL `http://127.0.0.1:18789/`
8. Open the browser automatically once the gateway is reachable
9. On stop, try `daemon stop` first, then fall back to port- and process-based cleanup
10. Track and display the runtime duration

## UI Overview

The current UI includes:

- `Clash Status`
- `Proxy Status`
- `OpenClaw Status`
- `Access URL`
- `Runtime Duration`
- `Start OpenClaw`
- `Stop OpenClaw`
- `Runtime Logs`

## Default Configuration

On first launch, the application creates a `config.json` file in the same directory as the `exe`.

Default values include:

- Proxy host: `127.0.0.1`
- Proxy port: `8090`
- OpenClaw gateway host: `127.0.0.1`
- OpenClaw gateway port: `18789`
- Startup timeout: `25` seconds
- Auto-enable system proxy: enabled
- Auto-open the access URL after startup: enabled

The following entries can be adjusted for different environments:

- `OpenClawCandidates`
- `ClashCandidates`
- `ClashProcessKeywords`

## Quick Start

### Option 1: Download the executable

Download the latest build from Releases:

- <https://github.com/jiwannian/start-openclaw-launcher/releases>

Then run `StartOpenClawLauncher.exe` directly.

### Option 2: Build locally

Requirements:

- Windows 10 / 11
- .NET 8 SDK

Build the project:

```powershell
dotnet build .\StartOpenClawLauncher\StartOpenClawLauncher.csproj
```

Run the project:

```powershell
dotnet run --project .\StartOpenClawLauncher\StartOpenClawLauncher.csproj
```

Publish a single-file executable:

```powershell
dotnet publish .\StartOpenClawLauncher\StartOpenClawLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Published output is generated at:

- `StartOpenClawLauncher\bin\Release\net8.0-windows\win-x64\publish\`

## Project Structure

```text
start openclaw/
├─ StartOpenClawLauncher/
│  ├─ Models/
│  ├─ Services/
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  └─ StartOpenClawLauncher.csproj
└─ .gitignore
```

## Tech Stack

- `C#`
- `WPF`
- `.NET 8`
- `Windows Registry` for system proxy control
- `GitHub CLI` for repository and release publishing

## Verified Behavior

This project has already been validated with a real local test flow, including:

- Launcher window opens correctly
- Start and stop actions work correctly
- System proxy can be automatically enabled to `127.0.0.1:8090`
- `OpenClaw` gateway can start and return `HTTP 200`
- The access URL can be opened automatically
- Runtime duration is updated live in the UI
- State files are cleared automatically after stop

## Known Boundaries

- This project currently targets Windows only and does not support macOS or Linux
- Automatic Clash startup depends on matching local install paths or process keywords
- If your machine uses a custom proxy tool or a non-standard install path, you may need to edit `config.json`

## Possible Next Steps

- Restore the user's previous system proxy settings after OpenClaw stops
- Add system tray support and minimize-to-tray behavior
- Add an editable startup-arguments UI
- Support multiple runtime configurations
- Add log export

## Disclaimer

This is a third-party desktop launcher and is not affiliated with the official `OpenClaw` or `Clash` teams. Make sure you use all related software and network settings legally and responsibly in your own environment.
