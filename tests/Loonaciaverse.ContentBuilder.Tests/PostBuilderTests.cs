using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class PostBuilderTests
{
    [Theory]
    [InlineData("2026-10-25", "beta", "alpha")]
    [InlineData("2026-10-26", "alpha", "beta")]
    public void RunWritesSortedPostsAndRoundTripsMetadataAndHtml(string alphaDate, string firstSlug, string secondSlug)
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Path.Combine(directory.Root, "source");
        var outputDir = Path.Combine(directory.Root, "wwwroot");
        WritePost(contentDir, "first-folder", """
            ---
            title: 두 번째 글
            slug: beta
            date: 2026-10-26
            summary: 두 번째 글의 요약입니다.
            tags: [threejs]
            ---
            본문입니다.
            """);
        WritePost(contentDir, "second-folder", $$"""
            ---
            title: 첫 번째 글
            slug: alpha
            date: {{alphaDate}}
            updated: 2026-10-27
            summary: 첫 번째 글의 요약입니다.
            tags: [blazor, dotnet]
            draft: true
            cover: cover.png
            ---
            ## 시작

            **안녕하세요**, ~~지난 이야기~~입니다.

            ```csharp
            Console.WriteLine("Hello");
            ```

            <iframe src="https://example.com/embed"></iframe>
            """);

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, false));

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        var indexJson = File.ReadAllText(Path.Combine(outputDir, "content", "posts.json"));
        var posts = JsonSerializer.Deserialize<PostSummary[]>(indexJson, ContentJson.Options)!;
        Assert.Equal(new[] { firstSlug, secondSlug }, posts.Select(post => post.Slug));
        var expectedAlpha = new PostSummary("alpha", "첫 번째 글", DateOnly.ParseExact(alphaDate, "yyyy-MM-dd"), new DateOnly(2026, 10, 27), "첫 번째 글의 요약입니다.", ["blazor", "dotnet"], true, "cover.png");
        var expectedBeta = new PostSummary("beta", "두 번째 글", new DateOnly(2026, 10, 26), null, "두 번째 글의 요약입니다.", ["threejs"], false, null);
        AssertSummary(expectedAlpha, posts.Single(post => post.Slug == "alpha"));
        AssertSummary(expectedBeta, posts.Single(post => post.Slug == "beta"));

        using var indexDocument = JsonDocument.Parse(indexJson);
        var alphaJson = indexDocument.RootElement.EnumerateArray().Single(post => post.GetProperty("slug").GetString() == "alpha");
        Assert.Equal(alphaDate, alphaJson.GetProperty("date").GetString());
        Assert.Equal("2026-10-27", alphaJson.GetProperty("updated").GetString());

        var detailsDir = Path.Combine(outputDir, "content", "posts");
        Assert.Equal(2, Directory.GetFiles(detailsDir, "*.json").Length);
        var alphaDetail = JsonSerializer.Deserialize<PostDetail>(File.ReadAllText(Path.Combine(detailsDir, "alpha.json")), ContentJson.Options)!;
        var betaDetail = JsonSerializer.Deserialize<PostDetail>(File.ReadAllText(Path.Combine(detailsDir, "beta.json")), ContentJson.Options)!;
        AssertSummary(expectedAlpha, alphaDetail.Meta);
        AssertSummary(expectedBeta, betaDetail.Meta);
        Assert.Contains("<h2", alphaDetail.Html);
        Assert.Contains("<strong>안녕하세요</strong>", alphaDetail.Html);
        Assert.Contains("<del>지난 이야기</del>", alphaDetail.Html);
        Assert.Contains("<pre><code class=\"language-csharp\">", alphaDetail.Html);
        Assert.Contains("Console.WriteLine(&quot;Hello&quot;);", alphaDetail.Html);
        Assert.Contains("<iframe src=\"https://example.com/embed\"></iframe>", alphaDetail.Html);
        Assert.DoesNotContain("summary:", alphaDetail.Html);
        Assert.Equal("<p>본문입니다.</p>\n", betaDetail.Html);

        var roundTrippedDetail = JsonSerializer.Deserialize<PostDetail>(JsonSerializer.Serialize(alphaDetail, ContentJson.Options), ContentJson.Options)!;
        AssertSummary(alphaDetail.Meta, roundTrippedDetail.Meta);
        Assert.Equal(alphaDetail.Html, roundTrippedDetail.Html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunWritesEmptyIndexWhenNoPostsExist(bool createPostsDirectory)
    {
        using var directory = new TemporaryDirectory();
        var contentDir = Directory.CreateDirectory(Path.Combine(directory.Root, "source")).FullName;
        var outputDir = Path.Combine(directory.Root, "wwwroot");
        if (createPostsDirectory)
        {
            Directory.CreateDirectory(Path.Combine(contentDir, "posts"));
        }

        var result = ContentBuildRunner.Run(new BuildOptions(contentDir, outputDir, false));

        Assert.True(result.Success);
        var json = File.ReadAllText(Path.Combine(outputDir, "content", "posts.json"));
        Assert.Equal("[]", json);
        Assert.Empty(JsonSerializer.Deserialize<PostSummary[]>(json, ContentJson.Options)!);
    }

    private static void WritePost(string contentDir, string folder, string markdown)
    {
        var postDir = Directory.CreateDirectory(Path.Combine(contentDir, "posts", folder)).FullName;
        File.WriteAllText(Path.Combine(postDir, "index.md"), markdown);
    }

    private static void AssertSummary(PostSummary expected, PostSummary actual)
    {
        Assert.Equal(expected.Slug, actual.Slug);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Date, actual.Date);
        Assert.Equal(expected.Updated, actual.Updated);
        Assert.Equal(expected.Summary, actual.Summary);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.Draft, actual.Draft);
        Assert.Equal(expected.Cover, actual.Cover);
    }
}
