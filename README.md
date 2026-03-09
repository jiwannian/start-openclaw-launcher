# Start OpenClaw Launcher

一个面向 Windows 的中文图形启动器，用来把 `OpenClaw` 的启动、停止、代理联动和访问入口整合成一套更适合普通用户的桌面操作流程。

它的目标不是替代 `OpenClaw` 本身，而是把“先确认代理、再启动 OpenClaw、再打开访问地址、最后统计运行时长”这套重复动作，压缩成更稳定的一键体验。

## 项目亮点

- 中文界面，面向桌面用户而不是命令行用户
- 一键启动 `OpenClaw`
- 一键关闭 `OpenClaw`
- 自动检测本机是否安装并运行 `Clash`
- 在检测到代理端口可用后，自动开启 Windows 系统代理
- 确认 `OpenClaw` 网关可访问后，自动打开访问地址
- 记录 `OpenClaw` 运行时长，并在关闭后展示本次运行时间
- 若未检测到 `OpenClaw`，自动跳转官方安装页面
- 绿色单文件发布，可直接分发 `exe`

## 适用场景

- 不想每次手动打开终端执行 `openclaw gateway run`
- 希望让 `OpenClaw` 与 `Clash` 配合工作更省步骤
- 想把 OpenClaw 交给非技术用户使用
- 想快速分发一个可直接运行的 Windows 启停工具

## 当前能力

本项目当前版本已经实现以下完整流程：

1. 检测 `Clash` 是否安装、是否运行
2. 若已安装但未运行，则尝试拉起 `Clash`
3. 检查代理端口是否就绪，默认 `127.0.0.1:8090`
4. 自动开启 Windows 系统代理
5. 检测 `OpenClaw` 是否安装
6. 若未安装，则提示并打开官方安装页
7. 启动 `OpenClaw` 网关，默认地址为 `http://127.0.0.1:18789/`
8. 在网关可访问后自动打开浏览器
9. 关闭时优先尝试 `daemon stop`，再按端口和进程兜底清理
10. 统计并展示本次运行时长

## 界面功能

当前版本界面包含：

- `Clash 状态`
- `代理状态`
- `OpenClaw 状态`
- `访问地址`
- `运行时长`
- `一键启动 OpenClaw`
- `一键关闭 OpenClaw`
- `运行日志`

## 默认配置

程序首次运行时，会在 `exe` 同目录生成 `config.json`。

默认配置包括：

- 代理主机：`127.0.0.1`
- 代理端口：`8090`
- OpenClaw 网关主机：`127.0.0.1`
- OpenClaw 网关端口：`18789`
- 启动超时：`25` 秒
- 自动开启系统代理：开启
- 启动成功后自动打开访问地址：开启

可根据本机环境修改：

- `OpenClawCandidates`
- `ClashCandidates`
- `ClashProcessKeywords`

## 快速开始

### 方式一：直接下载可执行文件

请前往 Releases 页面下载发布版：

- <https://github.com/jiwannian/start-openclaw-launcher/releases>

下载后直接运行 `StartOpenClawLauncher.exe` 即可。

### 方式二：本地编译运行

要求：

- Windows 10 / 11
- .NET 8 SDK

在项目根目录执行：

```powershell
dotnet build .\StartOpenClawLauncher\StartOpenClawLauncher.csproj
```

运行：

```powershell
dotnet run --project .\StartOpenClawLauncher\StartOpenClawLauncher.csproj
```

发布单文件 `exe`：

```powershell
dotnet publish .\StartOpenClawLauncher\StartOpenClawLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布产物默认位于：

- `StartOpenClawLauncher\bin\Release\net8.0-windows\win-x64\publish\`

## 项目结构

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

## 技术栈

- `C#`
- `WPF`
- `.NET 8`
- `Windows Registry`（系统代理控制）
- `GitHub CLI`（用于仓库发布与 Release）

## 已验证功能

项目已经完成过一轮真实联调验证，包括：

- 启动器窗口正常打开
- 启动按钮与关闭按钮联动正常
- 系统代理可自动开启到 `127.0.0.1:8090`
- `OpenClaw` 网关可启动并返回 `HTTP 200`
- 访问地址可自动打开
- 运行时长可实时显示
- 停止后状态文件可自动清理

## 已知边界

- 当前默认适配 Windows 环境，不支持 macOS / Linux
- “自动拉起 Clash”依赖本机 Clash 安装路径或进程关键字命中
- 若本机使用的是非常规代理软件或自定义安装位置，可能需要手动修改 `config.json`

## 后续可扩展方向

- 关闭 `OpenClaw` 时自动恢复用户原有系统代理
- 增加托盘运行与最小化到系统托盘
- 增加启动参数编辑界面
- 增加多套配置切换
- 增加日志导出功能

## 免责声明

本项目是一个第三方桌面启动器，不隶属于 `OpenClaw` 或 `Clash` 官方团队。请确保你在本地环境中合法、合规地使用相关软件与网络配置。
