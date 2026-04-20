# 屏幕准心工具 —— 子工具架构设计文档

> 所属项目：**FPS 工具箱 (FPSToolbox)**  
> 可执行文件：`CrosshairTool.exe`  
> 目标平台：Windows 10 / 11 (x64)  
> 技术栈：**C# + WPF (.NET 8)**

> ⚠️ **重要**：本工具是 FPSToolbox 的子工具，**不能独立安装或独立运行**。  
> 总体架构、安装流程、IPC 协议、用户数据路径见 **`docs/fps-toolbox-spec.md`**。

---

## 一、工具定位

在屏幕最顶层绘制一个自定义准心（Crosshair），**不影响鼠标点击操作（点击穿透）**。
由 FPSToolbox 主程序负责启动 / 停止 / 托盘菜单展示，自身不创建托盘图标。

### 核心特性

- 全屏透明覆盖层，永远置于所有窗口最上层（含全屏游戏）
- 鼠标点击穿透，不干扰游戏操作
- 准心样式完全自定义
- **无独立托盘图标**，受 Toolbox 托盘菜单和 IPC 指令控制
- 全局热键（F8 默认切换显隐）仍然由本进程注册，以保证响应延迟最低
- 低资源占用（< 5MB 内存，0% CPU 闲置时）

---

## 二、进程形态

### 2.1 启动方式

```
CrosshairTool.exe --parent-pid <toolbox-pid> --pipe FPSToolbox.IPC.<toolbox-pid>
```

- 只允许由 `FPSToolbox.exe` 作为父进程拉起（启动参数若缺失 `--parent-pid` 则直接退出并弹窗提示"请从 FPS 工具箱启动"）
- 使用全局 Mutex `Global\FPSToolbox_CrosshairTool_SingleInstance` 防止重复启动
- 启动后立即连接 IPC 管道，发送 `tool.ready` 事件

### 2.2 生命周期

```
                ┌──────────────┐
                │  Toolbox 启动 │
                └──────┬───────┘
                       │
                       ▼
       用户在主界面点击"启动准心工具"
                       │
                       ▼
           ToolManager.Start("CrosshairTool")
                       │
                       ▼
  Process.Start("CrosshairTool.exe --parent-pid ... --pipe ...")
                       │
                       ▼
           OverlayWindow 显示（按配置）
                       │
                       ▼
   ───────── 正常运行 ─────────
                       │
         收到 IPC `shutdown`    或   父进程已退出
                       │
                       ▼
           优雅关闭 OverlayWindow / 注销热键 / 退出
```

### 2.3 退出条件（任一触发即退出）

1. 收到 IPC `shutdown` 指令
2. 父进程（Toolbox）进程不存在（每 10s 轮询）
3. 用户从 OverlayWindow 的设置窗口点击"退出"
4. 未通过 Toolbox 启动（缺少 `--parent-pid`）

---

## 三、项目结构

```
src/CrosshairTool/
├── CrosshairTool.csproj
├── App.xaml / App.xaml.cs              # 启动参数解析、单例、IPC 初始化
│
├── Core/
│   ├── AppContext.cs                   # 全局状态单例
│   ├── HotkeyManager.cs                # 全局热键（F8 等）
│   ├── ConfigManager.cs                # 配置 / 预设读写
│   ├── ParentWatcher.cs                # 父进程监控
│   └── ToolIpcClient.cs                # 封装 FPSToolbox.Shared.Ipc.IpcClient
│
├── Models/
│   └── CrosshairConfig.cs
│
├── Windows/
│   ├── OverlayWindow.xaml / .cs        # 透明覆盖层
│   └── SettingsWindow.xaml / .cs       # 配置面板
│
├── Rendering/
│   └── CrosshairRenderer.cs
│
└── Resources/
    ├── icon.ico                        # 仅用于 SettingsWindow 和任务栏
    └── default-config.json
```

---

## 四、核心模块

### 4.1 OverlayWindow —— 透明覆盖层

与原 spec 保持一致，关键 Win32 属性：

```csharp
private void SetWindowProperties()
{
    var hwnd = new WindowInteropHelper(this).Handle;

    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    SetWindowLong(hwnd, GWL_EXSTYLE,
        exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
}
```

XAML：

```xml
<Window
    AllowsTransparency="True"
    Background="Transparent"
    WindowStyle="None"
    ShowInTaskbar="False"
    Topmost="True"
    Left="{Binding VirtualScreenLeft}"
    Top="{Binding VirtualScreenTop}"
    Width="{Binding VirtualScreenWidth}"
    Height="{Binding VirtualScreenHeight}" />
```

### 4.2 CrosshairConfig —— 准心配置模型

```csharp
public class CrosshairConfig
{
    public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;
    public string Color { get; set; } = "#00FF00";
    public double Opacity { get; set; } = 1.0;
    public int Size { get; set; } = 20;
    public int Thickness { get; set; } = 2;
    public int Gap { get; set; } = 4;

    public bool OutlineEnabled { get; set; } = true;
    public string OutlineColor { get; set; } = "#000000";
    public int OutlineThickness { get; set; } = 1;

    public bool DotEnabled { get; set; } = false;
    public int DotSize { get; set; } = 3;
    public string DotColor { get; set; } = "#00FF00";

    public bool TopLineEnabled { get; set; } = true;

    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public string ToggleHotkey { get; set; } = "F8";

    public string PresetName { get; set; } = "默认";
}

public enum CrosshairStyle
{
    Cross,
    Dot,
    Circle,
    CrossDot,
    Custom
}
```

### 4.3 CrosshairRenderer —— 渲染

与原 spec 相同，通过 WPF `DrawingContext` 绘制。关键逻辑：

```csharp
public void Draw(DrawingContext dc, CrosshairConfig config, Point center)
{
    var color = (Color)ColorConverter.ConvertFromString(config.Color);
    var brush = new SolidColorBrush(Color.FromArgb(
        (byte)(config.Opacity * 255), color.R, color.G, color.B));
    var pen = new Pen(brush, config.Thickness);

    var outlinePen = config.OutlineEnabled
        ? new Pen(new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(config.OutlineColor)),
            config.Thickness + config.OutlineThickness * 2)
        : null;

    switch (config.Style)
    {
        case CrosshairStyle.Cross:    DrawCross(dc, config, center, pen, outlinePen); break;
        case CrosshairStyle.Circle:   DrawCircle(dc, config, center, pen, outlinePen); break;
        case CrosshairStyle.CrossDot: DrawCross(dc, config, center, pen, outlinePen); break;
        case CrosshairStyle.Dot:      break;
    }

    if (config.DotEnabled || config.Style == CrosshairStyle.Dot
        || config.Style == CrosshairStyle.CrossDot)
        DrawDot(dc, config, center);
}
```

DrawCross / DrawCircle / DrawDot 的细节见 `Rendering/CrosshairRenderer.cs`（保留原 spec 实现）。

### 4.4 ConfigManager —— 配置持久化

**路径由 `FPSToolbox.Shared.PathService` 统一提供**，不再自己决定：

```csharp
// 使用 Shared 库
string configPath = PathService.GetToolConfigPath("CrosshairTool");
//   → %AppData%\FPSToolbox\CrosshairTool\config.json
string presetsDir = PathService.GetToolPresetsDir("CrosshairTool");
//   → %AppData%\FPSToolbox\CrosshairTool\presets\
```

接口保留：

```csharp
public class ConfigManager
{
    public CrosshairConfig Load();
    public void Save(CrosshairConfig config);

    public IReadOnlyList<string> GetPresetNames();
    public CrosshairConfig LoadPreset(string name);
    public void SavePreset(string name, CrosshairConfig config);
    public void DeletePreset(string name);
}
```

### 4.5 HotkeyManager —— 全局热键

**保留在本进程内注册**（而非由 Toolbox 代管），因为：
- 热键响应延迟低
- 本进程挂掉时热键自动失效，语义清晰
- 避免 Toolbox 需要识别"当前按下的热键应该发给哪个子工具"

注册对象仍是一个隐藏消息窗口（不是 `OverlayWindow`，因为后者是 `WS_EX_TRANSPARENT`）。

### 4.6 ToolIpcClient —— IPC 接入

#### 连接

```csharp
// 在 App.OnStartup 中
await _ipc.ConnectAsync(pipeName, CancellationToken.None);
await _ipc.SendEventAsync("tool.ready", new {
    tool = "CrosshairTool",
    version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
    pid = Environment.ProcessId
});
```

#### 处理来自 Toolbox 的指令

| Action | 行为 |
|--------|------|
| `crosshair.show` | 调用 `OverlayWindow.Show()` |
| `crosshair.hide` | 调用 `OverlayWindow.Hide()` |
| `crosshair.toggle` | 反转当前显隐 |
| `crosshair.openSettings` | 显示 `SettingsWindow`（若已存在则前置） |
| `crosshair.reloadConfig` | `ConfigManager.Load()` 并重绘 |
| `crosshair.applyPreset` | `payload: { name }` → 切换预设 |
| `shutdown` | 关闭所有窗口并退出进程 |
| `ping` | 回复 `pong` |

所有指令的执行必须 `Dispatcher.Invoke` 切回 UI 线程。

#### 主动上报事件

- 显示 / 隐藏状态变化 → `crosshair.visibility`
- 预设切换 / 配置保存 → `crosshair.configChanged`
- 进程即将退出 → `tool.exiting`

---

## 五、设置面板 UI

与原 spec 保持一致，参数清单：

| 分组 | 参数 | 控件 | 范围 |
|------|------|------|------|
| 样式 | 准心类型 | 下拉 | 十字 / 圆形 / 点 / 十字+点 |
| 颜色 | 准心颜色 | 颜色选择器 | 任意 |
| 颜色 | 透明度 | 滑块 | 0–100% |
| 颜色 | 轮廓颜色 | 颜色选择器 | 任意 |
| 尺寸 | 准心大小 | 数字+滑块 | 1–50 px |
| 尺寸 | 线条粗细 | 数字+滑块 | 1–10 px |
| 尺寸 | 中心间隙 | 数字+滑块 | 0–20 px |
| 线条 | 顶部线条 | 复选框 | on/off |
| 线条 | 轮廓 | 复选框 | on/off |
| 点 | 中心点 | 复选框 | on/off |
| 点 | 中心点大小 | 数字 | 1–10 px |
| 位置 | X / Y 偏移 | 数字 | -100–100 px |
| 热键 | 切换热键 | 热键录制 | 任意 |
| 预设 | 方案名 | 下拉 + 保存 / 加载 / 删除 | — |

内嵌 **PreviewCanvas**（200×200，黑底），参数变化立即：
1. 重绘预览 canvas
2. 重绘 OverlayWindow
3. 不自动保存，由用户显式点"保存"

**关闭 SettingsWindow 行为**：仅隐藏设置窗，不影响 OverlayWindow 和主进程（与原 spec 相同）。

---

## 六、关键技术点

### 6.1 全屏游戏兼容性

- 独占全屏（Exclusive Fullscreen）：`HWND_TOPMOST` 可能不生效 → 引导用户改"窗口化全屏" / "无边框"
- 无边框 / 窗口化全屏：完全兼容

### 6.2 多显示器

```csharp
this.Left   = SystemParameters.VirtualScreenLeft;
this.Top    = SystemParameters.VirtualScreenTop;
this.Width  = SystemParameters.VirtualScreenWidth;
this.Height = SystemParameters.VirtualScreenHeight;
```

默认主显示器中心为准心位置：

```csharp
var ps = Screen.PrimaryScreen.Bounds;
double cx = ps.Left + ps.Width / 2.0 + config.OffsetX;
double cy = ps.Top  + ps.Height / 2.0 + config.OffsetY;
```

### 6.3 不出现在 Alt+Tab / 任务栏

`WS_EX_TOOLWINDOW` + `ShowInTaskbar=False`。

### 6.4 崩溃日志

未处理异常 → `%AppData%\FPSToolbox\logs\CrosshairTool-yyyyMMdd.log`。

---

## 七、NuGet 依赖

```xml
<ItemGroup>
  <!-- 颜色选择器 -->
  <PackageReference Include="ColorPicker.WPF" Version="*" />

  <!-- 共用 IPC / 路径 / Native -->
  <ProjectReference Include="..\..\shared\FPSToolbox.Shared\FPSToolbox.Shared.csproj" />

  <!-- JSON：.NET 8 内置 System.Text.Json -->
</ItemGroup>
```

> 注意：**不再需要** `Hardcodet.NotifyIcon.Wpf`，因为本工具不创建托盘图标。

---

## 八、开发阶段规划

### Phase 1 —— 从原独立工具迁移到子工具形态

- [ ] 去除 `TrayIconManager`
- [ ] `App.OnStartup` 解析 `--parent-pid` / `--pipe`，缺失则弹窗并退出
- [ ] 集成 `FPSToolbox.Shared.Ipc.IpcClient`
- [ ] 实现全部指令响应和事件上报
- [ ] `ParentWatcher` 父进程监控

### Phase 2 —— 渲染与配置（复用原 MVP）

- [ ] OverlayWindow + 点击穿透 + 永远置顶
- [ ] CrosshairRenderer 全样式
- [ ] F8 热键

### Phase 3 —— 配置面板

- [ ] SettingsWindow 完整 UI
- [ ] 实时预览 canvas
- [ ] 预设保存 / 加载 / 删除（路径走 `PathService`）

### Phase 4 —— 打磨

- [ ] 多显示器测试
- [ ] 独占全屏提示
- [ ] 崩溃日志

---

## 九、与 Toolbox 的集成测试清单

- [ ] 冷启动：Toolbox 拉起 CrosshairTool，5s 内出现准心
- [ ] IPC：Toolbox 发 `toggle` → 准心立刻显隐
- [ ] 热键：F8 切换显隐 → Toolbox 收到 `crosshair.visibility` 事件并更新托盘菜单勾选
- [ ] 孤儿保护：kill `FPSToolbox.exe` → `CrosshairTool.exe` 10s 内自行退出
- [ ] 双启防护：直接双击 `CrosshairTool.exe` → 提示"请通过 FPS 工具箱启动"并退出
- [ ] 预设切换：Toolbox 托盘菜单切换预设 → 准心外观立刻变化
