using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CrosshairTool.Core;
using CrosshairTool.Models;
using CrosshairTool.Rendering;
using AppCtx = CrosshairTool.Core.AppContext;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CrosshairTool.Windows;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;
    private CrosshairConfig _editingConfig = new();
    private readonly CrosshairRenderer _renderer = new();
    private bool _isLoading;

    public SettingsWindow(ConfigManager configManager)
    {
        _isLoading = true;          // 阻止 InitializeComponent 期间控件事件触发
        InitializeComponent();
        _isLoading = false;
        _configManager = configManager;
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _editingConfig = AppCtx.Instance.Config.Clone();
        LoadConfigToUI();
        LoadPresetList();
        UpdatePreview();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 关闭时自动保存
        ApplyAndSave();
    }

    #region UI 加载

    private void LoadConfigToUI()
    {
        _isLoading = true;
        try
        {
            // 样式
            CmbStyle.SelectedIndex = (int)_editingConfig.Style;

            // 颜色
            TxtColor.Text = _editingConfig.Color;
            TxtOutlineColor.Text = _editingConfig.OutlineColor;
            TxtDotColor.Text = _editingConfig.DotColor;
            UpdateColorPreview(RectColorPreview, _editingConfig.Color);
            UpdateColorPreview(RectOutlinePreview, _editingConfig.OutlineColor);
            UpdateColorPreview(RectDotPreview, _editingConfig.DotColor);

            // 透明度
            SliderOpacity.Value = _editingConfig.Opacity;
            TxtOpacityVal.Text = $"{(int)(_editingConfig.Opacity * 100)}%";

            // 尺寸
            SliderSize.Value = _editingConfig.Size;
            TxtSize.Text = _editingConfig.Size.ToString();

            SliderThickness.Value = _editingConfig.Thickness;
            TxtThickness.Text = _editingConfig.Thickness.ToString();

            SliderGap.Value = _editingConfig.Gap;
            TxtGap.Text = _editingConfig.Gap.ToString();

            SliderDotSize.Value = _editingConfig.DotSize;
            TxtDotSize.Text = _editingConfig.DotSize.ToString();

            // 开关
            ChkTopLine.IsChecked = _editingConfig.TopLineEnabled;
            ChkOutline.IsChecked = _editingConfig.OutlineEnabled;
            ChkDot.IsChecked = _editingConfig.DotEnabled;

            // 偏移
            TxtOffsetX.Text = _editingConfig.OffsetX.ToString();
            TxtOffsetY.Text = _editingConfig.OffsetY.ToString();

            // 热键
            TxtHotkey.Text = _editingConfig.ToggleHotkey;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadPresetList()
    {
        var presets = _configManager.GetPresetNames();
        CmbPresets.Items.Clear();
        foreach (var name in presets)
            CmbPresets.Items.Add(name);
        CmbPresets.Text = _editingConfig.PresetName;
    }

    #endregion

    #region 实时预览

    private void UpdatePreview()
    {
        PreviewCanvas.Children.Clear();
        var center = new Point(100, 100);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            _renderer.Draw(dc, _editingConfig, center);
        }

        if (visual.Drawing == null) return;

        var image = new Image
        {
            Width = 200,
            Height = 200,
            Source = new DrawingImage(visual.Drawing)
        };
        PreviewCanvas.Children.Add(image);
    }

    private void NotifyConfigChanged()
    {
        if (_isLoading) return;

        // 同步到全局状态并触发 OverlayWindow 重绘
        AppCtx.Instance.Config = _editingConfig.Clone();
        AppCtx.Instance.NotifyConfigChanged();
        UpdatePreview();
    }

    #endregion

    #region 事件处理

    private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || CmbStyle.SelectedIndex < 0) return;
        _editingConfig.Style = (CrosshairStyle)CmbStyle.SelectedIndex;
        NotifyConfigChanged();
    }

    private void OnColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        _editingConfig.Color = TxtColor.Text.Trim();
        UpdateColorPreview(RectColorPreview, _editingConfig.Color);
        NotifyConfigChanged();
    }

    private void OnOutlineColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        _editingConfig.OutlineColor = TxtOutlineColor.Text.Trim();
        UpdateColorPreview(RectOutlinePreview, _editingConfig.OutlineColor);
        NotifyConfigChanged();
    }

    private void OnDotColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        _editingConfig.DotColor = TxtDotColor.Text.Trim();
        UpdateColorPreview(RectDotPreview, _editingConfig.DotColor);
        NotifyConfigChanged();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _editingConfig.Opacity = SliderOpacity.Value;
        TxtOpacityVal.Text = $"{(int)(SliderOpacity.Value * 100)}%";
        NotifyConfigChanged();
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _editingConfig.Size = (int)SliderSize.Value;
        _isLoading = true;
        TxtSize.Text = _editingConfig.Size.ToString();
        _isLoading = false;
        NotifyConfigChanged();
    }

    private void OnSizeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (int.TryParse(TxtSize.Text, out int val) && val >= 1 && val <= 50)
        {
            _editingConfig.Size = val;
            _isLoading = true;
            SliderSize.Value = val;
            _isLoading = false;
            NotifyConfigChanged();
        }
    }

    private void OnThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _editingConfig.Thickness = (int)SliderThickness.Value;
        _isLoading = true;
        TxtThickness.Text = _editingConfig.Thickness.ToString();
        _isLoading = false;
        NotifyConfigChanged();
    }

    private void OnThicknessTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (int.TryParse(TxtThickness.Text, out int val) && val >= 1 && val <= 10)
        {
            _editingConfig.Thickness = val;
            _isLoading = true;
            SliderThickness.Value = val;
            _isLoading = false;
            NotifyConfigChanged();
        }
    }

    private void OnGapChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _editingConfig.Gap = (int)SliderGap.Value;
        _isLoading = true;
        TxtGap.Text = _editingConfig.Gap.ToString();
        _isLoading = false;
        NotifyConfigChanged();
    }

    private void OnGapTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (int.TryParse(TxtGap.Text, out int val) && val >= 0 && val <= 20)
        {
            _editingConfig.Gap = val;
            _isLoading = true;
            SliderGap.Value = val;
            _isLoading = false;
            NotifyConfigChanged();
        }
    }

    private void OnDotSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading) return;
        _editingConfig.DotSize = (int)SliderDotSize.Value;
        _isLoading = true;
        TxtDotSize.Text = _editingConfig.DotSize.ToString();
        _isLoading = false;
        NotifyConfigChanged();
    }

    private void OnDotSizeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (int.TryParse(TxtDotSize.Text, out int val) && val >= 1 && val <= 10)
        {
            _editingConfig.DotSize = val;
            _isLoading = true;
            SliderDotSize.Value = val;
            _isLoading = false;
            NotifyConfigChanged();
        }
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _editingConfig.TopLineEnabled = ChkTopLine.IsChecked == true;
        _editingConfig.OutlineEnabled = ChkOutline.IsChecked == true;
        _editingConfig.DotEnabled = ChkDot.IsChecked == true;
        NotifyConfigChanged();
    }

    private void OnOffsetChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        if (int.TryParse(TxtOffsetX.Text, out int ox))
            _editingConfig.OffsetX = Math.Clamp(ox, -100, 100);
        if (int.TryParse(TxtOffsetY.Text, out int oy))
            _editingConfig.OffsetY = Math.Clamp(oy, -100, 100);
        NotifyConfigChanged();
    }

    #endregion

    #region 热键录制

    private void OnHotkeyBoxFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = "按下热键...";
        TxtHotkey.Foreground = new SolidColorBrush(Colors.Yellow);
    }

    private void OnHotkeyBoxLostFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = _editingConfig.ToggleHotkey;
        TxtHotkey.Foreground = new SolidColorBrush(Colors.White);
    }

    private void OnHotkeyCapture(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 忽略纯修饰键
        if (key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LWin || key == Key.RWin)
            return;

        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());

        _editingConfig.ToggleHotkey = string.Join("+", parts);
        TxtHotkey.Text = _editingConfig.ToggleHotkey;
        TxtHotkey.Foreground = new SolidColorBrush(Colors.White);

        // 通知热键更新（App.xaml.cs 会重新注册）
        NotifyConfigChanged();
        TxtStatus.Text = $"热键已更新：{_editingConfig.ToggleHotkey}（重启后生效）";
        Keyboard.ClearFocus();
    }

    #endregion

    #region 快速颜色

    private void OnQuickColor(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color)
        {
            TxtColor.Text = color;
        }
    }

    #endregion

    #region 预设管理

    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || CmbPresets.SelectedItem == null) return;
        var name = CmbPresets.SelectedItem.ToString() ?? "";
        var preset = _configManager.LoadPreset(name);
        if (preset != null)
        {
            _editingConfig = preset;
            LoadConfigToUI();
            NotifyConfigChanged();
            TxtStatus.Text = $"已加载预设：{name}";
        }
    }

    private void OnSavePreset(object sender, RoutedEventArgs e)
    {
        var name = CmbPresets.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            TxtStatus.Text = "请输入预设名称";
            return;
        }

        _editingConfig.PresetName = name;
        _configManager.SavePreset(name, _editingConfig);
        LoadPresetList();
        CmbPresets.Text = name;
        TxtStatus.Text = $"预设已保存：{name}";
    }

    private void OnDeletePreset(object sender, RoutedEventArgs e)
    {
        var name = CmbPresets.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        _configManager.DeletePreset(name);
        LoadPresetList();
        TxtStatus.Text = $"预设已删除：{name}";
    }

    #endregion

    #region 底部按钮

    private void OnResetDefault(object sender, RoutedEventArgs e)
    {
        _editingConfig = new CrosshairConfig();
        LoadConfigToUI();
        NotifyConfigChanged();
        TxtStatus.Text = "已重置为默认配置";
    }

    private void OnSaveAndClose(object sender, RoutedEventArgs e)
    {
        ApplyAndSave();
        Close();
    }

    private void ApplyAndSave()
    {
        AppCtx.Instance.Config = _editingConfig.Clone();
        AppCtx.Instance.NotifyConfigChanged();
        _configManager.Save(_editingConfig);
    }

    #endregion

    #region 辅助方法

    private static void UpdateColorPreview(Rectangle rect, string colorHex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            rect.Fill = new SolidColorBrush(color);
        }
        catch
        {
            rect.Fill = new SolidColorBrush(Colors.Transparent);
        }
    }

    #endregion
}
