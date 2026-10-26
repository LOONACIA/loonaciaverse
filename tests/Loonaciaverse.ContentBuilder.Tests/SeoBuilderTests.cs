using System.Text.Json;
using System.Xml.Linq;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class SeoBuilderTests
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly string[] SeoFiles = ["feed.xml", "sitemap.xml", "robots.txt"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunWritesRssFieldsInDateOrderAndAlwaysExcludesDrafts(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = includeDrafts };
        WritePost(options, "older", "2026-01-01", "tags: [csharp]\nupdated: 2026-12-31");
        WritePost(options, "newer", "2026-10-26", "tags: [unity, csharp]");
        WritePost(options, "draft", "2026-12-01", "draft: true\ntags: [secret]");
        var site = ContentTestData.SiteYaml.Replace("title: LOONACIAVERSE", "title: '우주 & <개발>'").Replace("description: 개인 우주입니다.", "description: '소개 & <내용>'");
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), site);

        Assert.True(ContentBuildRunner.Run(options).Success);

        var rss = XDocument.Load(Path.Combine(options.OutputDir, "feed.xml")).Root!;
        Assert.Equal("rss", rss.Name.LocalName);
        Assert.Equal("2.0", (string?)rss.Attribute("version"));
        var channel = Assert.Single(rss.Elements("channel"));
        Assert.Equal("우주 & <개발>", (string?)channel.Element("title"));
        Assert.Equal("https://example.com/", (string?)channel.Element("link"));
        Assert.Equal("소개 & <내용>", (string?)channel.Element("description"));
        Assert.Equal("ko", (string?)channel.Element("language"));
        var items = channel.Elements("item").ToArray();
        Assert.Equal(new[] { "https://example.com/blog/newer", "https://example.com/blog/older" }, items.Select(item => (string?)item.Element("link")));
        Assert.Equal(new[] { "Mon, 26 Oct 2026 00:00:00 GMT", "Thu, 01 Jan 2026 00:00:00 GMT" }, items.Select(item => (string?)item.Element("pubDate")));
        Assert.All(items, item =>
        {
            Assert.Equal("제목 & <코드> \"인용\"", (string?)item.Element("title"));
            Assert.Equal("요약 & <내용> \"인용\"", (string?)item.Element("description"));
            Assert.Equal((string?)item.Element("link"), (string?)item.Element("guid"));
            Assert.Equal("true", (string?)item.Element("guid")?.Attribute("isPermaLink"));
        });
        Assert.Equal(new[] { "unity", "csharp" }, items[0].Elements("category").Select(category => category.Value));
        Assert.Equal("csharp", Assert.Single(items[1].Elements("category")).Value);
        var posts = JsonSerializer.Deserialize<PostSummary[]>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "posts.json")), ContentJson.Options)!;
        Assert.Equal(includeDrafts, posts.Any(post => post.Draft));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SitemapContainsOnlyPublicRoutesWithDistinctTagsAndPostLastModifiedDates(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = includeDrafts };
        WritePost(options, "older", "2026-01-01", "tags: [csharp, csharp]\nupdated: 2026-02-03");
        WritePost(options, "newer", "2026-10-26", "tags: [unity, csharp]");
        WritePost(options, "draft", "2026-12-01", "draft: true\ntags: [secret]");
        ContentTestData.WriteProject(options.ContentDir, "public-project", ProjectMetadata("public-project"));
        ContentTestData.WriteProject(options.ContentDir, "draft-project", ProjectMetadata("draft-project") + "\ndraft: true");

        Assert.True(ContentBuildRunner.Run(options).Success);

        var root = XDocument.Load(Path.Combine(options.OutputDir, "sitemap.xml")).Root!;
        Assert.Equal(SitemapNamespace + "urlset", root.Name);
        var urls = root.Elements(SitemapNamespace + "url").ToArray();
        var expectedRoutes = new[] { "/", "/projects", "/blog", "/about", "/blog/newer", "/blog/older", "/projects/public-project", "/blog/tags/csharp", "/blog/tags/unity" };
        Assert.Equal(expectedRoutes.Select(route => "https://example.com" + route), urls.Select(url => (string?)url.Element(SitemapNamespace + "loc")));
        Assert.All(root.Descendants(), element => Assert.Equal(SitemapNamespace, element.Name.Namespace));
        Assert.Equal("2026-10-26", (string?)urls[4].Element(SitemapNamespace + "lastmod"));
        Assert.Equal("2026-02-03", (string?)urls[5].Element(SitemapNamespace + "lastmod"));
        Assert.All(urls.Where((_, index) => index is not (4 or 5)), url => Assert.Null(url.Element(SitemapNamespace + "lastmod")));
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com///", "https://example.com")]
    [InlineData("https://example.com/portfolio/", "https://example.com/portfolio")]
    public void NormalizesBaseUrlForAllAbsoluteUrlsAndRobots(string baseUrl, string normalized)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace("https://example.com", baseUrl));
        WritePost(options, "post", "2026-10-26", "tags: [csharp]");
        ContentTestData.WriteProject(options.ContentDir, "project", ProjectMetadata("project"));

        Assert.True(ContentBuildRunner.Run(options).Success);

        var feed = XDocument.Load(Path.Combine(options.OutputDir, "feed.xml"));
        Assert.Equal(new[] { normalized + "/", normalized + "/blog/post" }, feed.Descendants("link").Select(link => link.Value));
        Assert.Equal(normalized + "/blog/post", Assert.Single(feed.Descendants("guid")).Value);
        var sitemap = XDocument.Load(Path.Combine(options.OutputDir, "sitemap.xml"));
        var routes = new[] { "/", "/projects", "/blog", "/about", "/blog/post", "/projects/project", "/blog/tags/csharp" };
        Assert.Equal(routes.Select(route => normalized + route), sitemap.Descendants(SitemapNamespace + "loc").Select(loc => loc.Value));
        Assert.All(sitemap.Descendants(SitemapNamespace + "loc"), loc => Assert.True(Uri.IsWellFormedUriString(loc.Value, UriKind.Absolute)));
        Assert.Equal($"User-agent: *\nAllow: /\nSitemap: {normalized}/sitemap.xml\n", File.ReadAllText(Path.Combine(options.OutputDir, "robots.txt")));
        Assert.All(SeoFiles, fileName => Assert.False(File.Exists(Path.Combine(options.OutputDir, "content", fileName))));
    }

    [Fact]
    public void RebuildOverwritesSeoFilesAndRemovesPreviouslyPublishedDraftsAndTags()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory) with { IncludeDrafts = true };
        WritePost(options, "post", "2026-10-26", "tags: [csharp]");
        ContentTestData.WriteProject(options.ContentDir, "project", ProjectMetadata("project"));
        Assert.True(ContentBuildRunner.Run(options).Success);
        WritePost(options, "post", "2026-10-26", "draft: true\ntags: [csharp]");
        ContentTestData.WriteProject(options.ContentDir, "project", ProjectMetadata("project") + "\ndraft: true");
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace("https://example.com", "https://new.example.com/"));
        var preservedFile = Path.Combine(options.OutputDir, "index.html");
        File.WriteAllText(preservedFile, "keep html");

        Assert.True(ContentBuildRunner.Run(options).Success);

        Assert.Empty(XDocument.Load(Path.Combine(options.OutputDir, "feed.xml")).Descendants("item"));
        var sitemap = XDocument.Load(Path.Combine(options.OutputDir, "sitemap.xml"));
        var routes = new[] { "/", "/projects", "/blog", "/about" };
        Assert.Equal(routes.Select(route => "https://new.example.com" + route), sitemap.Descendants(SitemapNamespace + "loc").Select(loc => loc.Value));
        Assert.Equal("User-agent: *\nAllow: /\nSitemap: https://new.example.com/sitemap.xml\n", File.ReadAllText(Path.Combine(options.OutputDir, "robots.txt")));
        Assert.Equal("keep html", File.ReadAllText(preservedFile));
    }

    [Theory]
    [InlineData("missing-content")]
    [InlineData("post")]
    [InlineData("project")]
    [InlineData("about.md")]
    [InlineData("site.yml")]
    [InlineData("universe.yml")]
    [InlineData("content-output")]
    public void FailedBuildDeletesAllPreviousSeoFilesAndPreservesUnrelatedFiles(string failure)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.All(SeoFiles, fileName => Assert.True(File.Exists(Path.Combine(options.OutputDir, fileName))));
        var preservedFile = Path.Combine(options.OutputDir, "index.html");
        File.WriteAllText(preservedFile, "keep html");
        var assetDir = Directory.CreateDirectory(Path.Combine(options.OutputDir, "assets")).FullName;
        var assetFile = Path.Combine(assetDir, "feed.xml");
        File.WriteAllText(assetFile, "keep nested file");
        switch (failure)
        {
            case "missing-content":
                options = options with { ContentDir = Path.Combine(directory.Root, "missing") };
                break;
            case "post":
                WritePost(options, "post", "invalid", "");
                break;
            case "project":
                ContentTestData.WriteProject(options.ContentDir, "invalid", "draft: true");
                break;
            case "content-output":
                Directory.Delete(Path.Combine(options.OutputDir, "content"), recursive: true);
                File.WriteAllText(Path.Combine(options.OutputDir, "content"), "blocks content output");
                break;
            default:
                File.WriteAllText(Path.Combine(options.ContentDir, failure), "");
                break;
        }

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.All(SeoFiles, fileName => Assert.False(File.Exists(Path.Combine(options.OutputDir, fileName))));
        Assert.Equal("keep html", File.ReadAllText(preservedFile));
        Assert.Equal("keep nested file", File.ReadAllText(assetFile));
    }

    [Theory]
    [InlineData("feed.xml")]
    [InlineData("sitemap.xml")]
    [InlineData("robots.txt")]
    public void SeoWriteFailureCleansPartialOutputAndContinuesCleanupAfterDeletionErrors(string blockedFile)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        Assert.True(ContentBuildRunner.Run(options).Success);
        var blockedPath = Path.Combine(options.OutputDir, blockedFile);
        File.Delete(blockedPath);
        Directory.CreateDirectory(blockedPath);
        var preservedFile = Path.Combine(blockedPath, "keep.txt");
        File.WriteAllText(preservedFile, "keep directory contents");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains(blockedFile) && error.Contains("Could not clear failed build output"));
        Assert.All(SeoFiles, fileName => Assert.False(File.Exists(Path.Combine(options.OutputDir, fileName))));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
        Assert.Equal("keep directory contents", File.ReadAllText(preservedFile));
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(options.ContentDir);
        ContentTestData.WriteUniverse(options.ContentDir);
        return options;
    }

    private static void WritePost(BuildOptions options, string slug, string date, string metadata)
    {
        var postDir = Directory.CreateDirectory(Path.Combine(options.ContentDir, "posts", slug)).FullName;
        var frontMatter = $"title: '제목 & <코드> \"인용\"'\nslug: {slug}\ndate: {date}\nsummary: '요약 & <내용> \"인용\"'\n{metadata}";
        File.WriteAllText(Path.Combine(postDir, "index.md"), $"---\n{frontMatter}\n---\nBody");
    }

    private static string ProjectMetadata(string slug)
    {
        return $"title: Project\nslug: {slug}\nsummary: Summary\nperiod: 2026\nrole: Developer";
    }
}
