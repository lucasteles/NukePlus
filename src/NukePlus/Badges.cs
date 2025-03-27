using System.Collections.Frozen;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace NukePlus;

public static class Badges
{
    public static void ForCoverage(AbsolutePath output, AbsolutePath files) =>
        DotnetLocalTools.ReportGenerator(r => r
            .SetReports(files)
            .SetTargetDirectory(output)
            .SetReportTypes(ReportTypes.Badges));

    public static void ForDotNetVersion(AbsolutePath output, GlobalJson globalJson) =>
        DownloadShieldsIo(output / "dotnet_version_badge.svg", ".NET",
            globalJson.Sdk.Version, "blue");

    public static void ForTests(AbsolutePath output, string resultName)
    {
        var (passed, failed, skipped) =
            (NukeBuild.RootDirectory / "tests" / "**" / resultName)
            .GlobFiles()
            .Select(ExtractResults)
            .Aggregate((a, b) => a + b);

        var color =
            (passed, failed, skipped) switch
            {
                (_, > 0, _) => "critical",
                (_, _, > 10) => "orange",
                (0, 0, _) => "yellow",
                _ => "success",
            };
        List<string> messageBuilder = new();
        if (passed > 0) messageBuilder.Add($"{passed} passed");
        if (failed > 0) messageBuilder.Add($"{failed} failed");
        if (skipped > 0) messageBuilder.Add($"{skipped} skipped");
        var message = string.Join(",", messageBuilder);
        DownloadShieldsIo(output / "test_report_badge.svg", "tests", message, color);
    }

    public static void ForLineCount(AbsolutePath output, LocParams locParams)
    {
        var result = CountLinesOfCode(locParams);

        DownloadShieldsIo(output / "lines_of_code.svg", "Lines of Code",
            result.LineCount.ToString(), "blue");

        DownloadShieldsIo(output / "number_of_files.svg", "Code Files",
            result.FileCount.ToString(), "blue");
    }

    public static void DownloadShieldsIo(AbsolutePath fileName,
        string label, string message, string color)
    {
        if (!fileName.Parent.DirectoryExists())
            fileName.Parent.CreateDirectory();

        var url =
            $"https://img.shields.io/badge/{Uri.EscapeDataString($"{label}-{message}-{color}")}";

        HttpTasks.HttpDownloadFile(url, fileName);
    }

    static TestSummary ExtractResults(AbsolutePath testResult)
    {
        var counters =
            XDocument.Load(testResult)
                .XPathSelectElement("//*[local-name() = 'ResultSummary']")
                ?.XPathSelectElement("//*[local-name() = 'Counters']");
        return new(Value("passed"), Value("failed"), Value("total") - Value("executed"));

        int Value(string name) =>
            counters is not null && int.TryParse(counters.Attribute(name)?.Value, out var n)
                ? n
                : 0;
    }

    [Serializable]
    public sealed record LocParams(
        AbsolutePath RootPath,
        string InclusionGlob,
        string? ExclusionGlob = null,
        string? ExcludeDirs = null,
        bool IgnoreBlankLines = true,
        string? IgnoreLinePattern = null
    );

    static (int FileCount, int LineCount) CountLinesOfCode(LocParams p)
    {
        var ignoreDirs =
            p.RootPath.GlobDirectories(ParseGlobs(p.ExcludeDirs)).ToFrozenSet();

        var files =
            p.RootPath
                .GlobFiles(ParseGlobs(p.InclusionGlob))
                .Except(p.RootPath.GlobFiles(ParseGlobs(p.ExclusionGlob)))
                .Where(p => ignoreDirs.All(d => !d.Contains(p)))
                .ToArray();

        var totalLines =
            files
                .SelectMany(f => f.ReadAllLines())
                .Select(l => l.Trim())
                .Count(l =>
                {
                    if (p.IgnoreBlankLines && string.IsNullOrWhiteSpace(l))
                        return false;

                    if (string.IsNullOrWhiteSpace(p.IgnoreLinePattern))
                        return true;

                    return !Regex.IsMatch(l, p.IgnoreLinePattern, RegexOptions.IgnoreCase);
                });

        return (files.Length, totalLines);

        static string[] ParseGlobs(string? globs) =>
            string.IsNullOrWhiteSpace(globs)
                ? []
                : globs
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.StartsWith('/')
                                 || x.StartsWith(".//")
                                 || x.StartsWith('\\') || x.StartsWith(".\\")
                        ? $"{x.TrimStart('\\').TrimStart('/')}"
                        : $"**/{x}"
                    )
                    .ToArray();
    }

    record TestSummary(int Passed, int Failed, int Skipped)
    {
        public static TestSummary operator +(TestSummary s1, TestSummary s2)
            => new(s1.Passed + s2.Passed, s1.Failed + s2.Failed, s1.Skipped + s2.Skipped);
    }
}
