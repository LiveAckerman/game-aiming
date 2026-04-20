namespace CrosshairTool.Core;

/// <summary>
/// 解析子工具启动参数：--parent-pid N --pipe NAME
/// </summary>
public sealed record StartupArgs(int? ParentPid, string? PipeName)
{
    public bool IsValid => ParentPid.HasValue && !string.IsNullOrEmpty(PipeName);

    public static StartupArgs Parse(string[] args)
    {
        int? pid = null;
        string? pipe = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--parent-pid" && int.TryParse(args[i + 1], out var p)) pid = p;
            else if (args[i] == "--pipe") pipe = args[i + 1];
        }
        return new StartupArgs(pid, pipe);
    }
}
