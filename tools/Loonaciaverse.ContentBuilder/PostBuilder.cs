using System.Text.Json;
using Loonaciaverse.Core.Content;
using Markdig;

namespace Loonaciaverse.ContentBuilder;

public static class PostBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static BuildResult Build(string contentDir, string contentOutputDir, bool includeDrafts)
    {
        var postsDir = Path.Combine(contentDir, "posts");
        var documents = new List<FrontMatterDocument<PostFrontMatter>>();
        var errors = new List<string>();
        var validator = new ContentValidator();

        if (Directory.Exists(postsDir))
        {
            foreach (var postDir in Directory.EnumerateDirectories(postsDir).Order(StringComparer.Ordinal))
            {
                var sourcePath = Path.Combine(postDir, "index.md");
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(contentDir, sourcePath).Replace('\\', '/');
                try
                {
                    var document = FrontMatterReader.Read<PostFrontMatter>(File.ReadAllText(sourcePath), relativePath);
                    var data = document.Data;
                    validator.ValidateRequired(relativePath, "title", data.Title);
                    validator.ValidateSlug(relativePath, Path.GetFileName(postDir), data.Slug);
                    validator.ValidateDates(relativePath, data.Date, data.Updated);
                    validator.ValidateRequired(relativePath, "summary", data.Summary);
                    validator.ValidateTags(relativePath, data.Tags);
                    documents.Add(document);
                }
                catch (ContentValidationException exception)
                {
                    errors.Add(exception.Message);
                }
            }
        }

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
            var summary = new PostSummary(data.Slug, data.Title, date, updated, data.Summary, data.Tags ?? [], data.Draft, data.Cover);
            var detail = new PostDetail(summary, Markdown.ToHtml(document.Body, Pipeline));
            var postsOutputDir = Path.Combine(contentOutputDir, "posts");
            Directory.CreateDirectory(postsOutputDir);
            File.WriteAllText(Path.Combine(postsOutputDir, $"{summary.Slug}.json"), JsonSerializer.Serialize(detail, ContentJson.Options));
            summaries.Add(summary);
        }

        var sortedSummaries = summaries.OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).ToArray();
        File.WriteAllText(Path.Combine(contentOutputDir, "posts.json"), JsonSerializer.Serialize(sortedSummaries, ContentJson.Options));
        return new BuildResult([]);
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
