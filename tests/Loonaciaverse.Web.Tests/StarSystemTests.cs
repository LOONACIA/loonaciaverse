using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Loonaciaverse.Web.Tests;

public class StarSystemTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InitResultSelectsSceneOrListWithTheSameSiteHeading(bool ready)
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(ready);
        module.SetupVoid("dispose").SetVoidResult();
        var universe = TestContent.Universe();
        universe = universe with { Site = universe.Site with { Title = "TEST SYSTEM" } };

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        Assert.Equal(universe.Site.Title, component.Find("h1").TextContent);
        Assert.Equal("SELECT DESTINATION", component.Find("h1 + .hud-label").TextContent);
        if (ready)
        {
            var host = component.Find(".scene-host");
            Assert.Empty(host.ChildNodes);
            Assert.Equal(host.ParentElement, component.Find(".hud-layer").ParentElement);
            Assert.Empty(component.FindAll("article.hud-frame"));
        }
        else
        {
            Assert.Empty(component.FindAll(".scene-host"));
            Assert.Equal(4, component.FindAll("article.hud-frame").Count);
        }
    }

    [Fact]
    public void PreparingSceneKeepsAnEmptyHostAndHeadingUntilInitCompletes()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        var init = module.Setup<bool>("init", _ => true);
        module.SetupVoid("dispose").SetVoidResult();
        var universe = TestContent.Universe();

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        Assert.Empty(component.Find(".scene-host").ChildNodes);
        Assert.Empty(component.FindAll("article.hud-frame"));
        Assert.Empty(component.FindAll(".hud-layer button"));
        Assert.Equal(universe.Site.Title, component.Find("h1").TextContent);

        init.SetResult(false);
        component.WaitForAssertion(() => Assert.Equal(4, component.FindAll("article.hud-frame").Count));
    }

    [Fact]
    public void InitReceivesTheHostUniverseAndCallbackReferenceOnlyOnFirstRender()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        var init = module.Setup<bool>("init", _ => true);
        module.SetupVoid("dispose").SetVoidResult();
        var universe = TestContent.Universe();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));
        var invocation = Assert.Single(module.VerifyInvoke("init", 1));
        Assert.Equal(3, invocation.Arguments.Count);
        invocation.Arguments[0].ShouldBeElementReferenceTo(component.Find(".scene-host"));
        Assert.Same(universe, invocation.Arguments[1]);
        Assert.IsType<DotNetObjectReference<SceneInterop>>(invocation.Arguments[2]);

        init.SetResult(true);
        component.Render(parameters => parameters.Add(p => p.Universe, universe));

        context.JSInterop.VerifyInvoke("import", 1);
        module.VerifyInvoke("init", 1);
    }

    [Fact]
    public async Task ImportFailureShowsTheListWithoutUnhandledExceptions()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupModule("./js/scene.js");
        context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/scene.js")!.SetException(new JSException("Module unavailable"));

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        Assert.Equal(4, component.FindAll("article.hud-frame").Count);
        Assert.Empty(component.FindAll(".scene-host"));
        await context.DisposeComponentsAsync();
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task InitFailureShowsTheListAndStillDisposesTheScene()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetException(new JSException("Renderer failed"));
        module.SetupVoid("dispose").SetVoidResult();

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        Assert.Equal(4, component.FindAll("article.hud-frame").Count);
        Assert.Empty(component.FindAll(".scene-host"));
        await context.DisposeComponentsAsync();
        module.VerifyInvoke("dispose", 1);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task ComponentDisposalCleansUpTheSceneAndCallbacksOnce()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(true);
        module.SetupVoid("dispose").SetVoidResult();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        var invocation = Assert.Single(module.VerifyInvoke("init", 1));
        var callbacks = Assert.IsType<DotNetObjectReference<SceneInterop>>(invocation.Arguments[2]);
        var instance = component.Instance;

        await context.DisposeComponentsAsync();
        await instance.DisposeAsync();

        module.VerifyInvoke("dispose", 1);
        Assert.Throws<ObjectDisposedException>(() => callbacks.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisposalDuringInitWaitsThenCleansUpRegardlessOfTheResult(bool ready)
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        var init = module.Setup<bool>("init", _ => true);
        module.SetupVoid("dispose").SetVoidResult();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        var disposal = component.Instance.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        module.VerifyNotInvoke("dispose");
        init.SetResult(ready);
        await disposal;

        module.VerifyInvoke("dispose", 1);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task DisposalDuringImportReleasesTheLateModuleWithoutStartingTheScene()
    {
        var runtime = new PendingImportRuntime();
        var module = new TrackingModule();
        var scene = new SceneInterop(runtime);
        var initialization = scene.InitAsync(default, TestContent.Universe());

        var disposal = scene.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        runtime.Import.SetResult(module);

        Assert.False(await initialization);
        await disposal;
        Assert.Equal(0, module.InitCalls);
        Assert.Equal(1, module.SceneDisposeCalls);
        Assert.Equal(1, module.ReferenceDisposeCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposalReleasesTheModuleEvenWhenSceneCleanupThrows(bool disconnected)
    {
        using var context = new BunitContext();
        var module = new TrackingModule { CleanupException = disconnected ? new JSDisconnectedException("Disconnected") : new JSException("Cleanup failed") };
        context.JSInterop.SetupModule("./js/scene.js");
        context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/scene.js")!.SetResult(module);
        var scene = new SceneInterop(context.JSInterop.JSRuntime);
        Assert.True(await scene.InitAsync(default, TestContent.Universe()));

        await scene.DisposeAsync();
        await scene.DisposeAsync();

        Assert.Equal(1, module.SceneDisposeCalls);
        Assert.Equal(1, module.ReferenceDisposeCalls);
        Assert.Throws<ObjectDisposedException>(() => module.Callbacks!.Value);
    }

    [Fact]
    public async Task InteropCachesTheImportAndDoesNotInitializeAfterDisposal()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(true);
        module.SetupVoid("dispose").SetVoidResult();
        var scene = new SceneInterop(context.JSInterop.JSRuntime);

        Assert.True(await scene.InitAsync(default, TestContent.Universe()));
        Assert.True(await scene.InitAsync(default, TestContent.Universe()));
        await scene.DisposeAsync();
        Assert.False(await scene.InitAsync(default, TestContent.Universe()));

        context.JSInterop.VerifyInvoke("import", 1);
        module.VerifyInvoke("init", 2);
        module.VerifyInvoke("dispose", 1);
    }

    [Fact]
    public async Task DisposalBeforeInitIsSafeAndDoesNotImportAModule()
    {
        using var context = new BunitContext();
        var scene = new SceneInterop(context.JSInterop.JSRuntime);

        await scene.DisposeAsync();
        await scene.DisposeAsync();

        Assert.False(await scene.InitAsync(default, TestContent.Universe()));
        Assert.Empty(context.JSInterop.Invocations);
    }

    [Fact]
    public async Task SelectionPushesThenReplacesWithTheSameMarkerAndIgnoresTheSameBody()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        Assert.Empty(component.FindComponents<HudPanel>());
        Assert.False(component.Find(".hud-layer").ClassList.Contains("hud-open"));

        await ClickBodyAsync(component, module, "blog");

        var pushed = Assert.Single(navigation.History);
        Assert.Equal("/?focus=blog", pushed.Uri);
        Assert.False(pushed.Options.ReplaceHistoryEntry);
        var marker = pushed.Options.HistoryEntryState;
        Assert.False(string.IsNullOrEmpty(marker));
        Assert.Equal(marker, navigation.HistoryEntryState);
        Assert.Equal("blog", component.FindComponent<HudPanel>().Instance.Body.Id);
        Assert.True(component.Find(".hud-layer").ClassList.Contains("hud-open"));
        var focused = Assert.Single(module.VerifyInvoke("focus", 1));
        Assert.Equal(new object?[] { "blog", false }, focused.Arguments);

        await ClickBodyAsync(component, module, "projects");

        var replaced = navigation.History.First();
        Assert.Equal("/?focus=projects", replaced.Uri);
        Assert.True(replaced.Options.ReplaceHistoryEntry);
        Assert.Equal(marker, replaced.Options.HistoryEntryState);
        Assert.Equal("projects", component.FindComponent<BodyInfo>().Instance.Body.Id);
        var historyCount = navigation.History.Count;

        await ClickBodyAsync(component, module, "projects");

        Assert.Equal(historyCount, navigation.History.Count);
        module.VerifyInvoke("focus", 2);
        module.VerifyNotInvoke("clearFocus");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("external-entry")]
    public async Task SwitchingFromADirectFocusPreservesTheExistingHistoryState(string? entryState)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog", new NavigationOptions { HistoryEntryState = entryState });
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await ClickBodyAsync(component, module, "about");

        Assert.Equal("/?focus=about", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(entryState, navigation.History.First().Options.HistoryEntryState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingAPushedFocusUsesHistoryBackAndWaitsForTheUrl(bool closeButton)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        context.JSInterop.SetupVoid("history.back").SetVoidResult();
        var navigation = Navigation(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        await ClickBodyAsync(component, module, "blog");
        var historyCount = navigation.History.Count;

        if (closeButton)
        {
            component.Find("button.hud-back").Click();
        }
        else
        {
            await ClickBodyAsync(component, module, null);
        }

        context.JSInterop.VerifyInvoke("history.back", 1);
        Assert.Equal(historyCount, navigation.History.Count);
        Assert.Equal("blog", component.FindComponent<HudPanel>().Instance.Body.Id);
        module.VerifyNotInvoke("clearFocus");

        // bUnit records history.back but the browser's resulting LocationChanged must be simulated.
        await component.InvokeAsync(() => navigation.NavigateTo("/"));

        Assert.Empty(component.FindComponents<HudPanel>());
        module.VerifyInvoke("clearFocus", 1);
    }

    [Fact]
    public async Task RecreatedComponentRecognizesTheMarkerStoredInTheHistoryEntry()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        context.JSInterop.SetupVoid("history.back").SetVoidResult();
        var navigation = Navigation(context);
        var original = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        await ClickBodyAsync(original, module, "blog");
        var marker = navigation.HistoryEntryState;
        await context.DisposeComponentsAsync();
        navigation.NavigateTo("/blog");
        navigation.NavigateTo("/?focus=blog", new NavigationOptions { HistoryEntryState = marker });

        var recreated = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        recreated.Find("button.hud-back").Click();

        context.JSInterop.VerifyInvoke("history.back", 1);
        Assert.Equal(new object?[] { "blog", true }, module.VerifyInvoke("focus", 2).Last().Arguments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingADirectFocusReplacesTheUrlWithOverview(bool closeButton)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        if (closeButton)
        {
            component.Find("button.hud-back").Click();
        }
        else
        {
            await ClickBodyAsync(component, module, null);
        }

        Assert.Equal("/", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Empty(component.FindComponents<HudPanel>());
        module.VerifyInvoke("clearFocus", 1);
        context.JSInterop.VerifyNotInvoke("history.back");
    }

    [Theory]
    [InlineData("unknown", true)]
    [InlineData("", true)]
    [InlineData("BLOG", true)]
    [InlineData("blog&focus=about", true)]
    [InlineData("unknown", false)]
    public void InvalidFocusIsReplacedWithOverviewEvenWhenTheSceneIsUnavailable(string focus, bool ready)
    {
        using var context = new BunitContext();
        var module = SetupScene(context, ready);
        var navigation = Navigation(context);
        navigation.NavigateTo($"/?focus={focus}");

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        Assert.Equal("/", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Empty(component.FindComponents<HudPanel>());
        module.VerifyNotInvoke("focus");
    }

    [Theory]
    [InlineData("about")]
    [InlineData("projects")]
    [InlineData("blog")]
    [InlineData("playground")]
    [InlineData("%62log")]
    public void DirectFocusIsAppliedImmediatelyAfterInitAndRendersTheBodyInfo(string focus)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo($"/?focus={focus}#details");
        var universe = TestContent.Universe();
        var id = Uri.UnescapeDataString(focus);

        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        var invocation = Assert.Single(module.VerifyInvoke("focus", 1));
        Assert.Equal(new object?[] { id, true }, invocation.Arguments);
        Assert.Equal(new[] { "init", "focus" }, module.Invocations.Select(call => call.Identifier));
        var panel = component.FindComponent<HudPanel>();
        var body = universe.Bodies.Single(body => body.Id == id);
        Assert.Same(body, panel.Instance.Body);
        Assert.Same(universe.Stats, panel.Instance.Stats);
        Assert.Equal(body.Name, panel.Find("h2").TextContent);
        Assert.Single(navigation.History);
        module.VerifyNotInvoke("clearFocus");
    }

    [Fact]
    public async Task UrlChangesAnimateFocusAndClearItWithoutApplyingTheSameStateTwice()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=projects"));

        Assert.Equal(new object?[] { "projects", false }, module.VerifyInvoke("focus", 2).Last().Arguments);
        Assert.Equal("projects", component.FindComponent<HudPanel>().Instance.Body.Id);

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=projects&view=detail#same-body"));
        component.Render();
        module.VerifyInvoke("focus", 2);

        await component.InvokeAsync(() => navigation.NavigateTo("/"));
        await component.InvokeAsync(() => navigation.NavigateTo("/#overview"));

        module.VerifyInvoke("clearFocus", 1);
        Assert.Empty(component.FindComponents<HudPanel>());

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=about"));

        Assert.Equal(new object?[] { "about", false }, module.VerifyInvoke("focus", 3).Last().Arguments);
    }

    [Fact]
    public async Task UrlChangesDuringInitApplyOnlyTheLatestFocusImmediately()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/scene.js");
        var init = module.Setup<bool>("init", _ => true);
        module.SetupVoid("dispose").SetVoidResult();
        module.SetupVoid("focus", _ => true).SetVoidResult();
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=projects"));

        Assert.Empty(component.FindComponents<HudPanel>());
        module.VerifyNotInvoke("focus");
        init.SetResult(true);

        component.WaitForAssertion(() =>
        {
            Assert.Equal(new object?[] { "projects", true }, Assert.Single(module.VerifyInvoke("focus", 1)).Arguments);
            Assert.Equal("projects", component.FindComponent<HudPanel>().Instance.Body.Id);
            VerifyLastFocus(context, component.Find("h2"), 1);
        });
    }

    [Fact]
    public async Task EmptySpaceInOverviewDoesNotNavigateOrCallTheScene()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await ClickBodyAsync(component, module, null);

        Assert.Empty(Navigation(context).History);
        context.JSInterop.VerifyNotInvoke("history.back");
        module.VerifyNotInvoke("focus");
        module.VerifyNotInvoke("clearFocus");
    }

    [Fact]
    public async Task UnavailableSceneKeepsTheListWithoutFocusCommandsOrHud()
    {
        using var context = new BunitContext();
        var module = SetupScene(context, false);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=about"));
        await ClickBodyAsync(component, module, "projects");
        await ClickBodyAsync(component, module, null);

        Assert.EndsWith("/?focus=about", navigation.Uri);
        Assert.Empty(component.FindComponents<HudPanel>());
        Assert.Single(component.FindComponents<ListHome>());
        module.VerifyNotInvoke("focus");
        module.VerifyNotInvoke("clearFocus");

        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=unknown"));

        Assert.Equal("/", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Theory]
    [InlineData("/blog?focus=about")]
    [InlineData("/projects?focus=unknown")]
    [InlineData("/about")]
    public async Task LeavingHomeDoesNotApplyOrNormalizeFocus(string path)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await component.InvokeAsync(() => navigation.NavigateTo(path));

        Assert.Equal(path, navigation.History.First().Uri);
        Assert.Equal(2, navigation.History.Count);
        module.VerifyInvoke("focus", 1);
        module.VerifyNotInvoke("clearFocus");
    }

    [Fact]
    public async Task SceneFocusErrorsDoNotPreventHudUpdatesOrNavigation()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        module.SetupVoid("focus", _ => true).SetException(new JSException("Focus failed"));
        module.SetupVoid("clearFocus").SetException(new JSException("Clear failed"));
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        Assert.Equal("blog", component.FindComponent<HudPanel>().Instance.Body.Id);
        await ClickBodyAsync(component, module, "projects");
        Assert.Equal("projects", component.FindComponent<HudPanel>().Instance.Body.Id);
        component.Find("button.hud-back").Click();

        Assert.Equal("/", navigation.History.First().Uri);
        Assert.Empty(component.FindComponents<HudPanel>());
        module.VerifyInvoke("clearFocus", 1);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task DisposedComponentNoLongerHandlesNavigationOrSceneCallbacks()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        var scene = CallbackReference(module).Value;
        await context.DisposeComponentsAsync();

        navigation.NavigateTo("/?focus=unknown");
        await scene.OnBodyClicked("blog");

        Assert.Equal("/?focus=unknown", Assert.Single(navigation.History).Uri);
        module.VerifyNotInvoke("focus");
        module.VerifyNotInvoke("clearFocus");
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task InteropFocusCommandsAreNoOpsBeforeInitializationAfterFailureAndAfterDisposal()
    {
        using var context = new BunitContext();
        var module = SetupScene(context, false);
        var scene = new SceneInterop(context.JSInterop.JSRuntime);

        await scene.FocusAsync("blog", true);
        await scene.ClearFocusAsync();
        await scene.HighlightAsync("blog");
        await scene.WarpAsync("blog");
        Assert.Empty(context.JSInterop.Invocations);
        Assert.False(await scene.InitAsync(default, TestContent.Universe()));
        await scene.FocusAsync("blog", false);
        await scene.ClearFocusAsync();
        await scene.HighlightAsync(null);
        await scene.WarpAsync("blog");
        await scene.DisposeAsync();
        await scene.FocusAsync("blog", true);
        await scene.ClearFocusAsync();
        await scene.HighlightAsync("about");
        await scene.WarpAsync("about");

        module.VerifyNotInvoke("focus");
        module.VerifyNotInvoke("clearFocus");
        module.VerifyNotInvoke("highlight");
        module.VerifyNotInvoke("warp");
    }

    [Theory]
    [InlineData("about", "/about")]
    [InlineData("projects", "/projects")]
    [InlineData("blog", "/blog")]
    public async Task TravelHidesControlsAndWaitsForWarpBeforePushingTheDestination(string id, string route)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var warp = module.SetupVoid("warp", id);
        var navigation = Navigation(context);
        navigation.NavigateTo($"/?focus={id}");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        var travel = component.Find("button.hud-travel").ClickAsync(new MouseEventArgs());

        component.WaitForAssertion(() =>
        {
            Assert.Equal(new object?[] { id }, Assert.Single(module.VerifyInvoke("warp", 1)).Arguments);
            Assert.Equal($"/?focus={id}", Assert.Single(navigation.History).Uri);
            Assert.Empty(component.FindComponents<HudPanel>());
            Assert.Empty(component.FindAll(".hud-layer button"));
        });
        Assert.False(travel.IsCompleted);

        warp.SetVoidResult();
        await travel;

        Assert.Equal(route, navigation.History.First().Uri);
        Assert.Equal(2, navigation.History.Count);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task WarpingIgnoresSceneKeyboardStaleControlsAndLocationChanges()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var warp = module.SetupVoid("warp", "blog");
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        var panel = component.FindComponent<HudPanel>().Instance;
        var travel = component.Find("button.hud-travel").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() => module.VerifyInvoke("warp", 1));

        await ClickBodyAsync(component, module, "projects");
        await ClickBodyAsync(component, module, null);
        foreach (var key in new[] { "Escape", "ArrowLeft", "ArrowRight", "Enter" })
        {
            component.Find(".star-system").KeyDown(new KeyboardEventArgs { Key = key });
        }
        await component.InvokeAsync(() => panel.OnClose.InvokeAsync());
        await component.InvokeAsync(() => panel.OnTravel.InvokeAsync());

        Assert.Equal("/?focus=blog", Assert.Single(navigation.History).Uri);
        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=projects"));
        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=unknown"));
        component.Render();

        Assert.Equal("/?focus=unknown", navigation.History.First().Uri);
        Assert.Equal(3, navigation.History.Count);
        Assert.Empty(component.FindComponents<HudPanel>());
        Assert.Empty(component.FindAll(".hud-layer button"));
        module.VerifyInvoke("focus", 1);
        module.VerifyNotInvoke("clearFocus");
        module.VerifyNotInvoke("highlight");
        module.VerifyInvoke("warp", 1);
        context.JSInterop.VerifyNotInvoke("history.back");
        context.JSInterop.VerifyFocusAsyncInvoke(1);

        warp.SetVoidResult();
        await travel;

        Assert.Equal("/blog", navigation.History.First().Uri);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TravelStillPushesTheDestinationWhenWarpThrows(bool jsException)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        module.SetupVoid("warp", "blog").SetException<Exception>(jsException ? new JSException("Warp failed") : new InvalidOperationException("Unexpected warp failure"));
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        await component.Find("button.hud-travel").ClickAsync(new MouseEventArgs());

        module.VerifyInvoke("warp", 1);
        Assert.Equal("/blog", navigation.History.First().Uri);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Theory]
    [InlineData(true, "/blog")]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public async Task LockedOrRoutelessBodiesCannotStartWarp(bool locked, string? route)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=playground");
        var universe = TestContent.Universe();
        universe = universe with { Bodies = universe.Bodies.Select(body => body.Id == "playground" ? body with { Locked = locked, Route = route } : body).ToArray() };
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        Assert.True(component.Find("button.hud-travel").HasAttribute("disabled"));
        // Also enforce the guard for an already queued button callback.
        await component.InvokeAsync(() => component.FindComponent<HudPanel>().Instance.OnTravel.InvokeAsync());

        Assert.Equal("/?focus=playground", Assert.Single(navigation.History).Uri);
        Assert.Equal("playground", component.FindComponent<HudPanel>().Instance.Body.Id);
        Assert.Equal(4, component.FindAll(".hud-layer > button").Count);
        module.VerifyNotInvoke("warp");
    }

    [Fact]
    public async Task InteropWarpSwallowsJsExceptions()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        module.SetupVoid("warp", "blog").SetException(new JSException("Warp failed"));
        await using var scene = new SceneInterop(context.JSInterop.JSRuntime);
        Assert.True(await scene.InitAsync(default, TestContent.Universe()));

        await scene.WarpAsync("blog");

        Assert.Equal(new object?[] { "blog" }, Assert.Single(module.VerifyInvoke("warp", 1)).Arguments);
    }

    [Fact]
    public void ReadySceneRendersHiddenBodyButtonsInUniverseOrderWithAccessibleNames()
    {
        using var context = new BunitContext();
        SetupScene(context);
        var universe = TestContent.Universe();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        var buttons = component.FindAll(".hud-layer > button");
        Assert.Equal(universe.Bodies.Select(body => $"{body.Name} — {body.Subtitle}"), buttons.Select(button => button.TextContent));
        Assert.All(buttons, button =>
        {
            Assert.Contains("visually-hidden", button.ClassList);
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.False(button.HasAttribute("disabled"));
            Assert.False(button.HasAttribute("tabindex"));
        });
        Assert.Equal("-1", component.Find(".star-system").GetAttribute("tabindex"));
        context.JSInterop.VerifyNotInvoke("Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void UnavailableSceneDoesNotRenderBodyButtons()
    {
        using var context = new BunitContext();
        SetupScene(context, false);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        Assert.Empty(component.FindAll(".hud-layer button"));
        context.JSInterop.VerifyNotInvoke("Blazor._internal.domWrapper.focus");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BodyButtonFocusAndBlurHighlightTheBodyEvenWhenTheSceneThrows(bool throws)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        if (throws)
        {
            module.SetupVoid("highlight", _ => true).SetException(new JSException("Highlight failed"));
        }
        var universe = TestContent.Universe();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));

        for (var index = 0; index < universe.Bodies.Count; index++)
        {
            component.FindAll(".hud-layer > button")[index].Focus();
            Assert.Equal(new object?[] { universe.Bodies[index].Id }, module.Invocations.Last().Arguments);
            component.FindAll(".hud-layer > button")[index].Blur();
            Assert.Equal(new object?[] { null }, module.Invocations.Last().Arguments);
        }

        module.VerifyInvoke("highlight", 8);
        Assert.Empty(Navigation(context).History);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public void BodyButtonSelectionPushesThenReplacesAndPreservesTheMarker()
    {
        using var context = new BunitContext();
        SetupScene(context);
        var navigation = Navigation(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        component.FindAll(".hud-layer > button")[2].Click();

        var pushed = Assert.Single(navigation.History);
        Assert.Equal("/?focus=blog", pushed.Uri);
        Assert.False(pushed.Options.ReplaceHistoryEntry);
        var marker = pushed.Options.HistoryEntryState;
        Assert.False(string.IsNullOrEmpty(marker));

        component.FindAll(".hud-layer > button")[3].Click();

        Assert.Equal("/?focus=playground", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(marker, navigation.HistoryEntryState);
        Assert.Equal("playground", component.FindComponent<HudPanel>().Instance.Body.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapeClosesUsingTheExistingHistoryRule(bool pushed)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        context.JSInterop.SetupVoid("history.back").SetVoidResult();
        var navigation = Navigation(context);
        if (!pushed)
        {
            navigation.NavigateTo("/?focus=blog");
        }
        var (component, root) = RenderWithRootSnapshot(context, module);
        if (pushed)
        {
            component.FindAll(".hud-layer > button")[2].Click();
        }

        // Dispatch on the heading to cover key events bubbling from the HUD.
        component.Find("h2").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        if (pushed)
        {
            context.JSInterop.VerifyInvoke("history.back", 1);
            Assert.Equal("blog", component.FindComponent<HudPanel>().Instance.Body.Id);
            await component.InvokeAsync(() => navigation.NavigateTo("/"));
        }
        else
        {
            context.JSInterop.VerifyNotInvoke("history.back");
            Assert.Equal("/", navigation.History.First().Uri);
            Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        }

        Assert.Empty(component.FindComponents<HudPanel>());
        VerifyLastFocus(context, root, 2);
    }

    [Theory]
    [InlineData("ArrowRight", false)]
    [InlineData("ArrowLeft", false)]
    [InlineData("ArrowRight", true)]
    [InlineData("ArrowLeft", true)]
    public void ArrowKeysCycleAllBodiesWithReplaceAndPreserveHistoryState(string key, bool pushed)
    {
        using var context = new BunitContext();
        SetupScene(context);
        var navigation = Navigation(context);
        if (!pushed)
        {
            navigation.NavigateTo("/?focus=about", new NavigationOptions { HistoryEntryState = "external-entry" });
        }
        var universe = TestContent.Universe();
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, universe));
        if (pushed)
        {
            component.FindAll(".hud-layer > button")[0].Click();
        }
        var marker = navigation.HistoryEntryState;
        var heading = (IElement)component.Find("h2").Clone();

        for (var step = 1; step <= universe.Bodies.Count; step++)
        {
            component.Find("h2").KeyDown(new KeyboardEventArgs { Key = key });

            var index = key == "ArrowRight" ? step % universe.Bodies.Count : (universe.Bodies.Count - step) % universe.Bodies.Count;
            var expected = universe.Bodies[index];
            Assert.Equal($"/?focus={expected.Id}", navigation.History.First().Uri);
            Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
            Assert.Equal(marker, navigation.HistoryEntryState);
            Assert.Equal(expected.Name, component.Find("h2").TextContent);
            VerifyLastFocus(context, heading, step + 1);
        }
    }

    [Theory]
    [InlineData("ArrowLeft")]
    [InlineData("ArrowRight")]
    [InlineData("Escape")]
    [InlineData("Enter")]
    public void OverviewIgnoresRootKeyboardInput(string key)
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));

        component.Find(".star-system").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Empty(Navigation(context).History);
        module.VerifyNotInvoke("focus");
        module.VerifyNotInvoke("clearFocus");
        context.JSInterop.VerifyNotInvoke("history.back");
    }

    [Fact]
    public async Task DirectFocusAndBodyChangesFocusTheHeadingAndClosingFocusesTheRootOnlyOnce()
    {
        using var context = new BunitContext();
        var module = SetupScene(context);
        var navigation = Navigation(context);
        navigation.NavigateTo("/?focus=blog");
        var (component, root) = RenderWithRootSnapshot(context, module);
        var heading = (IElement)component.Find("h2").Clone();

        Assert.Equal("-1", component.Find("h2").GetAttribute("tabindex"));
        VerifyLastFocus(context, heading, 1);
        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=about"));
        VerifyLastFocus(context, heading, 2);
        await component.InvokeAsync(() => navigation.NavigateTo("/?focus=about#same-body"));
        component.Render();
        VerifyLastFocus(context, heading, 2);

        component.Find("button.hud-back").Click();
        VerifyLastFocus(context, root, 3);
        component.Render();
        VerifyLastFocus(context, root, 3);
        module.VerifyNotInvoke("highlight");
    }

    private static (IRenderedComponent<StarSystem> Component, IElement Root) RenderWithRootSnapshot(BunitContext context, BunitJSModuleInterop module)
    {
        var init = module.Setup<bool>("init", _ => true);
        var component = context.Render<StarSystem>(parameters => parameters.Add(p => p.Universe, TestContent.Universe()));
        // bUnit clears its synthetic element-reference attributes on later renders, so preserve the initial DOM identity.
        var root = (IElement)component.Find(".star-system").Clone();
        init.SetResult(true);
        component.WaitForAssertion(() => Assert.Equal(4, component.FindAll(".hud-layer > button").Count));
        return (component, root);
    }

    private static void VerifyLastFocus(BunitContext context, IElement target, int count)
    {
        context.JSInterop.VerifyFocusAsyncInvoke(count);
        var invocation = context.JSInterop.Invocations.Last(call => call.Identifier == "Blazor._internal.domWrapper.focus");
        invocation.Arguments[0].ShouldBeElementReferenceTo(target);
        Assert.Equal(true, invocation.Arguments[1]);
    }

    private static BunitJSModuleInterop SetupScene(BunitContext context, bool ready = true)
    {
        var module = context.JSInterop.SetupModule("./js/scene.js");
        module.Setup<bool>("init", _ => true).SetResult(ready);
        module.SetupVoid("focus", _ => true).SetVoidResult();
        module.SetupVoid("clearFocus").SetVoidResult();
        module.SetupVoid("highlight", _ => true).SetVoidResult();
        module.SetupVoid("dispose").SetVoidResult();
        return module;
    }

    private static BunitNavigationManager Navigation(BunitContext context) => Assert.IsType<BunitNavigationManager>(context.Services.GetRequiredService<NavigationManager>());

    private static DotNetObjectReference<SceneInterop> CallbackReference(BunitJSModuleInterop module) =>
        Assert.IsType<DotNetObjectReference<SceneInterop>>(module.VerifyInvoke("init", 1).Single().Arguments[2]);

    private static Task ClickBodyAsync(IRenderedComponent<StarSystem> component, BunitJSModuleInterop module, string? id) =>
        component.InvokeAsync(() => CallbackReference(module).Value.OnBodyClicked(id));

    private sealed class PendingImportRuntime : IJSRuntime
    {
        public TaskCompletionSource<IJSObjectReference> Import { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            Assert.Equal("./js/scene.js", Assert.Single(args!));
            return (TValue)(object)await Import.Task;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class TrackingModule : IJSObjectReference
    {
        public int InitCalls { get; private set; }
        public int SceneDisposeCalls { get; private set; }
        public int ReferenceDisposeCalls { get; private set; }
        public Exception? CleanupException { get; init; }
        public DotNetObjectReference<SceneInterop>? Callbacks { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "init")
            {
                InitCalls++;
                Callbacks = (DotNetObjectReference<SceneInterop>)args![2]!;
                return ValueTask.FromResult((TValue)(object)true);
            }

            Assert.Equal("dispose", identifier);
            SceneDisposeCalls++;
            if (CleanupException is not null)
            {
                throw CleanupException;
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            ReferenceDisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
