using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class BlogListTests
{
    [Fact]
    public void RendersPostsInServiceOrderWithMetadataAndDraftBadge()
    {
        using var context = new BunitContext();
        var posts = TestContent.Posts();
        var service = ConfigureService(context, posts);

        var component = context.Render<BlogList>();

        var articles = component.FindAll("article.post-list-item");
        Assert.Equal(posts.Select(post => post.Title), articles.Select(article => article.QuerySelector("h2")!.TextContent));
        for (var index = 0; index < posts.Count; index++)
        {
            var post = posts[index];
            var article = articles[index];
            Assert.Equal($"/blog/{post.Slug}", article.QuerySelector("h2 a")!.GetAttribute("href"));
            Assert.Equal(post.Summary, article.QuerySelector(".post-summary")!.TextContent);
            Assert.Equal(Formatting.FormatDate(post.Date), article.QuerySelector("time")!.TextContent);
            Assert.Equal(Formatting.FormatDate(post.Date), article.QuerySelector("time")!.GetAttribute("datetime"));
            var tags = article.QuerySelectorAll(".post-tags a.chip");
            Assert.Equal(post.Tags, tags.Select(tag => tag.TextContent));
            Assert.Equal(post.Tags.Select(tag => $"/blog/tags/{Uri.EscapeDataString(tag)}"), tags.Select(tag => tag.GetAttribute("href")));
            Assert.Equal(post.Draft ? 1 : 0, article.QuerySelectorAll(".badge.badge-draft").Length);
        }

        Assert.Equal("DRAFT", component.Find(".badge.badge-draft").TextContent);
        Assert.Equal("2024-01-02", articles[0].QuerySelector("time")!.TextContent);
        Assert.Single(service.PostsCalls);
    }

    [Fact]
    public void SortsTagChipsByPostCountThenOrdinalNameWithEncodedLinks()
    {
        using var context = new BunitContext();
        var posts = TestContent.Posts().ToArray();
        posts[0] = posts[0] with { Tags = ["Blazor", "Unity", "게임 개발", "Blazor"] };
        ConfigureService(context, posts);

        var component = context.Render<BlogList>();

        var tags = component.FindAll(".blog-tags a.chip");
        Assert.Equal(new[] { "Blazor", "C#", "Unity", "alpha", "게임 개발" }, tags.Select(tag => tag.TextContent));
        Assert.Equal(tags.Select(tag => $"/blog/tags/{Uri.EscapeDataString(tag.TextContent)}"), tags.Select(tag => tag.GetAttribute("href")));
        Assert.Equal("/blog/tags/C%23", tags[1].GetAttribute("href"));
    }

    [Fact]
    public void FiltersTagWithoutChangingServiceOrderAndOverridesUniverseLabel()
    {
        using var context = new BunitContext();
        var posts = TestContent.Posts();
        ConfigureService(context, posts);

        var component = context.Render<CascadingValue<Universe>>(parameters => parameters
            .Add(p => p.Value, TestContent.Universe())
            .AddChildContent<BlogList>(child => child.Add(p => p.Tag, "C#")));

        Assert.Equal(posts.Where(post => post.Tags.Contains("C#")).Select(post => post.Title), component.FindAll("article h2").Select(title => title.TextContent));
        Assert.Equal("TAG", component.Find(".page-header .hud-label").TextContent);
        Assert.Equal("C#", component.Find(".page-header h1").TextContent);
        Assert.Equal("/blog", component.Find("a.all-posts").GetAttribute("href"));
        Assert.Equal("전체 글", component.Find("a.all-posts").TextContent);
        Assert.Empty(component.FindAll(".blog-tags"));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("blazor")]
    public void MissingTagShowsLostInSpace(string tag)
    {
        using var context = new BunitContext();
        ConfigureService(context, TestContent.Posts());

        var component = context.Render<BlogList>(parameters => parameters.Add(p => p.Tag, tag));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("article, .page-header, .blog-empty"));
    }

    [Fact]
    public void EmptyListShowsMessage()
    {
        using var context = new BunitContext();
        ConfigureService(context, []);

        var component = context.Render<BlogList>();

        Assert.Equal("BLOG", component.Find(".page-header h1").TextContent);
        Assert.Equal("아직 기록된 글이 없습니다.", component.Find(".blog-empty").TextContent);
        Assert.Empty(component.FindAll("article, .lost-in-space, .chip"));
    }

    [Fact]
    public void EmptyListOnTagRouteShowsLostInSpace()
    {
        using var context = new BunitContext();
        ConfigureService(context, []);

        var component = context.Render<BlogList>(parameters => parameters.Add(p => p.Tag, "C#"));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll(".blog-empty"));
    }

    [Theory]
    [InlineData("/blog", "BLOG", "ARCHIVA", "BLOG · LOONACIAVERSE", 3)]
    [InlineData("/blog/tags/Blazor", "Blazor", "TAG", "Blazor · BLOG · LOONACIAVERSE", 3)]
    [InlineData("/blog/tags/C%23", "C#", "TAG", "C# · BLOG · LOONACIAVERSE", 2)]
    [InlineData("/blog/tags/%EA%B2%8C%EC%9E%84%20%EA%B0%9C%EB%B0%9C", "게임 개발", "TAG", "게임 개발 · BLOG · LOONACIAVERSE", 1)]
    public void RoutesRenderWithPageLayoutAndTitle(string route, string title, string label, string pageTitle, int count)
    {
        using var context = new BunitContext();
        ConfigureService(context, TestContent.Posts());
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal(title, component.Find("main .page-header h1").TextContent);
        Assert.Equal(label, component.Find("main .page-header .hud-label").TextContent);
        Assert.Equal(count, component.FindAll("main article").Count);
        Assert.Equal("/blog", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal("/feed.xml", component.Find("header a[href='/feed.xml']").GetAttribute("href"));
        Assert.Equal(pageTitle, head.Find("title").TextContent);
    }

    [Fact]
    public async Task NavigatingBetweenListAndTagsReloadsAndUpdatesContent()
    {
        using var context = new BunitContext();
        var service = ConfigureService(context, TestContent.Posts());
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blog");
        var component = context.Render<App>();

        await component.InvokeAsync(() => navigation.NavigateTo("/blog/tags/C%23"));
        Assert.Equal("C#", component.Find(".page-header h1").TextContent);
        Assert.Equal(2, component.FindAll("article").Count);

        await component.InvokeAsync(() => navigation.NavigateTo("/blog/tags/Unity"));
        Assert.Equal("Unity", component.Find(".page-header h1").TextContent);
        Assert.Single(component.FindAll("article"));

        await component.InvokeAsync(() => navigation.NavigateTo("/blog"));
        Assert.Equal("BLOG", component.Find(".page-header h1").TextContent);
        Assert.Equal(3, component.FindAll("article").Count);
        Assert.Equal(4, service.PostsCalls.Count);
    }

    [Fact]
    public void FailedLoadShowsSignalLostAndRetryLoadsPosts()
    {
        using var context = new BunitContext();
        var service = ConfigureService(context, TestContent.Posts());
        service.GetPostsAsyncHandler = _ => Task.FromException<IReadOnlyList<PostSummary>>(new HttpRequestException("Offline"));
        var component = context.Render<BlogList>();

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Empty(component.FindAll("article"));

        service.GetPostsAsyncHandler = _ => Task.FromResult(TestContent.Posts());
        component.Find("button").Click();

        Assert.Equal(3, component.FindAll("article").Count);
        Assert.Empty(component.FindAll("[role='alert']"));
        Assert.Equal(2, service.PostsCalls.Count);
    }

    [Theory]
    [InlineData(2026, 10, 20, "2026-10-20")]
    [InlineData(2024, 2, 29, "2024-02-29")]
    [InlineData(1, 1, 2, "0001-01-02")]
    [InlineData(9999, 12, 31, "9999-12-31")]
    public void FormatDateUsesFixedYearMonthDay(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, Formatting.FormatDate(new DateOnly(year, month, day)));
    }

    private static FakeContentService ConfigureService(BunitContext context, IReadOnlyList<PostSummary> posts)
    {
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetPostsAsyncHandler = _ => Task.FromResult(posts)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
