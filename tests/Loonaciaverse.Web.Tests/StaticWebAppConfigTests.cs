using System.Text.Json;

namespace Loonaciaverse.Web.Tests;

public class StaticWebAppConfigTests
{
    [Fact]
    public void NavigationFallbackRewritesToIndex()
    {
        using var config = ReadConfig();

        Assert.Equal("/index.html", config.RootElement.GetProperty("navigationFallback").GetProperty("rewrite").GetString());
    }

    [Fact]
    public void NavigationFallbackExcludesStaticAssetPathsAndExtensions()
    {
        using var config = ReadConfig();
        var exclude = config.RootElement.GetProperty("navigationFallback").GetProperty("exclude").EnumerateArray().Select(value => value.GetString()!).ToArray();

        foreach (var path in new[] { "/_framework/*", "/content/*", "/js/*", "/css/*", "/fonts/*" })
        {
            Assert.Contains(path, exclude);
        }

        var extensionPattern = Assert.Single(exclude, pattern => pattern.StartsWith("*.{", StringComparison.Ordinal) && pattern.EndsWith('}'));
        var extensions = extensionPattern[3..^1].Split(',');

        foreach (var extension in new[] { "png", "xml", "json", "wasm", "woff2" })
        {
            Assert.Contains(extension, extensions);
        }
    }

    [Fact]
    public void ContentRouteRequiresCacheRevalidation()
    {
        using var config = ReadConfig();
        var route = Assert.Single(config.RootElement.GetProperty("routes").EnumerateArray(), route => route.GetProperty("route").GetString() == "/content/*");
        var header = Assert.Single(route.GetProperty("headers").EnumerateObject(), header => string.Equals(header.Name, "Cache-Control", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("no-cache", header.Value.GetString());
    }

    private static JsonDocument ReadConfig()
    {
        var path = Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "staticwebapp.config.json");
        Assert.True(File.Exists(path), $"Expected static asset: {path}");

        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
