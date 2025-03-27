using System.Collections.Frozen;
using System.Globalization;
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
        var (fileCount, lineCount) = CountLinesOfCode(locParams);

        var lineCountText = lineCount.ToString("N0", CultureInfo.InvariantCulture);
        var fileCountText = fileCount.ToString("N0", CultureInfo.InvariantCulture);

        Log.Information("Code File Count: {FileCountText}", fileCountText);
        Log.Information("Lines of Code: {LineCountText}", lineCount);

        DownloadShieldsIo(
            output / "lines_of_code.svg",
            "Lines of Code",
            lineCountText,
            "blue"
        );

        DownloadShieldsIo(
            output / "number_of_files.svg",
            "Code Files",
            fileCountText,
            "blue"
        );
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
        string InclusionGlob,
        AbsolutePath? RootPath = null,
        string? ExclusionGlob = null,
        string? ExcludeDirs = null,
        string? IgnoreLinePattern = null,
        bool IgnoreBlankLines = true
    );

    static (int fileCount, int lineCount) CountLinesOfCode(LocParams p)
    {
        var rootPath = (p.RootPath ?? NukeBuild.RootDirectory);

        if (!string.IsNullOrWhiteSpace(rootPath.Extension))
            rootPath = rootPath.Parent ?? throw new InvalidOperationException($"Invalid directory path: {p.RootPath}");

        var ignoreDirs =
            rootPath.GlobDirectories(ParseGlobs(p.ExcludeDirs)).ToFrozenSet();

        var files =
            rootPath
                .GlobFiles(ParseGlobs(p.InclusionGlob))
                .Except(rootPath.GlobFiles(ParseGlobs(p.ExclusionGlob)))
                .Where(path => ignoreDirs.All(dir => !dir.Contains(path)))
                .ToArray();

        var totalLines =
            files
                .SelectMany(file => file.ReadAllLines())
                .Select(line => line.Trim())
                .Count(line =>
                {
                    if (p.IgnoreBlankLines && string.IsNullOrWhiteSpace(line))
                        return false;

                    if (string.IsNullOrWhiteSpace(p.IgnoreLinePattern))
                        return true;

                    return !Regex.IsMatch(line, p.IgnoreLinePattern, RegexOptions.IgnoreCase);
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
