// ReSharper disable once CheckNamespace

#pragma warning disable S3903

public static class Extensions
{
    public static T UseDotnetLocalTool<T>(this T tool,
        string localTool = "", params string[] arguments)
        where T : ToolOptions =>
        UseDotnetLocalTool(tool, null, localTool, arguments);

    public static T UseDotnetLocalTool<T>(
        this T tool,
        AbsolutePath? path,
        string localTool = "",
        params string[] arguments
    ) where T : ToolOptions =>
        tool
            .SetProcessWorkingDirectory(path ?? NukeBuild.RootDirectory)
            .SetProcessToolPath(DotNetPath)
            .SetProcessAdditionalArguments(arguments)
            .SetProcessFirstArguments(localTool);

    public static T SetProcessFirstArguments<T>(this T @this, params string[] preArgs) where T : ToolOptions => @this
        .Modify(baseOptions =>
        {
            if (baseOptions is not T options)
                throw new InvalidOperationException($"Invalid option type: ${baseOptions.GetType().Name}");

            string[] newArgs =
            [
                ..preArgs,
                .. options.ProcessAdditionalArguments ?? [],
            ];

            baseOptions.Set(() => options.ProcessAdditionalArguments, newArgs);
        });

    public static Project? FindProject(this Solution sln, string name) =>
        sln.AllProjects.SingleOrDefault(
            x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
}
