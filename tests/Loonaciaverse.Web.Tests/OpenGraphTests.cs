using System.Buffers.Binary;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Tests;

public class OpenGraphTests
{
    private const string UpdateGuidance = "content/site.yml과 index.html의 OG 값을 함께 고친다";

    [Fact]
    public void SiteMetadataMatchesBuiltSiteInfo()
    {
        var webRoot = ContentBuildIntegrationTests.FindWebRoot();
        var universePath = Path.Combine(webRoot, "content", "universe.json");
        var universe = Assert.IsType<Universe>(JsonSerializer.Deserialize<Universe>(File.ReadAllText(universePath), ContentJson.Options));
        var site = universe.Site;
        var baseUrl = site.BaseUrl.TrimEnd('/');
        var expectedValues = new Dictionary<string, string>
        {
            ["og:site_name"] = site.Title,
            ["og:title"] = site.Title,
            ["og:description"] = site.Description,
            ["og:url"] = baseUrl + "/",
            ["og:image"] = baseUrl + "/og-image.png",
            ["og:image:alt"] = site.Title
        };
        var document = ReadIndex();

        foreach (var (property, expected) in expectedValues)
        {
            var actual = ReadMeta(document, "property", property);
            Assert.True(expected == actual, $"{property}: expected '{expected}', actual '{actual}'. {UpdateGuidance}");
        }
    }

    [Theory]
    [InlineData("og:url")]
    [InlineData("og:image")]
    public void PublicUrlsAreAbsoluteHttpsUrls(string property)
    {
        var value = ReadMeta(ReadIndex(), "property", property);

        Assert.StartsWith("https://", value, StringComparison.Ordinal);
        Assert.True(Uri.TryCreate(value, UriKind.Absolute, out var url), $"Expected an absolute URL for {property}: {value}");
        Assert.Equal(Uri.UriSchemeHttps, url.Scheme);
        Assert.NotEmpty(url.Host);
    }

    [Fact]
    public void ImageUrlPointsToLocalPngWithRequiredDimensions()
    {
        var imageUrl = new Uri(ReadMeta(ReadIndex(), "property", "og:image"), UriKind.Absolute);
        var webRoot = ContentBuildIntegrationTests.FindWebRoot();
        var relativePath = Uri.UnescapeDataString(imageUrl.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var imagePath = Path.GetFullPath(Path.Combine(webRoot, relativePath));
        var pathInsideRoot = Path.GetRelativePath(webRoot, imagePath);

        Assert.False(Path.IsPathRooted(pathInsideRoot));
        Assert.NotEqual("..", pathInsideRoot);
        Assert.False(pathInsideRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        Assert.True(File.Exists(imagePath), $"Expected OG image in wwwroot: {imagePath}");

        var png = File.ReadAllBytes(imagePath);
        Assert.True(png.Length >= 33, "Expected a PNG signature and complete IHDR chunk.");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(13u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(8, 4)));
        Assert.True(png.AsSpan(12, 4).SequenceEqual("IHDR"u8), "Expected IHDR as the first PNG chunk.");
        Assert.Equal(1200u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(630u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Theory]
    [InlineData("property", "og:type", "website")]
    [InlineData("property", "og:locale", "ko_KR")]
    [InlineData("property", "og:image:width", "1200")]
    [InlineData("property", "og:image:height", "630")]
    [InlineData("name", "twitter:card", "summary_large_image")]
    public void SharingMetadataUsesRequiredValues(string attribute, string key, string expected)
    {
        Assert.Equal(expected, ReadMeta(ReadIndex(), attribute, key));
    }

    [Fact]
    public void IndexLeavesDescriptionAndCanonicalToRuntimePageMetadata()
    {
        var document = ReadIndex();

        Assert.Empty(document.QuerySelectorAll("head meta[name='description'], head link[rel='canonical']"));
    }

    private static IDocument ReadIndex() => new HtmlParser().ParseDocument(File.ReadAllText(Path.Combine(ContentBuildIntegrationTests.FindWebRoot(), "index.html")));

    private static string ReadMeta(IDocument document, string attribute, string key)
    {
        var elements = document.QuerySelectorAll($"head meta[{attribute}='{key}']");
        Assert.True(elements.Length == 1, $"Expected exactly one {key} meta tag, found {elements.Length}. {UpdateGuidance}");
        var content = elements[0].GetAttribute("content");
        Assert.True(content is not null, $"Expected content for {key}. {UpdateGuidance}");
        return content;
    }
}
