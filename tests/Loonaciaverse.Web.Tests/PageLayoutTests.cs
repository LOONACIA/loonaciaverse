using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Loonaciaverse.Web.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class PageLayoutTests
{
    [Fact]
    public void LoadsUniverseOnceAndCascadesNamesToPageHeader()
    {
        using var context = new BunitContext();
        var universe = TestContent.Universe();
        universe = universe with { Bodies = universe.Bodies.Select(body => body.Id == "about" ? body with { Name = "RENAMED STAR" } : body).ToArray() };
        var service = new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromResult(universe) };
        context.Services.AddSingleton<IContentService>(service);

        var component = context.Render<PageLayout>(parameters => parameters.Add(p => p.Body, Header()));
        component.Render(parameters => parameters.Add(p => p.Body, Header()));

        Assert.Equal("RENAMED STAR", component.Find("main .hud-label").TextContent);
        Assert.Equal("Profile", component.Find("main h1").TextContent);
        Assert.Equal("Metadata", component.Find("main .metadata").TextContent);
        Assert.Single(service.UniverseCalls);
    }

    [Fact]
    public void UniverseLoadFailureKeepsBodyWithoutLabel()
    {
        using var context = new BunitContext();
        var service = new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromException<Universe>(new HttpRequestException("Offline")) };
        context.Services.AddSingleton<IContentService>(service);

        var component = context.Render<PageLayout>(parameters => parameters.Add(p => p.Body, Header()));

        Assert.Equal("Profile", component.Find("main h1").TextContent);
        Assert.Empty(component.FindAll("main .hud-label"));
        Assert.Single(service.UniverseCalls);
    }

    [Fact]
    public async Task PendingUniverseDoesNotBlockBodyAndUpdatesLabelWhenLoaded()
    {
        using var context = new BunitContext();
        var completion = new TaskCompletionSource<Universe>();
        context.Services.AddSingleton<IContentService>(new FakeContentService { GetUniverseAsyncHandler = _ => completion.Task });
        var component = context.Render<PageLayout>(parameters => parameters.Add(p => p.Body, Header()));

        Assert.Equal("Profile", component.Find("main h1").TextContent);
        Assert.Empty(component.FindAll("main .hud-label"));

        await component.InvokeAsync(() => completion.SetResult(TestContent.Universe()));

        component.WaitForAssertion(() => Assert.Equal("LOONACIA", component.Find("main .hud-label").TextContent));
    }

    [Fact]
    public async Task DisposalCancelsUniverseToken()
    {
        using var context = new BunitContext();
        var service = new FakeContentService { GetUniverseAsyncHandler = _ => Task.FromResult(TestContent.Universe()) };
        context.Services.AddSingleton<IContentService>(service);
        context.Render<PageLayout>(parameters => parameters.Add(p => p.Body, Header()));
        var token = Assert.Single(service.UniverseCalls);
        Assert.False(token.IsCancellationRequested);

        await context.DisposeComponentsAsync();

        Assert.True(token.IsCancellationRequested);
    }

    private static RenderFragment Header() => builder =>
    {
        builder.OpenComponent<PageHeader>(0);
        builder.AddAttribute(1, nameof(PageHeader.BodyId), "about");
        builder.AddAttribute(2, nameof(PageHeader.Title), "Profile");
        builder.AddAttribute(3, nameof(PageHeader.ChildContent), (RenderFragment)(content => content.AddMarkupContent(0, "<span class=\"metadata\">Metadata</span>")));
        builder.CloseComponent();
    };
}
