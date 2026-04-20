# FPS 工具箱 —— 总架构设计文档

> 目标平台：Windows 10 / 11 (x64)  
> 技术栈：**C# + WPF (.NET 8)**  
> 发布形态：Inno Setup 打包 + 自包含发布（用户无需预装 .NET）

---

## 一、产品定义

**FPS 工具箱（FPSToolbox）** 是一个面向 FPS 游戏玩家的"工具中心"主程序。它本身不提供功能，而是承担：

1. **启动器**：作为统一入口，以卡片式界面列出所有子工具，一键启动 / 关闭
2. **管理器**：管理子工具的启用 / 禁用 / 安装 / 更新
3. **托盘中枢**：运行期间是系统托盘上**唯一**的图标，通过 IPC 控制所有子工具
4. **数据中心**：统一存放所有工具的配置和用户预设

当前包含的子工具：

| 子工具 | exe 名称 | 描述 |
|--------|----------|------|
| 屏幕准心工具 | `CrosshairTool.exe` | 屏幕最顶层绘制自定义准心，点击穿透 |
| 屏幕调节工具 | `GammaTool.exe` | 全屏 Gamma / 亮度 / 对比度调节（复刻 Gamma Panel） |

### 核心约束

- **子工具不可单独安装**：`CrosshairTool.exe` / `GammaTool.exe` 必须通过 `FPSToolbox` 安装器或 Toolbox 主界面才能安装
- **子工具可独立运行进程**：虽然不能单独安装，但安装完成后每个工具运行时是一个独立进程（便于崩溃隔离、权限隔离）
- **子工具可同时开启**：准心 Overlay 和 Gamma 调节可以同时工作

---

## 二、架构总览

### 2.1 多进程模型

```
┌────────────────────────────────────────────────────────┐
│                  FPSToolbox.exe (主进程)                │
│  ┌──────────────────────────────────────────────────┐  │
│  │  MainWindow  —— 工具中心卡片界面                 │  │
│  │  TrayIcon    —— 唯一的托盘图标 + 统一菜单         │  │
│  │  ToolManager —— 启动 / 停止 / 查询子工具状态      │  │
│  │  IpcServer   —— Named Pipe 服务端                │  │
│  │  ConfigHub   —— 全局数据根目录                   │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
          │ 启动子进程            │ 启动子进程
          │ Named Pipe IPC        │ Named Pipe IPC
          ▼                       ▼
┌───────────────────────┐   ┌───────────────────────┐
│  CrosshairTool.exe    │   │  GammaTool.exe        │
│  - OverlayWindow      │   │  - GammaPanelWindow   │
│  - CrosshairRenderer  │   │  - GammaApplier       │
│  - IpcClient          │   │  - IpcClient          │
│  - (无托盘图标)        │   │  - (无托盘图标)        │
└───────────────────────┘   └───────────────────────┘
```

### 2.2 关键点

- 三个 exe 都使用 **WPF / .NET 8**，统一构建
- **只有 `FPSToolbox.exe` 创建托盘图标**，子工具运行时**不显示托盘图标**，也不出现在任务栏（`ShowInTaskbar=False` 或只显示设置窗口）
- 子工具启动参数约定：`--parent-pid <toolbox-pid> --pipe <pipe-name>`，用来和 Toolbox 建立 IPC 连接，且子工具检测到父进程退出时自行退出（防止成为孤儿进程）
- **单例启动**：三个 exe 各自使用独立 Mutex 防止重复启动
  - `Global\FPSToolbox_SingleInstance`
  - `Global\FPSToolbox_CrosshairTool_SingleInstance`
  - `Global\FPSToolbox_GammaTool_SingleInstance`

---

## 三、目录结构

### 3.1 源码仓库结构

```
game-aiming/
├── FPSToolbox.sln
├── src/
│   ├── FPSToolbox/                       # 主程序
│   │   ├── FPSToolbox.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml           # 工具中心卡片界面
│   │   │   └── ToolCardControl.xaml      # 单个工具卡片
│   │   ├── ViewModels/
│   │   │   └── MainViewModel.cs
│   │   ├── Core/
│   │   │   ├── ToolManager.cs            # 启动/停止子工具
│   │   │   ├── ToolRegistry.cs           # 已安装工具的元数据
│   │   │   ├── IpcServer.cs              # Named Pipe 服务端
│   │   │   ├── TrayIconManager.cs
│   │   │   └── PathService.cs            # 统一数据路径
│   │   ├── Models/
│   │   │   ├── ToolDescriptor.cs
│   │   │   └── ToolboxSettings.cs
│   │   └── Resources/
│   │       ├── icon.ico                  # 2.png 转换而来
│   │       └── default-settings.json
│   │
│   ├── CrosshairTool/                    # 见 crosshair-tool-spec.md
│   └── GammaTool/                        # 见 gamma-tool-spec.md
│
├── shared/
│   └── FPSToolbox.Shared/                # 三个项目共用的库
│       ├── Ipc/
│       │   ├── IpcMessage.cs
│       │   ├── IpcClient.cs
│       │   └── PipeNames.cs
│       ├── Config/
│       │   └── JsonConfigStore.cs
│       └── Native/
│           └── NativeMethods.cs
│
├── installer/
│   ├── FPSToolbox.iss                    # Inno Setup 脚本
│   ├── uninstall-dialog.ps1              # 卸载时的"清除数据"询问
│   └── assets/
│       ├── toolbox-logo.ico
│       ├── crosshair-logo.ico
│       └── gamma-logo.ico
│
├── assets/                               # 原始 logo 素材
│   ├── 1.png                             # GammaTool logo（蝴蝶）
│   └── 2.png                             # FPSToolbox logo（盾牌 + 准心）
│
├── docs/
│   ├── fps-toolbox-spec.md               # ← 本文件
│   ├── crosshair-tool-spec.md
│   └── gamma-tool-spec.md
└── README.md
```

### 3.2 用户数据路径

```
%AppData%\FPSToolbox\                       ← 所有用户数据根目录
├── toolbox.json                            # Toolbox 自身设置（主题、开机自启、热键等）
├── installed-tools.json                    # 已安装子工具清单（name / version / exePath）
├── CrosshairTool\
│   ├── config.json                         # 当前生效配置
│   └── presets\*.json                      # 准心方案
└── GammaTool\
    ├── config.json
    └── presets\*.json                      # 配色方案
```

**程序安装路径**（由 Inno Setup 决定，默认）：

```
C:\Program Files\FPSToolbox\
├── FPSToolbox.exe
├── FPSToolbox.Shared.dll
├── tools\
│   ├── CrosshairTool\CrosshairTool.exe
│   └── GammaTool\GammaTool.exe
└── uninst000.exe
```

---

## 四、安装与卸载策略

### 4.1 安装器形态（方案 3：打包内置 + Toolbox 管理）

**`FPSToolbox-Setup.exe`**（唯一对外发布的安装包）：

- 使用 Inno Setup 制作
- 支持**组件选择**页（必选 + 可选）：

  ```
  [v] FPSToolbox 主程序         （必选，不可取消）
  [ ] 屏幕准心工具              （可选）
  [ ] 屏幕调节工具              （可选）
  ```

- 所有子工具的二进制都**打包在安装器内**。没勾选的工具，安装器**跳过复制**这些 exe，并**不写入** `installed-tools.json`
- 装完之后如果想追加安装某个工具：
  - **方式 A**：重新运行 `FPSToolbox-Setup.exe`，Inno Setup 会自动识别现有安装并提供"修改 / 修复"选项
  - **方式 B**：在 Toolbox 主界面点击未安装工具的卡片 → "从本地安装"，弹文件选择框让用户选刚下载的 `CrosshairTool-v1.0.0.zip`（后续云盘链接升级用）

> 云盘分发的补充包：将来作者会把单独打包好的 `CrosshairTool-vX.Y.Z.zip` / `GammaTool-vX.Y.Z.zip` 上传到云盘，用户从云盘下载 zip 后通过 Toolbox 的"从本地安装"按钮导入。Toolbox 负责校验、解压到 `tools\<Name>\`、更新 `installed-tools.json`。

### 4.2 "从本地安装"流程

1. 用户在 Toolbox 主界面对着未安装的工具卡片点击"安装"
2. 文件对话框选择 `XxxTool-vX.Y.Z.zip`
3. Toolbox 校验：
   - zip 内必须有 `manifest.json`（包含 `name`, `version`, `exeName`, `sha256`）
   - `name` 必须是已注册的白名单（`CrosshairTool` / `GammaTool`）
   - SHA-256 校验
4. 解压到 `tools\<Name>\`
5. 更新 `installed-tools.json`
6. 卡片切换到"已安装"状态

### 4.3 卸载流程

Inno Setup 卸载器启动时：

1. 先尝试 IPC 通知 `FPSToolbox.exe` 优雅退出（同时退出所有子工具）；超时则强杀进程
2. 执行自定义对话框（`uninstall-dialog.ps1` 或内嵌 `[Code]` 段 Pascal Script）：

   ```
   ┌─────────────────────────────────────────────┐
   │  卸载 FPS 工具箱                             │
   ├─────────────────────────────────────────────┤
   │                                              │
   │  [☐] 同时清除所有用户数据                    │
   │                                              │
   │  勾选后将删除 %AppData%\FPSToolbox\ 下的     │
   │  全部配置和预设方案（包括所有工具）。        │
   │                                              │
   │  不勾选则保留数据，下次重新安装可继续使用。  │
   │                                              │
   │          [ 确定 ]    [ 取消 ]                │
   └─────────────────────────────────────────────┘
   ```

3. 删除 `C:\Program Files\FPSToolbox\` 下所有文件
4. 若勾选了清除数据 → 递归删除 `%AppData%\FPSToolbox\`
5. 移除开机自启注册表项、全局热键残留

### 4.4 升级 / 修改

重新运行 `FPSToolbox-Setup.exe`：
- 检测已安装 → 进入 "Modify / Repair / Uninstall" 页
- "Modify" 允许重新勾选子工具组件（新勾的会安装，取消勾选的**只删除 exe**，**不删除用户数据**）

---

## 五、进程间通信（IPC）

### 5.1 传输层

- **Named Pipes**（`System.IO.Pipes.NamedPipeServerStream` / `NamedPipeClientStream`）
- Pipe 名称：`FPSToolbox.IPC.<toolbox-pid>`（含 PID 避免多用户 / 多登录会话冲突）
- 协议：**行分隔的 JSON**（每条消息一行，UTF-8，以 `\n` 结尾）
- 安全性：仅 `PipeDirection.InOut`，`PipeOptions.CurrentUserOnly`

### 5.2 消息结构

```jsonc
// 请求
{ "id": 123, "kind": "request", "action": "crosshair.toggle", "payload": { ... } }
// 响应
{ "id": 123, "kind": "response", "ok": true, "data": { ... } }
// 通知（无需响应）
{ "kind": "event", "topic": "crosshair.visibility", "payload": { "visible": true } }
```

### 5.3 Toolbox → 子工具（控制指令）

| Action | 说明 |
|--------|------|
| `crosshair.show` / `crosshair.hide` / `crosshair.toggle` | 显隐控制 |
| `crosshair.openSettings` | 弹出准心设置窗口 |
| `crosshair.reloadConfig` | 重新加载配置（配置文件被外部修改时） |
| `gamma.openPanel` | 弹出 Gamma 设置面板 |
| `gamma.applyPreset` | 应用指定预设 |
| `gamma.resetSystem` | 把系统 Gamma 恢复为默认 |
| `shutdown` | 子工具优雅退出 |
| `ping` | 心跳 |

### 5.4 子工具 → Toolbox（状态上报）

| Topic | Payload |
|-------|---------|
| `crosshair.visibility` | `{ visible: bool }` |
| `crosshair.configChanged` | `{ presetName: string }` |
| `gamma.previewState` | `{ previewing: bool }` |
| `gamma.schemeApplied` | `{ schemeName: string }` |
| `tool.ready` | `{ tool: string, version: string, pid: int }` |
| `tool.exiting` | `{ tool: string, reason: string }` |

### 5.5 心跳与孤儿保护

- Toolbox 每 5s 向每个子工具发 `ping`；连续 3 次无响应 → 视为失联，托盘菜单将该工具标红并提供"重启"
- 子工具启动时 `--parent-pid` 带入 Toolbox 的 PID；子工具每 10s 用 `Process.GetProcessById` 检查父进程是否存活，父进程已退出 → 自行退出

---

## 六、UI 设计（主界面）

### 6.1 MainWindow 布局

```
┌─────────────────────────────────────────────────────┐
│ 🛡 FPS 工具箱                     [ _ ] [ □ ] [ × ] │
├─────────────────────────────────────────────────────┤
│                                                      │
│  ┌─────────────────┐   ┌─────────────────┐          │
│  │  🦋              │   │  🎯              │          │
│  │  屏幕调节工具    │   │  屏幕准心工具    │          │
│  │                 │   │                 │          │
│  │  复刻 Gamma     │   │  自定义准心     │          │
│  │  Panel 的灰度、 │   │  全屏置顶、     │          │
│  │  亮度、对比度、 │   │  点击穿透、     │          │
│  │  RGB 通道调节。 │   │  多样式可选。   │          │
│  │                 │   │                 │          │
│  │  ● 已安装        │   │  ● 已安装        │          │
│  │  [启动] [设置]   │   │  [启动] [设置]   │          │
│  └─────────────────┘   └─────────────────┘          │
│                                                      │
│  ┌─────────────────┐                                 │
│  │  ➕              │                                 │
│  │  从本地安装工具  │                                 │
│  │  (选择 .zip)    │                                 │
│  └─────────────────┘                                 │
│                                                      │
├─────────────────────────────────────────────────────┤
│ [v] 开机自启   [v] 关闭按钮最小化到托盘              │
│                                       版本 1.0.0     │
└─────────────────────────────────────────────────────┘
```

### 6.2 卡片状态

| 状态 | 视觉 | 按钮 |
|------|------|------|
| 未安装 | 灰色 + 图标去色 | `[安装]` |
| 已安装、未启动 | 正常色 | `[启动]` `[设置]` `[卸载]` |
| 已启动 | 高亮边框 + 绿点 | `[停止]` `[设置]` |
| 失联 | 红色警告 | `[重启]` |

### 6.3 托盘右键菜单

```
FPS 工具箱
────────────────
屏幕准心
  ├─ 显示 / 隐藏        (F8)
  ├─ 打开设置...
  └─ 停止
屏幕调节
  ├─ 打开面板...
  ├─ 应用预设  ▶
  └─ 停止
────────────────
打开主界面
开机自启 ✓
────────────────
完全退出（退出所有工具）
```

### 6.4 关闭行为

- **主窗口 ×**：最小化到托盘（不退出任何进程）
- **托盘"完全退出"**：发送 `shutdown` 给所有子工具 → 等待 2s → 强杀未退出的 → Toolbox 自身退出

---

## 七、Toolbox 自身设置（toolbox.json）

```jsonc
{
  "autoStart": false,                 // 开机自启 FPSToolbox
  "minimizeToTrayOnClose": true,
  "theme": "dark",                    // dark | light | system
  "globalHotkeys": {
    "showMainWindow": "Ctrl+Alt+T"    // 可选
  },
  "lastUsedTools": ["CrosshairTool", "GammaTool"]
}
```

---

## 八、开发阶段规划

### Phase 0 —— 基础设施
- [ ] 搭建 Solution 结构：`FPSToolbox` / `CrosshairTool` / `GammaTool` / `FPSToolbox.Shared`
- [ ] `FPSToolbox.Shared.Ipc`：Named Pipe 服务端 + 客户端 + 消息编解码
- [ ] `PathService`：统一 `%AppData%\FPSToolbox\` 路径
- [ ] Logo 转 ICO（1.png → gamma-logo.ico，2.png → toolbox-logo.ico）

### Phase 1 —— Toolbox 主程序
- [ ] MainWindow 卡片 UI
- [ ] ToolManager：`Start(toolName)` / `Stop(toolName)` / `GetStatus()`
- [ ] TrayIconManager：分组菜单
- [ ] "从本地 zip 安装"流程

### Phase 2 —— 子工具接入
- [ ] CrosshairTool 按新 spec 改造为子进程形态（去掉独立托盘、接入 IPC）
- [ ] GammaTool 从零实现（详见 gamma-tool-spec.md）

### Phase 3 —— 安装器
- [ ] Inno Setup 脚本，组件选择页
- [ ] 自定义卸载对话框（清除数据勾选）

### Phase 4 —— 润色
- [ ] 失联监控 / 自动重启
- [ ] 开机自启 / 全局热键
- [ ] 崩溃日志 `%AppData%\FPSToolbox\logs\`

---

## 九、打包发布命令

```bash
# 三个 exe 各自发布
dotnet publish src/FPSToolbox/FPSToolbox.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/FPSToolbox
dotnet publish src/CrosshairTool/CrosshairTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/CrosshairTool
dotnet publish src/GammaTool/GammaTool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/GammaTool

# Inno Setup 打包
iscc installer/FPSToolbox.iss
# 产出：installer/Output/FPSToolbox-Setup-v1.0.0.exe
```

---

## 十、交叉引用

- 子工具「屏幕准心工具」细节：**`docs/crosshair-tool-spec.md`**
- 子工具「屏幕调节工具」细节：**`docs/gamma-tool-spec.md`**
