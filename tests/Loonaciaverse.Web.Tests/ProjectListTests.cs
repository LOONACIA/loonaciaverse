using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class ProjectListTests
{
    [Fact]
    public void RendersWholeCardLinksAndMetadataInServiceOrder()
    {
        using var context = new BunitContext();
        var projects = TestContent.Projects();
        var service = ConfigureService(context, projects);

        var component = context.Render<ProjectList>();

        var cards = component.FindAll("a.project-card");
        Assert.Equal(projects.Select(project => project.Title), cards.Select(card => card.QuerySelector("h2")!.TextContent));
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            var card = cards[index];
            Assert.Equal($"/projects/{project.Slug}", card.GetAttribute("href"));
            Assert.Equal(project.Period, card.QuerySelector(".project-period")!.TextContent);
            Assert.Equal(project.Role, card.QuerySelector(".project-role")!.TextContent);
            Assert.Equal(project.Summary, card.QuerySelector(".project-summary")!.TextContent);
            Assert.Equal(project.Stack, card.QuerySelectorAll(".project-stack .chip").Select(chip => chip.TextContent));
            Assert.Empty(card.QuerySelectorAll("a"));
        }
        Assert.Single(service.ProjectsCalls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RendersFeaturedAndDraftBadgesIndependently(bool featured, bool draft)
    {
        using var context = new BunitContext();
        var project = TestContent.Projects()[0] with { Featured = featured, Draft = draft };

        var component = context.Render<ProjectCard>(parameters => parameters.Add(p => p.Project, project));

        Assert.Equal(featured ? new[] { "FEATURED" } : [], component.FindAll(".badge-featured").Select(badge => badge.TextContent));
        Assert.Equal(draft ? new[] { "DRAFT" } : [], component.FindAll(".badge-draft").Select(badge => badge.TextContent));
    }

    [Fact]
    public void RendersThumbnailOrDecorativePlaceholder()
    {
        using var context = new BunitContext();
        var projects = TestContent.Projects();
        ConfigureService(context, projects);

        var component = context.Render<ProjectList>();

        var cards = component.FindAll(".project-card");
        var image = cards[0].QuerySelector("img.project-thumbnail")!;
        Assert.Equal(projects[0].Thumbnail, image.GetAttribute("src"));
        Assert.Equal(string.Empty, image.GetAttribute("alt"));
        Assert.Equal("lazy", image.GetAttribute("loading"));
        Assert.Empty(cards[0].QuerySelectorAll(".project-placeholder"));
        foreach (var card in cards.Skip(1))
        {
            Assert.Empty(card.QuerySelectorAll("img"));
            Assert.Equal("true", Assert.Single(card.QuerySelectorAll(".project-thumbnail.project-placeholder")).GetAttribute("aria-hidden"));
        }
    }

    [Fact]
    public void EmptyListShowsMessage()
    {
        using var context = new BunitContext();
        ConfigureService(context, []);

        var component = context.Render<ProjectList>();

        Assert.Equal("PROJECTS", component.Find(".page-header h1").TextContent);
        Assert.Equal("아직 공개한 프로젝트가 없습니다.", component.Find(".projects-empty").TextContent);
        Assert.Empty(component.FindAll(".project-card, .projects-grid, .lost-in-space"));
    }

    [Fact]
    public void RouteRendersWithPageLayoutAndTitle()
    {
        using var context = new BunitContext();
        ConfigureService(context, TestContent.Projects());
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal("PROJECTS", component.Find("main .page-header h1").TextContent);
        Assert.Equal("ARTIFEX", component.Find("main .page-header .hud-label").TextContent);
        Assert.Equal(3, component.FindAll("main .project-card").Count);
        Assert.Equal("/projects", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal("/?focus=projects", component.Find("header .logo").GetAttribute("href"));
        Assert.Equal("PROJECTS · LOONACIAVERSE", head.Find("title").TextContent);
    }

    [Fact]
    public void FailedLoadShowsSignalLostAndRetryLoadsProjects()
    {
        using var context = new BunitContext();
        var projects = TestContent.Projects();
        var service = ConfigureService(context, projects);
        service.GetProjectsAsyncHandler = _ => Task.FromException<IReadOnlyList<ProjectSummary>>(new HttpRequestException("Offline"));
        var component = context.Render<ProjectList>();

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Empty(component.FindAll(".project-card, .projects-empty"));

        service.GetProjectsAsyncHandler = _ => Task.FromResult(projects);
        component.Find("button").Click();

        Assert.Equal(projects.Count, component.FindAll(".project-card").Count);
        Assert.Empty(component.FindAll("[role='alert']"));
        Assert.Equal(2, service.ProjectsCalls.Count);
    }

    private static FakeContentService ConfigureService(BunitContext context, IReadOnlyList<ProjectSummary> projects)
    {
        var service = new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetProjectsAsyncHandler = _ => Task.FromResult(projects)
        };
        context.Services.AddSingleton<IContentService>(service);
        return service;
    }
}
