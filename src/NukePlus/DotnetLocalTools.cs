using Nuke.Common.Tools.DocFX;
using static NukePlus.NukePlus;

namespace NukePlus;

public static class DotnetLocalTools
{
    public static class DocFX
    {
        public const string DocFXSourceBranch = "DOCFX_SOURCE_BRANCH_NAME";

        public static readonly DotnetTool<DocFXBuildSettings> Build =
            DotnetLocalTool<DocFXBuildSettings>("docfx");

        public static readonly DotnetTool<DotNetBuildSettings> Serve =
            DotnetLocalTool("docfx", args => args.Add("--serve").Add("--open-browser"));
    }

    public static readonly DotnetTool<ReportGeneratorSettings> ReportGenerator =
        DotnetLocalTool<ReportGeneratorSettings>("reportgenerator");
}
