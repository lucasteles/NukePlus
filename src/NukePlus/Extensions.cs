// ReSharper disable once CheckNamespace

#pragma warning disable S3903

public static class Extensions
{
    public static T UseDotnetLocalTool<T>(this T tool, Solution solution,
        string localTool, Func<Arguments, Arguments> args)
        where T : ToolSettings => UseDotnetLocalTool(tool, solution, localTool, args(new()));

    public static T UseDotnetLocalTool<T>(this T tool, Solution solution,
        string localTool = "", Arguments? arguments = null)
        where T : ToolSettings => tool
        .SetProcessWorkingDirectory(solution.Directory)
        .SetProcessToolPath(DotNetPath)
        .SetProcessArgumentConfigurator(
            args => new Arguments().Add(localTool).Concatenate(args)
                .Concatenate(arguments ?? new()));

    public static Project? FindProject(this Solution sln, string name) =>
        sln.AllProjects.SingleOrDefault(
            x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
}
