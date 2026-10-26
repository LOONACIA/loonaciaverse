using System.Text.Json;
using Loonaciaverse.Core.Content;
using Markdig;

namespace Loonaciaverse.ContentBuilder;

public static class PostBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static void Build(string contentDir, string contentOutputDir)
    {
        var postsDir = Path.Combine(contentDir, "posts");
        var summaries = new List<PostSummary>();
        Directory.CreateDirectory(contentOutputDir);

        if (Directory.Exists(postsDir))
        {
            foreach (var postDir in Directory.EnumerateDirectories(postsDir))
            {
                var sourcePath = Path.Combine(postDir, "index.md");
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var document = FrontMatterReader.Read<PostFrontMatter>(File.ReadAllText(sourcePath), sourcePath);
                var data = document.Data;
                var date = DateOnly.FromDateTime(data.Date);
                DateOnly? updated = data.Updated is { } updatedDate ? DateOnly.FromDateTime(updatedDate) : null;
                var summary = new PostSummary(data.Slug, data.Title, date, updated, data.Summary, data.Tags, data.Draft, data.Cover);
                var detail = new PostDetail(summary, Markdown.ToHtml(document.Body, Pipeline));
                var postsOutputDir = Path.Combine(contentOutputDir, "posts");
                Directory.CreateDirectory(postsOutputDir);
                File.WriteAllText(Path.Combine(postsOutputDir, $"{summary.Slug}.json"), JsonSerializer.Serialize(detail, ContentJson.Options));
                summaries.Add(summary);
            }
        }

        var sortedSummaries = summaries.OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray();
        File.WriteAllText(Path.Combine(contentOutputDir, "posts.json"), JsonSerializer.Serialize(sortedSummaries, ContentJson.Options));
    }

    public sealed class PostFrontMatter
    {
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public DateTime Date { get; set; }
        public DateTime? Updated { get; set; }
        public string Summary { get; set; } = "";
        public List<string> Tags { get; set; } = [];
        public bool Draft { get; set; }
        public string? Cover { get; set; }
    }
}
