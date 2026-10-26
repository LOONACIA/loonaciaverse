using Bunit;
using Loonaciaverse.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class TopBarTests
{
    [Theory]
    [InlineData("/blog/x", "blog")]
    [InlineData("/blog/tags/dotnet", "blog")]
    [InlineData("/blog?tag=dotnet#latest", "blog")]
    [InlineData("/projects", "projects")]
    [InlineData("/projects/example?view=full#stack", "projects")]
    [InlineData("/about", "about")]
    [InlineData("/about#comms", "about")]
    [InlineData("/not-found", null)]
    [InlineData("/", null)]
    [InlineData("/?focus=blog#about", null)]
    [InlineData("/blogroll", null)]
    [InlineData("/other/blog", null)]
    public void RendersNavigationForCurrentSection(string path, string? section)
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(path);

        var component = context.Render<TopBar>();

        Assert.Equal("LOONACIAVERSE", component.Find("a.logo").TextContent);
        Assert.Equal(section is null ? "/" : $"/?focus={section}", component.Find("a.logo").GetAttribute("href"));
        foreach (var name in new[] { "projects", "blog", "about" })
        {
            var link = component.Find($"nav a[href='/{name}']");
            Assert.Equal(name.ToUpperInvariant(), link.TextContent);
            Assert.Equal(section == name ? "page" : null, link.GetAttribute("aria-current"));
            Assert.Equal(section == name, link.ClassList.Contains("active"));
        }

        Assert.Equal(section is null ? 0 : 1, component.FindAll("[aria-current='page']").Count);
        var rssLinks = component.FindAll("nav a[aria-label='RSS']");
        if (section == "blog")
        {
            var rss = Assert.Single(rssLinks);
            Assert.Equal("/feed.xml", rss.GetAttribute("href"));
            Assert.Equal("_top", rss.GetAttribute("target"));
            Assert.Equal("true", rss.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        }
        else
        {
            Assert.Empty(rssLinks);
        }
    }

    [Fact]
    public async Task NavigationUpdatesLogoActiveLinkAndRssWithoutRecreatingTopBar()
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blog/x");
        var component = context.Render<TopBar>();
        var instance = component.Instance;

        foreach (var section in new[] { "projects", "about", "blog", "not-found" })
        {
            await component.InvokeAsync(() => navigation.NavigateTo($"/{section}"));

            component.WaitForAssertion(() =>
            {
                Assert.Same(instance, component.Instance);
                Assert.Equal(section == "not-found" ? "/" : $"/?focus={section}", component.Find("a.logo").GetAttribute("href"));
                var activeLinks = component.FindAll("nav a[aria-current='page']");
                if (section == "not-found")
                {
                    Assert.Empty(activeLinks);
                    Assert.Empty(component.FindAll("nav a.active"));
                }
                else
                {
                    var activeLink = Assert.Single(activeLinks);
                    Assert.Equal($"/{section}", activeLink.GetAttribute("href"));
                    Assert.Contains("active", activeLink.ClassList);
                }

                Assert.Equal(section == "blog" ? 1 : 0, component.FindAll("a[aria-label='RSS']").Count);
            });
        }
    }

    [Fact]
    public void SectionIsRelativeToApplicationBasePath()
    {
        using var context = new BunitContext();
        var navigation = new SubpathNavigationManager();
        context.Services.AddSingleton<NavigationManager>(navigation);
        navigation.NavigateTo("blog?tag=dotnet#latest");

        var component = context.Render<TopBar>();

        Assert.Equal("/?focus=blog", component.Find("a.logo").GetAttribute("href"));
        Assert.Equal("/blog", component.Find("[aria-current='page']").GetAttribute("href"));
        Assert.Single(component.FindAll("a[aria-label='RSS']"));
    }

    private sealed class SubpathNavigationManager : NavigationManager
    {
        public SubpathNavigationManager() => Initialize("https://example.com/site/", "https://example.com/site/");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            NotifyLocationChanged(false);
        }
    }
}
