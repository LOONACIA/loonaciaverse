using System.Text.Json;
using Loonaciaverse.Core.Content;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Loonaciaverse.ContentBuilder;

public static class PostBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub).UseNoCookieMediaLinks().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static BuildResult Build(string contentDir, string contentOutputDir, bool includeDrafts)
    {
        var postsDir = Path.Combine(contentDir, "posts");
        var errors = new List<string>();
        var validator = new ContentValidator();
        var documents = ContentDocuments.ReadCollection<PostFrontMatter>(contentDir, "posts", (path, folder, data) =>
        {
            validator.ValidateRequired(path, "title", data.Title);
            validator.ValidateSlug(path, Path.GetFileName(folder), data.Slug);
            validator.ValidateDates(path, data.Date, data.Updated);
            validator.ValidateRequired(path, "summary", data.Summary);
            validator.ValidateTags(path, data.Tags);
            validator.ValidateCover(path, folder, data.Cover);
        }, errors);

        errors.AddRange(validator.Errors);
        if (errors.Count > 0)
        {
            return new BuildResult(errors);
        }

        var seriesCatalog = SeriesCatalog.Read(contentDir);
        var includedPosts = new List<(FrontMatterDocument<PostFrontMatter> Document, PostSummary Summary)>();
        Directory.CreateDirectory(contentOutputDir);
        foreach (var document in documents)
        {
            var data = document.Data;
            if (data.Draft && !includeDrafts)
            {
                continue;
            }

            var date = DateOnly.FromDateTime(data.Date!.Value);
            DateOnly? updated = data.Updated is { } updatedDate ? DateOnly.FromDateTime(updatedDate) : null;
            var assetBaseUrl = $"/content/posts/{data.Slug}/";
            var cover = ContentAssets.RewriteUrl(data.Cover, assetBaseUrl);
            var summary = new PostSummary(data.Slug, data.Title, date, updated, data.Summary, data.Tags ?? [], data.Draft, cover);
            includedPosts.Add((document, summary));
        }

        var seriesDetails = seriesCatalog.Select(series => new SeriesDetail(series.Slug, series.Title, series.Summary,
            includedPosts.Where(post => post.Document.Data.Series == series.Slug).Select(post => post.Summary)
                .OrderBy(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray())).Where(series => series.Posts.Count > 0).ToArray();
        var postSeries = seriesDetails.ToDictionary(series => series.Slug,
            series => new PostSeries(series.Slug, series.Title, series.Posts.Select(post => new SeriesPart(post.Slug, post.Title, post.Draft)).ToArray()), StringComparer.Ordinal);
        foreach (var (document, summary) in includedPosts)
        {
            var data = document.Data;
            var assetBaseUrl = $"/content/posts/{data.Slug}/";
            var markdown = Markdown.Parse(document.Body, Pipeline);
            var toc = markdown.Descendants<HeadingBlock>().Where(heading => heading.Level is 2 or 3).Select(CreateTocEntry).ToArray();
            ContentAssets.RewriteUrls(markdown, assetBaseUrl);
            var series = data.Series is not null && postSeries.TryGetValue(data.Series, out var foundSeries) ? foundSeries : null;
            var detail = new PostDetail(summary, markdown.ToHtml(Pipeline), toc, series);
            var postsOutputDir = Path.Combine(contentOutputDir, "posts");
            Directory.CreateDirectory(postsOutputDir);
            ContentAssets.CopyFiles(Path.Combine(postsDir, data.Slug), Path.Combine(postsOutputDir, data.Slug));
            File.WriteAllText(Path.Combine(postsOutputDir, $"{summary.Slug}.json"), JsonSerializer.Serialize(detail, ContentJson.Options));
        }

        foreach (var series in seriesDetails)
        {
            var seriesOutputDir = Path.Combine(contentOutputDir, "series");
            Directory.CreateDirectory(seriesOutputDir);
            File.WriteAllText(Path.Combine(seriesOutputDir, $"{series.Slug}.json"), JsonSerializer.Serialize(series, ContentJson.Options));
        }

        var sortedSummaries = includedPosts.Select(post => post.Summary).OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray();
        File.WriteAllText(Path.Combine(contentOutputDir, "posts.json"), JsonSerializer.Serialize(sortedSummaries, ContentJson.Options));
        return new BuildResult([]);
    }

    private static TocEntry CreateTocEntry(HeadingBlock heading)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer) { EnableHtmlForInline = false, EnableHtmlForBlock = false, EnableHtmlEscape = false };
        Pipeline.Setup(renderer);
        if (heading.Inline is not null)
        {
            renderer.Render(heading.Inline);
        }

        return new TocEntry(heading.Level, heading.GetAttributes().Id!, writer.ToString());
    }

    public sealed class PostFrontMatter
    {
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public DateTime? Date { get; set; }
        public DateTime? Updated { get; set; }
        public string Summary { get; set; } = "";
        public List<string> Tags { get; set; } = [];
        public bool Draft { get; set; }
        public string? Cover { get; set; }
        public string? Series { get; set; }
    }
}
