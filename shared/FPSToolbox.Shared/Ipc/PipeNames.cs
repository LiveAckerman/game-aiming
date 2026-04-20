namespace FPSToolbox.Shared.Ipc;

/// <summary>
/// 标准工具名常量 —— 白名单。
/// </summary>
public static class ToolIds
{
    public const string CrosshairTool = "CrosshairTool";
    public const string GammaTool = "GammaTool";

    public static readonly IReadOnlyList<string> All = new[] { CrosshairTool, GammaTool };

    public static bool IsKnown(string name) => All.Contains(name);
}

/// <summary>
/// Named Pipe 命名工具。Pipe 名称包含 Toolbox 的 PID，避免多登录会话冲突。
/// </summary>
public static class PipeNames
{
    public static string ForToolbox(int toolboxPid) =>
        $"FPSToolbox.IPC.{toolboxPid}";
}
