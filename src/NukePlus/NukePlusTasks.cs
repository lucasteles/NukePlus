using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json;

namespace NukePlus;

public delegate IReadOnlyCollection<Output> DotnetTool<TSettings>(
    Configure<TSettings>? configure = null
) where TSettings : ToolOptions;

public static class NukePlusTasks
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
        string localTool, string[] args, Configure<TSettings>? preConfig) where TSettings : ToolOptions, new() =>
        (Configure<TSettings>? configure = null) =>
        {
            var options = new TSettings()
                .SetProcessToolPath(DotNetPath)
                .SetProcessAdditionalArguments(args);

            if (preConfig is not null)
                options = preConfig.Invoke(options);

            if (configure is not null)
                options = configure.Invoke(options);

            var copy = (TSettings)JsonConvert.DeserializeObject(
                JsonConvert.SerializeObject(options),
                CreateSettingsTypeWithCommand<TSettings>(localTool)
            )!;

            return new CustomDotNetTasks().RunOptions(copy);
        };

    public static DotnetTool<DotnetToolOptions> DotnetLocalTool(
        string localTool, string[] args, Configure<DotnetToolOptions>? preConfig) =>
        DotnetLocalTool<DotnetToolOptions>(localTool, args, preConfig);

    public static DotnetTool<TSettings> DotnetLocalTool<TSettings>(string localTool, params string[] args)
        where TSettings : ToolOptions, new() =>
        DotnetLocalTool<TSettings>(localTool, args, null);

    public static DotnetTool<DotnetToolOptions> DotnetLocalTool(string localTool, params string[] args) =>
        DotnetLocalTool(localTool, args, null);

    class CustomDotNetTasks : DotNetTasks
    {
        public IReadOnlyCollection<Output> RunOptions<T>(T options) where T : ToolOptions, new() =>
            this.Run<T>(options);
    }

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

    static Type CreateSettingsTypeWithCommand<TSettings>(params string[] args)
        => CreateSettingsTypeWithCommand<TSettings>(new CommandAttribute
        {
            Arguments = args.JoinSpace(), Command = null, Type = null
        });

    static Type CreateSettingsTypeWithCommand<TSettings>(CommandAttribute baseAttr)
    {
        var attrType = typeof(CommandAttribute);
        var attrCtor = attrType.GetConstructor(Type.EmptyTypes)!;
        var properties = attrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite).ToArray();

        var propertyValues = properties.Select(p => p.GetValue(baseAttr, null)).ToArray();
        var fields = attrType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var fieldsValues = fields.Select(p => p.GetValue(baseAttr)).ToArray();

        CustomAttributeBuilder attrBuilder = new(
            attrCtor,
            [],
            properties,
            propertyValues,
            fields,
            fieldsValues
        );

        var type = typeof(TSettings);
        var aName = new AssemblyName("NukePlus");
        var tb = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run)
            .DefineDynamicModule(aName.Name!)
            .DefineType(type.Name + "Proxy", TypeAttributes.Public, type);
        tb.SetCustomAttribute(attrBuilder);

        return tb.CreateType();
    }
}
