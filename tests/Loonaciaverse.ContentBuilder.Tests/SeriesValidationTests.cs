using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Loonaciaverse.ContentBuilder.Tests;

public class SeriesValidationTests
{
    private const string SeriesYaml = "series:\n  - slug: example\n    title: Example series\n    summary: A series summary";
    private const string PostMetadata = "title: Hello\nslug: post\ndate: 2026-10-26\nsummary: A post summary";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RunRejectsUnknownSeriesIncludingExcludedDrafts(bool draft, bool hasCatalog)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        if (hasCatalog)
        {
            ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
            WritePost(options, "known", "example");
        }

        WritePost(options, "post", "unknown", draft);
        foreach (var includeDrafts in new[] { false, true })
        {
            var result = ContentBuildRunner.Run(options with { IncludeDrafts = includeDrafts });

            Assert.False(result.Success);
            Assert.Equal("posts/post/index.md: series 'unknown' is not defined in series.yml.", Assert.Single(result.Errors));
            AssertEmptyOutput(options);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("other: ignored")]
    [InlineData("series: []")]
    public void RunRejectsReferencesWhenCatalogHasNoDefinitions(string yaml)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, yaml);
        WritePost(options, "post", "example");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal("posts/post/index.md: series 'example' is not defined in series.yml.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("Example")]
    [InlineData("")]
    [InlineData("   ")]
    public void RunRequiresAnExactOrdinalSeriesReference(string reference)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        WritePost(options, "known", "example");
        WritePost(options, "post", reference);

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal($"posts/post/index.md: series '{reference}' is not defined in series.yml.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("slug", "example")]
    [InlineData("title", "Example series")]
    [InlineData("summary", "A series summary")]
    public void RunRejectsMissingEmptyAndWhitespaceRequiredFieldsWithoutFalseReferenceErrors(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        if (field != "slug")
        {
            WritePost(options, "post", "example");
        }

        foreach (var replacement in new[] { "", $"{field}:", $"{field}: null", $"{field}: ''", $"{field}: '   '" })
        {
            ContentTestData.WriteSeries(options.ContentDir, SeriesYaml.Replace($"{field}: {value}", replacement));

            var result = ContentBuildRunner.Run(options);

            Assert.False(result.Success);
            Assert.Equal($"series.yml: series[0]: {field} is required.", Assert.Single(result.Errors));
            AssertEmptyOutput(options);
        }
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("under_score")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("../escape")]
    [InlineData("two words")]
    public void RunRejectsInvalidSeriesSlugsWithoutFalseReferenceErrors(string slug)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml.Replace("slug: example", $"slug: '{slug}'"));
        WritePost(options, "post", slug);

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal($"series.yml: series[0]: slug '{slug}' must use lowercase letters, digits, and single hyphens between segments.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunRejectsDuplicateSeriesSlugsWithBothDefinitionPositions()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml + "\n  - slug: example\n    title: Duplicate\n    summary: Duplicate summary");
        WritePost(options, "post", "example");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal("series.yml: series[1]: Duplicate slug 'example' also used in series.yml: series[0].", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("~")]
    public void RunRejectsNullEntriesThatAreNotMappings(string entry)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, $"{SeriesYaml}\n  - {entry}");
        WritePost(options, "post", "example");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal("series.yml: series[1]: Series must be a mapping.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunRejectsEveryUnusedSeriesOnceAtItsFirstDefinitionPosition()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir,
            SeriesYaml + "\n  - slug: unused\n    title: Unused\n    summary: Unused summary\n  - slug: unused\n    title: Duplicate\n    summary: Duplicate");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal(new[]
        {
            "series.yml: series[2]: Duplicate slug 'unused' also used in series.yml: series[1].",
            "series.yml: series[0]: series 'example' is not referenced by any post.",
            "series.yml: series[1]: series 'unused' is not referenced by any post."
        }, result.Errors);
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunAcceptsSeriesReferencedOnlyByAnExcludedDraft()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        WritePost(options, "post", "example", draft: true);

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "content", "posts", "post.json")));
        Assert.False(Directory.Exists(Path.Combine(options.OutputDir, "content", "series")));
    }

    [Fact]
    public void RunReportsOnlyTheYamlErrorWhenSeriesCannotBeRead()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        const string yaml = "series: [unclosed";
        ContentTestData.WriteSeries(options.ContentDir, yaml);
        WritePost(options, "post", "example");
        var exception = Assert.ThrowsAny<YamlException>(() => new DeserializerBuilder().Build().Deserialize<object>(yaml));

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal($"series.yml: Invalid YAML: {exception.Message}", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Fact]
    public void RunAllowsTheSameSlugForASeriesAndAPost()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        WritePost(options, "example", "example");

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.True(File.Exists(Path.Combine(options.OutputDir, "content", "posts", "example.json")));
        Assert.True(File.Exists(Path.Combine(options.OutputDir, "content", "series", "example.json")));
    }

    [Fact]
    public void RunCollectsSeriesAndPostErrorsAndClearsPreviousOutput()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        WritePost(options, "post", "example");
        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.True(File.Exists(Path.Combine(options.OutputDir, "content", "series", "example.json")));
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml.Replace("title: Example series", "title: ''") + "\n  - null\n  - slug: unused\n    title: Unused\n    summary: Unused");
        ContentTestData.WritePost(options.ContentDir, "post", PostMetadata.Replace("summary: A post summary", "summary: ''") + "\nseries: example");
        WritePost(options, "unknown", "unknown", draft: true);
        ContentTestData.WritePost(options.ContentDir, "malformed", "tags: [unclosed");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal(6, result.Errors.Count);
        Assert.Contains("series.yml: series[0]: title is required.", result.Errors);
        Assert.Contains("series.yml: series[1]: Series must be a mapping.", result.Errors);
        Assert.Contains("series.yml: series[2]: series 'unused' is not referenced by any post.", result.Errors);
        Assert.Contains("posts/post/index.md: summary is required.", result.Errors);
        Assert.Contains("posts/unknown/index.md: series 'unknown' is not defined in series.yml.", result.Errors);
        Assert.StartsWith("posts/malformed/index.md: ", Assert.Single(result.Errors, error => error.StartsWith("posts/malformed/")));
        AssertEmptyOutput(options);
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var contentDir = Path.Combine(directory.Root, "source");
        ContentTestData.WriteAbout(contentDir);
        ContentTestData.WriteUniverse(contentDir);
        return new BuildOptions(contentDir, Path.Combine(directory.Root, "wwwroot"), false);
    }

    private static void WritePost(BuildOptions options, string slug, string series, bool draft = false)
    {
        var metadata = PostMetadata.Replace("slug: post", $"slug: {slug}") + $"\nseries: '{series}'\ndraft: {draft.ToString().ToLowerInvariant()}";
        ContentTestData.WritePost(options.ContentDir, slug, metadata);
    }

    private static void AssertEmptyOutput(BuildOptions options)
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "sitemap.xml")));
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "feed.xml")));
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "robots.txt")));
    }
}
