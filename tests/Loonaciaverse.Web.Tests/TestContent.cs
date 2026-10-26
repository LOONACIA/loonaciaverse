using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Tests;

public static class TestContent
{
    public static AboutProfile About() => new(
        "루나", "개발자", ["C#", "Blazor", "게임 개발"], "loona@example.com", "https://github.com/example", "https://www.linkedin.com/in/example", "<h2>소개</h2><p>함께 <strong>만듭니다</strong>.</p>");

    public static Universe Universe()
    {
        var about = About();
        return new Universe(
            new SiteInfo("LOONACIAVERSE", "https://example.com", "루나", "루나의 우주"),
            [
                Body("about", "LOONACIA", "ABOUT", "/about"),
                Body("projects", "ARTIFEX", "PROJECTS", "/projects"),
                Body("blog", "ARCHIVA", "BLOG", "/blog"),
                Body("playground", "ARCADIA", "PLAYGROUND", null, true)
            ],
            new HudStats(new BlogStats(0, null, [], null), new ProjectStats(0, [], null), new AboutStats(about.Title, about.Stack, about.Email, about.Github, about.Linkedin)));
    }

    private static CelestialBody Body(string id, string name, string subtitle, string? route, bool locked = false) =>
        new(id, name, subtitle, "테스트 천체", route, locked, new BodyVisual("#123456", "#abcdef", 1, 0.8), id == "about" ? null : new BodyOrbit(10, 60, 0));
}
