# 屏幕准心工具 —— 架构设计文档

> 适用游戏：三角洲行动（及其他 FPS 游戏）  
> 目标平台：Windows 10 / 11 (x64)  
> 推荐技术栈：**C# + WPF (.NET 8)**

---

## 一、项目概述

本工具在屏幕最顶层绘制一个自定义准心（Crosshair），不影响任何鼠标点击操作（点击穿透），用户可通过独立配置面板实时调整准心的外观，并通过系统托盘随时切换显示/隐藏。

### 核心特性

- 全屏透明覆盖层，永远置于所有窗口最上层（含全屏游戏）
- 鼠标点击穿透，不干扰游戏操作
- 准心样式完全自定义
- 系统托盘驻留，支持全局热键
- 低资源占用（< 5MB 内存，0% CPU 闲置时）

---

## 二、技术选型说明

### 为什么选 C# WPF / WinForms，而不是 Electron

| 对比维度 | C# WPF | Electron |
|---------|--------|----------|
| 点击穿透实现 | Win32 API 原生支持，稳定 | 需要 hack，部分游戏不兼容 |
| 全屏游戏穿透 | `HWND_TOPMOST` + D3D 兼容 | 经常被游戏全屏覆盖 |
| 内存占用 | ~5–15 MB | ~80–150 MB |
| 分发包体积 | ~10 MB（含 .NET 8 自包含） | ~80 MB+ |
| 开发难度 | 中等 | 低（但坑多） |

**结论：强烈推荐 C# WPF + .NET 8**

---

## 三、项目结构

```
CrosshairTool/
├── CrosshairTool.sln
├── src/
│   └── CrosshairTool/
│       ├── CrosshairTool.csproj
│       ├── App.xaml                    # 应用入口，单例控制
│       ├── App.xaml.cs
│       │
│       ├── Core/
│       │   ├── AppContext.cs           # 全局状态单例
│       │   ├── HotkeyManager.cs        # 全局热键注册
│       │   └── ConfigManager.cs        # 配置读写
│       │
│       ├── Models/
│       │   └── CrosshairConfig.cs      # 准心配置数据模型
│       │
│       ├── Windows/
│       │   ├── OverlayWindow.xaml      # 透明覆盖层窗口
│       │   ├── OverlayWindow.xaml.cs
│       │   ├── SettingsWindow.xaml     # 配置面板
│       │   └── SettingsWindow.xaml.cs
│       │
│       ├── Rendering/
│       │   └── CrosshairRenderer.cs    # 准心绘制逻辑（WPF DrawingContext）
│       │
│       ├── TrayIcon/
│       │   └── TrayIconManager.cs      # 系统托盘图标管理
│       │
│       └── Resources/
│           ├── icon.ico
│           └── default-config.json
```

---

## 四、核心模块详解

### 4.1 OverlayWindow —— 覆盖层窗口

这是最关键的模块，需要用 Win32 API 实现以下效果：

**窗口属性设置（必须）：**

```csharp
// 在 Window_Loaded 中调用
private void SetWindowProperties()
{
    var hwnd = new WindowInteropHelper(this).Handle;

    // 1. 永远置顶
    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

    // 2. 点击穿透（核心！）
    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
}
```

**XAML 窗口属性：**

```xml
<Window
    AllowsTransparency="True"
    Background="Transparent"
    WindowStyle="None"
    ShowInTaskbar="False"
    Topmost="True"
    Left="0" Top="0"
    Width="{Binding SystemWidth}"
    Height="{Binding SystemHeight}">
```

**注意事项：**
- 窗口需覆盖全部显示器（多显示器场景取所有屏幕的 union 矩形）
- 游戏全屏独占模式（Exclusive Fullscreen）下，`HWND_TOPMOST` 可能不生效，需引导用户使用**无边框窗口模式**或**窗口化全屏**

---

### 4.2 CrosshairConfig —— 准心配置模型

```csharp
public class CrosshairConfig
{
    // 基础
    public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;  // 样式类型
    public string Color { get; set; } = "#00FF00";                      // 颜色（HEX）
    public double Opacity { get; set; } = 1.0;                          // 透明度 0~1
    public int Size { get; set; } = 20;                                 // 准心整体大小（px）
    public int Thickness { get; set; } = 2;                             // 线条粗细（px）
    public int Gap { get; set; } = 4;                                   // 中心间隙（px）

    // 轮廓
    public bool OutlineEnabled { get; set; } = true;
    public string OutlineColor { get; set; } = "#000000";
    public int OutlineThickness { get; set; } = 1;

    // 中心点
    public bool DotEnabled { get; set; } = false;
    public int DotSize { get; set; } = 3;
    public string DotColor { get; set; } = "#00FF00";

    // 顶部线条
    public bool TopLineEnabled { get; set; } = true;

    // 位置偏移
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    // 热键
    public string ToggleHotkey { get; set; } = "F8";

    // 当前预设名称
    public string PresetName { get; set; } = "默认";
}

public enum CrosshairStyle
{
    Cross,      // 十字准心
    Dot,        // 纯中心点
    Circle,     // 圆形
    CrossDot,   // 十字 + 中心点
    Custom      // 自定义（保留）
}
```

---

### 4.3 CrosshairRenderer —— 准心渲染

在 OverlayWindow 的 `OnRender` 中调用，使用 WPF `DrawingContext`：

```csharp
public class CrosshairRenderer
{
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
            case CrosshairStyle.Cross:
                DrawCross(dc, config, center, pen, outlinePen);
                break;
            case CrosshairStyle.Circle:
                DrawCircle(dc, config, center, pen, outlinePen);
                break;
            // ... 其他样式
        }

        if (config.DotEnabled)
            DrawDot(dc, config, center);
    }

    private void DrawCross(DrawingContext dc, CrosshairConfig cfg,
        Point c, Pen pen, Pen outlinePen)
    {
        int halfGap = cfg.Gap;
        int halfLen = halfGap + cfg.Size;

        // 轮廓（先画，在下层）
        if (outlinePen != null)
        {
            if (cfg.TopLineEnabled)
                dc.DrawLine(outlinePen, new Point(c.X, c.Y - halfLen), new Point(c.X, c.Y - halfGap));
            dc.DrawLine(outlinePen, new Point(c.X, c.Y + halfGap), new Point(c.X, c.Y + halfLen));
            dc.DrawLine(outlinePen, new Point(c.X - halfLen, c.Y), new Point(c.X - halfGap, c.Y));
            dc.DrawLine(outlinePen, new Point(c.X + halfGap, c.Y), new Point(c.X + halfLen, c.Y));
        }

        // 主体
        if (cfg.TopLineEnabled)
            dc.DrawLine(pen, new Point(c.X, c.Y - halfLen), new Point(c.X, c.Y - halfGap));
        dc.DrawLine(pen, new Point(c.X, c.Y + halfGap), new Point(c.X, c.Y + halfLen));
        dc.DrawLine(pen, new Point(c.X - halfLen, c.Y), new Point(c.X - halfGap, c.Y));
        dc.DrawLine(pen, new Point(c.X + halfGap, c.Y), new Point(c.X + halfLen, c.Y));
    }
}
```

---

### 4.4 ConfigManager —— 配置持久化

```csharp
public class ConfigManager
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrosshairTool");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public CrosshairConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new CrosshairConfig();  // 返回默认配置

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<CrosshairConfig>(json) ?? new CrosshairConfig();
    }

    public void Save(CrosshairConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    // 预设管理（多套准心配置切换）
    public List<string> GetPresetNames() { ... }
    public CrosshairConfig LoadPreset(string name) { ... }
    public void SavePreset(string name, CrosshairConfig config) { ... }
}
```

---

### 4.5 HotkeyManager —— 全局热键

```csharp
public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private HwndSource _source;
    private Dictionary<int, Action> _hotkeys = new();
    private int _idCounter = 9000;

    public void Initialize(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WndProc);
    }

    public int Register(uint modifiers, uint vk, Action callback)
    {
        int id = _idCounter++;
        RegisterHotKey(/* hwnd */, id, modifiers, vk);
        _hotkeys[id] = callback;
        return id;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _hotkeys.TryGetValue(wParam.ToInt32(), out var action))
        {
            action?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

---

### 4.6 TrayIconManager —— 系统托盘

```csharp
public class TrayIconManager
{
    private NotifyIcon _notifyIcon;

    public void Initialize(Action onToggle, Action onSettings, Action onExit)
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon("Resources/icon.ico"),
            Text = "CrosshairTool",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(onToggle, onSettings, onExit)
        };

        _notifyIcon.DoubleClick += (_, _) => onSettings();
    }

    private ContextMenuStrip BuildContextMenu(Action toggle, Action settings, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示/隐藏准心 (F8)", null, (_, _) => toggle());
        menu.Items.Add("设置...", null, (_, _) => settings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());
        return menu;
    }
}
```

---

## 五、配置面板 UI 设计

### 可调节参数清单

| 分组 | 参数 | 控件类型 | 范围 |
|------|------|---------|------|
| 样式 | 准心类型 | 下拉选择 | 十字/圆形/点/十字+点 |
| 颜色 | 准心颜色 | 颜色选择器 | 任意颜色 |
| 颜色 | 透明度 | 滑块 | 0%–100% |
| 颜色 | 轮廓颜色 | 颜色选择器 | 任意颜色 |
| 尺寸 | 准心大小 | 数字输入+滑块 | 1–50 px |
| 尺寸 | 线条粗细 | 数字输入+滑块 | 1–10 px |
| 尺寸 | 中心间隙 | 数字输入+滑块 | 0–20 px |
| 线条 | 顶部线条开关 | 复选框 | on/off |
| 线条 | 轮廓开关 | 复选框 | on/off |
| 点 | 中心点开关 | 复选框 | on/off |
| 点 | 中心点大小 | 数字输入 | 1–10 px |
| 位置 | X/Y 偏移 | 数字输入 | -100–100 px |
| 热键 | 切换热键 | 热键录制框 | 任意键 |
| 预设 | 预设名称 | 文本+保存/加载 | — |

### 实时预览

配置面板内嵌一个 `PreviewCanvas`（黑色背景 200×200），任何参数变化立即调用 `CrosshairRenderer.Draw()` 更新预览，同时实时更新 OverlayWindow 上的准心。

---

## 六、关键技术点与坑

### 6.1 全屏游戏兼容性

- **独占全屏（Exclusive Fullscreen）**：DX/Vulkan 独占全屏会绕过 Windows 合成器，`HWND_TOPMOST` 无法覆盖。**解决方案**：在 README 中说明用户需将三角洲行动设置为"窗口化全屏"模式。
- **无边框全屏（Borderless Windowed）**：完全兼容，推荐用户使用此模式。

### 6.2 多显示器支持

```csharp
// 获取所有显示器的 union 矩形
var virtualScreen = System.Windows.SystemParameters.VirtualScreenLeft;
this.Left = System.Windows.SystemParameters.VirtualScreenLeft;
this.Top = System.Windows.SystemParameters.VirtualScreenTop;
this.Width = System.Windows.SystemParameters.VirtualScreenWidth;
this.Height = System.Windows.SystemParameters.VirtualScreenHeight;
```

### 6.3 准心中心定位

三角洲行动默认使用**主显示器中心**作为准心位置：

```csharp
var primaryScreen = Screen.PrimaryScreen;
double centerX = primaryScreen.Bounds.Left + primaryScreen.Bounds.Width / 2.0 + config.OffsetX;
double centerY = primaryScreen.Bounds.Top + primaryScreen.Bounds.Height / 2.0 + config.OffsetY;
```

### 6.4 防止窗口出现在 Alt+Tab

```csharp
// 让窗口不出现在任务切换列表
SetWindowLong(hwnd, GWL_EXSTYLE,
    GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
```

### 6.5 开机自启动（可选）

```csharp
using var key = Registry.CurrentUser.OpenSubKey(
    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
key.SetValue("CrosshairTool", Process.GetCurrentProcess().MainModule.FileName);
```

---

## 七、NuGet 依赖

```xml
<ItemGroup>
  <!-- 系统托盘（WPF 原生不含 NotifyIcon） -->
  <PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="2.0.1" />

  <!-- 颜色选择器控件 -->
  <PackageReference Include="ColorPicker.WPF" Version="*" />

  <!-- JSON 序列化（.NET 8 内置，无需额外包） -->
</ItemGroup>
```

---

## 八、开发阶段规划

### Phase 1 —— 核心功能（MVP）

- [ ] 创建 WPF 项目，配置透明覆盖层窗口
- [ ] 实现 Win32 点击穿透 + 永远置顶
- [ ] 实现基础十字准心渲染（固定参数）
- [ ] 系统托盘图标 + 右键菜单
- [ ] F8 热键切换显示/隐藏

### Phase 2 —— 配置系统

- [ ] 设计 `CrosshairConfig` 数据模型
- [ ] 实现 JSON 配置读写
- [ ] 基础配置面板 UI（颜色、大小、间隙）
- [ ] 实时预览画布

### Phase 3 —— 完整自定义

- [ ] 完整配置面板（所有参数）
- [ ] 多样式准心（十字/圆/点/组合）
- [ ] 颜色选择器 + 透明度调节
- [ ] 轮廓控制
- [ ] 热键自定义录制

### Phase 4 —— 进阶功能（可选）

- [ ] 预设管理（保存/加载多套配置）
- [ ] 开机自启动选项
- [ ] 多显示器自定义准心位置
- [ ] 准心动画效果（射击时扩散模拟）

---

## 九、给 Cursor 的额外说明

1. **项目类型**：WPF App (.NET 8)，不是 WinForms，不是控制台
2. **Win32 API**：所有 P/Invoke 声明统一放在 `Core/NativeMethods.cs`
3. **单例启动**：`App.xaml.cs` 中检测 Mutex，防止重复启动
4. **MVVM 模式**：SettingsWindow 使用 MVVM，OverlayWindow 可以 code-behind（渲染性能优先）
5. **线程安全**：配置更新必须通过 `Dispatcher.Invoke` 切回 UI 线程再更新渲染
6. **打包**：发布时使用 `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`，生成单个 exe，无需用户安装 .NET
