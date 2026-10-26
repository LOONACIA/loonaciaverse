using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class UniverseBuilderTests
{
    [Fact]
    public void RunMapsSiteBodiesVisualsOrbitsAndAboutToCamelCaseJson()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        ContentTestData.WriteAbout(options.ContentDir, ContentTestData.AboutMetadata + "\nstack: [csharp, unity]\ngithub: https://github.com/\nlinkedin: https://linkedin.com/");

        Assert.True(ContentBuildRunner.Run(options).Success);

        var universe = ReadUniverse(options);
        Assert.Equal(new SiteInfo("LOONACIAVERSE", "https://example.com", "LOONACIA", "개인 우주입니다."), universe.Site);
        Assert.Equal(new[] { "about", "projects", "blog", "playground" }, universe.Bodies.Select(body => body.Id));
        var about = universe.Bodies[0];
        Assert.Equal(new CelestialBody("about", "LOONACIA", "ABOUT", "소개입니다.", "/about", false, new BodyVisual("#fff2cc", "#ffd780", 3.5, 1.8), null), about);
        Assert.Equal(new BodyOrbit(8, 75, 0.5), universe.Bodies[1].Orbit);
        Assert.Equal(new BodyVisual("#236b8e", "#5fd4ff", 4, 0.4), universe.Bodies[1].Visual);
        Assert.Equal("/projects", universe.Bodies[1].Route);
        Assert.Equal("/blog", universe.Bodies[2].Route);
        Assert.True(universe.Bodies[3].Locked);
        Assert.Null(universe.Bodies[3].Route);
        Assert.NotNull(universe.Bodies[3].Orbit);
        Assert.Equal("Game & .NET Developer", universe.Stats.About.Title);
        Assert.Equal(new[] { "csharp", "unity" }, universe.Stats.About.Stack);
        Assert.Equal("you@example.com", universe.Stats.About.Email);
        Assert.Equal("https://github.com/", universe.Stats.About.Github);
        Assert.Equal("https://linkedin.com/", universe.Stats.About.Linkedin);
        Assert.Equal(0, universe.Stats.Blog.PostCount);
        Assert.Null(universe.Stats.Blog.LastPublished);
        Assert.Null(universe.Stats.Blog.Latest);
        Assert.Equal(0, universe.Stats.Projects.ProjectCount);
        Assert.Null(universe.Stats.Projects.Featured);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.OutputDir, "content", "universe.json")));
        Assert.Equal("https://example.com", json.RootElement.GetProperty("site").GetProperty("baseUrl").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("stats").GetProperty("blog").GetProperty("postCount").GetInt32());
    }

    [Theory]
    [InlineData("title", "LOONACIAVERSE")]
    [InlineData("baseUrl", "https://example.com")]
    [InlineData("author", "LOONACIA")]
    [InlineData("description", "개인 우주입니다.")]
    public void RejectsMissingEmptyNullAndWhitespaceSiteFields(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var replacement in new[] { "", $"{field}:", $"{field}: null", $"{field}: '   '" })
        {
            File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace($"{field}: {value}", replacement));
            AssertFailure(options, $"site.yml: {field} is required.");
        }
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("//example.com")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("mailto:hello@example.com")]
    [InlineData("https://")]
    public void RejectsNonAbsoluteOrNonHttpBaseUrl(string baseUrl)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace("https://example.com", baseUrl));
        AssertFailure(options, "site.yml: baseUrl must be an absolute http(s) URL.");
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/site/")]
    public void AcceptsAbsoluteHttpAndHttpsBaseUrls(string baseUrl)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), ContentTestData.SiteYaml.Replace("https://example.com", baseUrl));
        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.Equal(baseUrl, ReadUniverse(options).Site.BaseUrl);
    }

    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    [InlineData("playground")]
    public void RejectsEachMissingOrDuplicateBodyId(string id)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        var blocks = ContentTestData.UniverseYaml.Split("  - id:");
        var body = blocks.Single(block => block.StartsWith($" {id}\n") || block.StartsWith($" {id}\r\n"));
        var missing = string.Join("  - id:", blocks.Where(block => block != body));
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), missing);
        AssertFailure(options, $"universe.yml: Missing body id '{id}'.");

        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), ContentTestData.UniverseYaml + "\n  - id:" + body);
        var result = ContentBuildRunner.Run(options);
        Assert.Contains($"Duplicate body id '{id}'.", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Blog")]
    [InlineData("null")]
    public void RejectsUnknownBodyIdsAndReportsMissingExpectedId(string id)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), ContentTestData.UniverseYaml.Replace("id: blog", $"id: {id}"));
        var result = ContentBuildRunner.Run(options);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains("Unknown body id"));
        Assert.Contains("universe.yml: Missing body id 'blog'.", result.Errors);
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    public void UnlockedBodiesRequireNonblankRoute(string id)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var replacement in new[] { "", "route: null", "route: ''", "route: '   '" })
        {
            File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), ContentTestData.UniverseYaml.Replace($"route: /{id}", replacement));
            var result = ContentBuildRunner.Run(options);
            Assert.EndsWith(": route is required.", Assert.Single(result.Errors));
            AssertEmptyOutput(options);
        }
    }

    [Fact]
    public void UnlockingPlaygroundRequiresRoute()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), ContentTestData.UniverseYaml.Replace("locked: true", "locked: false"));
        AssertFailure(options, "universe.yml: bodies[3]: route is required.");
    }

    [Theory]
    [InlineData("projects", "8, periodSeconds: 75, phase: 0.5", 1)]
    [InlineData("blog", "12, periodSeconds: 100, phase: 2.5", 2)]
    [InlineData("playground", "16, periodSeconds: 120, phase: 4.5", 3)]
    public void PlanetsRequireOrbitEvenWhenLocked(string id, string orbit, int index)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var replacement in new[] { "", "orbit: null" })
        {
            File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), ContentTestData.UniverseYaml.Replace($"orbit: {{ radius: {orbit} }}", replacement));
            AssertFailure(options, $"universe.yml: bodies[{index}]: orbit is required for '{id}'.");
        }
    }

    [Fact]
    public void AboutMustNotHaveOrbit()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        var yaml = ContentTestData.UniverseYaml.Replace("route: /about", "route: /about\n    orbit: { radius: 1, periodSeconds: 60, phase: 0 }");
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), yaml);
        AssertFailure(options, "universe.yml: bodies[0]: about must not have an orbit.");
    }

    [Theory]
    [InlineData("site.yml")]
    [InlineData("universe.yml")]
    public void ReportsMissingEmptyAndMalformedYamlWithSourcePath(string fileName)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        var path = Path.Combine(options.ContentDir, fileName);
        File.Delete(path);
        AssertFailure(options, $"{fileName}: File is required.");
        File.WriteAllText(path, "");
        AssertFailure(options, $"{fileName}: YAML is empty.");
        File.WriteAllText(path, "invalid: [unclosed");
        var result = ContentBuildRunner.Run(options);
        Assert.StartsWith($"{fileName}: Invalid YAML:", Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData("bodies: []")]
    [InlineData("bodies: null")]
    [InlineData("other: []")]
    public void ReportsAllMissingIdsWhenBodiesAreAbsent(string yaml)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), yaml);
        var result = ContentBuildRunner.Run(options);
        Assert.Equal(4, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.StartsWith("universe.yml: Missing body id", error));
        AssertEmptyOutput(options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StatsMatchEmittedIndexesIncludingDraftFilteringAndFeaturedSortOnRebuild(bool includeDrafts)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        foreach (var slug in new[] { "published", "draft" })
        {
            var draft = slug == "draft";
            var postDir = Directory.CreateDirectory(Path.Combine(options.ContentDir, "posts", slug)).FullName;
            var date = draft ? "2026-10-26" : "2026-01-01";
            var metadata = $"title: {slug}\nslug: {slug}\ndate: {date}\nsummary: Summary\ntags: [{slug}]\ndraft: {draft.ToString().ToLowerInvariant()}";
            File.WriteAllText(Path.Combine(postDir, "index.md"), $"---\n{metadata}\n---\nBody");
            var projectMetadata = $"title: {slug}\nslug: {slug}\nsummary: Summary\nperiod: 2026\nrole: Developer\nstack: [{slug}]\nfeatured: true\norder: {(draft ? 0 : 1)}";
            ContentTestData.WriteProject(options.ContentDir, slug, projectMetadata + $"\ndraft: {draft.ToString().ToLowerInvariant()}");
        }

        ContentTestData.WriteProject(options.ContentDir, "later", "title: Later\nslug: later\nsummary: Summary\nperiod: 2026\nrole: Developer\nfeatured: true\norder: 2");
        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = true }).Success);
        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = includeDrafts }).Success);
        var universe = ReadUniverse(options);
        Assert.Equal(includeDrafts ? 2 : 1, universe.Stats.Blog.PostCount);
        Assert.Equal(includeDrafts ? 3 : 2, universe.Stats.Projects.ProjectCount);
        Assert.Equal(includeDrafts ? "draft" : "published", universe.Stats.Blog.Latest?.Slug);
        Assert.Equal(includeDrafts ? "draft" : "published", universe.Stats.Projects.Featured?.Slug);
        Assert.Equal(includeDrafts ? new DateOnly(2026, 10, 26) : new DateOnly(2026, 1, 1), universe.Stats.Blog.LastPublished);
        Assert.Equal(includeDrafts ? new[] { "draft", "published" } : ["published"], universe.Stats.Blog.TopTags);
        Assert.Equal(includeDrafts ? new[] { "draft", "published" } : ["published"], universe.Stats.Projects.TopStack);
        var contentOutputDir = Path.Combine(options.OutputDir, "content");
        using var posts = JsonDocument.Parse(File.ReadAllText(Path.Combine(contentOutputDir, "posts.json")));
        using var projects = JsonDocument.Parse(File.ReadAllText(Path.Combine(contentOutputDir, "projects.json")));
        Assert.Equal(posts.RootElement[0].GetRawText(), JsonSerializer.Serialize(universe.Stats.Blog.Latest, ContentJson.Options));
        Assert.Equal(projects.RootElement[0].GetRawText(), JsonSerializer.Serialize(universe.Stats.Projects.Featured, ContentJson.Options));
    }

    [Fact]
    public void CollectsYamlAndEarlierBuilderErrorsAndClearsPreviousSuccessfulOutput()
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        Assert.True(ContentBuildRunner.Run(options).Success);
        ContentTestData.WriteAbout(options.ContentDir, "stack: []");
        File.WriteAllText(Path.Combine(options.ContentDir, "site.yml"), "{}");
        File.WriteAllText(Path.Combine(options.ContentDir, "universe.yml"), "bodies: []");

        var result = ContentBuildRunner.Run(options);

        Assert.Equal(11, result.Errors.Count);
        Assert.Equal(3, result.Errors.Count(error => error.StartsWith("about.md:")));
        Assert.Equal(4, result.Errors.Count(error => error.StartsWith("site.yml:")));
        Assert.Equal(4, result.Errors.Count(error => error.StartsWith("universe.yml:")));
        AssertEmptyOutput(options);
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(options.ContentDir);
        ContentTestData.WriteUniverse(options.ContentDir);
        return options;
    }

    private static Universe ReadUniverse(BuildOptions options)
    {
        return JsonSerializer.Deserialize<Universe>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "universe.json")), ContentJson.Options)!;
    }

    private static void AssertFailure(BuildOptions options, string message)
    {
        var result = ContentBuildRunner.Run(options);
        Assert.False(result.Success);
        Assert.Equal(message, Assert.Single(result.Errors));
        AssertEmptyOutput(options);
    }

    private static void AssertEmptyOutput(BuildOptions options)
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }
}
