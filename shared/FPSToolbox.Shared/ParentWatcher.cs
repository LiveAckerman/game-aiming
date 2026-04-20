using System.Diagnostics;
using System.Windows.Threading;

namespace FPSToolbox.Shared;

/// <summary>
/// 子工具监控父进程（Toolbox）。父进程退出后，本进程自退。
/// </summary>
public sealed class ParentWatcher : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Process? _parent;
    private readonly Action _onParentExited;
    private bool _fired;

    public ParentWatcher(int parentPid, Action onParentExited)
    {
        _onParentExited = onParentExited;
        try { _parent = Process.GetProcessById(parentPid); }
        catch { _parent = null; }

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        if (_parent == null) { Fire(); return; }
        _timer.Start();
    }

    private void Check()
    {
        try
        {
            if (_parent == null || _parent.HasExited) Fire();
        }
        catch { Fire(); }
    }

    private void Fire()
    {
        if (_fired) return;
        _fired = true;
        _timer.Stop();
        try { _onParentExited(); } catch { }
    }

    public void Dispose() => _timer.Stop();
}
