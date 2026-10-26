using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class BlogPostTests
{
    [Fact]
    public void RendersPostTocInBothVariantsAndScrollsToOriginalHeadingId()
    {
        using var context = new BunitContext();
        var post = TestContent.Post() with { Toc = [new(2, "나만의-우주를-시작하며", "나만의 우주를 시작하며"), new(3, "세부-내용", "세부 내용")] };
        ConfigureService(context, post);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/blog/{post.Meta.Slug}");
        var module = context.JSInterop.SetupModule("./js/toc.js");
        module.SetupVoid("scrollToHeading", _ => true).SetVoidResult();

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        Assert.Contains("has-toc", component.Find(".post-layout").ClassList);
        foreach (var variant in component.FindAll(".toc"))
        {
            Assert.Equal(post.Toc.Select(entry => entry.Text), variant.QuerySelectorAll("a").Select(link => link.TextContent));
            Assert.Equal(post.Toc.Select(entry => $"/blog/{post.Meta.Slug}#{Uri.EscapeDataString(entry.Id)}"), variant.QuerySelectorAll("a").Select(link => link.GetAttribute("href")));
        }
        component.Find(".toc-desktop a").Click();
        Assert.Equal(post.Toc[0].Id, Assert.Single(Assert.Single(module.VerifyInvoke("scrollToHeading", 1)).Arguments));
    }

    [Fact]
    public void PostWithoutTocRendersBodyWithoutContentsOrSidebarColumn()
    {
        using var context = new BunitContext();
        var post = TestContent.Post() with { Toc = [] };
        ConfigureService(context, post);

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        Assert.Empty(component.FindAll(".toc"));
        Assert.DoesNotContain("has-toc", component.Find(".post-layout").ClassList);
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{post.Html}</div>");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RendersTitleAndPublishedDateWithOptionalUpdatedDate(bool hasUpdated)
    {
        using var context = new BunitContext();
        var post = TestContent.Post();
        post = post with { Meta = post.Meta with { Updated = hasUpdated ? new DateOnly(2026, 10, 20) : null } };
        var service = ConfigureService(context, post);

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        Assert.Equal(post.Meta.Title, component.Find(".page-header h1").TextContent);
        var meta = component.Find(".post-meta");
        var text = string.Join(" ", meta.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(hasUpdated ? "PUBLISHED 2024-01-02 · UPDATED 2026-10-20" : "PUBLISHED 2024-01-02", text);
        var dates = meta.QuerySelectorAll("time");
        Assert.Equal(hasUpdated ? 2 : 1, dates.Length);
        Assert.Equal("2024-01-02", dates[0].GetAttribute("datetime"));
        if (hasUpdated)
        {
            Assert.Equal("2026-10-20", dates[1].GetAttribute("datetime"));
        }
        Assert.Equal(post.Meta.Slug, Assert.Single(service.PostCalls).Slug);
    }

    [Fact]
    public void RendersTagsDraftCoverBodyAndReturnLinkInOrder()
    {
        using var context = new BunitContext();
        var post = TestContent.Post() with { Meta = TestContent.Post().Meta with { Tags = ["C#", "게임 개발"] } };
        ConfigureService(context, post);

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        var tags = component.FindAll(".page-header .post-tags a.chip");
        Assert.Equal(post.Meta.Tags, tags.Select(tag => tag.TextContent));
        Assert.Equal(new[] { "/blog/tags/C%23", "/blog/tags/%EA%B2%8C%EC%9E%84%20%EA%B0%9C%EB%B0%9C" }, tags.Select(tag => tag.GetAttribute("href")));
        Assert.Equal("DRAFT", component.Find(".page-header .badge.badge-draft").TextContent);
        var cover = component.Find("img.post-cover");
        Assert.Equal(post.Meta.Cover, cover.GetAttribute("src"));
        Assert.Equal(string.Empty, cover.GetAttribute("alt"));
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{post.Html}</div>");
        var returnLink = component.Find("a.all-posts");
        Assert.Equal("/blog", returnLink.GetAttribute("href"));
        Assert.Equal("← ALL POSTS", returnLink.TextContent);
        Assert.Equal(new[] { "page-header", "post-cover", "prose", "all-posts" }, component.Find("article.blog-post").Children.Select(child => child.ClassName));
    }

    [Fact]
    public void OmitsDraftBadgeAndCoverForPublishedPostWithoutCover()
    {
        using var context = new BunitContext();
        var post = TestContent.Post();
        post = post with { Meta = post.Meta with { Draft = false, Cover = null } };
        ConfigureService(context, post);

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        Assert.Empty(component.FindAll(".badge-draft, img.post-cover"));
        Assert.Equal(new[] { "page-header", "prose", "all-posts" }, component.Find("article.blog-post").Children.Select(child => child.ClassName));
    }

    [Fact]
    public void MissingSlugShowsLostInSpace()
    {
        using var context = new BunitContext();
        var service = ConfigureService(context, null);

        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, "missing"));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("article.blog-post, .prose"));
        Assert.Equal("missing", Assert.Single(service.PostCalls).Slug);
    }

    [Fact]
    public void ChangingSlugReloadsAndReplacesPost()
    {
        using var context = new BunitContext();
        var first = TestContent.Post();
        var second = first with { Meta = first.Meta with { Slug = "second-post", Title = "두 번째 기록" }, Html = "<p>새로운 본문</p>" };
        var service = ConfigureService(context, first);
        service.GetPostAsyncHandler = (slug, _) => Task.FromResult<PostDetail?>(slug == first.Meta.Slug ? first : second);
        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, first.Meta.Slug));

        component.Render(parameters => parameters.Add(p => p.Slug, second.Meta.Slug));

        Assert.Equal(new[] { first.Meta.Slug, second.Meta.Slug }, service.PostCalls.Select(call => call.Slug));
        Assert.True(service.PostCalls[0].Token.IsCancellationRequested);
        Assert.Equal(second.Meta.Title, component.Find("h1").TextContent);
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{second.Html}</div>");
    }

    [Fact]
    public void FailedLoadShowsSignalLostAndRetryLoadsPost()
    {
        using var context = new BunitContext();
        var post = TestContent.Post();
        var service = ConfigureService(context, post);
        service.GetPostAsyncHandler = (_, _) => Task.FromException<PostDetail?>(new HttpRequestException("Offline"));
        var component = context.Render<BlogPost>(parameters => parameters.Add(p => p.Slug, post.Meta.Slug));

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Empty(component.FindAll("article.blog-post"));

        service.GetPostAsyncHandler = (_, _) => Task.FromResult<PostDetail?>(post);
        component.Find("button").Click();

        Assert.Equal(post.Meta.Title, component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("[role='alert']"));
        Assert.Equal(new[] { post.Meta.Slug, post.Meta.Slug }, service.PostCalls.Select(call => call.Slug));
    }

    [Fact]
    public async Task RouteRendersWithPageLayoutAndUpdatesTitleWhenNavigatingBetweenPosts()
    {
        using var context = new BunitContext();
        var first = TestContent.Post();
        var second = first with { Meta = first.Meta with { Slug = "second-post", Title = "두 번째 기록" } };
        var service = ConfigureService(context, first);
        service.GetPostAsyncHandler = (slug, _) => Task.FromResult<PostDetail?>(slug == first.Meta.Slug ? first : second);
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/blog/{first.Meta.Slug}");
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal(first.Meta.Title, component.Find("main .page-header h1").TextContent);
        Assert.Equal("ARCHIVA", component.Find("main .page-header .hud-label").TextContent);
        Assert.Equal("/blog", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal("/feed.xml", component.Find("header a[href='/feed.xml']").GetAttribute("href"));
        Assert.Equal($"{first.Meta.Title} · LOONACIAVERSE", head.Find("title").TextContent);

        await component.InvokeAsync(() => navigation.NavigateTo($"/blog/{second.Meta.Slug}"));

        Assert.Equal(second.Meta.Title, component.Find("main .page-header h1").TextContent);
        Assert.Equal($"{second.Meta.Title} · LOONACIAVERSE", head.Find("title").TextContent);
        Assert.Equal(new[] { first.Meta.Slug, second.Meta.Slug }, service.PostCalls.Select(call => call.Slug));
    }

    private static FakeContentService ConfigureService(BunitContext context, PostDetail? post)
    {
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetPostAsyncHandler = (_, _) => Task.FromResult(post)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
