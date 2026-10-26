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
        var universe = TestContent.Universe();
        universe = universe with { Site = universe.Site with { Title = "TEST SYSTEM" } };
        context.Services.AddSingleton<IContentService>(new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromResult(universe) });

        var component = context.Render<Home>();

        Assert.Equal(universe.Site.Title, component.Find("h1").TextContent);
    }
}
