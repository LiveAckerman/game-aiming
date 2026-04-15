# CrosshairTool — FPS 游戏屏幕准心工具

适用于三角洲行动及其他 FPS 游戏的屏幕准心叠加工具。

## 功能特性

- 全屏透明覆盖层，永远置于所有窗口最顶层（含全屏游戏）
- 鼠标点击穿透，完全不干扰游戏操作
- 准心样式：十字 / 圆形 / 点 / 十字+点
- 实时配置面板，参数即改即现
- 系统托盘常驻，F8 热键切换显示/隐藏
- 支持多显示器（准心默认定位在主屏中心）
- 配置自动保存，支持预设管理

## 环境要求

- **操作系统**：Windows 10 / 11 (x64)
- **构建工具**：[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 构建与运行

```bash
# 克隆后进入项目目录
cd src/CrosshairTool

# 还原依赖并运行
dotnet run

# 发布为单文件 EXE（无需用户安装 .NET）
dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true -c Release
```

发布产物位于 `src/CrosshairTool/bin/Release/net8.0-windows/win-x64/publish/`。

## 使用说明

1. 运行程序后，准心默认显示在主屏幕中心
2. 系统托盘出现准心图标，双击打开设置面板
3. `F8` 键切换准心显示/隐藏
4. 设置面板中可实时调整所有参数，关闭面板时自动保存

## 游戏兼容性说明

> **独占全屏（Exclusive Fullscreen）模式**：DX/Vulkan 独占全屏会绕过 Windows 合成器，准心覆盖层将无法显示在游戏画面之上。
>
> **解决方案**：在游戏设置中将画面模式改为「**窗口化全屏**」或「**无边框窗口**」，三角洲行动默认支持此模式。

## 项目结构

```
src/CrosshairTool/
├── App.xaml(.cs)               # 应用入口，单例控制，热键注册
├── Core/
│   ├── NativeMethods.cs        # Win32 P/Invoke 声明
│   ├── AppContext.cs           # 全局状态单例
│   ├── HotkeyManager.cs        # 全局热键管理
│   └── ConfigManager.cs        # 配置读写、预设管理
├── Models/
│   └── CrosshairConfig.cs      # 准心配置数据模型
├── Windows/
│   ├── OverlayWindow.xaml(.cs) # 透明覆盖层（点击穿透 + 永远置顶）
│   └── SettingsWindow.xaml(.cs)# 配置面板（实时预览）
├── Rendering/
│   └── CrosshairRenderer.cs    # WPF DrawingContext 准心绘制
├── TrayIcon/
│   └── TrayIconManager.cs      # 系统托盘图标
└── Resources/
    └── icon.ico
```

## 配置文件位置

配置保存在 `%AppData%\CrosshairTool\config.json`，预设保存在 `%AppData%\CrosshairTool\presets\`。
