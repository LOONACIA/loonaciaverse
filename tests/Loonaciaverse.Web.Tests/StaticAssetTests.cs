using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Loonaciaverse.Web;

namespace Loonaciaverse.Web.Tests;

public class StaticAssetTests
{
    [Theory]
    [InlineData("css/app.css")]
    [InlineData("fonts/pretendard/pretendardvariable-dynamic-subset.css")]
    public void StylesheetRelativeUrlsResolveToFilesInsideWebRoot(string stylesheet)
    {
        var webRoot = ContentBuildIntegrationTests.FindWebRoot();
        var cssPath = Path.Combine(webRoot, stylesheet);
        var css = File.ReadAllText(cssPath);
        var urls = Regex.Matches(css, """url\(\s*['"]?(?<url>[^'"\s)]+)['"]?\s*\)""");

        Assert.NotEmpty(urls);

        foreach (Match match in urls)
        {
            var url = match.Groups["url"].Value;
            if (url.StartsWith('/') || url.StartsWith('#') || Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cssPath)!, Uri.UnescapeDataString(url.Split('?', '#')[0])));
            AssertFileInsideWebRoot(webRoot, path);
        }
    }

    [Fact]
    public void IndexLinksExistingThemePretendardAndGeneratedIsolationStylesheets()
    {
        var webRoot = ContentBuildIntegrationTests.FindWebRoot();
        var html = File.ReadAllText(Path.Combine(webRoot, "index.html"));
        var stylesheets = new List<string>();

        foreach (Match link in Regex.Matches(html, @"<link\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(link.Value, """\brel\s*=\s*['"]stylesheet['"]""", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var href = Regex.Match(link.Value, """\bhref\s*=\s*['"](?<href>[^'"]+)['"]""", RegexOptions.IgnoreCase);
            Assert.True(href.Success, $"Stylesheet link has no href: {link.Value}");
            var path = href.Groups["href"].Value.Replace("#[.{fingerprint}]", "");
            stylesheets.Add(path);
            if (path == "Loonaciaverse.Web.styles.css")
            {
                var configuration = typeof(App).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
                var intermediateRoot = Path.GetFullPath(Path.Combine(webRoot, "..", "obj", configuration));
                var bundle = Assert.Single(Directory.EnumerateFiles(intermediateRoot, path, SearchOption.AllDirectories), file => Path.GetFileName(Path.GetDirectoryName(file)) == "bundle");
                var css = File.ReadAllText(bundle);
                Assert.Contains("Components/TopBar.razor.rz.scp.css", css);
                Assert.Contains("Components/LostInSpace.razor.rz.scp.css", css);
                Assert.Contains("Layout/PageLayout.razor.rz.scp.css", css);
            }
            else
            {
                AssertFileInsideWebRoot(webRoot, Path.GetFullPath(Path.Combine(webRoot, path)));
            }
        }

        Assert.Contains("css/app.css", stylesheets);
        Assert.Contains("fonts/pretendard/pretendardvariable-dynamic-subset.css", stylesheets);
        Assert.Contains("Loonaciaverse.Web.styles.css", stylesheets);
        Assert.True(stylesheets.IndexOf("css/app.css") < stylesheets.IndexOf("Loonaciaverse.Web.styles.css"));
    }

    [Theory]
    [InlineData("--color-bg", "#05070d")]
    [InlineData("--color-surface", "rgba(11, 18, 32, 0.8)")]
    [InlineData("--color-hud", "#5fd4ff")]
    [InlineData("--color-hud-dim", "rgba(95, 212, 255, 0.35)")]
    [InlineData("--color-text", "#e6eef7")]
    [InlineData("--color-text-muted", "#8a9bb0")]
    [InlineData("--color-locked", "#ffb347")]
    [InlineData("--color-danger", "#ff5f6d")]
    public void RootDefinesDesignColorTokenWithSpecifiedValue(string token, string expectedValue)
    {
        var css = File.ReadAllText(Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "css", "app.css"));
        var root = Assert.Single(Regex.Matches(css, @":root\s*\{(?<body>[^}]*)\}").Cast<Match>());
        var declaration = Assert.Single(Regex.Matches(root.Groups["body"].Value, $@"{Regex.Escape(token)}\s*:\s*(?<value>[^;]+);").Cast<Match>());

        Assert.Equal(expectedValue, declaration.Groups["value"].Value.Trim());
    }

    [Theory]
    [InlineData("#app")]
    [InlineData("noscript")]
    public void BootScreenAndNoscriptLinkToDetailPagesInOrder(string selector)
    {
        var html = File.ReadAllText(Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "index.html"));
        var document = new HtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(html);
        var container = document.QuerySelector(selector);
        Assert.NotNull(container);
        var links = container.QuerySelectorAll("a");

        Assert.Equal(new[] { "/projects", "/blog", "/about" }, links.Select(link => link.GetAttribute("href")));
        Assert.Equal(new[] { "PROJECTS", "BLOG", "ABOUT" }, links.Select(link => link.TextContent.Trim()));
    }

    [Fact]
    public void BootScreenContainsNavigationSystemBootLabelAndSeparateNoscriptNotice()
    {
        var html = File.ReadAllText(Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "index.html"));
        var document = new HtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(html);
        var app = document.QuerySelector("#app");
        Assert.NotNull(app);

        Assert.Contains("NAVIGATION SYSTEM BOOT", app.TextContent);
        Assert.Null(app.QuerySelector("noscript"));
        var noscript = document.QuerySelector("body > noscript");
        Assert.NotNull(noscript);
        Assert.Contains("이 사이트는 JavaScript가 필요합니다.", noscript.TextContent);
    }

    [Theory]
    [InlineData("width: var(--blazor-load-percentage, 0%);")]
    [InlineData("content: var(--blazor-load-percentage-text, \"0%\");")]
    public void BootGaugeUsesBlazorLoadVariablesWithInitialFallbacks(string declaration)
    {
        var css = File.ReadAllText(Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "css", "app.css"));

        Assert.Contains(declaration, css);
    }

    private static void AssertFileInsideWebRoot(string webRoot, string path)
    {
        var relativePath = Path.GetRelativePath(webRoot, path);
        Assert.False(Path.IsPathRooted(relativePath), $"Asset is outside wwwroot: {path}");
        Assert.NotEqual("..", relativePath);
        Assert.False(relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal), $"Asset is outside wwwroot: {path}");
        Assert.True(File.Exists(path), $"Expected static asset: {path}");
    }
}
