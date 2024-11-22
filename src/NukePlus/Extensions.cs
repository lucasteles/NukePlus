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
        Solution? solution,
        string localTool = "",
        params string[] arguments
    ) where T : ToolOptions =>
        tool
            .SetProcessWorkingDirectory(solution?.Directory ?? NukeBuild.RootDirectory)
            .SetProcessToolPath(DotNetPath)
            .Modify(baseOptions =>
            {
                if (baseOptions is not T options)
                    throw new InvalidOperationException($"Invalid option type: ${baseOptions.GetType().Name}");

                string[] args =
                [
                    localTool,
                    .. options.ProcessAdditionalArguments ?? [],
                    .. arguments
                ];

                baseOptions.Set(() => options.ProcessAdditionalArguments, args);
            });

    public static Project? FindProject(this Solution sln, string name) =>
        sln.AllProjects.SingleOrDefault(
            x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
}
