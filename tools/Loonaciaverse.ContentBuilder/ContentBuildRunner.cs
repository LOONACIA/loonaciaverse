namespace Loonaciaverse.ContentBuilder;

public static class ContentBuildRunner
{
    public static BuildResult Run(BuildOptions options)
    {
        if (!Directory.Exists(options.ContentDir))
        {
            return new BuildResult([$"{options.ContentDir}: Content directory does not exist."]);
        }

        try
        {
            var contentOutputDir = Path.Combine(Path.GetFullPath(options.OutputDir), "content");
            if (Directory.Exists(contentOutputDir))
            {
                Directory.Delete(contentOutputDir, recursive: true);
            }

            Directory.CreateDirectory(contentOutputDir);
            PostBuilder.Build(options.ContentDir, contentOutputDir);
            return new BuildResult([]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new BuildResult([$"{options.OutputDir}: {exception.Message}"]);
        }
    }
}
