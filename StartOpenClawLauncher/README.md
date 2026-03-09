# StartOpenClawLauncher

第一版中文绿色启动器，目标功能：

- 一键启动 OpenClaw
- 一键关闭 OpenClaw
- 如果检测到 Clash，则优先尝试启动 Clash 并等待代理端口就绪
- 如果未检测到 OpenClaw，则提示并打开官方安装页面

## 开发运行

```powershell
dotnet build
```

## 发布单文件 exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布产物默认在：`bin\Release\net8.0-windows\win-x64\publish\`
