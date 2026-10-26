using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Loonaciaverse.Web.Tests;

public class TableOfContentsTests
{
    private static readonly IReadOnlyList<TocEntry> Entries =
    [
        new(2, "나만의-우주를-시작하며", "나만의 우주를 시작하며"),
        new(3, "세부-내용", "<strong>세부 내용</strong>"),
        new(2, "마무리", "마무리")
    ];

    [Fact]
    public void RendersBothVariantsWithOrderedEscapedLinksAndLevelThreeIndentation()
    {
        using var context = new BunitContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/blog/first-post?view=full#old");

        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Entries));

        Assert.Equal("CONTENTS", component.Find("details.toc.toc-mobile summary").TextContent);
        Assert.False(component.Find("details").HasAttribute("open"));
        Assert.Equal("CONTENTS", component.Find("nav.toc.toc-desktop").GetAttribute("aria-label"));
        Assert.Equal("CONTENTS", component.Find("nav .toc-label").TextContent);
        foreach (var variant in component.FindAll(".toc"))
        {
            var links = variant.QuerySelectorAll("a");
            Assert.Equal(Entries.Select(entry => entry.Text), links.Select(link => link.TextContent));
            Assert.Equal(Entries.Select(entry => $"/blog/first-post#{Uri.EscapeDataString(entry.Id)}"), links.Select(link => link.GetAttribute("href")));
            Assert.Equal(Entries.Select(entry => entry.Level == 3), variant.QuerySelectorAll("li").Select(item => item.ClassList.Contains("toc-level-3")));
            Assert.Empty(variant.QuerySelectorAll("strong"));
        }
        Assert.Empty(context.JSInterop.Invocations);
    }

    [Fact]
    public void EmptyEntriesRenderNothing()
    {
        using var context = new BunitContext();

        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Array.Empty<TocEntry>()));

        component.MarkupMatches(string.Empty);
        Assert.Empty(context.JSInterop.Invocations);
    }

    [Fact]
    public void ClickingEitherVariantScrollsWithOriginalIdUsingOneImportAndPreservesNavigation()
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blog/first-post?view=full#old");
        var originalUri = navigation.Uri;
        var module = context.JSInterop.SetupModule("./js/toc.js");
        module.SetupVoid("scrollToHeading", _ => true).SetVoidResult();
        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Entries));

        component.Find(".toc-mobile a").Click();
        component.FindAll(".toc-desktop a")[1].Click();

        context.JSInterop.VerifyInvoke("import", 1);
        var calls = module.VerifyInvoke("scrollToHeading", 2);
        Assert.Equal(Entries.Take(2).Select(entry => entry.Id), calls.Select(call => Assert.IsType<string>(Assert.Single(call.Arguments))));
        Assert.All(component.FindAll("a"), link => Assert.True(link.HasAttribute("blazor:onclick:preventDefault")));
        Assert.Equal(originalUri, navigation.Uri);
    }

    [Fact]
    public void RerenderUsesTheCurrentPathAndEntries()
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/blog/first-post");
        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Entries));
        navigation.NavigateTo("/blog/second-post?query=1#old");
        IReadOnlyList<TocEntry> entries = [new(2, "새-항목", "새 항목")];

        component.Render(parameters => parameters.Add(p => p.Entries, entries));

        Assert.All(component.FindAll("a"), link => Assert.Equal($"/blog/second-post#{Uri.EscapeDataString(entries[0].Id)}", link.GetAttribute("href")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JavaScriptFailureLeavesContentsReadableWithoutUnhandledExceptions(bool importFails)
    {
        using var context = new BunitContext();
        var module = context.JSInterop.SetupModule("./js/toc.js");
        if (importFails)
        {
            context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/toc.js")!.SetException(new JSException("Module unavailable"));
        }
        else
        {
            module.SetupVoid("scrollToHeading", _ => true).SetException(new JSException("Scroll failed"));
        }
        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Entries));

        component.Find("a").Click();
        component.Find("a").Click();

        Assert.Equal(Entries[0].Text, component.Find("a").TextContent);
        await context.DisposeComponentsAsync();
        context.JSInterop.VerifyInvoke("import", 1);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task DisposalReleasesTheCachedModuleOnce()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupModule("./js/toc.js");
        var module = new TrackingModule();
        context.JSInterop.TryGetInvokeHandler<IJSObjectReference>("import", "./js/toc.js")!.SetResult(module);
        var component = context.Render<TableOfContents>(parameters => parameters.Add(p => p.Entries, Entries));
        component.Find("a").Click();
        var contents = component.Instance;

        await context.DisposeComponentsAsync();
        await contents.DisposeAsync();

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
