namespace Loonaciaverse.ContentBuilder.Tests;

internal static class ContentTestData
{
    public const string AboutMetadata = "name: LOONACIA\ntitle: Game & .NET Developer\nemail: you@example.com";
    public const string SiteYaml = "title: LOONACIAVERSE\nbaseUrl: https://example.com\nauthor: LOONACIA\ndescription: 개인 우주입니다.";
    public const string UniverseYaml = """
        bodies:
          - id: about
            name: LOONACIA
            subtitle: ABOUT
            tagline: 소개입니다.
            route: /about
            visual: { baseColor: '#fff2cc', atmosphereColor: '#ffd780', noiseScale: 3.5, glowIntensity: 1.8 }
          - id: projects
            name: ARTIFEX
            subtitle: PROJECTS
            tagline: 작업들입니다.
            route: /projects
            visual: { baseColor: '#236b8e', atmosphereColor: '#5fd4ff', noiseScale: 4, glowIntensity: 0.4, surface: terran }
            orbit: { radius: 8, periodSeconds: 75, phase: 0.5 }
          - id: blog
            name: ARCHIVA
            subtitle: BLOG
            tagline: 기록입니다.
            route: /blog
            visual: { baseColor: '#6956a8', atmosphereColor: '#bda5ff', noiseScale: 5, glowIntensity: 0.3, surface: gas }
            orbit: { radius: 12, periodSeconds: 100, phase: 2.5 }
          - id: playground
            name: ARCADIA
            subtitle: PLAYGROUND
            tagline: 실험을 준비합니다.
            locked: true
            visual: { baseColor: '#785339', atmosphereColor: '#ffb347', noiseScale: 6, glowIntensity: 0.2, surface: desert }
            orbit: { radius: 16, periodSeconds: 120, phase: 4.5 }
        """;

    public static void WriteUniverse(string contentDir)
    {
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "site.yml"), SiteYaml);
        File.WriteAllText(Path.Combine(contentDir, "universe.yml"), UniverseYaml);
    }

    public static void WriteAbout(string contentDir, string metadata = AboutMetadata, string body = "소개입니다.")
    {
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "about.md"), $"---\n{metadata}\n---\n{body}");
    }

    public static void WriteProject(string contentDir, string folder, string metadata, string body = "본문입니다.")
    {
        var projectDir = Directory.CreateDirectory(Path.Combine(contentDir, "projects", folder)).FullName;
        File.WriteAllText(Path.Combine(projectDir, "index.md"), $"---\n{metadata}\n---\n{body}");
    }
}
