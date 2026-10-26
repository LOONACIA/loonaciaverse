using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Loonaciaverse.Web.Tests;

public class ContentLoaderTests
{
    [Fact]
    public async Task PendingLoadShowsBusyThenRendersChildContent()
    {
        using var context = new BunitContext();
        var completion = new TaskCompletionSource<AboutProfile?>();
        var profile = TestContent.About();
        var component = RenderLoader(context, _ => completion.Task);

        Assert.Single(component.FindAll("[aria-busy='true']"));
        Assert.Empty(component.FindAll("[role='alert'], h1, .loaded-content"));

        await component.InvokeAsync(() => completion.SetResult(profile));

        component.WaitForAssertion(() => Assert.Equal(profile.Name, component.Find(".loaded-content").TextContent));
        Assert.Empty(component.FindAll("[aria-busy]"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoadFailureShowsSignalLostAndRetry(bool synchronous)
    {
        using var context = new BunitContext();
        var component = RenderLoader(context, _ => synchronous ? throw new HttpRequestException("Offline") : Task.FromException<AboutProfile?>(new HttpRequestException("Offline")));

        AssertFailure(component);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CancellationBeforeDisposalShowsSignalLostAndRetry(bool synchronous)
    {
        using var context = new BunitContext();
        var component = RenderLoader(context, _ => synchronous ? throw new TaskCanceledException("Timeout") : Task.FromCanceled<AboutProfile?>(new CancellationToken(true)));

        AssertFailure(component);
    }

    [Fact]
    public async Task RetryShowsBusyThenRendersSuccessfulContent()
    {
        using var context = new BunitContext();
        var completion = new TaskCompletionSource<AboutProfile?>();
        var calls = 0;
        var profile = TestContent.About();
        var component = RenderLoader(context, _ => ++calls == 1 ? Task.FromException<AboutProfile?>(new HttpRequestException("Offline")) : completion.Task);
        AssertFailure(component);

        var retry = component.Find("button").ClickAsync(new MouseEventArgs());

        component.WaitForAssertion(() => Assert.Single(component.FindAll("[aria-busy='true']")));
        Assert.Empty(component.FindAll("[role='alert'], .loaded-content"));
        await component.InvokeAsync(() => completion.SetResult(profile));
        await retry;

        Assert.Equal(profile.Name, component.Find(".loaded-content").TextContent);
        Assert.Empty(component.FindAll("[role='alert'], [aria-busy]"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RetryFailureShowsSignalLostAgain()
    {
        using var context = new BunitContext();
        var calls = 0;
        var component = RenderLoader(context, _ =>
        {
            calls++;
            return Task.FromException<AboutProfile?>(new HttpRequestException("Offline"));
        });

        await component.Find("button").ClickAsync(new MouseEventArgs());

        AssertFailure(component);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void MissingContentShowsLostInSpace()
    {
        using var context = new BunitContext();
        var component = RenderLoader(context, _ => Task.FromResult<AboutProfile?>(null));

        Assert.Equal("LOST IN SPACE", component.Find("h1").TextContent);
        Assert.Empty(component.FindAll("[aria-busy], [role='alert'], .loaded-content"));
    }

    [Fact]
    public void MissingContentRendersCustomNotFound()
    {
        using var context = new BunitContext();
        var component = context.Render<ContentLoader<AboutProfile>>(parameters => parameters
            .Add(p => p.Load, _ => Task.FromResult<AboutProfile?>(null))
            .Add(p => p.ChildContent, Content())
            .Add(p => p.NotFound, builder => builder.AddMarkupContent(0, "<p>Custom missing content</p>")));

        component.MarkupMatches("<p>Custom missing content</p>");
    }

    [Fact]
    public async Task DisposalCancelsLoadToken()
    {
        using var context = new BunitContext();
        CancellationToken token = default;
        RenderLoader(context, cancellationToken =>
        {
            token = cancellationToken;
            return Task.FromResult<AboutProfile?>(TestContent.About());
        });
        Assert.True(token.CanBeCanceled);
        Assert.False(token.IsCancellationRequested);

        await context.DisposeComponentsAsync();

        Assert.True(token.IsCancellationRequested);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task CompletionAfterDisposalDoesNotUpdateContentOrShowFailure(string outcome)
    {
        using var context = new BunitContext();
        var completion = new TaskCompletionSource<AboutProfile?>();
        var component = RenderLoader(context, _ => completion.Task);
        var markup = component.Markup;
        await component.InvokeAsync(component.Instance.Dispose);

        await component.InvokeAsync(() =>
        {
            switch (outcome)
            {
                case "success":
                    completion.SetResult(TestContent.About());
                    break;
                case "failure":
                    completion.SetException(new HttpRequestException("Offline"));
                    break;
                case "cancellation":
                    completion.SetCanceled();
                    break;
            }
        });

        Assert.Equal(markup, component.Markup);
        Assert.Single(component.FindAll("[aria-busy='true']"));
        Assert.Empty(component.FindAll(".loaded-content, [role='alert']"));
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public void ParameterChangesDoNotReloadContent()
    {
        using var context = new BunitContext();
        var calls = 0;
        var profile = TestContent.About();
        Func<CancellationToken, Task<AboutProfile?>> load = _ =>
        {
            calls++;
            return Task.FromResult<AboutProfile?>(profile);
        };
        RenderFragment<AboutProfile> content = item => builder => builder.AddContent(0, item.Name);
        var component = context.Render<ContentLoader<AboutProfile>>(parameters => parameters.Add(p => p.Load, load).Add(p => p.ChildContent, content));

        component.Render(parameters => parameters.Add(p => p.Load, _ =>
        {
            calls++;
            return Task.FromResult<AboutProfile?>(profile with { Name = "Replacement" });
        }));

        Assert.Equal(1, calls);
        Assert.Equal(profile.Name, component.Markup);
    }

    private static IRenderedComponent<ContentLoader<AboutProfile>> RenderLoader(BunitContext context, Func<CancellationToken, Task<AboutProfile?>> load)
    {
        return context.Render<ContentLoader<AboutProfile>>(parameters => parameters.Add(p => p.Load, load).Add(p => p.ChildContent, Content()));
    }

    private static RenderFragment<AboutProfile> Content() => item => builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "loaded-content");
        builder.AddContent(2, item.Name);
        builder.CloseElement();
    };

    private static void AssertFailure(IRenderedComponent<ContentLoader<AboutProfile>> component)
    {
        var alert = component.Find("[role='alert']");
        Assert.Equal("SIGNAL LOST", alert.QuerySelector("h2.hud-label")?.TextContent);
        Assert.Equal("콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.", alert.QuerySelector("p")?.TextContent);
        Assert.Equal("RETRY", component.Find("button[type='button']").TextContent);
        Assert.Empty(component.FindAll("[aria-busy], .loaded-content, .lost-in-space"));
    }
}
