using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FPSToolbox.Core;
using FPSToolbox.Models;
using FPSToolbox.Shared.Ipc;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace FPSToolbox;

public partial class MainWindow : Window
{
    private readonly ToolManager _toolManager;
    private readonly ToolRegistry _registry;
    private readonly Models.ToolboxSettings _settings;
    private readonly Action _saveSettings;
    private readonly Dictionary<string, Border> _cards = new();
    private readonly UpdateChecker _updateChecker;
    private UpdateCheckResult? _latestUpdate;

    public bool AllowClose { get; set; }

    public MainWindow(ToolManager tm, ToolRegistry registry,
        Models.ToolboxSettings settings, Action saveSettings)
    {
        InitializeComponent();
        _toolManager = tm;
        _registry = registry;
        _settings = settings;
        _saveSettings = saveSettings;
        _updateChecker = new UpdateChecker(_registry);

        var ver = GetToolboxVersion();
        TxtToolboxVersion.Text = "v" + ver;
        TxtFooterVersion.Text = "版本 v" + ver;
        ChkAutoStart.IsChecked = _settings.AutoStart;
        ChkMinToTray.IsChecked = _settings.MinimizeToTrayOnClose;

        BuildCards();
        _toolManager.StateChanged += name => Dispatcher.Invoke(() => RefreshCard(name));

        Closing += (_, e) =>
        {
            if (!AllowClose && _settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
            }
        };

        // 启动后异步检查一次(3 秒后再查,避免拖慢 UI 首帧)
        Loaded += async (_, _) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await CheckUpdatesAsync(silent: true);
        };
    }

    private static string GetToolboxVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    // ──────────────────────────────────────────────────────────────
    // 更新检查
    // ──────────────────────────────────────────────────────────────

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdate.IsEnabled = false;
        BtnCheckUpdate.Content = "🔄 检查中...";
        try
        {
            await CheckUpdatesAsync(silent: false);
        }
        finally
        {
            BtnCheckUpdate.IsEnabled = true;
            BtnCheckUpdate.Content = "🔄 检查更新";
        }
    }

    private async Task CheckUpdatesAsync(bool silent)
    {
        _latestUpdate = await _updateChecker.CheckAsync();
        Dispatcher.Invoke(() =>
        {
            ApplyUpdateBadges();
            foreach (var desc in ToolRegistry.AllDescriptors)
                RefreshCard(desc.Name);
        });

        if (!silent && _latestUpdate != null)
        {
            if (!string.IsNullOrEmpty(_latestUpdate.ErrorMessage))
            {
                MessageBox.Show(this, $"检查更新失败：{_latestUpdate.ErrorMessage}",
                    "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!_latestUpdate.Toolbox.HasUpdate
                && !_latestUpdate.Crosshair.HasUpdate
                && !_latestUpdate.Gamma.HasUpdate)
            {
                MessageBox.Show(this, "当前已是最新版本。",
                    "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ApplyUpdateBadges()
    {
        if (_latestUpdate?.Toolbox is { HasUpdate: true } tb
            && !string.IsNullOrEmpty(tb.LatestVersion))
        {
            TxtNewToolboxVersion.Text = $"🆕 v{tb.LatestVersion} 可用";
            ToolboxUpdateBadge.Visibility = Visibility.Visible;
            BtnUpdateToolbox.Visibility = Visibility.Visible;
        }
        else
        {
            ToolboxUpdateBadge.Visibility = Visibility.Collapsed;
            BtnUpdateToolbox.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnUpdateToolboxClicked(object sender, RoutedEventArgs e)
    {
        var tb = _latestUpdate?.Toolbox;
        if (tb == null || !tb.HasUpdate) return;

        // 先问用户在线/离线
        var url = tb.ToolboxOnlineInstallerUrl ?? tb.ToolboxOfflineInstallerUrl;
        if (string.IsNullOrEmpty(url))
        {
            MessageBox.Show(this,
                "Release 里没有找到安装包。请到 GitHub 下载：\n" + UpdateConstants.ReleasesWebUrl,
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var notes = string.IsNullOrWhiteSpace(tb.ReleaseNotes) ? "(无)" : tb.ReleaseNotes;
        var msg = $"发现新版本：FPS 工具箱 v{tb.LatestVersion}\n" +
                  $"当前版本：v{tb.CurrentVersion}\n\n" +
                  $"更新内容:\n{notes}\n\n" +
                  $"现在下载并安装？(安装过程中工具箱会自动关闭重启)";
        var result = MessageBox.Show(this, msg, "FPS 工具箱",
            MessageBoxButton.OKCancel, MessageBoxImage.Information);
        if (result != MessageBoxResult.OK) return;

        BtnUpdateToolbox.IsEnabled = false;
        BtnUpdateToolbox.Content = "下载中...";
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(),
                $"FPSToolbox_Setup_{Guid.NewGuid():N}.exe");
            await ToolDownloader.DownloadRawAsync(url, tmp,
                new Progress<(long received, long? total)>(p =>
                {
                    var mb = p.received / 1024.0 / 1024.0;
                    BtnUpdateToolbox.Content = $"下载中 {mb:F1} MB";
                }));

            // 启动 installer,自己退出
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tmp,
                UseShellExecute = true,
            });
            AllowClose = true;
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"下载失败：{ex.Message}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnUpdateToolbox.IsEnabled = true;
            BtnUpdateToolbox.Content = "立即更新";
        }
    }

    private void BuildCards()
    {
        CardsPanel.Children.Clear();
        _cards.Clear();
        foreach (var desc in ToolRegistry.AllDescriptors)
        {
            var card = BuildCard(desc);
            _cards[desc.Name] = card;
            CardsPanel.Children.Add(card);
        }
        // 末尾加一张"从本地安装"卡片(备用:网盘失效/内网/手动分发 zip 时使用)
        CardsPanel.Children.Add(BuildInstallCard());

        foreach (var desc in ToolRegistry.AllDescriptors)
            RefreshCard(desc.Name);
    }

    private Border BuildInstallCard()
    {
        var border = new Border
        {
            Width = 300,
            Height = 200,
            Margin = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = "＋ 从本地安装工具",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "选择本地的 .zip 安装包\n（网盘失效 / 离线分发时使用）",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 14),
        });
        var btn = new Button { Content = "选择 .zip", Width = 120, HorizontalAlignment = HorizontalAlignment.Center };
        btn.Click += OnInstallZipClicked;
        stack.Children.Add(btn);
        border.Child = stack;
        return border;
    }

    private void OnInstallZipClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择工具安装包",
            Filter = "工具包 (*.zip)|*.zip",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var installed = ToolPackageInstaller.InstallFromZip(dlg.FileName,
                AppDomain.CurrentDomain.BaseDirectory);
            _registry.Upsert(installed);
            MessageBox.Show(this,
                $"已成功安装：{installed.Name} v{installed.Version}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
            foreach (var desc in ToolRegistry.AllDescriptors)
                RefreshCard(desc.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"安装失败：{ex.Message}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Border BuildCard(ToolDescriptor desc)
    {
        var border = new Border
        {
            Width = 300,
            Height = 200,
            Margin = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Tag = desc.Name,
        };

        var stack = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = desc.DisplayName,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);
        var version = new TextBlock
        {
            Tag = "version",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
        };
        Grid.SetColumn(version, 1);
        header.Children.Add(version);
        var desc2 = new TextBlock
        {
            Text = desc.Description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var status = new TextBlock
        {
            Name = "StatusText",
            FontSize = 12,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 10, 0, 0),
            Tag = "status"
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 10, 0, 0),
            Tag = "buttons"
        };

        stack.Children.Add(header);
        stack.Children.Add(desc2);
        stack.Children.Add(status);
        stack.Children.Add(buttons);
        border.Child = stack;
        return border;
    }

    private ComponentUpdateInfo? GetUpdateInfoFor(string toolId)
    {
        if (_latestUpdate == null) return null;
        return toolId switch
        {
            ToolIds.CrosshairTool => _latestUpdate.Crosshair,
            ToolIds.GammaTool => _latestUpdate.Gamma,
            _ => null,
        };
    }

    private void RefreshCard(string name)
    {
        if (!_cards.TryGetValue(name, out var card)) return;
        var info = _toolManager.Get(name);
        var stack = (StackPanel)card.Child;
        var header = stack.Children.OfType<Grid>().FirstOrDefault();
        var versionText = header?.Children.OfType<TextBlock>()
            .FirstOrDefault(t => (t.Tag as string) == "version");
        var statusText = stack.Children.OfType<TextBlock>().FirstOrDefault(t => (t.Tag as string) == "status");
        var buttons = stack.Children.OfType<StackPanel>().FirstOrDefault(s => (s.Tag as string) == "buttons");
        var updateInfo = GetUpdateInfoFor(name);
        var installed = _registry.Get(name);

        if (versionText != null)
        {
            if (installed != null)
                versionText.Text = "v" + installed.Version;
            else if (!string.IsNullOrEmpty(updateInfo?.LatestVersion))
                versionText.Text = "最新 v" + updateInfo.LatestVersion;
            else
                versionText.Text = "";
        }

        if (statusText != null)
        {
            statusText.Text = info.State switch
            {
                ToolRuntimeState.NotInstalled => "○ 未安装",
                ToolRuntimeState.Installed => "● 已安装",
                ToolRuntimeState.Starting => "● 启动中...",
                ToolRuntimeState.Running => "✓ 运行中",
                ToolRuntimeState.Disconnected => "✗ 失联",
                _ => ""
            };
            statusText.Foreground = info.State switch
            {
                ToolRuntimeState.Running => Brushes.LightGreen,
                ToolRuntimeState.NotInstalled => Brushes.Gray,
                ToolRuntimeState.Disconnected => Brushes.OrangeRed,
                _ => Brushes.LightGray,
            };
        }

        if (buttons != null)
        {
            buttons.Children.Clear();
            switch (info.State)
            {
                case ToolRuntimeState.NotInstalled:
                    var install = new Button { Content = "下载并安装" };
                    install.Click += async (_, _) => await OnDownloadAndInstallAsync(name, statusText, install);
                    buttons.Children.Add(install);
                    break;

                case ToolRuntimeState.Installed:
                    var start = new Button { Content = "启动" };
                    start.Click += (_, _) => _toolManager.Start(name);
                    buttons.Children.Add(start);

                    if (updateInfo?.HasUpdate == true)
                    {
                        var updateBtn = new Button
                        {
                            Content = $"更新到 v{updateInfo.LatestVersion}",
                            Style = (Style)FindResource("SecondaryButton")
                        };
                        updateBtn.Click += async (_, _) =>
                            await OnUpdateToolAsync(name, statusText, updateBtn);
                        buttons.Children.Add(updateBtn);
                    }
                    break;

                case ToolRuntimeState.Starting:
                case ToolRuntimeState.Running:
                    var openSettings = new Button
                    {
                        Content = name == ToolIds.GammaTool ? "打开面板" : "打开设置"
                    };
                    openSettings.Click += async (_, _) =>
                    {
                        var session = _toolManager.Get(name).Session;
                        if (session != null)
                        {
                            var action = name == ToolIds.GammaTool
                                ? IpcActions.GammaOpenPanel
                                : IpcActions.CrosshairOpenSettings;
                            try { await session.SendRequestAsync(action, timeout: TimeSpan.FromSeconds(2)); }
                            catch { }
                        }
                    };
                    buttons.Children.Add(openSettings);

                    var stop = new Button
                    {
                        Content = "停止",
                        Style = (Style)FindResource("DangerButton")
                    };
                    stop.Click += async (_, _) => await _toolManager.StopAsync(name);
                    buttons.Children.Add(stop);

                    if (updateInfo?.HasUpdate == true)
                    {
                        var updateBtn2 = new Button
                        {
                            Content = $"更新 v{updateInfo.LatestVersion}",
                            Style = (Style)FindResource("SecondaryButton")
                        };
                        updateBtn2.Click += async (_, _) =>
                            await OnUpdateToolAsync(name, statusText, updateBtn2);
                        buttons.Children.Add(updateBtn2);
                    }
                    break;

                case ToolRuntimeState.Disconnected:
                    var restart = new Button { Content = "重启" };
                    restart.Click += async (_, _) =>
                    {
                        await _toolManager.StopAsync(name);
                        _toolManager.Start(name);
                    };
                    buttons.Children.Add(restart);
                    break;
            }
        }
    }

    private async Task OnDownloadAndInstallAsync(string name, TextBlock? statusText, Button button)
    {
        // 没查过 GitHub API → 先同步查一次(避免启动 3 秒内就走错误 fallback)
        if (_latestUpdate == null)
        {
            if (statusText != null)
            {
                statusText.Text = "正在查询最新版本...";
                statusText.Foreground = Brushes.LightSkyBlue;
            }
            await CheckUpdatesAsync(silent: true);
        }

        // 优先使用 GitHub 最新版;没查到再回退到 ToolRegistry.DownloadUrl
        var url = GetUpdateInfoFor(name)?.ToolZipUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var desc = ToolRegistry.GetDescriptor(name);
            url = desc?.DownloadUrl;
        }
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(this,
                $"未找到 {name} 的下载链接。\n\n请检查网络后再试,或到 GitHub Releases 页手动下载:\n{UpdateConstants.ReleasesWebUrl}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await PerformInstallAsync(name, url, statusText, button, "下载中...");
    }

    /// <summary>已安装状态下升级到最新版(先停止正在运行的进程,再替换文件)</summary>
    private async Task OnUpdateToolAsync(string name, TextBlock? statusText, Button button)
    {
        var upd = GetUpdateInfoFor(name);
        if (upd?.ToolZipUrl == null) return;

        var notes = string.IsNullOrWhiteSpace(upd.ReleaseNotes) ? "(无)" : upd.ReleaseNotes;
        var msg = $"{upd.DisplayName} 新版本：v{upd.LatestVersion}\n" +
                  $"当前版本：v{upd.CurrentVersion}\n\n" +
                  $"更新内容:\n{notes}\n\n" +
                  $"立即下载并安装？(正在运行的工具会被停止)";
        if (MessageBox.Show(this, msg, "FPS 工具箱",
                MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            return;

        // 先停止正在运行的实例,避免文件被占用
        if (_toolManager.Get(name).State is ToolRuntimeState.Running or ToolRuntimeState.Starting)
            await _toolManager.StopAsync(name);

        await PerformInstallAsync(name, upd.ToolZipUrl, statusText, button, "更新中...");
    }

    private async Task PerformInstallAsync(string name, string url,
        TextBlock? statusText, Button button, string busyLabel)
    {
        button.IsEnabled = false;
        var origContent = button.Content;
        try
        {
            var progress = new Progress<(long received, long? total)>(p =>
            {
                if (statusText == null) return;
                var mb = p.received / 1024.0 / 1024.0;
                if (p.total.HasValue && p.total.Value > 0)
                {
                    var totalMb = p.total.Value / 1024.0 / 1024.0;
                    var pct = p.received * 100.0 / p.total.Value;
                    statusText.Text = $"↓ 下载中 {pct:F1}%  ({mb:F2} / {totalMb:F2} MB)";
                }
                else
                {
                    statusText.Text = $"↓ 下载中  {mb:F2} MB";
                }
                statusText.Foreground = Brushes.LightSkyBlue;
            });

            button.Content = busyLabel;
            var installed = await ToolDownloader.DownloadAndInstallAsync(
                url, AppDomain.CurrentDomain.BaseDirectory, progress);

            _registry.Upsert(installed);
            MessageBox.Show(this,
                $"{installed.Name} v{installed.Version} 安装完成",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Information);
            foreach (var d in ToolRegistry.AllDescriptors)
                RefreshCard(d.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"下载或安装失败：{ex.Message}",
                "FPS 工具箱", MessageBoxButton.OK, MessageBoxImage.Error);
            button.IsEnabled = true;
            button.Content = origContent;
            RefreshCard(name);
        }
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        _settings.AutoStart = ChkAutoStart.IsChecked == true;
        AutoStartRegistry.Apply(_settings.AutoStart);
        _saveSettings();
    }

    private void OnMinToTrayChanged(object sender, RoutedEventArgs e)
    {
        _settings.MinimizeToTrayOnClose = ChkMinToTray.IsChecked == true;
        _saveSettings();
    }
}
