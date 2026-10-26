using Bunit;
using Loonaciaverse.Web.Components;

namespace Loonaciaverse.Web.Tests;

public class ListHomeTests
{
    [Fact]
    public void RendersCardsAndTravelLinksInUniverseOrder()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();

        var component = context.Render<ListHome>(parameters => parameters.Add(p => p.Universe, universe));

        var cards = component.FindAll("article.hud-frame");
        Assert.Equal(4, cards.Count);
        Assert.Equal(universe.Bodies.Select(body => body.Name), cards.Select(card => card.QuerySelector("h2")!.TextContent));
        Assert.Equal(universe.Bodies.Count, component.FindComponents<BodyInfo>().Count);
        for (var index = 0; index < universe.Bodies.Count; index++)
        {
            var body = universe.Bodies[index];
            var card = cards[index];
            var travel = card.QuerySelector("footer .travel")!;
            Assert.Equal("TRAVEL", travel.TextContent);
            if (body.Locked)
            {
                Assert.NotEqual("A", travel.TagName);
                Assert.Equal("true", travel.GetAttribute("aria-disabled"));
                Assert.Null(travel.GetAttribute("href"));
            }
            else
            {
                Assert.Equal("A", travel.TagName);
                Assert.Equal(body.Route, travel.GetAttribute("href"));
                Assert.Null(travel.GetAttribute("aria-disabled"));
            }
            Assert.Equal(body, component.FindComponents<BodyInfo>()[index].Instance.Body);
            Assert.Equal(universe.Stats, component.FindComponents<BodyInfo>()[index].Instance.Stats);
        }
        Assert.Empty(component.FindAll("a a, a article"));
    }

    [Theory]
    [InlineData(true, "/about")]
    [InlineData(false, null)]
    public void LockedBodyOrMissingRouteDisablesTravel(bool locked, string? route)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        universe = universe with { Bodies = [universe.Bodies[0] with { Locked = locked, Route = route }] };

        var component = context.Render<ListHome>(parameters => parameters.Add(p => p.Universe, universe));

        var travel = component.Find("footer .travel");
        Assert.Equal("TRAVEL", travel.TextContent);
        Assert.NotEqual("A", travel.TagName);
        Assert.Equal("true", travel.GetAttribute("aria-disabled"));
        Assert.Null(travel.GetAttribute("href"));
        Assert.Empty(component.FindAll("footer a"));
    }

    [Fact]
    public void KeepsFeaturedAndLatestLinksSeparateFromTravel()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        universe = universe with
        {
            Bodies = universe.Bodies.Reverse().ToArray(),
            Stats = universe.Stats with
            {
                Projects = universe.Stats.Projects with { Featured = TestContent.Projects()[0] },
                Blog = universe.Stats.Blog with { Latest = TestContent.Posts()[0] }
            }
        };

        var component = context.Render<ListHome>(parameters => parameters.Add(p => p.Universe, universe));

        Assert.Equal(universe.Bodies.Select(body => body.Name), component.FindAll("article h2").Select(heading => heading.TextContent));
        Assert.Equal("/projects/first-project", component.Find("a.body-featured").GetAttribute("href"));
        Assert.Equal("/blog/first-post", component.Find("a.body-latest").GetAttribute("href"));
        Assert.Equal(new[] { "/blog", "/projects", "/about" }, component.FindAll("footer a").Select(link => link.GetAttribute("href")));
        Assert.Empty(component.FindAll("a a, a article"));
    }
}
