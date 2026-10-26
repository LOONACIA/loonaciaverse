using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class PostAssetsAndTocTests
{
    [Fact]
    public void TocContainsOnlyLevelsTwoAndThreeInOrderWithUniqueIdsMatchingHtml()
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, """
            # 제목
            ## 시작
            ### 세부 **내용**과 `코드` &amp; [링크](guide.pdf)
            #### 제외
            ## 시작
            ### 마무리

            ```markdown
            ## 코드 안 헤딩
            ```
            """);

        Assert.True(ContentBuildRunner.Run(options).Success);
        var detail = ReadDetail(options);

        Assert.Equal(new[] { 2, 3, 2, 3 }, detail.Toc.Select(entry => entry.Level));
        Assert.Equal(new[] { "시작", "세부 내용과 코드 & 링크", "시작", "마무리" }, detail.Toc.Select(entry => entry.Text));
        Assert.All(detail.Toc, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Id)));
        Assert.Equal(detail.Toc.Count, detail.Toc.Select(entry => entry.Id).Distinct().Count());
        var htmlIds = Regex.Matches(detail.Html, "<h[23] id=\"([^\"]+)\"").Select(match => WebUtility.HtmlDecode(match.Groups[1].Value));
        Assert.Equal(detail.Toc.Select(entry => entry.Id), htmlIds);
    }

    [Fact]
    public void CopiesAllAttachmentsRecursivelyAndRewritesCoverInIndexAndDetail()
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, "![orbit](images/orbit.svg)", "cover: images/orbit.svg?version=1#center");
        var postDir = Path.Combine(options.ContentDir, "posts", "example");
        Directory.CreateDirectory(Path.Combine(postDir, "images"));
        File.WriteAllText(Path.Combine(postDir, "images", "orbit.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(postDir, "images", "index.md"), "Nested attachment");
        File.WriteAllBytes(Path.Combine(postDir, "download.bin"), [0, 1, 127, 255]);

        Assert.True(ContentBuildRunner.Run(options).Success);

        var outputDir = Path.Combine(options.OutputDir, "content", "posts", "example");
        Assert.Equal("<svg/>", File.ReadAllText(Path.Combine(outputDir, "images", "orbit.svg")));
        Assert.Equal("Nested attachment", File.ReadAllText(Path.Combine(outputDir, "images", "index.md")));
        Assert.Equal(new byte[] { 0, 1, 127, 255 }, File.ReadAllBytes(Path.Combine(outputDir, "download.bin")));
        Assert.False(File.Exists(Path.Combine(outputDir, "index.md")));
        Assert.Equal(3, Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Length);
        var detail = ReadDetail(options);
        Assert.Equal("/content/posts/example/images/orbit.svg?version=1#center", detail.Meta.Cover);
        Assert.Contains("src=\"/content/posts/example/images/orbit.svg\"", detail.Html);
        var posts = JsonSerializer.Deserialize<PostSummary[]>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "posts.json")), ContentJson.Options)!;
        Assert.Equal(detail.Meta.Cover, Assert.Single(posts).Cover);
    }

    [Theory]
    [InlineData("images/orbit.svg", "/content/posts/example/images/orbit.svg")]
    [InlineData("./guide.pdf?download=1#page2", "/content/posts/example/./guide.pdf?download=1#page2")]
    [InlineData("https://example.com/orbit.svg", "https://example.com/orbit.svg")]
    [InlineData("//example.com/orbit.svg", "//example.com/orbit.svg")]
    [InlineData("/shared/orbit.svg", "/shared/orbit.svg")]
    [InlineData("#section", "#section")]
    [InlineData("mailto:hello@example.com", "mailto:hello@example.com")]
    [InlineData("data:image/png;base64,AAAA", "data:image/png;base64,AAAA")]
    public void RewritesInlineAndReferenceImagesAndLinksUsingUrlRules(string url, string expectedUrl)
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, $"![image]({url})\n\n[link]({url})\n\n![reference image][asset]\n\n[reference link][asset]\n\n[asset]: {url}");

        Assert.True(ContentBuildRunner.Run(options).Success);
        var html = ReadDetail(options).Html;

        Assert.Equal(2, Regex.Matches(html, Regex.Escape($"src=\"{expectedUrl}\"")).Count);
        Assert.Equal(2, Regex.Matches(html, Regex.Escape($"href=\"{expectedUrl}\"")).Count);
    }

    [Fact]
    public void KeepsMarkdownAndHtmlInsideCodeUnchanged()
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, """
            ```markdown
            ![orbit](orbit.svg)
            [download](guide.pdf)
            <img src="orbit.svg">
            ```

            `![orbit](orbit.svg)`

                [download](guide.pdf)
            """);

        Assert.True(ContentBuildRunner.Run(options).Success);
        var html = ReadDetail(options).Html;

        Assert.Contains("![orbit](orbit.svg)\n[download](guide.pdf)\n&lt;img src=&quot;orbit.svg&quot;&gt;", html);
        Assert.Contains("<code>![orbit](orbit.svg)</code>", html);
        Assert.Contains("<pre><code>[download](guide.pdf)", html);
        Assert.DoesNotContain("/content/posts/", html);
        Assert.Empty(ReadDetail(options).Toc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsMissingCoverEvenForExcludedDraftAndLeavesNoPartialOutput(bool draft)
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, "Body", $"cover: missing.svg\ndraft: {draft.ToString().ToLowerInvariant()}");

        var result = ContentBuildRunner.Run(options);

        Assert.False(result.Success);
        Assert.Equal("posts/example/index.md: cover 'missing.svg' must reference an existing file in the content folder.", Assert.Single(result.Errors));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Theory]
    [InlineData("https://example.com/cover.svg")]
    [InlineData("/shared/cover.svg")]
    [InlineData("//example.com/cover.svg")]
    public void PreservesNonRelativeCoverWithoutRequiringLocalFile(string cover)
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, "Body", $"cover: {cover}");

        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.Equal(cover, ReadDetail(options).Meta.Cover);
    }

    [Fact]
    public void ExcludedDraftAttachmentsAreRemovedOnRebuild()
    {
        using var directory = new TemporaryDirectory();
        var options = WritePost(directory, "Body", "draft: true");
        File.WriteAllText(Path.Combine(options.ContentDir, "posts", "example", "orbit.svg"), "<svg/>");
        Assert.True(ContentBuildRunner.Run(options with { IncludeDrafts = true }).Success);
        var assetPath = Path.Combine(options.OutputDir, "content", "posts", "example", "orbit.svg");
        Assert.True(File.Exists(assetPath));

        Assert.True(ContentBuildRunner.Run(options).Success);
        Assert.False(File.Exists(assetPath));
    }

    private static BuildOptions WritePost(TemporaryDirectory directory, string body, string metadata = "")
    {
        var options = new BuildOptions(Path.Combine(directory.Root, "source"), Path.Combine(directory.Root, "wwwroot"), false);
        var postDir = Directory.CreateDirectory(Path.Combine(options.ContentDir, "posts", "example")).FullName;
        File.WriteAllText(Path.Combine(postDir, "index.md"), $"---\ntitle: Example\nslug: example\ndate: 2026-10-26\nsummary: Summary\n{metadata}\n---\n{body}");
        return options;
    }

    private static PostDetail ReadDetail(BuildOptions options)
    {
        return JsonSerializer.Deserialize<PostDetail>(File.ReadAllText(Path.Combine(options.OutputDir, "content", "posts", "example.json")), ContentJson.Options)!;
    }
}
