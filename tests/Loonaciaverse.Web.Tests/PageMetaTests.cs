using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class PageMetaTests
{
    [Theory]
    [InlineData(null, "TEST SYSTEM")]
    [InlineData("", "TEST SYSTEM")]
    [InlineData("ABOUT", "ABOUT · TEST SYSTEM")]
    [InlineData("C# · BLOG", "C# · BLOG · TEST SYSTEM")]
    public void TitleUsesSiteInfoWithOptionalPageTitle(string? title, string expected)
    {
        using var context = new BunitContext();
        var head = RenderHead(context);

        RenderMeta(context, Universe(), title);

        Assert.Equal(expected, head.Find("title").TextContent);
        Assert.Single(head.FindAll("title"));
    }

    [Theory]
    [InlineData(null, "사이트 기본 설명")]
    [InlineData("글 요약 & 소개", "글 요약 & 소개")]
    [InlineData("", "")]
    public void DescriptionUsesOverrideOrSiteDefault(string? description, string expected)
    {
        using var context = new BunitContext();
        var head = RenderHead(context);

        RenderMeta(context, Universe(), description: description);

        Assert.Equal(expected, head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Single(head.FindAll("meta[name='description']"));
    }

    [Theory]
    [InlineData("https://metadata.example", "/", "https://metadata.example/")]
    [InlineData("https://metadata.example/", "/blog?sort=date#heading", "https://metadata.example/blog")]
    [InlineData("https://metadata.example///", "/blog/tags/C%23?source=hud#intro", "https://metadata.example/blog/tags/C%23")]
    [InlineData("https://metadata.example/", "/blog/tags/%EA%B2%8C%EC%9E%84?search=space#part", "https://metadata.example/blog/tags/%EA%B2%8C%EC%9E%84")]
    public void CanonicalUsesConfiguredBaseUrlAndEscapedPathWithoutQueryOrFragment(string baseUrl, string route, string expected)
    {
        using var context = new BunitContext();
        var universe = Universe();
        universe = universe with { Site = universe.Site with { BaseUrl = baseUrl } };
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        var head = RenderHead(context);

        RenderMeta(context, universe);

        Assert.Equal(expected, head.Find("link[rel='canonical']").GetAttribute("href"));
        Assert.Single(head.FindAll("link[rel='canonical']"));
        Assert.Empty(head.FindAll("meta[name='robots']"));
    }

    [Fact]
    public void NoIndexReplacesCanonicalWithRobotsMeta()
    {
        using var context = new BunitContext();
        var head = RenderHead(context);

        RenderMeta(context, Universe(), "LOST IN SPACE", noIndex: true);

        Assert.Equal("LOST IN SPACE · TEST SYSTEM", head.Find("title").TextContent);
        Assert.Equal("사이트 기본 설명", head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Equal("noindex", head.Find("meta[name='robots']").GetAttribute("content"));
        Assert.Empty(head.FindAll("link[rel='canonical']"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABOUT")]
    public void MissingUniverseOnlyRendersExplicitTitle(string? title)
    {
        using var context = new BunitContext();
        var head = RenderHead(context);

        RenderMeta(context, null, title, "설명", noIndex: true);

        Assert.Equal(string.IsNullOrEmpty(title) ? [] : new[] { title }, head.FindAll("title").Select(element => element.TextContent));
        Assert.Empty(head.FindAll("meta, link"));
    }

    [Fact]
    public void CascadingUniverseChangesUpdateAndRemoveSiteMetadata()
    {
        using var context = new BunitContext();
        var head = RenderHead(context);
        RenderFragment content = builder =>
        {
            builder.OpenComponent<PageMeta>(0);
            builder.AddAttribute(1, nameof(PageMeta.Title), "ABOUT");
            builder.CloseComponent();
        };
        var component = context.Render<CascadingValue<Universe?>>(parameters => parameters.Add(p => p.Value, null).AddChildContent(content));
        Assert.Equal("ABOUT", head.Find("title").TextContent);
        Assert.Empty(head.FindAll("meta, link"));

        component.Render(parameters => parameters.Add(p => p.Value, Universe()).AddChildContent(content));

        Assert.Single(component.FindComponents<PageMeta>());
        Assert.Equal("ABOUT · TEST SYSTEM", head.Find("title").TextContent);
        Assert.Equal("사이트 기본 설명", head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Equal("https://metadata.example/", head.Find("link[rel='canonical']").GetAttribute("href"));

        component.Render(parameters => parameters.Add(p => p.Value, null).AddChildContent(content));

        Assert.Equal("ABOUT", head.Find("title").TextContent);
        Assert.Empty(head.FindAll("meta, link"));
    }

    [Theory]
    [InlineData("/", "TEST SYSTEM", "사이트 기본 설명")]
    [InlineData("/about", "ABOUT · TEST SYSTEM", "사이트 기본 설명")]
    [InlineData("/blog", "BLOG · TEST SYSTEM", "사이트 기본 설명")]
    [InlineData("/blog/tags/C%23", "C# · BLOG · TEST SYSTEM", "사이트 기본 설명")]
    [InlineData("/blog/first-post", "첫 번째 기록 · TEST SYSTEM", "오래된 글도 서비스 순서를 따릅니다.")]
    [InlineData("/projects", "PROJECTS · TEST SYSTEM", "사이트 기본 설명")]
    [InlineData("/projects/first-project", "첫 번째 프로젝트 · TEST SYSTEM", "서비스 순서를 따릅니다.")]
    public void RoutesRenderOnePageMetaWithContentAndSiteInfo(string path, string title, string description)
    {
        using var context = new BunitContext();
        ConfigureContent(context);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(path + "?source=test#intro");
        var head = RenderHead(context);

        var component = context.Render<App>();

        Assert.Single(component.FindComponents<PageMeta>());
        Assert.Equal(title, head.Find("title").TextContent);
        Assert.Equal(description, head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Equal("https://metadata.example" + path, head.Find("link[rel='canonical']").GetAttribute("href"));
        Assert.Empty(head.FindAll("meta[name='robots']"));
    }

    [Theory]
    [InlineData("/not-found")]
    [InlineData("/unknown-coordinate")]
    [InlineData("/blog/missing")]
    [InlineData("/blog/tags/missing")]
    [InlineData("/projects/missing")]
    public void MissingRoutesSlugsAndTagsRenderOneNoIndexPageMeta(string path)
    {
        using var context = new BunitContext();
        ConfigureContent(context);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        var head = RenderHead(context);

        var component = context.Render<App>();

        Assert.Equal("LOST IN SPACE", component.Find("main h1").TextContent);
        Assert.Single(component.FindComponents<PageMeta>());
        Assert.Equal("LOST IN SPACE · TEST SYSTEM", head.Find("title").TextContent);
        Assert.Equal("noindex", head.Find("meta[name='robots']").GetAttribute("content"));
        Assert.Empty(head.FindAll("link[rel='canonical']"));
    }

    [Fact]
    public async Task NavigatingBetweenSuccessAndMissingContentReplacesHeadMetadata()
    {
        using var context = new BunitContext();
        ConfigureContent(context);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blog/first-post");
        var head = RenderHead(context);
        var component = context.Render<App>();
        Assert.Equal(TestContent.Post().Meta.Summary, head.Find("meta[name='description']").GetAttribute("content"));

        await component.InvokeAsync(() => navigation.NavigateTo("/blog/missing"));

        Assert.Single(component.FindComponents<PageMeta>());
        Assert.Equal("LOST IN SPACE · TEST SYSTEM", head.Find("title").TextContent);
        Assert.Equal("noindex", head.Find("meta[name='robots']").GetAttribute("content"));
        Assert.Empty(head.FindAll("link[rel='canonical']"));

        await component.InvokeAsync(() => navigation.NavigateTo("/projects/first-project?source=hud#intro"));

        Assert.Single(component.FindComponents<PageMeta>());
        Assert.Equal("첫 번째 프로젝트 · TEST SYSTEM", head.Find("title").TextContent);
        Assert.Equal(TestContent.Project().Meta.Summary, head.Find("meta[name='description']").GetAttribute("content"));
        Assert.Equal("https://metadata.example/projects/first-project", head.Find("link[rel='canonical']").GetAttribute("href"));
        Assert.Empty(head.FindAll("meta[name='robots']"));
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("/blog")]
    [InlineData("/blog/first-post")]
    [InlineData("/projects")]
    [InlineData("/projects/first-project")]
    public void PendingOrFailedContentDoesNotRenderPageMeta(string path)
    {
        using var context = new BunitContext();
        var service = ConfigureContent(context);
        var completion = new TaskCompletionSource();
        service.GetAboutAsyncHandler = async _ => { await completion.Task; return TestContent.About(); };
        service.GetPostsAsyncHandler = async _ => { await completion.Task; return TestContent.Posts(); };
        service.GetPostAsyncHandler = async (_, _) => { await completion.Task; return TestContent.Post(); };
        service.GetProjectsAsyncHandler = async _ => { await completion.Task; return TestContent.Projects(); };
        service.GetProjectAsyncHandler = async (_, _) => { await completion.Task; return TestContent.Project(); };
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        var head = RenderHead(context);
        var component = context.Render<App>();
        Assert.Empty(component.FindComponents<PageMeta>());
        Assert.Empty(head.FindAll("title, meta, link"));

        completion.SetException(new HttpRequestException("Offline"));

        component.WaitForAssertion(() => Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent));
        Assert.Empty(component.FindComponents<PageMeta>());
        Assert.Empty(head.FindAll("title, meta, link"));
    }

    private static Universe Universe() => TestContent.Universe() with { Site = new SiteInfo("TEST SYSTEM", "https://metadata.example/", "작성자", "사이트 기본 설명") };

    private static IRenderedComponent<HeadOutlet> RenderHead(BunitContext context)
    {
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        return context.Render<HeadOutlet>();
    }

    private static IRenderedComponent<CascadingValue<Universe?>> RenderMeta(BunitContext context, Universe? universe, string? title = null, string? description = null, bool noIndex = false) =>
        context.Render<CascadingValue<Universe?>>(parameters => parameters.Add(p => p.Value, universe)
            .AddChildContent<PageMeta>(child => child.Add(p => p.Title, title).Add(p => p.Description, description).Add(p => p.NoIndex, noIndex)));

    private static FakeContentService ConfigureContent(BunitContext context)
    {
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        var scene = context.JSInterop.SetupModule("./js/scene.js");
        scene.Setup<bool>("init", _ => true).SetResult(true);
        scene.SetupVoid("dispose").SetVoidResult();
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(Universe()),
            GetAboutAsyncHandler = _ => Task.FromResult(TestContent.About()),
            GetPostsAsyncHandler = _ => Task.FromResult(TestContent.Posts()),
            GetPostAsyncHandler = (slug, _) => Task.FromResult<PostDetail?>(slug == "first-post" ? TestContent.Post() : null),
            GetProjectsAsyncHandler = _ => Task.FromResult(TestContent.Projects()),
            GetProjectAsyncHandler = (slug, _) => Task.FromResult<ProjectDetail?>(slug == "first-project" ? TestContent.Project() : null)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
