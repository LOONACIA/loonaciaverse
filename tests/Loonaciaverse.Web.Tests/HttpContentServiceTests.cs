using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Loonaciaverse.Web.Tests;

public class HttpContentServiceTests
{
    private static readonly Uri BaseAddress = new("https://example.com/site/");
    private static readonly PostSummary Post = new("hello-2026", "Hello", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), "A post", ["dotnet"], false, "/cover.svg");
    private static readonly ProjectSummary Project = new(
        "hello-2026", "Site", "A project", "2026", "Developer", ["C#"], new Dictionary<string, string> { ["github"] = "https://github.com/example/site" }, null, true, 1, false);
    private static readonly AboutProfile About = new("Loona", "Developer", ["C#"], "loona@example.com", "https://github.com/example", null, "<p>About me</p>");

    public static TheoryData<string, string, object> ContentCases => new()
    {
        {
            "universe", "content/universe.json",
            new Universe(
                new SiteInfo("Loonaciaverse", "https://example.com/site", "Loona", "My universe"),
                [new CelestialBody("blog", "Blog", "Posts", "/blog", false, new BodyVisual("#123456", "#abcdef", 1.5, 0.8, "gas"), new BodyOrbit(10, 60, 0.5))],
                new HudStats(new BlogStats(1, Post.Date, ["dotnet"], Post), new ProjectStats(1, ["C#"], Project), new AboutStats(About.Title, About.Stack, About.Email, About.Github, About.Linkedin)))
        },
        { "posts", "content/posts.json", new[] { Post } },
        { "post", "content/posts/hello-2026.json", new PostDetail(Post, "<h2 id=\"intro\">Intro</h2>", [new TocEntry(2, "intro", "Intro")]) },
        {
            "post", "content/posts/hello-2026.json",
            new PostDetail(Post, "<p>Series post</p>", [], new PostSeries("first-series", "첫 번째 시리즈", [new SeriesPart(Post.Slug, Post.Title, Post.Draft), new SeriesPart("draft-post", "초안 편", true)]))
        },
        { "series", "content/series/hello-2026.json", new SeriesDetail("hello-2026", "첫 번째 시리즈", "시리즈 설명입니다.", [Post]) },
        { "projects", "content/projects.json", new[] { Project } },
        { "project", "content/projects/hello-2026.json", new ProjectDetail(Project, "<p>Project details</p>") },
        { "about", "content/about.json", About }
    };

    [Theory]
    [MemberData(nameof(ContentCases))]
    public async Task MethodsRequestRelativePathsAndDeserializeContent(string method, string path, object expected)
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected, expected.GetType(), options: ContentJson.Options)
        }));
        using var client = CreateClient(handler);
        var service = new HttpContentService(client);

        var actual = await InvokeAsync(service, method);

        Assert.Equivalent(expected, actual);
        Assert.Equal(new Uri(BaseAddress, path), Assert.Single(handler.Requests));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("../about")]
    [InlineData("hello/world")]
    [InlineData("hello\\world")]
    [InlineData("%2e%2e")]
    [InlineData("Hello")]
    [InlineData("hello_world")]
    [InlineData("-hello")]
    [InlineData("hello-")]
    [InlineData("hello--world")]
    [InlineData("hello?draft=true")]
    [InlineData("hello#fragment")]
    [InlineData("hello\n")]
    [InlineData("hello\r\n")]
    [InlineData(" hello")]
    [InlineData("안녕")]
    public async Task InvalidSlugsReturnNullWithoutRequests(string? slug)
    {
        using var handler = StatusHandler(HttpStatusCode.InternalServerError);
        using var client = CreateClient(handler);
        var service = new HttpContentService(client);

        Assert.Null(await service.GetPostAsync(slug!));
        Assert.Null(await service.GetSeriesAsync(slug!));
        Assert.Null(await service.GetProjectAsync(slug!));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("post", "a")]
    [InlineData("project", "0")]
    [InlineData("series", "a")]
    [InlineData("post", "hello-2026")]
    [InlineData("project", "hello-2026")]
    [InlineData("series", "hello-2026")]
    public async Task DetailNotFoundReturnsNull(string method, string slug)
    {
        using var handler = StatusHandler(HttpStatusCode.NotFound);
        using var client = CreateClient(handler);
        var service = new HttpContentService(client);

        var result = method switch
        {
            "post" => (object?)await service.GetPostAsync(slug),
            "series" => await service.GetSeriesAsync(slug),
            "project" => await service.GetProjectAsync(slug),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        Assert.Null(result);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("universe")]
    [InlineData("posts")]
    [InlineData("post")]
    [InlineData("series")]
    [InlineData("projects")]
    [InlineData("project")]
    [InlineData("about")]
    public async Task ServerErrorsPropagate(string method)
    {
        using var handler = StatusHandler(HttpStatusCode.InternalServerError);
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => InvokeAsync(new HttpContentService(client), method));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Theory]
    [InlineData("universe")]
    [InlineData("posts")]
    [InlineData("projects")]
    [InlineData("about")]
    public async Task RequiredContentNotFoundThrows(string method)
    {
        using var handler = StatusHandler(HttpStatusCode.NotFound);
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => InvokeAsync(new HttpContentService(client), method));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Theory]
    [InlineData("universe")]
    [InlineData("posts")]
    [InlineData("post")]
    [InlineData("series")]
    [InlineData("projects")]
    [InlineData("project")]
    [InlineData("about")]
    public async Task CancellationReachesPendingRequests(string method)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new FakeHandler(async (_, token) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InvokeAsync(new HttpContentService(client), method, cancellation.Token));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [MemberData(nameof(ContentCases))]
    public async Task RepeatedCallsRequestContentAgain(string method, string path, object expected)
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected, expected.GetType(), options: ContentJson.Options)
        }));
        using var client = CreateClient(handler);
        var service = new HttpContentService(client);

        await InvokeAsync(service, method);
        await InvokeAsync(service, method);

        Assert.Equal(new[] { new Uri(BaseAddress, path), new Uri(BaseAddress, path) }, handler.Requests);
    }

    [Theory]
    [InlineData("universe")]
    [InlineData("posts")]
    [InlineData("projects")]
    [InlineData("about")]
    public async Task RequiredContentRejectsJsonNull(string method)
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") }));
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<JsonException>(() => InvokeAsync(new HttpContentService(client), method));
    }

    [Fact]
    public void ConfigureServicesRegistersScopedContentService()
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services, BaseAddress.AbsoluteUri);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var service = firstScope.ServiceProvider.GetRequiredService<IContentService>();

        Assert.IsType<HttpContentService>(service);
        Assert.Same(service, firstScope.ServiceProvider.GetRequiredService<IContentService>());
        Assert.NotSame(service, secondScope.ServiceProvider.GetRequiredService<IContentService>());
        Assert.Equal(BaseAddress, firstScope.ServiceProvider.GetRequiredService<HttpClient>().BaseAddress);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler) { BaseAddress = BaseAddress };

    private static FakeHandler StatusHandler(HttpStatusCode status) => new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

    private static async Task<object?> InvokeAsync(IContentService service, string method, CancellationToken cancellationToken = default) => method switch
    {
        "universe" => await service.GetUniverseAsync(cancellationToken),
        "posts" => await service.GetPostsAsync(cancellationToken),
        "post" => await service.GetPostAsync("hello-2026", cancellationToken),
        "series" => await service.GetSeriesAsync("hello-2026", cancellationToken),
        "projects" => await service.GetProjectsAsync(cancellationToken),
        "project" => await service.GetProjectAsync("hello-2026", cancellationToken),
        "about" => await service.GetAboutAsync(cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Requests.Add(request.RequestUri!);
            return respond(request, cancellationToken);
        }
    }
}
