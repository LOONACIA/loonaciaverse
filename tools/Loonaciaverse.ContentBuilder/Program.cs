namespace Loonaciaverse.ContentBuilder;

public static class Program
{
    public static int Main(string[] args)
    {
        string? contentDir = null;
        string? outputDir = null;
        var includeDrafts = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--content" when contentDir is null && HasValue(args, index):
                    contentDir = args[++index];
                    break;
                case "--output" when outputDir is null && HasValue(args, index):
                    outputDir = args[++index];
                    break;
                case "--include-drafts" when !includeDrafts:
                    includeDrafts = true;
                    break;
                default:
                    return Usage();
            }
        }

        if (contentDir is null || outputDir is null)
        {
            return Usage();
        }

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, includeDrafts));
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine(error.ReplaceLineEndings(" "));
        }

        return result.Success ? 0 : 1;
    }

    private static bool HasValue(string[] args, int index)
    {
        return index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]) && !args[index + 1].StartsWith("--", StringComparison.Ordinal);
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: Loonaciaverse.ContentBuilder --content <dir> --output <dir> [--include-drafts]");
        return 2;
    }
}
