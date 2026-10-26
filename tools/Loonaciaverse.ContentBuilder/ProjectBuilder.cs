using System.Text.Json;
using Loonaciaverse.Core.Content;
using Markdig;

namespace Loonaciaverse.ContentBuilder;

public static class ProjectBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static BuildResult Build(string contentDir, string contentOutputDir, bool includeDrafts)
    {
        var errors = new List<string>();
        var validator = new ContentValidator();
        var documents = ContentDocuments.ReadCollection<ProjectFrontMatter>(contentDir, "projects", (path, folder, data) =>
        {
            validator.ValidateRequired(path, "title", data.Title);
            validator.ValidateSlug(path, Path.GetFileName(folder), data.Slug);
            validator.ValidateRequired(path, "summary", data.Summary);
            validator.ValidateRequired(path, "period", data.Period);
            validator.ValidateRequired(path, "role", data.Role);
            validator.ValidateAsset(path, folder, "thumbnail", data.Thumbnail);
        }, errors);
        errors.AddRange(validator.Errors);
        if (errors.Count > 0)
        {
            return new BuildResult(errors);
        }

        var summaries = new List<ProjectSummary>();
        Directory.CreateDirectory(contentOutputDir);
        foreach (var document in documents.Where(document => includeDrafts || !document.Data.Draft))
        {
            var data = document.Data;
            var assetBaseUrl = $"/content/projects/{data.Slug}/";
            var thumbnail = ContentAssets.RewriteUrl(data.Thumbnail, assetBaseUrl);
            var summary = new ProjectSummary(data.Slug, data.Title, data.Summary, data.Period, data.Role, data.Stack ?? [], data.Links ?? [], thumbnail, data.Featured, data.Order, data.Draft);
            var markdown = Markdown.Parse(document.Body, Pipeline);
            ContentAssets.RewriteUrls(markdown, assetBaseUrl);
            var detail = new ProjectDetail(summary, markdown.ToHtml(Pipeline));
            var projectsOutputDir = Path.Combine(contentOutputDir, "projects");
            Directory.CreateDirectory(projectsOutputDir);
            ContentAssets.CopyFiles(Path.Combine(contentDir, "projects", data.Slug), Path.Combine(projectsOutputDir, data.Slug));
            File.WriteAllText(Path.Combine(projectsOutputDir, $"{data.Slug}.json"), JsonSerializer.Serialize(detail, ContentJson.Options));
            summaries.Add(summary);
        }

        var sortedSummaries = summaries.OrderByDescending(project => project.Featured).ThenBy(project => project.Order).ThenBy(project => project.Title, StringComparer.Ordinal).ToArray();
        File.WriteAllText(Path.Combine(contentOutputDir, "projects.json"), JsonSerializer.Serialize(sortedSummaries, ContentJson.Options));
        return new BuildResult([]);
    }

    public sealed class ProjectFrontMatter
    {
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Period { get; set; } = "";
        public string Role { get; set; } = "";
        public List<string> Stack { get; set; } = [];
        public Dictionary<string, string> Links { get; set; } = [];
        public string? Thumbnail { get; set; }
        public bool Featured { get; set; }
        public int Order { get; set; }
        public bool Draft { get; set; }
    }
}
