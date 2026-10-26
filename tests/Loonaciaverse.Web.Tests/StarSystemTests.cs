using Bunit;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Services;
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
