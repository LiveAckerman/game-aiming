# FPS 工具箱 (FPS Toolbox)

> 一站式 FPS 游戏辅助工具集 —— 主框架 + 可插拔子工具,独立升级、独立发布。

[![GitHub release](https://img.shields.io/github/v/release/LiveAckerman/game-aiming?filter=toolbox-*&label=toolbox)](https://github.com/LiveAckerman/game-aiming/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)](#)

**GitHub 开源地址**: https://github.com/LiveAckerman/game-aiming

---

## 功能总览

FPS 工具箱由一个**主框架**和两个**可选子工具**组成。主框架作为启动中心 + 自动更新管理器,子工具可以按需安装/卸载。

### 🎯 屏幕准心工具 (CrosshairTool)

| 能力 | 说明 |
| --- | --- |
| 全屏透明准心 | 始终置顶,覆盖游戏画面中心 |
| 点击穿透 | 完全不影响鼠标/键盘操作 |
| 多种样式 | 十字 / 圆形 / 点 / 十字+点,颜色/轮廓/尺寸随意调 |
| 热键切换 | `F8` 一键显示/隐藏 |
| 多显示器 | 自动识别主屏 |
| 预设管理 | 多方案保存,一键切换 |

### 🌈 屏幕调节工具 (GammaTool)

完整复刻 **Gamma Panel** 的功能,覆盖 Windows 层面无法做到的精细显示调整。

| 能力 | 说明 |
| --- | --- |
| Gamma/亮度/对比度 | 三路滑块独立调节 |
| RGB 独立通道 | 红绿蓝三色各自增益,消除偏色 |
| LUT 曲线预览 | 实时可视化当前 Look-Up Table |
| 多显示器 | 每个显示器独立方案 |
| 方案预设 | 日/夜/游戏/电影等场景一键切换 |
| 全局热键 | 方案之间盲切 |
| 预览按钮 | 调整前先看效果,不满意一键还原 |

### 🛡 FPS 工具箱主框架 (FPSToolbox)

| 能力 | 说明 |
| --- | --- |
| 统一启动中心 | 所有子工具从主界面卡片启动/停止 |
| 唯一托盘图标 | 子工具不独立占用托盘,菜单分组管理 |
| 自动更新 | 启动时从 GitHub Releases 检查新版,支持一键静默升级 |
| 在线/离线安装 | 装机时自动从微软 CDN 拉 .NET 8,也提供内嵌 runtime 的离线包 |
| 组件可选安装 | 安装时勾选需要的子工具;卸载时同样勾选 |
| 数据保护 | 卸载时询问是否保留用户配置,重装后无缝继续 |
| 开机自启 + 最小化到托盘 | 两个选项独立 |

---

## 架构设计

```
┌─────────────────────────────────────┐
│   FPSToolbox.exe  (主框架 / 托盘)    │
│   ┌─────────────────────────────┐   │
│   │ UpdateChecker (GitHub API)  │   │
│   │ ToolManager (进程生命周期)   │   │
│   │ NotifyIcon (唯一托盘)       │   │
│   └──────────┬──────────┬───────┘   │
│              │ IPC      │ IPC       │
│     (Named Pipe JSON)   │           │
└──────────────┼──────────┼───────────┘
               ▼          ▼
   ┌────────────────┐ ┌────────────────┐
   │ CrosshairTool  │ │  GammaTool     │
   │ overlay 覆盖层 │ │  SetDeviceGammaRamp │
   └────────────────┘ └────────────────┘
        (独立进程,父进程死则自退出)
```

### 关键技术点

- **多进程 + 命名管道 IPC**: 子工具崩溃不影响主框架,主框架退出子工具自动清理
- **GitHub Releases API**: 三个组件独立 tag(`toolbox-v*`、`crosshair-v*`、`gamma-v*`),独立升级
- **Inno Setup 静默升级**: 自动更新走 `/SILENT` 模式,升完自动启动新版,用户零交互
- **自动感知已装子工具**: 重装主框架时自动扫 `tools\` 目录勾选组件,卸载时勾选保留

---

## 下载安装

### 方式一:从 GitHub Releases 下载

https://github.com/LiveAckerman/game-aiming/releases

| 文件 | 用途 | 大小 |
| --- | --- | --- |
| `FPSToolbox_Setup_v<x>.exe` | 在线版,安装时自动从微软 CDN 拉 .NET 8 | ~3 MB |
| `FPSToolbox_Setup_v<x>_offline.exe` | 离线版,内嵌 .NET 8 Desktop Runtime | ~60 MB |

**新用户选在线版**,干净小巧;内网环境或网络差选离线版。

### 方式二:安装后从主界面补装子工具

主框架安装时可以不勾选子工具,运行后在主界面每个卡片上点**"下载并安装"**按钮,从 GitHub Releases 拉对应 zip 自动解压安装。

---

## 使用说明

1. 启动 FPS 工具箱 → 看到三个卡片
2. 点每个卡片的**"启动"**按钮运行对应子工具
3. 准心工具 `F8` 切换显示;Gamma 工具从主界面点"打开面板"调整
4. 工具使用期间主框架可最小化到托盘,不占任务栏
5. 右键托盘图标 → 分组菜单直接控制所有工具

## 数据位置

所有用户数据统一存放在 `%AppData%\FPSToolbox\` :

```
%AppData%\FPSToolbox\
├── toolbox-settings.json        # 主框架设置(开机自启等)
├── installed-tools.json         # 已安装子工具清单
├── CrosshairTool\
│   ├── config.json
│   └── presets\
└── GammaTool\
    ├── config.json
    └── presets\
```

卸载时会弹窗询问是否清除,**默认保留**,重装可无缝继续。

---

## 开发 & 构建

### 环境要求

- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php)(发版时需要)
- [GitHub CLI `gh`](https://cli.github.com/)(发版时需要)

### 本地运行

```powershell
# 克隆 & 还原依赖
git clone https://github.com/LiveAckerman/game-aiming.git
cd game-aiming
dotnet build FPSToolbox.sln -c Release

# 运行主框架(子工具会被主框架按需拉起,不要单独运行)
dotnet run --project src/FPSToolbox
```

### 发版

完整流程见 [`docs/release-process.md`](docs/release-process.md)。简要命令:

```powershell
# 只发主框架
.\scripts\release.ps1 -Target toolbox -Version 1.2.0 -Notes "新功能..."

# 只发子工具
.\scripts\release.ps1 -Target gamma -Version 1.1.0
.\scripts\release.ps1 -Target crosshair -Version 1.1.0

# 三合一全发
.\scripts\release.ps1 -Target all -Version 1.0.0
```

---

## 项目结构

```
game-aiming/
├── FPSToolbox.sln                    # 解决方案
├── shared/FPSToolbox.Shared/         # 所有项目共享的基础设施
│   ├── Ipc/                          # 命名管道 IPC(JSON over Pipe)
│   ├── Native/                       # Win32 P/Invoke
│   ├── Hotkeys/                      # 全局热键
│   ├── Config/                       # JSON 配置持久化
│   ├── PathService.cs                # 统一路径(%AppData%\FPSToolbox)
│   └── ParentWatcher.cs              # 子进程监视父进程生命周期
├── src/
│   ├── FPSToolbox/                   # 主框架 + 托盘 + 更新器
│   │   ├── Core/
│   │   │   ├── UpdateChecker.cs      # GitHub Releases API 客户端
│   │   │   ├── ToolManager.cs        # 子工具进程管理
│   │   │   ├── ToolDownloader.cs     # zip/exe 下载
│   │   │   ├── ToolPackageInstaller.cs
│   │   │   └── ToolRegistry.cs       # 已装工具清单
│   │   └── Tray/TrayIconManager.cs   # 唯一托盘
│   ├── CrosshairTool/                # 屏幕准心工具
│   └── GammaTool/                    # 屏幕调节工具
├── installer/
│   ├── FPSToolbox.iss                # Inno Setup 脚本(在线/离线双模式)
│   └── Languages/                    # 中文简体语言包
├── scripts/
│   ├── publish.ps1                   # dotnet publish 三个项目
│   ├── pack-tool.ps1                 # 打子工具分发 zip
│   └── release.ps1                   # 一键发 GitHub Release
└── docs/
    ├── fps-toolbox-spec.md
    ├── crosshair-tool-spec.md
    ├── gamma-tool-spec.md
    └── release-process.md
```

---

## 游戏兼容性

> **独占全屏(Exclusive Fullscreen)**:DX/Vulkan 独占全屏会绕过 Windows 合成器,准心覆盖层无法显示在游戏画面上。
>
> **解决**:在游戏里把画面模式改成 **"窗口化全屏"** 或 **"无边框窗口"**(主流 FPS 包括三角洲行动、CS2、Valorant、Apex、PUBG 都支持,性能几乎一致)。
>
> **Gamma 调节**:所有游戏都生效,无需额外设置,因为它直接改了显卡输出 LUT。

---

## 反馈 & 贡献

- 🐛 **报 Bug / 提需求**: https://github.com/LiveAckerman/game-aiming/issues
- ⭐ **给个 Star**: https://github.com/LiveAckerman/game-aiming
- 🔀 **PR**: 欢迎!请先开 issue 讨论大方向

## License

MIT License © 2026 LiveAckerman
