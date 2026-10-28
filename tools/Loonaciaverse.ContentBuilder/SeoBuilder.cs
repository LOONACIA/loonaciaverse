using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder;

public static class SeoBuilder
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static void Build(string outputDir)
    {
        var contentOutputDir = Path.Combine(outputDir, "content");
        var site = ReadOutput<Universe>(contentOutputDir, "universe.json").Site;
        var posts = ReadOutput<PostSummary[]>(contentOutputDir, "posts.json")
            .Where(post => !post.Draft).OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray();
        var projects = ReadOutput<ProjectSummary[]>(contentOutputDir, "projects.json").Where(project => !project.Draft).OrderBy(project => project.Slug, StringComparer.Ordinal).ToArray();
        var baseUrl = site.BaseUrl.TrimEnd('/');
        var channel = new XElement("channel",
            new XElement("title", site.Title),
            new XElement("link", baseUrl + "/"),
            new XElement("description", site.Description),
            new XElement("language", "ko"));
        foreach (var post in posts)
        {
            var url = $"{baseUrl}/blog/{post.Slug}";
            var published = post.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("R", CultureInfo.InvariantCulture);
            channel.Add(new XElement("item",
                new XElement("title", post.Title),
                new XElement("link", url),
                new XElement("guid", new XAttribute("isPermaLink", "true"), url),
                new XElement("pubDate", published),
                new XElement("description", post.Summary),
                post.Tags.Select(tag => new XElement("category", tag))));
        }

        new XDocument(new XElement("rss", new XAttribute("version", "2.0"), channel)).Save(Path.Combine(outputDir, "feed.xml"));

        var urls = new XElement(SitemapNamespace + "urlset");
        foreach (var route in new[] { "/", "/projects", "/blog", "/about" })
        {
            urls.Add(CreateSitemapUrl(baseUrl + route));
        }

        foreach (var post in posts)
        {
            urls.Add(CreateSitemapUrl($"{baseUrl}/blog/{post.Slug}", post.Updated ?? post.Date));
        }

        foreach (var project in projects)
        {
            urls.Add(CreateSitemapUrl($"{baseUrl}/projects/{project.Slug}"));
        }

        foreach (var tag in posts.SelectMany(post => post.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            urls.Add(CreateSitemapUrl($"{baseUrl}/blog/tags/{tag}"));
        }

        var seriesDir = Path.Combine(contentOutputDir, "series");
        if (Directory.Exists(seriesDir))
        {
            var seriesDetails = Directory.EnumerateFiles(seriesDir, "*.json").Select(path => ReadOutput<SeriesDetail>(seriesDir, Path.GetFileName(path)));
            foreach (var series in seriesDetails.Where(series => series.Posts.Any(post => !post.Draft)).OrderBy(series => series.Slug, StringComparer.Ordinal))
            {
                urls.Add(CreateSitemapUrl($"{baseUrl}/blog/series/{series.Slug}"));
            }
        }

        new XDocument(urls).Save(Path.Combine(outputDir, "sitemap.xml"));
        File.WriteAllText(Path.Combine(outputDir, "robots.txt"), $"User-agent: *\nAllow: /\nSitemap: {baseUrl}/sitemap.xml\n");
    }

    internal static IReadOnlyList<string> ClearOutput(string outputDir)
    {
        var errors = new List<string>();
        foreach (var fileName in new[] { "feed.xml", "sitemap.xml", "robots.txt" })
        {
            try
            {
                File.Delete(Path.Combine(outputDir, fileName));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errors.Add($"{outputDir}/{fileName}: Could not clear failed build output: {exception.Message}");
            }
        }

        return errors;
    }

    private static XElement CreateSitemapUrl(string url, DateOnly? lastModified = null)
    {
        var element = new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", url));
        if (lastModified is { } date)
        {
            element.Add(new XElement(SitemapNamespace + "lastmod", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return element;
    }

    private static T ReadOutput<T>(string contentOutputDir, string fileName)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(contentOutputDir, fileName)), ContentJson.Options)!;
    }
}
