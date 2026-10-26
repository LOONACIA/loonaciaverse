using Bunit;
using Loonaciaverse.Web.Components;
using Microsoft.JSInterop;

namespace Loonaciaverse.Web.Tests;

public class ContentBodyTests
{
    [Fact]
    public void FirstRenderHighlightsOnlyTheBodyElement()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/highlight.js");
        module.SetupVoid("highlightAll", _ => true).SetVoidResult();
        const string html = "<pre><code class=\"language-csharp\">public class Example { }</code></pre>";

        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, html));

        component.MarkupMatches($"<div class=\"prose\">{html}</div>");
        context.JSInterop.VerifyInvoke("import", 1);
        var invocation = Assert.Single(module.VerifyInvoke("highlightAll", 1));
        Assert.Single(invocation.Arguments).ShouldBeElementReferenceTo(component.Find(".prose"));
    }

    [Fact]
    public void UnchangedHtmlDoesNotHighlightAgain()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/highlight.js");
        module.SetupVoid("highlightAll", _ => true).SetVoidResult();
        const string html = "<p>Unchanged</p>";
        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, html));

        component.Render(parameters => parameters.Add(p => p.Html, html));

        module.VerifyInvoke("highlightAll", 1);
        context.JSInterop.VerifyInvoke("import", 1);
    }

    [Fact]
    public void ChangedHtmlHighlightsAgainUsingTheCachedModule()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/highlight.js");
        module.SetupVoid("highlightAll", _ => true).SetVoidResult();
        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, "<p>Original</p>"));

        component.Render(parameters => parameters.Add(p => p.Html, "<p>Updated</p>"));

        component.Find(".prose").MarkupMatches("<div class=\"prose\"><p>Updated</p></div>");
        module.VerifyInvoke("highlightAll", 2);
        context.JSInterop.VerifyInvoke("import", 1);
    }

    [Fact]
    public async Task ImportFailureLeavesContentReadableWithoutUnhandledExceptions()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupModule("./js/highlight.js");
        context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/highlight.js")!.SetException(new JSException("Module unavailable"));

        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, "<p>Readable</p>"));
        component.Render(parameters => parameters.Add(p => p.Html, "<p>Still readable</p>"));

        component.Find(".prose").MarkupMatches("<div class=\"prose\"><p>Still readable</p></div>");
        await context.DisposeComponentsAsync();
        context.JSInterop.VerifyInvoke("import", 1);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public void HighlightFailureLeavesContentReadableWithoutUnhandledExceptions()
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/highlight.js");
        module.SetupVoid("highlightAll", _ => true).SetException(new JSException("Highlight failed"));

        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, "<p>Readable</p>"));

        component.Find(".prose").MarkupMatches("<div class=\"prose\"><p>Readable</p></div>");
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task DisposalReleasesTheImportedModuleOnce()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupModule("./js/highlight.js");
        var module = new TrackingModule();
        context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/highlight.js")!.SetResult(module);
        var component = context.Render<ContentBody>(parameters => parameters.Add(p => p.Html, "<p>Content</p>"));
        var body = component.Instance;

        await context.DisposeComponentsAsync();
        await body.DisposeAsync();

        Assert.Equal(1, module.DisposeCalls);
    }

    private sealed class TrackingModule : IJSObjectReference
    {
        public int DisposeCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }
    }
}
