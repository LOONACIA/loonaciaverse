using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class PostValidationTests
{
    private const string ValidMetadata = "title: Hello\nslug: example\ndate: 2026-10-26\nsummary: A summary\ntags: [dotnet, blazor-wasm]";

    [Theory]
    [InlineData("title", "Hello")]
    [InlineData("slug", "example")]
    [InlineData("date", "2026-10-26")]
    [InlineData("summary", "A summary")]
    public void RunRejectsMissingAndEmptyRequiredFields(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var replacement in new[] { "", $"{field}:", $"{field}: null" })
        {
            WritePost(options, "example", ValidMetadata.Replace($"{field}: {value}", replacement));

            var result = ContentBuildRunner.Run(options);

            Assert.False(result.Success);
            Assert.Equal($"posts/example/index.md: {field} is required.", Assert.Single(result.Errors));
            AssertEmptyOutput(options);
        }
    }

    [Theory]
    [InlineData("title", "Hello")]
    [InlineData("slug", "example")]
    [InlineData("summary", "A summary")]
    public void RunRejectsWhitespaceRequiredFields(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata.Replace($"{field}: {value}", $"{field}: '   '"));

        var result = ContentBuildRunner.Run(options);

        Assert.Equal($"posts/example/index.md: {field} is required.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("under_score")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("../escape")]
    [InlineData("example\\n")]
    public void RunRejectsInvalidSlugFormat(string slug)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata.Replace("slug: example", $"slug: \"{slug}\""));

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.StartsWith("posts/example/index.md: slug '") && error.Contains("must use lowercase"));
        AssertEmptyOutput(options);
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "escape.json")));
    }

    [Fact]
    public void RunRejectsSlugThatDoesNotMatchFolder()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "another-folder", ValidMetadata);

        var result = ContentBuildRunner.Run(options);

        Assert.Equal("posts/another-folder/index.md: slug 'example' must match folder name 'another-folder'.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunRejectsDuplicateSlugsIncludingExcludedDraftsWithBothPaths()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata);
        WritePost(options, "other", ValidMetadata + "\ndraft: true");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        var duplicateError = Assert.Single(result.Errors, error => error.Contains("Duplicate slug"));
        Assert.StartsWith("posts/other/index.md: ", duplicateError);
        Assert.Contains("posts/example/index.md", duplicateError);
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("under_score")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("'two words'")]
    [InlineData("''")]
    [InlineData("null")]
    public void RunRejectsInvalidTags(string tag)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata.Replace("[dotnet, blazor-wasm]", $"[{tag}]"));

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.StartsWith("posts/example/index.md: tag '", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("2026-10-25", false)]
    [InlineData("2026-10-26", true)]
    [InlineData("2026-10-27", true)]
    public void RunRequiresUpdatedOnOrAfterDate(string updated, bool success)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata + $"\nupdated: {updated}");

        var result = ContentBuildRunner.Run(options);

        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal("posts/example/index.md: updated must be on or after date.", Assert.Single(result.Errors));
            AssertEmptyOutput(options);
        }
    }

    [Theory]
    [InlineData("# No front matter")]
    [InlineData("---\ntitle: Unclosed")]
    [InlineData("---\ntags: [unclosed\n---\nBody")]
    [InlineData("---\ndate: not-a-date\n---\nBody")]
    [InlineData("---\nupdated: not-a-date\n---\nBody")]
    [InlineData("---\n---\nBody")]
    public void RunReturnsFrontMatterParsingErrors(string markdown)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata);
        File.WriteAllText(Path.Combine(options.ContentDir, "posts", "example", "index.md"), markdown);

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.StartsWith("posts/example/index.md: ", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunCollectsAllErrorsAndRemovesPreviousOutputWithoutWritingPartialResults()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", ValidMetadata);
        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.True(File.Exists(Path.Combine(options.OutputDir, "content", "posts", "example.json")));
        WritePost(options, "invalid", "slug: BAD\ndate: 2026-10-26\nupdated: 2026-10-25\ntags: [Bad, also_bad]\ndraft: true");
        WritePost(options, "malformed", "tags: [unclosed");
        WritePost(options, "missing-fields", "draft: true");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal(12, result.Errors.Count);
        Assert.Equal(7, result.Errors.Count(error => error.StartsWith("posts/invalid/index.md: ")));
        Assert.Equal(4, result.Errors.Count(error => error.StartsWith("posts/missing-fields/index.md: ")));
        Assert.Single(result.Errors, error => error.StartsWith("posts/malformed/index.md: "));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunFiltersDraftIndexAndDetailAndPreservesDraftFlag(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = includeDrafts };
        WritePost(options, "example", ValidMetadata);
        WritePost(options, "draft-post", ValidMetadata.Replace("slug: example", "slug: draft-post") + "\ndraft: true");
        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = true }).Success);

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        var contentOutputDir = Path.Combine(options.OutputDir, "content");
        var posts = JsonSerializer.Deserialize<PostSummary[]>(File.ReadAllText(Path.Combine(contentOutputDir, "posts.json")), ContentJson.Options)!;
        Assert.False(posts.Single(post => post.Slug == "example").Draft);
        Assert.Equal(includeDrafts ? 2 : 1, posts.Length);
        var draftPath = Path.Combine(contentOutputDir, "posts", "draft-post.json");
        Assert.Equal(includeDrafts, File.Exists(draftPath));
        if (includeDrafts)
        {
            Assert.True(posts.Single(post => post.Slug == "draft-post").Draft);
            var detail = JsonSerializer.Deserialize<PostDetail>(File.ReadAllText(draftPath), ContentJson.Options)!;
            Assert.True(detail.Meta.Draft);
        }
    }

    [Fact]
    public void ProgramReportsEveryValidationErrorToStandardErrorAndExitsWithOne()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        WritePost(options, "example", "draft: true");
        var previousError = Console.Error;
        using var errorOutput = new StringWriter();
        try
        {
            Console.SetError(errorOutput);

            var exitCode = Program.Main(["--content", options.ContentDir, "--output", options.OutputDir]);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Console.SetError(previousError);
        }

        var errors = errorOutput.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, errors.Length);
        Assert.All(errors, error => Assert.StartsWith("posts/example/index.md: ", error));
        AssertEmptyOutput(options);
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        return new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
    }

    private static void WritePost(BuildOptions options, string folder, string metadata)
    {
        var postDir = Directory.CreateDirectory(Path.Combine(options.ContentDir, "posts", folder)).FullName;
        File.WriteAllText(Path.Combine(postDir, "index.md"), $"---\n{metadata}\n---\nBody");
    }

    private static void AssertEmptyOutput(BuildOptions options)
    {
        var contentOutputDir = Path.Combine(options.OutputDir, "content");
        Assert.False(File.Exists(Path.Combine(contentOutputDir, "posts.json")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(contentOutputDir));
    }
}
