using System.Text.Json;
using Loonaciaverse.Core.Content;
using Markdig;

namespace Loonaciaverse.ContentBuilder;

public static class AboutBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static BuildResult Build(string contentDir, string contentOutputDir)
    {
        var sourcePath = Path.Combine(contentDir, "about.md");
        if (!File.Exists(sourcePath))
        {
            return new BuildResult(["about.md: File is required."]);
        }

        FrontMatterDocument<AboutFrontMatter> document;
        try
        {
            document = FrontMatterReader.Read<AboutFrontMatter>(File.ReadAllText(sourcePath), "about.md");
        }
        catch (ContentValidationException exception)
        {
            return new BuildResult([exception.Message]);
        }

        var data = document.Data;
        var validator = new ContentValidator();
        validator.ValidateRequired("about.md", "name", data.Name);
        validator.ValidateRequired("about.md", "title", data.Title);
        validator.ValidateRequired("about.md", "email", data.Email);
        if (validator.Errors.Count > 0)
        {
            return new BuildResult(validator.Errors);
        }

        var profile = new AboutProfile(data.Name, data.Title, data.Stack ?? [], data.Email, data.Github, data.Linkedin, Markdown.ToHtml(document.Body, Pipeline));
        Directory.CreateDirectory(contentOutputDir);
        File.WriteAllText(Path.Combine(contentOutputDir, "about.json"), JsonSerializer.Serialize(profile, ContentJson.Options));
        return new BuildResult([]);
    }

    public sealed class AboutFrontMatter
    {
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public List<string> Stack { get; set; } = [];
        public string Email { get; set; } = "";
        public string? Github { get; set; }
        public string? Linkedin { get; set; }
    }
}
