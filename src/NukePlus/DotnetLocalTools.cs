using static NukePlus.NukePlusTasks;

namespace NukePlus;

public static class DotnetLocalTools
{
    public static class DocFX
    {
        public const string DocFXSourceBranchName = "DOCFX_SOURCE_BRANCH_NAME";

        public static readonly DotnetTool Build = DotnetLocalTool("docfx");

        public static readonly DotnetTool Serve = DotnetLocalTool("docfx", "--serve", "--open-browser");
    }

    public static readonly DotnetTool<ReportGeneratorSettings> ReportGenerator =
        DotnetLocalTool<ReportGeneratorSettings>("reportgenerator");
}
