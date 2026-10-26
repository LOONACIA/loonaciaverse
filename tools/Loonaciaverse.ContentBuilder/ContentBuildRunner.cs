namespace Loonaciaverse.ContentBuilder;

public static class ContentBuildRunner
{
    public static BuildResult Run(BuildOptions options)
    {
        if (!Directory.Exists(options.ContentDir))
        {
            return new BuildResult([$"{options.ContentDir}: Content directory does not exist.", .. SeoBuilder.ClearOutput(options.OutputDir)]);
        }

        string? contentOutputDir = null;
        var errors = new List<string>();
        try
        {
            contentOutputDir = Path.Combine(Path.GetFullPath(options.OutputDir), "content");
            if (Directory.Exists(contentOutputDir))
            {
                Directory.Delete(contentOutputDir, recursive: true);
            }

            Directory.CreateDirectory(contentOutputDir);
            errors.AddRange(PostBuilder.Build(options.ContentDir, contentOutputDir, options.IncludeDrafts).Errors);
            errors.AddRange(ProjectBuilder.Build(options.ContentDir, contentOutputDir, options.IncludeDrafts).Errors);
            errors.AddRange(AboutBuilder.Build(options.ContentDir, contentOutputDir).Errors);
            errors.AddRange(UniverseBuilder.Build(options.ContentDir, contentOutputDir, generateOutput: errors.Count == 0).Errors);
            if (errors.Count == 0)
            {
                SeoBuilder.Build(options.OutputDir);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            errors.Add($"{options.OutputDir}: {exception.Message}");
        }

        if (errors.Count > 0 && contentOutputDir is not null)
        {
            try
            {
                if (Directory.Exists(contentOutputDir))
                {
                    Directory.Delete(contentOutputDir, recursive: true);
                }

                Directory.CreateDirectory(contentOutputDir);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errors.Add($"{contentOutputDir}: Could not clear failed build output: {exception.Message}");
            }
        }

        if (errors.Count > 0)
        {
            errors.AddRange(SeoBuilder.ClearOutput(options.OutputDir));
        }

        return new BuildResult(errors);
    }
}
