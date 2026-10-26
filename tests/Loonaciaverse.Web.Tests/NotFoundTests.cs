using Bunit;
using Loonaciaverse.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class NotFoundTests
{
    [Fact]
    public void RendersLostInSpaceMessageAndReturnLink()
    {
        using var context = new BunitContext();

        var component = context.Render<NotFound>();

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Equal("요청한 좌표에서 아무 신호도 찾지 못했습니다.", component.Find("p").TextContent);
        var returnLink = component.Find("a");
        Assert.Equal("RETURN TO ORBIT", returnLink.TextContent);
        Assert.Equal("/", returnLink.GetAttribute("href"));
    }

    [Theory]
    [InlineData("/not-found")]
    [InlineData("/unknown-coordinate")]
    public void RouterShowsNotFoundWithPageLayoutAndTitle(string path)
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string>("Blazor._internal.PageTitle.getAndRemoveExistingTitle").SetResult(string.Empty);
        context.Services.AddSingleton<Loonaciaverse.Core.Content.IContentService>(new FakeContentService());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(path);
        var head = context.Render<HeadOutlet>();

        var component = context.Render<App>();

        Assert.Equal("LOST IN SPACE", component.Find("main h1").TextContent);
        Assert.Equal("LOONACIAVERSE", component.Find("header a.logo").TextContent);
        Assert.Equal("/", component.Find("header a.logo").GetAttribute("href"));
        Assert.Equal("LOST IN SPACE · LOONACIAVERSE", head.Find("title").TextContent);
    }
}
