using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class HomeTests
{
    [Fact]
    public void HomeRendersSiteHeading()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(true);
        module.SetupVoid("dispose").SetVoidResult();
        var universe = TestContent.Universe();
        universe = universe with { Site = universe.Site with { Title = "TEST SYSTEM" } };
        context.Services.AddSingleton<IContentService>(new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromResult(universe) });

        var component = context.Render<Home>();

        Assert.Equal(universe.Site.Title, component.Find("h1").TextContent);
    }

    [Fact]
    public void HomeRendersDestinationLabelAndFourListCards()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(false);
        module.SetupVoid("dispose").SetVoidResult();
        var universe = TestContent.Universe();
        var service = new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromResult(universe) };
        context.Services.AddSingleton<IContentService>(service);

        var component = context.Render<Home>();

        Assert.Equal("SELECT DESTINATION", component.Find("h1 + .hud-label").TextContent);
        Assert.Equal(4, component.FindAll("article.hud-frame").Count);
        Assert.Equal(universe.Bodies.Select(body => body.Name), component.FindAll("article h2").Select(heading => heading.TextContent));
        Assert.Single(service.UniverseCalls);
    }
}
