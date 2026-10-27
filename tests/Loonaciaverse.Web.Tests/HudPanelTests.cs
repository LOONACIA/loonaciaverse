using Bunit;
using Loonaciaverse.Web.Components;

namespace Loonaciaverse.Web.Tests;

public class HudPanelTests
{
    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    [InlineData("playground")]
    public void PanelUsesBodyInfoAndGroupsBackAndTravelButtons(string id)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var body = universe.Bodies.Single(body => body.Id == id);
        var component = context.Render<HudPanel>(parameters => parameters.Add(p => p.Body, body).Add(p => p.Stats, universe.Stats));

        var panel = component.Find("section.hud-panel.hud-frame");
        Assert.Equal($"{body.Name} — {body.Subtitle}", panel.GetAttribute("aria-label"));
        Assert.Same(body, component.FindComponent<BodyInfo>().Instance.Body);
        Assert.Same(universe.Stats, component.FindComponent<BodyInfo>().Instance.Stats);
        Assert.Equal(body.Name, component.Find("h2").TextContent);
        Assert.Equal(2, component.FindAll("button").Count);
        Assert.Equal(2, component.FindAll(".hud-actions > button").Count);
        var back = component.Find(".hud-actions > button:first-child");
        Assert.Contains("hud-back", back.ClassList);
        Assert.Equal("button", back.GetAttribute("type"));
        Assert.Equal("BACK", back.TextContent);
        var travel = component.Find(".hud-actions > button.hud-travel");
        Assert.Equal("TRAVEL", travel.TextContent);
        Assert.Equal("button", travel.GetAttribute("type"));
        Assert.Equal(body.Locked || body.Route is null, travel.HasAttribute("disabled"));
        if (body.Locked)
        {
            Assert.Equal("COMING SOON", component.Find(".body-coming-soon").TextContent);
        }
    }

    [Theory]
    [InlineData(false, "/blog", false)]
    [InlineData(false, null, true)]
    [InlineData(true, "/blog", true)]
    [InlineData(true, null, true)]
    public void TravelIsDisabledForLockedOrRoutelessBodies(bool locked, string? route, bool disabled)
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var body = universe.Bodies[2] with { Locked = locked, Route = route };
        var component = context.Render<HudPanel>(parameters => parameters.Add(p => p.Body, body).Add(p => p.Stats, universe.Stats));

        Assert.Equal(disabled, component.Find("button.hud-travel").HasAttribute("disabled"));
    }

    [Fact]
    public void TravelButtonRaisesOnTravel()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var travelCount = 0;
        var component = context.Render<HudPanel>(parameters => parameters
            .Add(p => p.Body, universe.Bodies[2]).Add(p => p.Stats, universe.Stats).Add(p => p.OnTravel, () => travelCount++));

        component.Find("button.hud-travel").Click();

        Assert.Equal(1, travelCount);
    }

    [Fact]
    public async Task FocusHeadingDelegatesToTheBodyInfoHeadingWithoutScrolling()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var component = context.Render<HudPanel>(parameters => parameters.Add(p => p.Body, universe.Bodies[2]).Add(p => p.Stats, universe.Stats));

        await component.InvokeAsync(() => component.Instance.FocusHeadingAsync().AsTask());

        var invocation = context.JSInterop.VerifyFocusAsyncInvoke();
        invocation.Arguments[0].ShouldBeElementReferenceTo(component.Find("h2"));
        Assert.Equal(true, invocation.Arguments[1]);
    }

    [Fact]
    public void BackButtonRaisesOnClose()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var closeCount = 0;
        var component = context.Render<HudPanel>(parameters => parameters
            .Add(p => p.Body, universe.Bodies[2]).Add(p => p.Stats, universe.Stats).Add(p => p.OnClose, () => closeCount++));

        component.Find("button.hud-back").Click();

        Assert.Equal(1, closeCount);
    }
}
