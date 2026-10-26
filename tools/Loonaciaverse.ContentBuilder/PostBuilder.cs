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
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).UseAdvancedExtensions().UseYamlFrontMatter().Build();

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

        var summaries = new List<PostSummary>();
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
            var markdown = Markdown.Parse(document.Body, Pipeline);
            var toc = markdown.Descendants<HeadingBlock>().Where(heading => heading.Level is 2 or 3).Select(CreateTocEntry).ToArray();
            ContentAssets.RewriteUrls(markdown, assetBaseUrl);
            var detail = new PostDetail(summary, markdown.ToHtml(Pipeline), toc);
            var postsOutputDir = Path.Combine(contentOutputDir, "posts");
            Directory.CreateDirectory(postsOutputDir);
            ContentAssets.CopyFiles(Path.Combine(postsDir, data.Slug), Path.Combine(postsOutputDir, data.Slug));
            File.WriteAllText(Path.Combine(postsOutputDir, $"{summary.Slug}.json"), JsonSerializer.Serialize(detail, ContentJson.Options));
            summaries.Add(summary);
        }

        var sortedSummaries = summaries.OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray();
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
    }
}
