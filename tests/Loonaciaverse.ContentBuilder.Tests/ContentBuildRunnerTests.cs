namespace Loonaciaverse.ContentBuilder.Tests;

public class ContentBuildRunnerTests
{
    [Fact]
    public void RunCollectsPostProjectAndAboutErrorsAndClearsPreviousOutput()
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Path.Combine(directory.Root, "source");
        var options = new BuildOptions(contentDir, Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(contentDir);
        Assert.True(ContentBuildRunner.Run(options).Success);
        var postDir = Directory.CreateDirectory(Path.Combine(contentDir, "posts", "invalid")).FullName;
        File.WriteAllText(Path.Combine(postDir, "index.md"), "---\ndraft: true\n---\nBody");
        ContentTestData.WriteProject(contentDir, "invalid", "draft: true");
        ContentTestData.WriteAbout(contentDir, "stack: [csharp]");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal(12, result.Errors.Count);
        Assert.Equal(4, result.Errors.Count(error => error.StartsWith("posts/invalid/index.md: ")));
        Assert.Equal(5, result.Errors.Count(error => error.StartsWith("projects/invalid/index.md: ")));
        Assert.Equal(3, result.Errors.Count(error => error.StartsWith("about.md: ")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Fact]
    public void RunFailsForMissingContentWithoutDeletingOutput()
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Path.Combine(directory.Root, "missing");
        var outputDir = Path.Combine(directory.Root, "wwwroot");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        var oldFile = Path.Combine(outputDir, "content", "old.json");
        File.WriteAllText(oldFile, "old content");

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, false));

        Assert.False(result.Success);
        Assert.Contains(contentDir, Assert.Single(result.Errors));
        Assert.Equal("old content", File.ReadAllText(oldFile));
    }

    [Fact]
    public void RunRecreatesContentOutputAndPreservesOtherFiles()
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Directory.CreateDirectory(Path.Combine(directory.Root, "source")).FullName;
        var sourceFile = Path.Combine(contentDir, "index.md");
        File.WriteAllText(sourceFile, "source markdown");
        var outputDir = Path.Combine(directory.Root, "wwwroot");
        var contentOutputDir = Path.Combine(outputDir, "content");
        Directory.CreateDirectory(Path.Combine(contentOutputDir, "posts"));
        File.WriteAllText(Path.Combine(contentOutputDir, "old.json"), "{}");
        File.WriteAllText(Path.Combine(contentOutputDir, "posts", "deleted.json"), "{}");
        var otherFile = Path.Combine(outputDir, "index.html");
        File.WriteAllText(otherFile, "keep html");
        Directory.CreateDirectory(Path.Combine(outputDir, "css"));
        var styleFile = Path.Combine(outputDir, "css", "site.css");
        File.WriteAllText(styleFile, "keep css");

        ContentTestData.WriteAbout(contentDir);

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, true));

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.True(Directory.Exists(contentOutputDir));
        Assert.Equal(new[] { "about.json", "posts.json", "projects.json" }, Directory.EnumerateFileSystemEntries(contentOutputDir).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal("[]", File.ReadAllText(Path.Combine(contentOutputDir, "projects.json")));
        Assert.Equal("[]", File.ReadAllText(Path.Combine(contentOutputDir, "posts.json")));
        Assert.Equal("keep html", File.ReadAllText(otherFile));
        Assert.Equal("keep css", File.ReadAllText(styleFile));
        Assert.Equal("source markdown", File.ReadAllText(sourceFile));
    }

    [Fact]
    public void RunCreatesMissingOutputDirectory()
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Directory.CreateDirectory(Path.Combine(directory.Root, "source")).FullName;
        var outputDir = Path.Combine(directory.Root, "wwwroot");

        ContentTestData.WriteAbout(contentDir);

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, false));

        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(outputDir, "content")));
    }
}
