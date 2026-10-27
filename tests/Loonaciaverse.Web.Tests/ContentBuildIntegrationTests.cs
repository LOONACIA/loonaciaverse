using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Tests;

public class ContentBuildIntegrationTests
{
    [Fact]
    public void WebBuildGeneratesContentReadableByCoreModels()
    {
        var webRoot = FindWebRoot();

        var universe = ReadContent<Universe>(webRoot, "universe.json");
        ReadContent<PostSummary[]>(webRoot, "posts.json");
        ReadContent<ProjectSummary[]>(webRoot, "projects.json");
        var about = ReadContent<AboutProfile>(webRoot, "about.json");

        Assert.False(string.IsNullOrWhiteSpace(universe.Site.Title));
        Assert.NotEmpty(universe.Bodies);
        Assert.False(string.IsNullOrWhiteSpace(about.Name));
    }

    [Theory]
    [InlineData("feed.xml")]
    [InlineData("sitemap.xml")]
    [InlineData("robots.txt")]
    public void WebBuildGeneratesSeoFiles(string fileName)
    {
        var path = Path.Combine(FindWebRoot(), fileName);

        Assert.True(File.Exists(path), $"Expected build output: {path}");
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(path)));
    }

    private static T ReadContent<T>(string webRoot, string fileName) where T : class
    {
        var path = Path.Combine(webRoot, "content", fileName);
        Assert.True(File.Exists(path), $"Expected build output: {path}");

        var content = JsonSerializer.Deserialize<T>(File.ReadAllText(path), ContentJson.Options);

        return Assert.IsType<T>(content);
    }

    internal static string FindWebRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                return Path.Combine(directory.FullName, "src", "Loonaciaverse.Web", "wwwroot");
            }
        }

        throw new DirectoryNotFoundException($"Could not find the repository root above {AppContext.BaseDirectory}.");
    }
}
