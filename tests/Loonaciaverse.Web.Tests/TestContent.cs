using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Tests;

public static class TestContent
{
    public static PostDetail Post() => new(
        Posts()[0] with { Updated = new DateOnly(2026, 10, 20), Draft = true, Cover = "/images/post-cover.webp" },
        "<h2 id=\"소개\">소개</h2><p>함께 <strong>기록합니다</strong>.</p><pre><code class=\"language-csharp\">var value = 1;</code></pre>",
        [new TocEntry(2, "소개", "소개")]);

    public static IReadOnlyList<PostSummary> Posts() =>
    [
        new("first-post", "첫 번째 기록", new DateOnly(2024, 1, 2), null, "오래된 글도 서비스 순서를 따릅니다.", ["Blazor", "Unity", "게임 개발"], false, null),
        new("draft-post", "작성 중인 기록", new DateOnly(2026, 10, 20), null, "아직 작성 중입니다.", ["Blazor", "C#"], true, null),
        new("last-post", "마지막 기록", new DateOnly(2025, 2, 3), null, "기술을 기록합니다.", ["Blazor", "C#", "alpha"], false, null)
    ];

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
