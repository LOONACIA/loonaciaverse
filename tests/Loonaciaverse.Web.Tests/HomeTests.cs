using Bunit;
using Loonaciaverse.Web.Pages;

namespace Loonaciaverse.Web.Tests;

public class HomeTests
{
    [Fact]
    public void HomeRendersSiteHeading()
    {
        using var context = new BunitContext();

        var component = context.Render<Home>();

        Assert.Equal("LOONACIAVERSE", component.Find("h1").TextContent);
    }
}
