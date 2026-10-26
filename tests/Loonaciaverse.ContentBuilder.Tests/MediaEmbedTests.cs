using System.Text.Json;
using System.Text.RegularExpressions;

namespace Loonaciaverse.ContentBuilder.Tests;

public class MediaEmbedTests
{
    [Theory]
    [InlineData("posts/example.json", "/content/posts/example/cover.png")]
    [InlineData("projects/example.json", "/content/projects/example/cover.png")]
    [InlineData("about.json", "cover.png")]
    public void RunEmbedsYouTubeWithoutCookiesAndPreservesImagesAndOtherHosts(string detailPath, string imageUrl)
    {
        using var directory = new TemporaryDirectory();
        var options = CreateOptions(directory);
        const string body = """
            ![Watch video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)

            ![Short link](https://youtu.be/aqz-KE-bpKQ)

            ![Vimeo video](https://vimeo.com/123456789)

            ![alt](cover.png)
            """;
        ContentTestData.WriteAbout(options.ContentDir, body: body);
        ContentTestData.WriteProject(options.ContentDir, "example", "title: Example\nslug: example\nsummary: A summary\nperiod: 2026 - present\nrole: Developer", body);
        var postDir = Directory.CreateDirectory(Path.Combine(options.ContentDir, "posts", "example")).FullName;
        File.WriteAllText(Path.Combine(postDir, "index.md"), $"---\ntitle: Example\nslug: example\ndate: 2026-10-27\nsummary: A summary\n---\n{body}");
        foreach (var assetDir in new[] { postDir, Path.Combine(options.ContentDir, "projects", "example"), options.ContentDir })
        {
            File.WriteAllBytes(Path.Combine(assetDir, "cover.png"), [1, 2, 3]);
        }

        var result = ContentBuildRunner.Run(options);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(options.OutputDir, "content", detailPath)));
        var html = document.RootElement.GetProperty("html").GetString()!;
        var iframes = Regex.Matches(html, @"<iframe\b[^>]*>").Select(match => match.Value).ToArray();
        Assert.Equal(3, iframes.Length);
        foreach (var videoId in new[] { "dQw4w9WgXcQ", "aqz-KE-bpKQ" })
        {
            var iframe = Assert.Single(iframes, iframe => iframe.Contains($"src=\"https://www.youtube-nocookie.com/embed/{videoId}\""));
            Assert.Contains("class=\"youtube\"", iframe);
            Assert.Contains("allowfullscreen", iframe);
        }

        Assert.DoesNotContain("www.youtube.com/embed", html);
        var vimeo = Assert.Single(iframes, iframe => iframe.Contains("src=\"https://player.vimeo.com/video/123456789\""));
        Assert.Contains("class=\"vimeo\"", vimeo);
        var image = Assert.Single(Regex.Matches(html, @"<img\b[^>]*>").Cast<Match>());
        Assert.Contains($"src=\"{imageUrl}\"", image.Value);
        Assert.Contains("alt=\"alt\"", image.Value);
    }

    private static BuildOptions CreateOptions(TemporaryDirectory directory)
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteAbout(options.ContentDir);
        ContentTestData.WriteUniverse(options.ContentDir);
        return options;
    }
}
