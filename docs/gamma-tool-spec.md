# 屏幕调节工具 —— 子工具架构设计文档

> 所属项目：**FPS 工具箱 (FPSToolbox)**  
> 可执行文件：`GammaTool.exe`  
> 目标平台：Windows 10 / 11 (x64)  
> 技术栈：**C# + WPF (.NET 8)**  
> 参考产品：**Gamma Panel 1.0.0** ( http://www.stars.benchmark.pl )

> ⚠️ **重要**：本工具是 FPSToolbox 的子工具，**不能独立安装或独立运行**。  
> 总体架构、安装流程、IPC 协议、用户数据路径见 **`docs/fps-toolbox-spec.md`**。

---

## 一、工具定位

在 Windows 上通过调整显卡的 **Gamma Ramp / LUT**（Look-Up Table）实时调节屏幕的：

- 灰度 (Gamma)
- 亮度 (Brightness)
- 对比度 (Contrast)

支持**红 / 绿 / 蓝三通道独立调节**，支持**多显示器独立调节**，支持**配色方案**保存与热键切换。整体布局与 Gamma Panel 1.0.0 对齐。

### 核心特性

- 实时调节屏幕 Gamma / 亮度 / 对比度
- RGB 三通道独立调节 + "连接"（同步）模式
- LUT 曲线实时预览
- **预览按钮**：点击后才真正把当前调节应用到屏幕，再次点击退出预览恢复原状；保存方案等同于"应用并退出预览"
- 配色方案（预设）管理，支持热键切换
- 多显示器独立调节
- 开机自启时可选自动应用默认方案
- 退出工具时自动把系统 Gamma 恢复为默认，避免屏幕颜色异常

---

## 二、进程形态

### 2.1 启动方式

```
GammaTool.exe --parent-pid <toolbox-pid> --pipe FPSToolbox.IPC.<toolbox-pid>
```

- 缺少 `--parent-pid` → 弹窗"请从 FPS 工具箱启动"并退出
- 全局 Mutex：`Global\FPSToolbox_GammaTool_SingleInstance`
- 启动后连接 IPC，发送 `tool.ready`
- 默认启动时**直接打开 GammaPanelWindow**（与 Gamma Panel 一致）

### 2.2 退出规则

1. 用户在窗口点"退出(E)"
2. 收到 IPC `shutdown`
3. 父进程退出
4. 窗口 × 关闭 → **直接退出进程**（用户确认："退出工具/关闭窗口时就退出程序"）
5. **退出前必须恢复系统 Gamma 为默认**，否则屏幕颜色会留在异常状态

> "隐藏(H)" 按钮的含义是**仅隐藏窗口但保持当前已应用的方案**，不退出进程。窗口 × 与"退出"语义相同（关闭即退出）。

---

## 三、项目结构

```
src/GammaTool/
├── GammaTool.csproj
├── App.xaml / App.xaml.cs
│
├── Core/
│   ├── GammaApplier.cs            # 核心：计算 LUT + 调 SetDeviceGammaRamp
│   ├── LutCalculator.cs           # Gamma / Brightness / Contrast / RGB → 256×3 LUT
│   ├── MonitorEnumerator.cs       # 枚举显示器 + 获取 HDC
│   ├── SchemeManager.cs           # 配色方案（预设）读写
│   ├── HotkeyManager.cs           # 全局热键（方案切换）
│   ├── ParentWatcher.cs
│   ├── GammaRampBackup.cs         # 启动时备份系统默认 Gamma Ramp
│   └── ToolIpcClient.cs
│
├── Models/
│   ├── GammaConfig.cs             # 当前调节参数
│   ├── GammaScheme.cs             # 保存的配色方案
│   └── MonitorInfo.cs
│
├── Windows/
│   ├── GammaPanelWindow.xaml      # 主面板（复刻 Gamma Panel 布局）
│   └── HotkeyCaptureDialog.xaml
│
├── Controls/
│   ├── LutCurveView.xaml          # LUT 曲线预览控件
│   └── ChannelToggle.xaml         # 连接 / 红 / 绿 / 蓝 切换按钮组
│
└── Resources/
    └── icon.ico                   # 由 assets/1.png 转成
```

---

## 四、底层原理：SetDeviceGammaRamp

### 4.1 Win32 API

```csharp
[DllImport("gdi32.dll")]
static extern bool SetDeviceGammaRamp(IntPtr hDC, ref GammaRamp ramp);

[DllImport("gdi32.dll")]
static extern bool GetDeviceGammaRamp(IntPtr hDC, out GammaRamp ramp);

[StructLayout(LayoutKind.Sequential)]
public struct GammaRamp
{
    // 3 通道 × 256 个 16-bit 值
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Red;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Green;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public ushort[] Blue;
}
```

### 4.2 LUT 计算公式

对每个通道 `c ∈ {R, G, B}`，对每个输入值 `i ∈ [0, 255]`：

```
// 归一化 [0,1]
x = i / 255.0

// 1) 对比度 (Contrast, 默认 1.0，范围 0.1~4.0) —— 围绕中点 0.5
x = (x - 0.5) * contrast + 0.5

// 2) 亮度 (Brightness, 默认 0，范围 -1.0~1.0)
x = x + brightness

// 3) 灰度 (Gamma, 默认 1.0，范围 0.1~4.0)
// 注意夹到 [0,1] 避免 pow 负数
x = Clamp(x, 0, 1)
x = Math.Pow(x, 1.0 / gamma)

// 映射到 16-bit
lut[i] = (ushort)(Clamp(x, 0, 1) * 65535)
```

三通道可以分别携带不同的 `gamma / brightness / contrast`（对应 Gamma Panel 中切换"红/绿/蓝"标签页分别调节）；"连接(L)" 模式下三通道共享一组参数。

### 4.3 Windows 10/11 注意事项

Windows 系统默认会**限制 Gamma Ramp 偏离单位曲线的幅度**（`EnableDriverWarning` 之类的机制），表现为极端参数被系统"拉回"。解决方案：

```
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers]
"GammaCorrectBehavior"=dword:00000000          ; 不推荐，影响全局
[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ICM]
"GdiICMGammaRange"=dword:00000100              ; 扩大 Gamma Ramp 允许范围到 256
```

- 首次启动时检测 `GdiICMGammaRange`；若未设置，弹提示对话框询问是否写入（需要管理员，按"需要提权"流程 UAC 一次）
- 用户拒绝则仍可工作，但极端滑块可能被截断，提示用户

### 4.4 多显示器

- 启动时通过 `EnumDisplayMonitors` + `CreateDC("DISPLAY", deviceName, null, null)` 为每个显示器创建独立 HDC
- 用户在面板下拉框选择"所有显示器"或具体某个，之后的调节只应用到被选中的显示器
- 每个显示器维护独立的 `GammaConfig`（在 `GammaScheme.MonitorOverrides` 中保存）

---

## 五、UI 设计（复刻 Gamma Panel 1.0.0）

### 5.1 主面板布局

```
┌──────────────────────────────────────────────────────────────┐
│ 🦋 Gamma Panel                                            ×  │
├──────────────────────────────────────────────────────────────┤
│ ┌─ 颜色调节 ─────────────────────────────────────────────┐   │
│ │                                                        │   │
│ │  灰度:                                        1.00     │   │
│ │  ├──────────────●─────────────┤  ┌────────────────┐   │   │
│ │                                   │ LUT: 16777216   │   │   │
│ │  亮度:                           0%│                 │   │   │
│ │  ├──────────────●─────────────┤  │    (LUT 曲线)   │   │   │
│ │                                   │                 │   │   │
│ │  对比度:                      1.00 │                 │   │   │
│ │  ├──────────────●─────────────┤  └────────────────┘   │   │
│ │                                                        │   │
│ │  [↻ 默认(R)] [连接(L)] [● 红色] [● 绿色] [● 蓝色]      │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                                │
│  配色方案:         热键:                                       │
│  [默认      ▼]    [无       ] [保存(S)] [删除(D)]              │
│                                                                │
│  [退出(E)]   Gamma Panel 1.0.0 · 复刻版 · FPS 工具箱           │
│              [预览(P)]              [隐藏(H)]                  │
└──────────────────────────────────────────────────────────────┘
```

### 5.2 控件清单

| 区域 | 控件 | 说明 |
|------|------|------|
| 灰度滑块 | Slider + 数值输入框 | 0.1 ~ 4.0，步进 0.01，默认 1.00 |
| 亮度滑块 | Slider + 百分比输入 | -100% ~ +100%，步进 1%，默认 0% |
| 对比度滑块 | Slider + 数值输入框 | 0.1 ~ 4.0，步进 0.01，默认 1.00 |
| LUT 预览 | `LutCurveView` 自绘 | 实时绘制当前通道 LUT 曲线 |
| 默认(R) | Button | 把当前通道重置为 gamma=1 / bright=0 / contrast=1 |
| 连接(L) | ToggleButton | 按下：三通道共享参数；弹起：可分别选 RGB |
| 红/绿/蓝 | RadioButton 组 | 连接模式下禁用；独立模式下切换当前编辑的通道 |
| 配色方案下拉 | ComboBox | 列出 `SchemeManager.GetAll()` |
| 热键 | 只读输入 + 点击录制 | 该方案绑定的全局热键 |
| 保存(S) | Button | 把当前参数保存为配色方案；**同时应用并退出预览** |
| 删除(D) | Button | 删除当前选中的方案 |
| 预览(P) / 退出预览 | ToggleButton（文字切换） | 详见 §6 |
| 隐藏(H) | Button | 仅隐藏窗口，不退出进程，不改变 Gamma 应用状态 |
| 退出(E) | Button | 关闭窗口 = 退出进程（并恢复系统 Gamma） |

### 5.3 显示器选择

在"配色方案"一行左侧补一个 `ComboBox`：

```
显示器:  [主显示器 (\\.\DISPLAY1 - 2560×1440) ▼]
         [所有显示器]
         [主显示器 ...]
         [副显示器 ...]
```

所有调节与显示/预览应用都作用于当前选中的目标。

---

## 六、预览按钮逻辑（用户显式确认）

### 6.1 四种工作状态

| 状态 | 滑块调整是否影响屏幕 | LUT 曲线预览 | 按钮文字 |
|------|---------------------|---------------|----------|
| **未预览（编辑中）** | ❌ 不影响屏幕 | ✅ 实时更新 | `预览(P)` |
| **预览中** | ✅ 实时应用到屏幕 | ✅ 实时更新 | `退出预览` |
| **点击"退出预览"** | 恢复预览前的系统 Gamma | 仍展示当前参数 | `预览(P)` |
| **点击"保存"** | 把当前参数应用到屏幕并持久化；自动退出预览状态（按钮回到 `预览(P)`） | 保持展示 | `预览(P)` |

### 6.2 状态机

```
                   [编辑中]
                   /       \
   点击 预览(P)  /          \  点击 保存(S)
               ▼            ▼
         [预览中]       应用 + 持久化
          /   \             │
点击退出 /     \ 点击保存    ▼
预览    /       \         [编辑中]
       ▼         ▼
   [编辑中]   应用 + 持久化
                  │
                  ▼
              [编辑中]
```

### 6.3 实现细节

- `GammaApplier` 维护三个 LUT 句柄：
  - `_systemDefaultLut`：进程启动时 `GetDeviceGammaRamp` 备份的原始值
  - `_currentAppliedLut`：当前实际写入显卡的 LUT
  - `_editingLut`：面板里用户正在编辑的 LUT（仅用于 LUT 曲线预览，不写入显卡）

- **未预览**：`_editingLut` 随滑块变化，`LutCurveView` 绑定它；`_currentAppliedLut == _systemDefaultLut`（或上次保存应用的方案）

- **进入预览**：`_currentAppliedLut := _editingLut`，并 `SetDeviceGammaRamp`
- **预览中调整**：每次滑块变化 → 重算 `_editingLut` → `_currentAppliedLut := _editingLut` → `SetDeviceGammaRamp`
- **退出预览**：`_currentAppliedLut := _previousAppliedLut`（进入预览前的快照） → `SetDeviceGammaRamp`
- **保存**：`SchemeManager.Save(scheme)` + `_currentAppliedLut := _editingLut` + `SetDeviceGammaRamp` + 按钮回到 `预览(P)`

- **进程退出**（任何原因）：`SetDeviceGammaRamp(_systemDefaultLut)` 恢复系统默认

### 6.4 IPC 事件

- 进入 / 退出预览：`gamma.previewState` → `{ previewing: bool }`
- 保存并应用：`gamma.schemeApplied` → `{ schemeName: string }`

---

## 七、数据模型

### 7.1 GammaConfig —— 单通道参数

```csharp
public class ChannelParams
{
    public double Gamma      { get; set; } = 1.0;   // 0.1 ~ 4.0
    public double Brightness { get; set; } = 0.0;   // -1.0 ~ 1.0
    public double Contrast   { get; set; } = 1.0;   // 0.1 ~ 4.0
}

public class GammaConfig
{
    public bool Linked { get; set; } = true;        // 连接(L) 模式
    public ChannelParams All   { get; set; } = new();  // Linked=true 时使用
    public ChannelParams Red   { get; set; } = new();
    public ChannelParams Green { get; set; } = new();
    public ChannelParams Blue  { get; set; } = new();
}
```

### 7.2 GammaScheme —— 配色方案

```csharp
public class GammaScheme
{
    public string Name { get; set; } = "默认";
    public string? Hotkey { get; set; }                            // 例："Ctrl+Shift+1"

    // 显示器级别参数；key = 显示器 DeviceName，"*" 表示应用于所有显示器
    public Dictionary<string, GammaConfig> MonitorConfigs { get; set; } = new();
}
```

### 7.3 存储路径

```
%AppData%\FPSToolbox\GammaTool\
├── config.json              # { currentSchemeName, selectedMonitor, uiState }
└── presets\
    ├── 默认.json             # GammaScheme
    ├── 护眼.json
    ├── 游戏-暗场.json
    └── ...
```

---

## 八、GammaApplier 关键实现

```csharp
public class GammaApplier : IDisposable
{
    private readonly Dictionary<string, IntPtr> _monitorHdcs = new(); // DeviceName -> HDC
    private readonly Dictionary<string, GammaRamp> _systemDefaults = new();
    private readonly Dictionary<string, GammaRamp> _currentApplied = new();

    public void Initialize()
    {
        foreach (var m in MonitorEnumerator.EnumAll())
        {
            var hdc = CreateDC("DISPLAY", m.DeviceName, null, IntPtr.Zero);
            _monitorHdcs[m.DeviceName] = hdc;

            GetDeviceGammaRamp(hdc, out var original);
            _systemDefaults[m.DeviceName] = original;
            _currentApplied[m.DeviceName] = original;
        }
    }

    public void Apply(string deviceName, GammaConfig config)
    {
        var ramp = LutCalculator.Build(config);
        SetDeviceGammaRamp(_monitorHdcs[deviceName], ref ramp);
        _currentApplied[deviceName] = ramp;
    }

    public void ApplyAll(GammaConfig config)
    {
        foreach (var dn in _monitorHdcs.Keys) Apply(dn, config);
    }

    public void RestoreSystemDefaults()
    {
        foreach (var (dn, hdc) in _monitorHdcs)
        {
            var ramp = _systemDefaults[dn];
            SetDeviceGammaRamp(hdc, ref ramp);
        }
    }

    public void Dispose()
    {
        RestoreSystemDefaults();
        foreach (var hdc in _monitorHdcs.Values) DeleteDC(hdc);
    }
}
```

`LutCalculator.Build` 严格按 §4.2 公式生成三通道 256 项的 `GammaRamp`。

---

## 九、IPC 接入

### 9.1 接收来自 Toolbox 的指令

| Action | 行为 |
|--------|------|
| `gamma.openPanel` | 显示 `GammaPanelWindow`（若隐藏） |
| `gamma.applyPreset` | `payload: { name }` → 加载方案并应用到屏幕，退出预览状态 |
| `gamma.listSchemes` | 返回所有方案名 + 热键 |
| `gamma.resetSystem` | `GammaApplier.RestoreSystemDefaults()` |
| `shutdown` | 恢复系统 Gamma → 关闭窗口 → 退出进程 |
| `ping` | 回复 `pong` |

### 9.2 主动上报

| Topic | 场景 |
|-------|------|
| `gamma.previewState` | 预览按钮状态变化 |
| `gamma.schemeApplied` | 保存或切换方案 |
| `gamma.schemesChanged` | 新建 / 删除方案 |
| `tool.ready` / `tool.exiting` | 生命周期 |

Toolbox 收到 `gamma.schemesChanged` 后刷新托盘菜单"应用预设 ▶"子菜单。

---

## 十、全局热键

- 每个 `GammaScheme.Hotkey` 在进程内注册一次，触发时切换到该方案并**立刻应用**（不进入预览）
- 热键录制复用 Crosshair 的 `HotkeyCaptureDialog` 组件（移动到 `FPSToolbox.Shared`）
- 进程退出时统一注销

---

## 十一、LUT 曲线预览控件（LutCurveView）

- 自绘 `FrameworkElement`，200×200 左右
- 背景黑色 `#000`，边框灰
- 绘制内容：
  - 对角线辅助（灰色，45°）
  - 三条曲线：红 / 绿 / 蓝，分别使用该通道的 LUT 数据 `lut[i] → (i, 255 - lut[i]/257)`
  - 左上角文字：`LUT: <uniqueColorCount>`，与原 Gamma Panel 一致（统计 `(R<<16)|(G<<8)|B` 的去重数量）
- 连接模式下三条曲线重合；独立模式下显示差异

---

## 十二、关键流程清单

### 启动
1. 解析 `--parent-pid` / `--pipe`（缺失则退出）
2. 连接 IPC → `tool.ready`
3. `GammaApplier.Initialize()`（备份系统默认 Gamma）
4. 加载 `config.json`，确定上次选中的方案和显示器
5. **不自动应用**任何方案，保持系统默认状态（仅在面板里展示参数）
6. 显示 `GammaPanelWindow`，按钮文字 `预览(P)`

### 用户调整滑块（未预览）
1. 更新 `GammaConfig`
2. `LutCalculator.Build` → 刷新 `LutCurveView`
3. **不调用** `SetDeviceGammaRamp`

### 点击预览
1. 当前 `_currentApplied` 做快照 `_previewSnapshot`
2. `GammaApplier.Apply(...)` 实际写入显卡
3. 按钮文字改为 `退出预览`
4. 发 `gamma.previewState { previewing: true }`

### 预览中继续调整
1. `GammaApplier.Apply(...)` 实时刷新

### 点击退出预览
1. 把 `_previewSnapshot` 写回显卡
2. 按钮文字改为 `预览(P)`
3. 发 `gamma.previewState { previewing: false }`

### 点击保存
1. `SchemeManager.Save(name, currentConfig)`
2. `GammaApplier.Apply(...)` 确保最新参数写入显卡
3. 按钮文字改为 `预览(P)`（自动退出预览状态）
4. 发 `gamma.schemeApplied` + `gamma.previewState { previewing: false }`

### 关闭窗口 / 点击退出
1. `GammaApplier.Dispose()`（内部 `RestoreSystemDefaults`）
2. 注销热键 / 断开 IPC → 发 `tool.exiting`
3. 进程退出

---

## 十三、开发阶段规划

### Phase 1 —— 核心引擎
- [ ] `NativeMethods`：`GetDeviceGammaRamp` / `SetDeviceGammaRamp` / `CreateDC` / `DeleteDC` / `EnumDisplayMonitors`
- [ ] `LutCalculator`：公式 + 单元测试（边界值：gamma=0.1 / 4.0，bright=±1，contrast=0.1 / 4.0）
- [ ] `GammaApplier`：多显示器初始化、Apply、恢复
- [ ] Windows 10/11 `GdiICMGammaRange` 检测 + 提示

### Phase 2 —— UI 主面板
- [ ] GammaPanelWindow 布局（复刻 Gamma Panel）
- [ ] 三个滑块 + 数值输入 + 双向绑定
- [ ] "默认 / 连接 / 红 / 绿 / 蓝" 按钮组
- [ ] 显示器下拉 + `MonitorEnumerator`
- [ ] LutCurveView 自绘

### Phase 3 —— 预览 & 方案
- [ ] 预览按钮状态机
- [ ] SchemeManager 读写
- [ ] 配色方案下拉 / 保存 / 删除
- [ ] 热键录制 + 注册

### Phase 4 —— 集成
- [ ] IPC 客户端接入
- [ ] ParentWatcher
- [ ] 单例 Mutex
- [ ] 退出时恢复系统 Gamma（含异常退出：AppDomain.UnhandledException / ProcessExit）

### Phase 5 —— 打磨
- [ ] 多显示器实机测试
- [ ] 崩溃日志 `%AppData%\FPSToolbox\logs\GammaTool-yyyyMMdd.log`
- [ ] HDR 显示器兼容性说明（HDR 开启时 Gamma Ramp 可能被系统忽略）

---

## 十四、集成测试清单

- [ ] Toolbox 启动 GammaTool → 面板弹出，系统 Gamma 未变化
- [ ] 拖动滑块 → LUT 曲线立刻变化，屏幕不变
- [ ] 点击"预览" → 屏幕立刻变化，按钮变"退出预览"
- [ ] 预览中调整 → 屏幕实时变化
- [ ] 点击"退出预览" → 屏幕恢复到预览前状态
- [ ] 点击"保存" → 屏幕保持当前效果，按钮回到"预览"，方案出现在下拉列表
- [ ] 关闭窗口 → 进程退出，屏幕颜色恢复系统默认
- [ ] 热键切换方案 → 屏幕立刻应用新方案
- [ ] kill Toolbox → 10s 内 GammaTool 自退 + 恢复屏幕
- [ ] 多显示器：仅对选中显示器生效
- [ ] 直接双击 `GammaTool.exe` → 弹提示并退出
- [ ] Windows 10/11 未写注册表时：极端 Gamma（0.1 / 4.0）应提示用户并尽量生效

---

## 十五、依赖

```xml
<ItemGroup>
  <ProjectReference Include="..\..\shared\FPSToolbox.Shared\FPSToolbox.Shared.csproj" />
  <!-- JSON：.NET 8 内置 -->
  <!-- 不需要 NotifyIcon.Wpf（子工具无托盘） -->
</ItemGroup>
```
