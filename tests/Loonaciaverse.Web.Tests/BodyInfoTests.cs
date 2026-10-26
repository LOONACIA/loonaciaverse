using AngleSharp.Dom;
using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;

namespace Loonaciaverse.Web.Tests;

public class BodyInfoTests
{
    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    [InlineData("playground")]
    public void RendersBodyHeader(string id)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var body = universe.Bodies.Single(body => body.Id == id);

        var component = RenderBody(context, body, universe.Stats);

        Assert.Equal(body.Name, component.Find("h2").TextContent);
        Assert.Equal("-1", component.Find("h2").GetAttribute("tabindex"));
        Assert.Equal(body.Subtitle, component.Find("header .hud-label").TextContent);
        Assert.Equal(body.Tagline, component.Find(".body-tagline").TextContent);
    }

    [Fact]
    public async Task FocusHeadingMovesFocusToTheBodyNameWithoutScrolling()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var component = RenderBody(context, universe.Bodies[0], universe.Stats);

        await component.InvokeAsync(() => component.Instance.FocusHeadingAsync().AsTask());

        var invocation = context.JSInterop.VerifyFocusAsyncInvoke();
        invocation.Arguments[0].ShouldBeElementReferenceTo(component.Find("h2"));
        Assert.Equal(true, invocation.Arguments[1]);
    }

    [Fact]
    public void AboutRendersClassStackAndAccessibleCommsLinks()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();

        var component = RenderBody(context, universe.Bodies[0], universe.Stats);

        Assert.Equal(new[] { "CLASS", "PRIMARY STACK", "COMMS" }, component.FindAll("dl dt.hud-label").Select(label => label.TextContent));
        Assert.Equal(universe.Stats.About.Title, Value(component, "CLASS").TextContent);
        Assert.Equal(universe.Stats.About.Stack, Value(component, "PRIMARY STACK").QuerySelectorAll(".chip").Select(chip => chip.TextContent));
        var email = component.Find("a[aria-label='EMAIL']");
        Assert.Equal($"mailto:{universe.Stats.About.Email}", email.GetAttribute("href"));
        foreach (var (label, href) in new[] { ("GITHUB", universe.Stats.About.Github), ("LINKEDIN", universe.Stats.About.Linkedin) })
        {
            var link = component.Find($"a[aria-label='{label}']");
            Assert.Equal(href, link.GetAttribute("href"));
            Assert.Equal("_blank", link.GetAttribute("target"));
            Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        }
        Assert.Equal(3, Value(component, "COMMS").QuerySelectorAll("a svg[aria-hidden='true']").Length);
        Assert.Empty(component.FindAll("script, link, img"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("https://github.com/example", null)]
    [InlineData(null, "https://www.linkedin.com/in/example")]
    public void AboutOmitsOnlyMissingSocialLinks(string? github, string? linkedin)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var stats = universe.Stats with { About = universe.Stats.About with { Github = github, Linkedin = linkedin } };

        var component = RenderBody(context, universe.Bodies[0], stats);

        Assert.Single(component.FindAll("a[aria-label='EMAIL']"));
        Assert.Equal(github is not null, component.FindAll("a[aria-label='GITHUB']").Any());
        Assert.Equal(linkedin is not null, component.FindAll("a[aria-label='LINKEDIN']").Any());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ProjectsRendersStatsAndFeaturedCard(bool hasThumbnail, bool draft)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var featured = TestContent.Projects()[0] with { Thumbnail = hasThumbnail ? "/images/featured.webp" : null, Draft = draft };
        var stats = universe.Stats with { Projects = new ProjectStats(12, ["C#", "게임 개발", "Blazor"], featured) };

        var component = RenderBody(context, universe.Bodies[1], stats);

        Assert.Equal(new[] { "PROJECTS", "PRIMARY STACK", "FEATURED" }, component.FindAll("dl dt.hud-label").Select(label => label.TextContent));
        Assert.Equal("12", Value(component, "PROJECTS").TextContent);
        Assert.Equal(stats.Projects.TopStack, Value(component, "PRIMARY STACK").QuerySelectorAll(".chip").Select(chip => chip.TextContent));
        var card = Value(component, "FEATURED").QuerySelector("a")!;
        Assert.Equal($"/projects/{featured.Slug}", card.GetAttribute("href"));
        Assert.Equal(featured.Title, card.QuerySelector("h3")!.TextContent);
        Assert.Equal(draft ? new[] { "DRAFT" } : [], card.QuerySelectorAll(".badge.badge-draft").Select(badge => badge.TextContent));
        if (hasThumbnail)
        {
            var image = Assert.Single(card.QuerySelectorAll("img"));
            Assert.Equal(featured.Thumbnail, image.GetAttribute("src"));
            Assert.Equal(string.Empty, image.GetAttribute("alt"));
        }
        else
        {
            Assert.Empty(card.QuerySelectorAll("img"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BlogRendersStatsAndLatestCard(bool draft)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var latest = TestContent.Posts()[0] with { Draft = draft };
        var stats = universe.Stats with { Blog = new BlogStats(24, new DateOnly(2026, 1, 2), ["Unity", "게임 개발", "Blazor"], latest) };

        var component = RenderBody(context, universe.Bodies[2], stats);

        Assert.Equal(new[] { "POSTS", "LAST TRANSMISSION", "TOP TAGS", "LATEST" }, component.FindAll("dl dt.hud-label").Select(label => label.TextContent));
        Assert.Equal("24", Value(component, "POSTS").TextContent);
        Assert.Equal("2026-01-02", Value(component, "LAST TRANSMISSION").TextContent);
        Assert.Equal("Unity · 게임 개발 · Blazor", Value(component, "TOP TAGS").TextContent);
        var card = Value(component, "LATEST").QuerySelector("a")!;
        Assert.Equal($"/blog/{latest.Slug}", card.GetAttribute("href"));
        Assert.Equal(latest.Title, card.QuerySelector("h3")!.TextContent);
        Assert.Equal(latest.Summary, card.QuerySelector("p")!.TextContent);
        Assert.Equal(draft ? new[] { "DRAFT" } : [], card.QuerySelectorAll(".badge.badge-draft").Select(badge => badge.TextContent));
    }

    [Fact]
    public void MissingLatestAndPublicationDateRenderWithoutCard()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();

        var component = RenderBody(context, universe.Bodies[2], universe.Stats);

        Assert.Equal("0", Value(component, "POSTS").TextContent);
        Assert.Equal("—", Value(component, "LAST TRANSMISSION").TextContent);
        Assert.Equal(string.Empty, Value(component, "TOP TAGS").TextContent);
        Assert.Empty(component.FindAll("a, .body-card"));
    }

    [Fact]
    public void MissingFeaturedRendersWithoutCard()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();

        var component = RenderBody(context, universe.Bodies[1], universe.Stats);

        Assert.Equal("0", Value(component, "PROJECTS").TextContent);
        Assert.Empty(Value(component, "PRIMARY STACK").QuerySelectorAll(".chip"));
        Assert.Empty(component.FindAll("a, .body-card"));
    }

    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    [InlineData("playground")]
    [InlineData("unknown")]
    public void LockedBodyShowsOnlyStatusAfterHeaderRegardlessOfId(string id)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var body = universe.Bodies[0] with { Id = id, Locked = true };
        var stats = universe.Stats with
        {
            Projects = new ProjectStats(12, ["Unity"], TestContent.Projects()[0]),
            Blog = new BlogStats(24, new DateOnly(2026, 10, 20), ["Blazor"], TestContent.Posts()[0])
        };

        var component = RenderBody(context, body, stats);

        Assert.Equal("STATUS", Assert.Single(component.FindAll("dl dt.hud-label")).TextContent);
        Assert.Equal("SIGNAL UNCONFIRMED", Assert.Single(component.FindAll("dl dd")).TextContent);
        Assert.Equal("COMING SOON", component.Find(".body-coming-soon").TextContent);
        Assert.Empty(component.FindAll("a, .chip, .body-card, .body-metric"));
        Assert.Equal(new[] { body.Name, body.Subtitle, body.Tagline, "STATUS", "SIGNAL UNCONFIRMED", "COMING SOON" },
            component.FindAll("h2, p, dt, dd").Select(element => element.TextContent));
    }

    [Fact]
    public void UnknownUnlockedBodyRendersOnlyHeader()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var body = universe.Bodies[0] with { Id = "unknown" };

        var component = RenderBody(context, body, universe.Stats);

        Assert.Equal(body.Name, component.Find("h2").TextContent);
        Assert.Equal(body.Subtitle, component.Find(".hud-label").TextContent);
        Assert.Equal(body.Tagline, component.Find(".body-tagline").TextContent);
        Assert.Single(component.Find(".body-info").Children);
        Assert.Empty(component.FindAll("dl, a, .body-coming-soon"));
    }

    private static IRenderedComponent<BodyInfo> RenderBody(BunitContext context, CelestialBody body, HudStats stats) =>
        context.Render<BodyInfo>(parameters => parameters.Add(p => p.Body, body).Add(p => p.Stats, stats));

    private static IElement Value(IRenderedComponent<BodyInfo> component, string label) => component.FindAll("dt").Single(element => element.TextContent == label).NextElementSibling!;
}
