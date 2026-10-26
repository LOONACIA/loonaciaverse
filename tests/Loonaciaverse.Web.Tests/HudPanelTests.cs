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
    public void PanelUsesBodyInfoAndAnAccessibleCloseButtonWithoutTravel(string id)
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
        var close = Assert.Single(component.FindAll("button"));
        Assert.Equal("CLOSE", close.GetAttribute("aria-label"));
        Assert.Equal("button", close.GetAttribute("type"));
        Assert.Equal("✕", close.TextContent);
        Assert.DoesNotContain("TRAVEL", component.Markup);
        if (body.Locked)
        {
            Assert.Equal("COMING SOON", component.Find(".body-coming-soon").TextContent);
        }
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
    public void CloseButtonRaisesOnClose()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        var closeCount = 0;
        var component = context.Render<HudPanel>(parameters => parameters
            .Add(p => p.Body, universe.Bodies[2]).Add(p => p.Stats, universe.Stats).Add(p => p.OnClose, () => closeCount++));

        component.Find("button[aria-label='CLOSE']").Click();

        Assert.Equal(1, closeCount);
    }
}
