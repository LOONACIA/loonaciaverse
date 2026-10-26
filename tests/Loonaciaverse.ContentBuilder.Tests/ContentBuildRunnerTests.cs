namespace Loonaciaverse.ContentBuilder.Tests;

public class ContentBuildRunnerTests
{
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

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, true));

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.True(Directory.Exists(contentOutputDir));
        Assert.Empty(Directory.EnumerateFileSystemEntries(contentOutputDir));
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

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, false));

        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(outputDir, "content")));
    }
}
