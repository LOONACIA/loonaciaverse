using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class ProjectBuilderTests
{
    private const string Metadata = "title: Example\nslug: example\nsummary: A summary\nperiod: 2026 - present\nrole: Developer";

    [Fact]
    public void RunSortsByFeaturedThenOrderThenTitleAndMapsMetadataAndHtml()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "ordinary", Metadata.Replace("slug: example", "slug: ordinary") + "\norder: -1");
        ContentTestData.WriteProject(options.ContentDir, "later", Metadata.Replace("slug: example", "slug: later") + "\nfeatured: true\norder: 2");
        ContentTestData.WriteProject(options.ContentDir, "first", Metadata.Replace("slug: example", "slug: first").Replace("title: Example", "title: Alpha") + "\nfeatured: true\norder: 1");
        ContentTestData.WriteProject(options.ContentDir, "example",
            Metadata + "\nfeatured: true\norder: 1\nstack: [csharp, blazor, threejs]\nlinks:\n  github: https://github.com/\n  video: https://example.com/video",
            "## 개요\n\n**소개**와 ~~이전~~ 내용\n\n```csharp\nvar value = 1;\n```");

        Assert.True(ContentBuildRunner.Run(options).Success);

        var projects = ReadIndex(options);
        Assert.Equal(new[] { "first", "example", "later", "ordinary" }, projects.Select(project => project.Slug));
        var summary = projects[1];
        Assert.Equal("Example", summary.Title);
        Assert.Equal("A summary", summary.Summary);
        Assert.Equal("2026 - present", summary.Period);
        Assert.Equal("Developer", summary.Role);
        Assert.Equal(new[] { "csharp", "blazor", "threejs" }, summary.Stack);
        Assert.Equal(2, summary.Links.Count);
        Assert.Equal("https://github.com/", summary.Links["github"]);
        Assert.Equal("https://example.com/video", summary.Links["video"]);
        Assert.Null(summary.Thumbnail);
        Assert.True(summary.Featured);
        Assert.Equal(1, summary.Order);
        Assert.False(summary.Draft);
        var detail = ReadDetail(options, "example");
        Assert.Equal(JsonSerializer.Serialize(summary, ContentJson.Options), JsonSerializer.Serialize(detail.Meta, ContentJson.Options));
        Assert.Contains("<h2", detail.Html);
        Assert.Contains("<strong>소개</strong>", detail.Html);
        Assert.Contains("<del>이전</del>", detail.Html);
        Assert.Contains("<pre><code class=\"language-csharp\">", detail.Html);
        Assert.DoesNotContain("summary:", detail.Html);
        Assert.Empty(projects[0].Stack);
        Assert.Empty(projects[0].Links);
    }

    [Theory]
    [InlineData("title", "Example")]
    [InlineData("slug", "example")]
    [InlineData("summary", "A summary")]
    [InlineData("period", "2026 - present")]
    [InlineData("role", "Developer")]
    public void RejectsMissingEmptyNullAndWhitespaceRequiredFieldsEvenInDrafts(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var replacement in new[] { "", $"{field}:", $"{field}: null", $"{field}: '   '" })
        {
            ContentTestData.WriteProject(options.ContentDir, "example", Metadata.Replace($"{field}: {value}", replacement) + "\ndraft: true");
            var result = ContentBuildRunner.Run(options);
            Assert.Equal($"projects/example/index.md: {field} is required.", Assert.Single(result.Errors));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
        }
    }

    [Theory]
    [InlineData("Upper")]
    [InlineData("under_score")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("../escape")]
    public void RejectsInvalidSlugs(string slug)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata.Replace("slug: example", $"slug: {slug}"));

        var result = ContentBuildRunner.Run(options);

        Assert.Contains(result.Errors, error => error.Contains("must use lowercase"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
        Assert.False(File.Exists(Path.Combine(options.OutputDir, "escape.json")));
    }

    [Fact]
    public void RejectsFolderMismatchAndDuplicateSlugIncludingExcludedDraft()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata);
        ContentTestData.WriteProject(options.ContentDir, "other", Metadata + "\ndraft: true");

        var result = ContentBuildRunner.Run(options);

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("projects/other/index.md: slug 'example' must match folder name 'other'.", result.Errors);
        Assert.Contains("projects/other/index.md: Duplicate slug 'example' also used in projects/example/index.md.", result.Errors);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Theory]
    [InlineData("stack: [unclosed")]
    [InlineData("order: not-a-number")]
    [InlineData("featured: not-a-bool")]
    [InlineData("links: [not-a-map]")]
    public void ReportsMalformedFrontMatter(string metadata)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata + "\n" + metadata);

        var result = ContentBuildRunner.Run(options);

        Assert.StartsWith("projects/example/index.md: Invalid YAML", Assert.Single(result.Errors));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopiesAssetsRewritesUrlsAndFiltersDraftIndexDetailAndAssetsOnRebuild(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata + "\nthumbnail: images/photo.svg?v=1#center\ndraft: true",
            "![photo](images/photo.svg)\n\n[download][file]\n\n[file]: guide.pdf\n\n[web](https://example.com/)\n\n![shared](/shared.svg)\n\n`![code](photo.svg)`");
        var projectDir = Path.Combine(options.ContentDir, "projects", "example");
        Directory.CreateDirectory(Path.Combine(projectDir, "images"));
        File.WriteAllText(Path.Combine(projectDir, "images", "photo.svg"), "<svg/>");
        File.WriteAllBytes(Path.Combine(projectDir, "guide.pdf"), [1, 2, 3]);

        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = true }).Success);
        var detail = ReadDetail(options, "example");
        Assert.True(detail.Meta.Draft);
        Assert.Equal("/content/projects/example/images/photo.svg?v=1#center", detail.Meta.Thumbnail);
        Assert.Equal(detail.Meta.Thumbnail, Assert.Single(ReadIndex(options)).Thumbnail);
        Assert.Contains("src=\"/content/projects/example/images/photo.svg\"", detail.Html);
        Assert.Contains("href=\"/content/projects/example/guide.pdf\"", detail.Html);
        Assert.Contains("href=\"https://example.com/\"", detail.Html);
        Assert.Contains("src=\"/shared.svg\"", detail.Html);
        Assert.Contains("<code>![code](photo.svg)</code>", detail.Html);
        var assetDir = Path.Combine(options.OutputDir, "content", "projects", "example");
        Assert.Equal("<svg/>", File.ReadAllText(Path.Combine(assetDir, "images", "photo.svg")));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(assetDir, "guide.pdf")));
        Assert.False(File.Exists(Path.Combine(assetDir, "index.md")));

        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = includeDrafts }).Success);
        Assert.Equal(includeDrafts ? 1 : 0, ReadIndex(options).Length);
        Assert.Equal(includeDrafts, File.Exists(Path.Combine(options.OutputDir, "content", "projects", "example.json")));
        Assert.Equal(includeDrafts, Directory.Exists(assetDir));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMissingThumbnailEvenForExcludedDrafts(bool draft)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata + $"\nthumbnail: missing.svg\ndraft: {draft.ToString().ToLowerInvariant()}");

        var result = ContentBuildRunner.Run(options);

        Assert.Equal("projects/example/index.md: thumbnail 'missing.svg' must reference an existing file in the content folder.", Assert.Single(result.Errors));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Theory]
    [InlineData("https://example.com/photo.svg")]
    [InlineData("//example.com/photo.svg")]
    [InlineData("/shared/photo.svg")]
    public void PreservesNonRelativeThumbnail(string thumbnail)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteProject(options.ContentDir, "example", Metadata + $"\nthumbnail: {thumbnail}");

        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.Equal(thumbnail, ReadDetail(options, "example").Meta.Thumbnail);
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(options.ContentDir);
        ContentTestData.WriteUniverse(options.ContentDir);
        return options;
    }

    private static ProjectSummary[] ReadIndex(BuildOptions options)
    {
        return JsonSerializer.Deserialize<ProjectSummary[]>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "projects.json")), ContentJson.Options)!;
    }

    private static ProjectDetail ReadDetail(BuildOptions options, string slug)
    {
        return JsonSerializer.Deserialize<ProjectDetail>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "projects", $"{slug}.json")), ContentJson.Options)!;
    }
}
