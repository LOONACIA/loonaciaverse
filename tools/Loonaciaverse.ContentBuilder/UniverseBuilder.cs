using System.Text.Json;
using Loonaciaverse.Core.Content;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Loonaciaverse.ContentBuilder;

public static class UniverseBuilder
{
    private static readonly string[] BodyIds = ["about", "projects", "blog", "playground"];
    private static readonly string[] PlanetSurfaces = ["terran", "gas", "desert"];
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    public static BuildResult Build(string contentDir, string contentOutputDir, bool generateOutput = true)
    {
        var errors = new List<string>();
        var site = ReadYaml<SiteData>(contentDir, "site.yml", errors);
        var universe = ReadYaml<UniverseData>(contentDir, "universe.yml", errors);
        var validator = new ContentValidator();
        if (site is not null)
        {
            validator.ValidateRequired("site.yml", "title", site.Title);
            validator.ValidateRequired("site.yml", "baseUrl", site.BaseUrl);
            validator.ValidateRequired("site.yml", "author", site.Author);
            validator.ValidateRequired("site.yml", "description", site.Description);
            if (!string.IsNullOrWhiteSpace(site.BaseUrl) && (!Uri.TryCreate(site.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            {
                errors.Add("site.yml: baseUrl must be an absolute http(s) URL.");
            }
        }

        if (universe is not null)
        {
            ValidateBodies(universe.Bodies ?? [], validator, errors);
        }

        errors.AddRange(validator.Errors);
        // Still validate YAML when earlier builders failed, but do not read their incomplete output.
        if (errors.Count > 0 || !generateOutput)
        {
            return new BuildResult(errors);
        }

        var posts = ReadOutput<PostSummary[]>(contentOutputDir, "posts.json");
        var projects = ReadOutput<ProjectSummary[]>(contentOutputDir, "projects.json");
        var about = ReadOutput<AboutProfile>(contentOutputDir, "about.json");
        var siteInfo = new SiteInfo(site!.Title, site.BaseUrl, site.Author, site.Description);
        var bodies = universe!.Bodies!.Select(body => CreateBody(body!)).ToArray();
        var result = new Universe(siteInfo, bodies, HudStatsCalculator.Calculate(posts, projects, about));
        File.WriteAllText(Path.Combine(contentOutputDir, "universe.json"), JsonSerializer.Serialize(result, ContentJson.Options));
        return new BuildResult([]);
    }

    private static T? ReadYaml<T>(string contentDir, string fileName, List<string> errors) where T : class
    {
        var path = Path.Combine(contentDir, fileName);
        if (!File.Exists(path))
        {
            errors.Add($"{fileName}: File is required.");
            return null;
        }

        try
        {
            var data = Deserializer.Deserialize<T>(File.ReadAllText(path));
            if (data is null)
            {
                errors.Add($"{fileName}: YAML is empty.");
            }

            return data;
        }
        catch (YamlException exception)
        {
            errors.Add($"{fileName}: Invalid YAML: {exception.Message}");
            return null;
        }
    }

    private static void ValidateBodies(IReadOnlyList<BodyData?> bodies, ContentValidator validator, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < bodies.Count; index++)
        {
            var body = bodies[index];
            var sourcePath = $"universe.yml: bodies[{index}]";
            if (body is null)
            {
                errors.Add($"{sourcePath}: Body must be a mapping.");
                continue;
            }

            if (!BodyIds.Contains(body.Id, StringComparer.Ordinal))
            {
                errors.Add($"{sourcePath}: Unknown body id '{body.Id}'.");
            }
            else if (!ids.Add(body.Id))
            {
                errors.Add($"{sourcePath}: Duplicate body id '{body.Id}'.");
            }

            validator.ValidateRequired(sourcePath, "name", body.Name);
            validator.ValidateRequired(sourcePath, "subtitle", body.Subtitle);
            if (!body.Locked)
            {
                validator.ValidateRequired(sourcePath, "route", body.Route);
            }

            if (body.Visual is null)
            {
                errors.Add($"{sourcePath}: visual is required.");
            }
            else if (body.Id == "about" && body.Visual.Surface is not null)
            {
                errors.Add($"{sourcePath}: about must not have a surface.");
            }
            else if (body.Id != "about" && !PlanetSurfaces.Contains(body.Visual.Surface, StringComparer.Ordinal))
            {
                errors.Add($"{sourcePath}: surface must be one of {string.Join(", ", PlanetSurfaces)}.");
            }

            if (body.Id == "about" && body.Orbit is not null)
            {
                errors.Add($"{sourcePath}: about must not have an orbit.");
            }
            else if (body.Id != "about" && body.Orbit is null)
            {
                errors.Add($"{sourcePath}: orbit is required for '{body.Id}'.");
            }
        }

        foreach (var id in BodyIds.Where(id => !ids.Contains(id)))
        {
            errors.Add($"universe.yml: Missing body id '{id}'.");
        }
    }

    private static T ReadOutput<T>(string contentOutputDir, string fileName)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(contentOutputDir, fileName)), ContentJson.Options)!;
    }

    private static CelestialBody CreateBody(BodyData body)
    {
        var visual = new BodyVisual(body.Visual!.BaseColor, body.Visual.AtmosphereColor, body.Visual.NoiseScale, body.Visual.GlowIntensity, body.Visual.Surface);
        var orbit = body.Orbit is { } data ? new BodyOrbit(data.Radius, data.PeriodSeconds, data.Phase) : null;
        return new CelestialBody(body.Id, body.Name, body.Subtitle, body.Route, body.Locked, visual, orbit);
    }

    public sealed class SiteData
    {
        public string Title { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class UniverseData
    {
        public List<BodyData?>? Bodies { get; set; }
    }

    public sealed class BodyData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string? Route { get; set; }
        public bool Locked { get; set; }
        public VisualData? Visual { get; set; }
        public OrbitData? Orbit { get; set; }
    }

    public sealed class VisualData
    {
        public string BaseColor { get; set; } = "";
        public string AtmosphereColor { get; set; } = "";
        public double NoiseScale { get; set; }
        public double GlowIntensity { get; set; }
        public string? Surface { get; set; }
    }

    public sealed class OrbitData
    {
        public double Radius { get; set; }
        public double PeriodSeconds { get; set; }
        public double Phase { get; set; }
    }
}
