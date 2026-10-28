using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class BlogSeriesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RendersSeriesAndPartsInServiceOrderWithMetadataAndDraftBadge(bool reverseOrder)
    {
        using var context = new BunitContext();
        var series = TestContent.Series();
        if (reverseOrder)
        {
            series = series with { Posts = series.Posts.Reverse().ToArray() };
        }
        var service = ConfigureService(context, series);

        var component = context.Render<CascadingValue<Universe>>(parameters => parameters
            .Add(p => p.Value, TestContent.Universe())
            .AddChildContent<BlogSeries>(child => child.Add(p => p.Slug, series.Slug)));

        Assert.Equal("SERIES", component.Find(".page-header .hud-label").TextContent);
        Assert.Equal(series.Title, component.Find(".page-header h1").TextContent);
        Assert.Equal(series.Summary, component.Find(".series-summary").TextContent);
        Assert.Equal("전체 글", component.Find("a.all-posts").TextContent);
        Assert.Equal("/blog", component.Find("a.all-posts").GetAttribute("href"));
        var articles = component.FindAll("article.post-list-item");
        Assert.Equal(series.Posts.Select(post => post.Title), articles.Select(article => article.QuerySelector("h2")!.TextContent));
        Assert.Equal(new[] { "PART 1", "PART 2", "PART 3" }, articles.Select(article => article.QuerySelector(".post-part")!.TextContent));
        for (var index = 0; index < series.Posts.Count; index++)
        {
            var post = series.Posts[index];
            var article = articles[index];
            Assert.Equal($"/blog/{post.Slug}", article.QuerySelector("h2 a")!.GetAttribute("href"));
            Assert.Equal(Formatting.FormatDate(post.Date), article.QuerySelector("time")!.TextContent);
            Assert.Equal(Formatting.FormatDate(post.Date), article.QuerySelector("time")!.GetAttribute("datetime"));
            Assert.Equal(post.Summary, article.QuerySelector(".post-summary")!.TextContent);
            Assert.Contains("post-part", article.QuerySelector(".post-meta")!.FirstElementChild!.ClassList);
            Assert.Equal(post.Draft ? 1 : 0, article.QuerySelectorAll(".badge.badge-draft").Length);
        }
        Assert.Equal("DRAFT", component.Find(".badge.badge-draft").TextContent);
        Assert.Equal(series.Slug, Assert.Single(service.SeriesCalls).Slug);
    }

    [Fact]
    public void MissingSeriesShowsLostInSpace()
    {
        using var context = new BunitContext();
        var service = ConfigureService(context, null);

        var component = context.Render<BlogSeries>(parameters => parameters.Add(p => p.Slug, "missing"));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("article, .page-header, .series-summary"));
        Assert.Equal("missing", Assert.Single(service.SeriesCalls).Slug);
    }

    [Fact]
    public void FailedLoadShowsSignalLostAndRetryLoadsSeries()
    {
        using var context = new BunitContext();
        var series = TestContent.Series();
        var service = ConfigureService(context, series);
        service.GetSeriesAsyncHandler = (_, _) => Task.FromException<SeriesDetail?>(new HttpRequestException("Offline"));
        var component = context.Render<BlogSeries>(parameters => parameters.Add(p => p.Slug, series.Slug));

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Empty(component.FindAll("article"));
        Assert.Equal("RETRY", component.Find("button").TextContent);

        service.GetSeriesAsyncHandler = (_, _) => Task.FromResult<SeriesDetail?>(series);
        component.Find("button").Click();

        Assert.Equal(series.Title, component.Find("h1").TextContent);
        Assert.Equal(3, component.FindAll("article").Count);
        Assert.Empty(component.FindAll("[role='alert']"));
        Assert.Equal(new[] { series.Slug, series.Slug }, service.SeriesCalls.Select(call => call.Slug));
    }

    [Fact]
    public void ChangingSlugReloadsAndCancelsPreviousToken()
    {
        using var context = new BunitContext();
        var first = TestContent.Series();
        var second = first with { Slug = "second-series", Title = "두 번째 시리즈", Summary = "새로운 시리즈입니다.", Posts = [first.Posts[1]] };
        var service = ConfigureService(context, first);
        service.GetSeriesAsyncHandler = (slug, _) => Task.FromResult<SeriesDetail?>(slug == first.Slug ? first : second);
        var component = context.Render<BlogSeries>(parameters => parameters.Add(p => p.Slug, first.Slug));

        component.Render(parameters => parameters.Add(p => p.Slug, second.Slug));

        Assert.Equal(new[] { first.Slug, second.Slug }, service.SeriesCalls.Select(call => call.Slug));
        Assert.True(service.SeriesCalls[0].Token.IsCancellationRequested);
        Assert.False(service.SeriesCalls[1].Token.IsCancellationRequested);
        Assert.Equal(second.Title, component.Find("h1").TextContent);
        Assert.Equal(second.Summary, component.Find(".series-summary").TextContent);
        Assert.Single(component.FindAll("article"));
        Assert.Equal("PART 1", component.Find(".post-part").TextContent);
        Assert.Equal(second.Posts[0].Title, component.Find("article h2").TextContent);
    }

    [Fact]
    public async Task ChangingSlugDuringPendingLoadCancelsAndIgnoresOldResult()
    {
        using var context = new BunitContext();
        var series = TestContent.Series();
        var pending = new TaskCompletionSource<SeriesDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = ConfigureService(context, series);
        service.GetSeriesAsyncHandler = (slug, _) => slug == "pending" ? pending.Task : Task.FromResult<SeriesDetail?>(series);
        var component = context.Render<BlogSeries>(parameters => parameters.Add(p => p.Slug, "pending"));
        Assert.Single(component.FindAll("[aria-busy='true']"));

        component.Render(parameters => parameters.Add(p => p.Slug, series.Slug));

        Assert.True(service.SeriesCalls[0].Token.IsCancellationRequested);
        await component.InvokeAsync(() => pending.SetResult(series with { Title = "이전 결과" }));
        component.WaitForAssertion(() => Assert.Equal(series.Title, component.Find("h1").TextContent));
        Assert.Equal(new[] { "pending", series.Slug }, service.SeriesCalls.Select(call => call.Slug));
    }

    [Fact]
    public void RouteRendersWithPageLayoutTitleAndDescription()
    {
        using var context = new BunitContext();
        var series = TestContent.Series();
        var service = ConfigureService(context, series);
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/blog/series/first-series");
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal(series.Title, component.Find("main .page-header h1").TextContent);
        Assert.Equal("SERIES", component.Find("main .page-header .hud-label").TextContent);
        Assert.Equal(3, component.FindAll("main article").Count);
        Assert.Equal("/blog", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal("/feed.xml", component.Find("header a[href='/feed.xml']").GetAttribute("href"));
        Assert.Equal($"{series.Title} · BLOG · LOONACIAVERSE", head.Find("title").TextContent);
        Assert.Equal(series.Summary, head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Equal(series.Slug, Assert.Single(service.SeriesCalls).Slug);
    }

    [Fact]
    public void PostListItemWithoutPartOmitsPartLabel()
    {
        using var context = new BunitContext();

        var component = context.Render<PostListItem>(parameters => parameters.Add(p => p.Post, TestContent.Posts()[0]));

        Assert.Empty(component.FindAll(".post-part"));
        Assert.Equal("TIME", component.Find(".post-meta").FirstElementChild!.TagName);
    }

    private static FakeContentService ConfigureService(BunitContext context, SeriesDetail? series)
    {
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetSeriesAsyncHandler = (_, _) => Task.FromResult(series)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
