using static NukePlus.NukePlus;

namespace NukePlus;

public static class DotnetLocalTools
{
    public static class DocFX
    {
        public const string DocFXSourceBranchName = "DOCFX_SOURCE_BRANCH_NAME";

        public static readonly DotnetTool Build =
            DotnetLocalTool("docfx");

        public static readonly DotnetTool Serve =
            DotnetLocalTool("docfx", args => args.Add("--serve").Add("--open-browser"));
    }

    public static readonly DotnetTool<ReportGeneratorSettings> ReportGenerator =
        DotnetLocalTool<ReportGeneratorSettings>("reportgenerator");
}
