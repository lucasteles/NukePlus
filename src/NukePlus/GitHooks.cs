namespace NukePlus;

public static class GitHooks
{
    const string HookScript =
        """
        #!/bin/sh
        #!/bin/sh
        ECHO 'Running pre-commit for C# files'

        # Find all changed files for this commit
        # Compute the diff only once to save a small amount of time.
        CHANGED_FILES=$(git diff --name-only --cached --diff-filter=ACMR)
        # Get only changed files that match our file suffix pattern
        get_pattern_files() {
          pattern=$(echo "$*"| sed "s/ /\$\\\|/g")
          echo "$CHANGED_FILES" | { grep "$pattern$" || true; }
        }
        # Get all changed csharp files
        CSHARP_FILES=$(get_pattern_files .cs)

        if [[ -n "$CSHARP_FILES" ]]
        then
            dotnet format -v normal --include $CSHARP_FILES
            # Add back the modified files to staging
            echo "$CSHARP_FILES" | xargs git add
        fi
        """;

    static AbsolutePath HooksPath => NukeBuild.RootDirectory / ".git" / "hooks" / "pre-commit";

    public static void InstallFormatPreCommit()
    {
        if (HooksPath.FileExists()) HooksPath.DeleteFile();
        HooksPath.WriteAllText(HookScript);
    }
}
