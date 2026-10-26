using Bunit;
using Loonaciaverse.Core.Content;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using ProjectDetailPage = Loonaciaverse.Web.Pages.ProjectDetail;

namespace Loonaciaverse.Web.Tests;

public class ProjectDetailTests
{
    [Fact]
    public void RendersMetadataAndExternalLinksWithUppercaseLabels()
    {
        using var context = new BunitContext();
        var project = TestContent.Project();
        var service = ConfigureService(context, project);

        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, project.Meta.Slug));

        Assert.Equal(project.Meta.Title, component.Find(".page-header h1").TextContent);
        var rows = component.FindAll("dl.hud-frame .project-meta-row");
        Assert.Equal(new[] { "PERIOD", "ROLE", "STACK", "LINKS" }, rows.Select(row => row.QuerySelector("dt")!.TextContent));
        Assert.Equal(project.Meta.Period, rows[0].QuerySelector("dd")!.TextContent);
        Assert.Equal(project.Meta.Role, rows[1].QuerySelector("dd")!.TextContent);
        Assert.Equal(project.Meta.Stack, rows[2].QuerySelectorAll("dd .chip").Select(chip => chip.TextContent));
        var links = rows[3].QuerySelectorAll("dd a");
        Assert.Equal(new[] { "GITHUB", "DEMO" }, links.Select(link => link.TextContent));
        Assert.Equal(project.Meta.Links.Values, links.Select(link => link.GetAttribute("href")));
        Assert.All(links, link =>
        {
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        });
        Assert.Equal(project.Meta.Slug, Assert.Single(service.ProjectCalls).Slug);
    }

    [Fact]
    public void OmitsLinksRowForEmptyLinks()
    {
        using var context = new BunitContext();
        var project = TestContent.Project();
        project = project with { Meta = project.Meta with { Links = new Dictionary<string, string>() } };
        ConfigureService(context, project);

        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, project.Meta.Slug));

        Assert.Equal(new[] { "PERIOD", "ROLE", "STACK" }, component.FindAll("dl dt").Select(label => label.TextContent));
        Assert.Empty(component.FindAll(".project-links"));
    }

    [Fact]
    public void RendersHeroHeaderDraftMetadataAndBodyInOrder()
    {
        using var context = new BunitContext();
        var project = TestContent.Project();
        ConfigureService(context, project);

        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, project.Meta.Slug));

        var hero = component.Find("img.project-hero");
        Assert.Equal(project.Meta.Thumbnail, hero.GetAttribute("src"));
        Assert.Equal(string.Empty, hero.GetAttribute("alt"));
        Assert.Equal("DRAFT", component.Find(".page-header .badge.badge-draft").TextContent);
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{project.Html}</div>");
        Assert.Equal(new[] { "project-hero", "page-header", "hud-frame", "prose" }, component.Find("article.project-detail").Children.Select(child => child.ClassName));
    }

    [Fact]
    public void OmitsHeroPlaceholderAndDraftBadgeForPublishedProjectWithoutThumbnail()
    {
        using var context = new BunitContext();
        var project = TestContent.Project();
        project = project with { Meta = project.Meta with { Thumbnail = null, Draft = false } };
        ConfigureService(context, project);

        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, project.Meta.Slug));

        Assert.Empty(component.FindAll(".project-hero, .project-placeholder, .badge-draft"));
        Assert.Equal(new[] { "page-header", "hud-frame", "prose" }, component.Find("article.project-detail").Children.Select(child => child.ClassName));
    }

    [Fact]
    public void MissingSlugShowsLostInSpaceWithoutProjectTitle()
    {
        using var context = new BunitContext();
        var service = ConfigureService(context, null);

        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, "missing"));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("article.project-detail, .prose"));
        Assert.Empty(component.FindComponents<PageTitle>());
        Assert.Equal("missing", Assert.Single(service.ProjectCalls).Slug);
    }

    [Fact]
    public void ChangingSlugReloadsAndReplacesProject()
    {
        using var context = new BunitContext();
        var first = TestContent.Project();
        var second = first with { Meta = TestContent.Projects()[1], Html = "<p>다른 프로젝트의 본문</p>" };
        var service = ConfigureService(context, first);
        service.GetProjectAsyncHandler = (slug, _) => Task.FromResult<ProjectDetail?>(slug == first.Meta.Slug ? first : second);
        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, first.Meta.Slug));

        component.Render(parameters => parameters.Add(p => p.Slug, second.Meta.Slug));

        Assert.Equal(new[] { first.Meta.Slug, second.Meta.Slug }, service.ProjectCalls.Select(call => call.Slug));
        Assert.True(service.ProjectCalls[0].Token.IsCancellationRequested);
        Assert.Equal(second.Meta.Title, component.Find("h1").TextContent);
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{second.Html}</div>");
        Assert.Empty(component.FindAll(".project-hero, .project-links"));
    }

    [Fact]
    public void FailedLoadShowsSignalLostAndRetryLoadsProject()
    {
        using var context = new BunitContext();
        var project = TestContent.Project();
        var service = ConfigureService(context, project);
        service.GetProjectAsyncHandler = (_, _) => Task.FromException<ProjectDetail?>(new HttpRequestException("Offline"));
        var component = context.Render<ProjectDetailPage>(parameters => parameters.Add(p => p.Slug, project.Meta.Slug));

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Empty(component.FindAll("article.project-detail"));
        Assert.Empty(component.FindComponents<PageTitle>());

        service.GetProjectAsyncHandler = (_, _) => Task.FromResult<ProjectDetail?>(project);
        component.Find("button").Click();

        Assert.Equal(project.Meta.Title, component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("[role='alert']"));
        Assert.Equal(new[] { project.Meta.Slug, project.Meta.Slug }, service.ProjectCalls.Select(call => call.Slug));
    }

    [Fact]
    public async Task RouteRendersWithPageLayoutAndUpdatesTitleWhenNavigatingBetweenProjects()
    {
        using var context = new BunitContext();
        var first = TestContent.Project();
        var second = first with { Meta = TestContent.Projects()[1] };
        var service = ConfigureService(context, first);
        service.GetProjectAsyncHandler = (slug, _) => Task.FromResult<ProjectDetail?>(slug == first.Meta.Slug ? first : second);
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/projects/{first.Meta.Slug}");
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal(first.Meta.Title, component.Find("main .page-header h1").TextContent);
        Assert.Equal("ARTIFEX", component.Find("main .page-header .hud-label").TextContent);
        Assert.Equal("/projects", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal($"{first.Meta.Title} · LOONACIAVERSE", head.Find("title").TextContent);

        await component.InvokeAsync(() => navigation.NavigateTo($"/projects/{second.Meta.Slug}"));

        Assert.Equal(second.Meta.Title, component.Find("main .page-header h1").TextContent);
        Assert.Equal($"{second.Meta.Title} · LOONACIAVERSE", head.Find("title").TextContent);
        Assert.Equal(new[] { first.Meta.Slug, second.Meta.Slug }, service.ProjectCalls.Select(call => call.Slug));
    }

    private static FakeContentService ConfigureService(BunitContext context, ProjectDetail? project)
    {
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetProjectAsyncHandler = (_, _) => Task.FromResult(project)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
