using System.Text.Json;
using System.Xml.Linq;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class SeriesBuilderTests
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const string SeriesYaml = """
        series:
          - slug: zeta-series
            title: 두 번째 시리즈
            summary: 두 번째 시리즈의 설명입니다.
          - slug: alpha-series
            title: 첫 번째 시리즈
            summary: 첫 번째 시리즈의 설명입니다.
        """;

    [Fact]
    public void RunWritesSeriesPartsAndSummariesInDateThenOrdinalSlugOrder()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        ContentTestData.WritePost(options.ContentDir, "alpha", Metadata("alpha", "2026-10-25", "alpha-series", "updated: 2026-10-27\ncover: cover.png"), "## Heading\nBody");
        File.WriteAllBytes(Path.Combine(options.ContentDir, "posts", "alpha", "cover.png"), [1, 2, 3]);
        ContentTestData.WritePost(options.ContentDir, "beta", Metadata("beta", "2026-10-24", "alpha-series"));
        ContentTestData.WritePost(options.ContentDir, "zeta", Metadata("zeta", "2026-10-24", "alpha-series"));
        ContentTestData.WritePost(options.ContentDir, "gamma", Metadata("gamma", "2026-10-23", "alpha-series"));
        ContentTestData.WritePost(options.ContentDir, "orbit", Metadata("orbit", "2026-10-26", "zeta-series"));
        ContentTestData.WritePost(options.ContentDir, "standalone", Metadata("standalone", "2026-10-27"));

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        var alphaPosts = new[]
        {
            ExpectedSummary("gamma", new DateOnly(2026, 10, 23)),
            ExpectedSummary("beta", new DateOnly(2026, 10, 24)),
            ExpectedSummary("zeta", new DateOnly(2026, 10, 24)),
            ExpectedSummary("alpha", new DateOnly(2026, 10, 25)) with { Updated = new DateOnly(2026, 10, 27), Cover = "/content/posts/alpha/cover.png" }
        };
        var zetaPosts = new[] { ExpectedSummary("orbit", new DateOnly(2026, 10, 26)) };
        AssertSeriesOutput(options, "alpha-series", "첫 번째 시리즈", "첫 번째 시리즈의 설명입니다.", alphaPosts);
        AssertSeriesOutput(options, "zeta-series", "두 번째 시리즈", "두 번째 시리즈의 설명입니다.", zetaPosts);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(options.OutputDir, "content", "series"), "*.json").Length);
        var standalone = ReadOutput<PostDetail>(options, "posts/standalone.json");
        Assert.Null(standalone.Series);
        AssertSummary(ExpectedSummary("standalone", new DateOnly(2026, 10, 27)), standalone.Meta);
        var alpha = ReadOutput<PostDetail>(options, "posts/alpha.json");
        Assert.Contains("<h2 id=\"heading\">Heading</h2>", alpha.Html);
        Assert.Equal(new TocEntry(2, "heading", "Heading"), Assert.Single(alpha.Toc));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(options.OutputDir, "content", "posts", "alpha", "cover.png")));
        var posts = ReadOutput<PostSummary[]>(options, "posts.json");
        Assert.Equal(new[] { "standalone", "orbit", "alpha", "beta", "zeta", "gamma" }, posts.Select(post => post.Slug));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunIncludesOnlyBuiltPartsAndOmitsSeriesWithNoBuiltParts(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = includeDrafts };
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml);
        ContentTestData.WritePost(options.ContentDir, "public", Metadata("public", "2026-10-25", "alpha-series"));
        ContentTestData.WritePost(options.ContentDir, "draft", Metadata("draft", "2026-10-24", "alpha-series", "draft: true"));
        ContentTestData.WritePost(options.ContentDir, "draft-only", Metadata("draft-only", "2026-10-26", "zeta-series", "draft: true"));

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        var publicPost = ExpectedSummary("public", new DateOnly(2026, 10, 25));
        var draftPost = ExpectedSummary("draft", new DateOnly(2026, 10, 24)) with { Draft = true };
        PostSummary[] expectedPosts = includeDrafts ? [draftPost, publicPost] : [publicPost];
        AssertSeriesOutput(options, "alpha-series", "첫 번째 시리즈", "첫 번째 시리즈의 설명입니다.", expectedPosts);
        Assert.Equal(includeDrafts, File.Exists(Path.Combine(options.OutputDir, "content", "posts", "draft.json")));
        Assert.Equal(includeDrafts, File.Exists(Path.Combine(options.OutputDir, "content", "posts", "draft-only.json")));
        Assert.Equal(includeDrafts, File.Exists(Path.Combine(options.OutputDir, "content", "series", "zeta-series.json")));
        if (includeDrafts)
        {
            var draftOnly = ExpectedSummary("draft-only", new DateOnly(2026, 10, 26)) with { Draft = true };
            AssertSeriesOutput(options, "zeta-series", "두 번째 시리즈", "두 번째 시리즈의 설명입니다.", [draftOnly]);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("other: ignored")]
    public void RunSucceedsWithoutSeriesDefinitionsOrReferences(string? yaml)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        if (yaml is not null)
        {
            ContentTestData.WriteSeries(options.ContentDir, yaml);
        }

        ContentTestData.WritePost(options.ContentDir, "standalone", Metadata("standalone", "2026-10-25"));

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Null(ReadOutput<PostDetail>(options, "posts/standalone.json").Series);
        var seriesDir = Path.Combine(options.OutputDir, "content", "series");
        Assert.True(!Directory.Exists(seriesDir) || !Directory.EnumerateFiles(seriesDir, "*.json").Any());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SitemapAppendsPublicSeriesAfterTagsInOrdinalSlugOrder(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = includeDrafts };
        ContentTestData.WriteSeries(options.ContentDir, SeriesYaml + "\n  - slug: draft-series\n    title: 초안 시리즈\n    summary: 초안만 있습니다.");
        ContentTestData.WritePost(options.ContentDir, "alpha", Metadata("alpha", "2026-10-25", "alpha-series", "updated: 2026-10-27"));
        ContentTestData.WritePost(options.ContentDir, "zeta", Metadata("zeta", "2026-10-24", "zeta-series"));
        var draftMetadata = Metadata("mixed-draft", "2026-10-26", "alpha-series", "draft: true").Replace("tags: [unity, csharp]", "tags: [secret]");
        ContentTestData.WritePost(options.ContentDir, "mixed-draft", draftMetadata);
        var draftOnlyMetadata = Metadata("draft-only", "2026-10-26", "draft-series", "draft: true").Replace("tags: [unity, csharp]", "tags: [secret]");
        ContentTestData.WritePost(options.ContentDir, "draft-only", draftOnlyMetadata);
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace("https://example.com", "https://example.com/portfolio/"));

        Assert.True(ContentBuildRunner.Run(options).Success);

        var urls = XDocument.Load(Path.Combine(options.OutputDir, "sitemap.xml")).Root!.Elements(SitemapNamespace + "url").ToArray();
        var routes = new[] { "/", "/projects", "/blog", "/about", "/blog/alpha", "/blog/zeta", "/blog/tags/csharp", "/blog/tags/unity", "/blog/series/alpha-series", "/blog/series/zeta-series" };
        Assert.Equal(routes.Select(route => "https://example.com/portfolio" + route), urls.Select(url => (string?)url.Element(SitemapNamespace + "loc")));
        Assert.All(urls.TakeLast(2), url => Assert.Null(url.Element(SitemapNamespace + "lastmod")));
        Assert.Equal(includeDrafts, File.Exists(Path.Combine(options.OutputDir, "content", "series", "draft-series.json")));
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(options.ContentDir);
        ContentTestData.WriteUniverse(options.ContentDir);
        return options;
    }

    private static string Metadata(string slug, string date, string? series = null, string additional = "")
    {
        var metadata = $"title: {slug} 제목\nslug: {slug}\ndate: {date}\nsummary: {slug} 요약\ntags: [unity, csharp]\n{additional}";
        return series is null ? metadata : metadata + $"\nseries: {series}";
    }

    private static PostSummary ExpectedSummary(string slug, DateOnly date)
    {
        return new PostSummary(slug, $"{slug} 제목", date, null, $"{slug} 요약", ["unity", "csharp"], false, null);
    }

    private static T ReadOutput<T>(BuildOptions options, string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(options.OutputDir, "content", path)), ContentJson.Options)!;
    }

    private static void AssertSeriesOutput(BuildOptions options, string slug, string title, string summary, IReadOnlyList<PostSummary> expectedPosts)
    {
        var detail = ReadOutput<SeriesDetail>(options, $"series/{slug}.json");
        Assert.Equal(slug, detail.Slug);
        Assert.Equal(title, detail.Title);
        Assert.Equal(summary, detail.Summary);
        Assert.Equal(expectedPosts.Select(post => post.Slug), detail.Posts.Select(post => post.Slug));
        for (var index = 0; index < expectedPosts.Count; index++)
        {
            AssertSummary(expectedPosts[index], detail.Posts[index]);
        }

        foreach (var expectedPost in expectedPosts)
        {
            var post = ReadOutput<PostDetail>(options, $"posts/{expectedPost.Slug}.json");
            AssertSummary(expectedPost, post.Meta);
            var series = Assert.IsType<PostSeries>(post.Series);
            Assert.Equal(slug, series.Slug);
            Assert.Equal(title, series.Title);
            Assert.Equal(expectedPosts.Select(part => new SeriesPart(part.Slug, part.Title, part.Draft)), series.Parts);
        }
    }

    private static void AssertSummary(PostSummary expected, PostSummary actual)
    {
        Assert.Equal(expected.Slug, actual.Slug);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Updated, actual.Updated);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.Draft, actual.Draft);
        Assert.Equal(expected.Cover, actual.Cover);
    }
}
