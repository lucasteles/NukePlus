// ReSharper disable once CheckNamespace

#pragma warning disable S3903

public static class NukePlusExtensions
{
    extension<T>(T tool) where T : ToolOptions
    {
        public T UseDotnetLocalTool(string localTool = "", params string[] arguments) =>
            tool.UseDotnetLocalTool(null, localTool, arguments);

        public T UseDotnetLocalTool(AbsolutePath? path, string localTool = "", params string[] arguments) =>
            tool
                .SetProcessWorkingDirectory(path ?? NukeBuild.RootDirectory)
                .SetProcessToolPath(DotNetPath)
                .SetProcessAdditionalArguments(arguments)
                .SetProcessFirstArguments(localTool!);

        public T SetProcessFirstArguments(params string[] preArgs) => tool.Modify(baseOptions =>
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
    }

    public static Project? FindProject(this Solution sln, string name) =>
        sln.AllProjects.SingleOrDefault(x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));

    extension(Project project)
    {
        public bool IsCSharpProject() =>
            project.FileName?.EndsWith(".csproj", StringComparison.CurrentCultureIgnoreCase) is true;

        public bool IsFSharpProject() =>
            project.FileName?.EndsWith(".fsproj", StringComparison.CurrentCultureIgnoreCase) is true;
    }
}
