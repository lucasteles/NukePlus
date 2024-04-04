namespace NukePlus;

public delegate IReadOnlyCollection<Output> DotnetTool<TSettings>(
    Configure<TSettings>? configure = null
) where TSettings : ToolSettings;

public static class NukePlus
{
    public static Tool BrowserTool => GetTool(
        Platform switch
        {
            PlatformFamily.Windows => "explorer",
            PlatformFamily.OSX => "open",
            _ => Array.Find(["google-chrome", "firefox",], CommandExists),
        } ?? throw new InvalidOperationException("Unable to find a browser tool"));

    public static void OpenBrowser(AbsolutePath path)
    {
        Assert.FileExists(path);
        try
        {
            BrowserTool($"{path.ToString().DoubleQuoteIfNeeded()}");
        }
        catch (Exception e)
        {
            if (!IsWin) // Windows explorer always return 1
                Log.Error(e, "Unable to open report");
        }
    }

    public static Tool GetTool(string name) =>
        ToolResolver.TryGetEnvironmentTool(name) ??
        ToolResolver.GetPathTool(name);

    public static IProcess RunCommand(string command, params string[] args) =>
        ProcessTasks.StartProcess(command,
            string.Join(" ", args.Select(a => a.DoubleQuoteIfNeeded())),
            NukeBuild.RootDirectory);

    public static bool CommandExists(string command)
    {
        using var process = RunCommand("which", command);
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public static DotnetTool<TSettings> DotnetLocalTool<TSettings>(
        string localTool,
        Configure<Arguments>? args = null,
        Configure<TSettings>? preConfig = null
    ) where TSettings : ToolSettings, new() =>
        (Configure<TSettings>? configure = null) =>
        {
            var toolSettings = new TSettings()
                .SetProcessWorkingDirectory(NukeBuild.RootDirectory)
                .SetProcessToolPath(DotNetPath)
                .SetProcessArgumentConfigurator(procArgs => new Arguments()
                    .Add(localTool).Concatenate(procArgs)
                    .Concatenate(args?.Invoke(new()) ?? new Arguments()));

            if (preConfig is not null) toolSettings = preConfig(toolSettings);
            if (configure is not null) toolSettings = configure(toolSettings);

            using var process = ProcessTasks.StartProcess(toolSettings);
            (toolSettings.ProcessExitHandler ?? ((_, p) => DotNetExitHandler.Invoke(null, p)))
                .Invoke(toolSettings, process.AssertWaitForExit());

            return process.Output;
        };

    public static DotnetTool<DotnetToolSettings> DotnetLocalTool(
        string localTool,
        Configure<Arguments>? args = null,
        Configure<DotnetToolSettings>? preConfig = null
    ) => DotnetLocalTool<DotnetToolSettings>(localTool, args, preConfig);

    public static void UpdateLocalTools() =>
        DotNet($"tool list", logOutput: false)
            .Skip(2)
            .Select(c => c.Text.Split(' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ForEach(line =>
            {
                try
                {
                    if (line is not [{ } tool, { } version, ..]) return;
                    Log.Information("* Updating {Tool}:", tool);
                    var isPre =
                        Array.Exists(["rc", "preview", "beta", "alpha",],
                            version.ToLower().Contains)
                            ? "--prerelease"
                            : string.Empty;
                    DotNet($"tool update {tool} {isPre}");
                }
                catch (Exception e)
                {
                    Log.Warning("Tool Update: {Message}", e.Message);
                }
            });

    public static async Task RunCommandUntil(
        string command,
        IEnumerable<string> args,
        Func<OutputType, string, bool> pred,
        TimeSpan? timeout = null)
    {
        TaskCompletionSource tcs = new();
        var process = ProcessTasks.StartProcess(
            command,
            string.Join(" ", args.Select(a => a.DoubleQuoteIfNeeded())),
            NukeBuild.RootDirectory,
            logger: (t, msg) =>
            {
                if (t == OutputType.Err)
                    Log.Error("{Msg}", msg);
                else
                    Log.Information("{Msg}", msg);

                if (tcs.Task.IsCompleted || !pred(t, msg)) return;
                tcs.SetResult();
            });

        await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromMinutes(1));
        if (process.HasExited) process.AssertZeroExitCode();
    }
}
