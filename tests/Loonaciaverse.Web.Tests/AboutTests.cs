using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class AboutTests
{
    [Fact]
    public void ContentLoadFailureShowsSignalLost()
    {
        using var context = new BunitContext();
        var service = new FakeContentService { GetAboutAsyncHandler = _ => Task.FromException<AboutProfile>(new HttpRequestException("Offline")) };
        context.Services.AddSingleton<IContentService>(service);

        var component = context.Render<About>();

        Assert.Equal("SIGNAL LOST", component.Find("[role='alert'] .hud-label").TextContent);
        Assert.Equal("RETRY", component.Find("button[type='button']").TextContent);
        Assert.Empty(component.FindAll("article"));
        Assert.Single(service.AboutCalls);
    }

    [Fact]
    public void RendersProfileContentAndCommsInOrder()
    {
        using var context = new BunitContext();
        var profile = TestContent.About();
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        var service = new FakeContentService { GetAboutAsyncHandler = _ => Task.FromResult(profile) };
        context.Services.AddSingleton<IContentService>(service);

        var component = context.Render<About>();

        Assert.Equal(profile.Name, component.Find("h1").TextContent);
        Assert.Equal(profile.Title, component.Find(".about-title").TextContent);
        Assert.Equal(profile.Stack, component.FindAll("ul.about-stack li.chip").Select(chip => chip.TextContent));
        component.Find(".prose").MarkupMatches($"<div class=\"prose\">{profile.Html}</div>");
        Assert.Equal("COMMS", component.Find(".comms h2.hud-label").TextContent);
        var email = component.Find(".comms a[href^='mailto:']");
        Assert.Equal($"mailto:{profile.Email}", email.GetAttribute("href"));
        Assert.Equal("EMAIL", email.TextContent);
        foreach (var (label, url) in new[] { ("GITHUB", profile.Github), ("LINKEDIN", profile.Linkedin) })
        {
            var link = component.Find($".comms a[href='{url}']");
            Assert.Equal(label, link.TextContent);
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        }

        Assert.Equal(new[] { "page-header", "about-title", "about-stack", "prose", "comms" }, component.Find("article").Children.Select(child => child.ClassName));
        Assert.Empty(component.FindAll(".page-header .hud-label"));
        Assert.Single(service.AboutCalls);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("https://github.com/example", null)]
    [InlineData(null, "https://www.linkedin.com/in/example")]
    public void OmitsOnlyMissingSocialLinks(string? github, string? linkedin)
    {
        using var context = new BunitContext();
        var profile = TestContent.About() with { Github = github, Linkedin = linkedin };
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        context.Services.AddSingleton<IContentService>(new FakeContentService { GetAboutAsyncHandler = _ => Task.FromResult(profile) });

        var component = context.Render<About>();

        var labels = component.FindAll(".comms a").Select(link => link.TextContent).ToArray();
        Assert.Contains("EMAIL", labels);
        Assert.Equal(github is not null, labels.Contains("GITHUB"));
        Assert.Equal(linkedin is not null, labels.Contains("LINKEDIN"));
    }

    [Fact]
    public void CascadedUniverseSuppliesAboutHeaderLabel()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IContentService>(new FakeContentService { GetAboutAsyncHandler = _ => Task.FromResult(TestContent.About()) });
        var universe = TestContent.Universe();
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();

        var component = context.Render<CascadingValue<Universe>>(parameters => parameters.Add(p => p.Value, universe).AddChildContent<About>());

        Assert.Equal(universe.Bodies.Single(body => body.Id == "about").Name, component.Find(".page-header .hud-label").TextContent);
    }

    [Fact]
    public void RouteRendersAboutWithPageLayoutAndTitle()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IContentService>(new FakeContentService
        {
            GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()),
            GetAboutAsyncHandler = _ => Task.FromResult(TestContent.About())
        });
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        context.JSInterop.SetupModule("./js/highlight.js").SetupVoid("highlightAll", _ => true).SetVoidResult();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/about");
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal(TestContent.About().Name, component.Find("main h1").TextContent);
        Assert.Equal("/about", component.Find("header nav a[aria-current='page']").GetAttribute("href"));
        Assert.Equal("ABOUT · LOONACIAVERSE", head.Find("title").TextContent);
    }
}
