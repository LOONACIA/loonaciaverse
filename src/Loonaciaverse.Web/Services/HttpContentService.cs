using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Services;

public sealed partial class HttpContentService(HttpClient httpClient) : IContentService
{
    public Task<Universe> GetUniverseAsync(CancellationToken cancellationToken = default) => GetRequiredAsync<Universe>("content/universe.json", cancellationToken);

    public Task<IReadOnlyList<PostSummary>> GetPostsAsync(CancellationToken cancellationToken = default) => GetRequiredAsync<IReadOnlyList<PostSummary>>("content/posts.json", cancellationToken);

    public Task<PostDetail?> GetPostAsync(string slug, CancellationToken cancellationToken = default) => GetDetailAsync<PostDetail>("posts", slug, cancellationToken);

    public Task<SeriesDetail?> GetSeriesAsync(string slug, CancellationToken cancellationToken = default) => GetDetailAsync<SeriesDetail>("series", slug, cancellationToken);

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        GetRequiredAsync<IReadOnlyList<ProjectSummary>>("content/projects.json", cancellationToken);

    public Task<ProjectDetail?> GetProjectAsync(string slug, CancellationToken cancellationToken = default) => GetDetailAsync<ProjectDetail>("projects", slug, cancellationToken);

    public Task<AboutProfile> GetAboutAsync(CancellationToken cancellationToken = default) => GetRequiredAsync<AboutProfile>("content/about.json", cancellationToken);

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        return await httpClient.GetFromJsonAsync<T>(path, ContentJson.Options, cancellationToken) ?? throw new JsonException($"Content at '{path}' must not be null.");
    }

    private async Task<T?> GetDetailAsync<T>(string directory, string slug, CancellationToken cancellationToken) where T : class
    {
        if (string.IsNullOrEmpty(slug) || !SlugPattern().IsMatch(slug))
        {
            return null;
        }

        using var response = await httpClient.GetAsync($"content/{directory}/{slug}.json", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ContentJson.Options, cancellationToken);
    }

    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex SlugPattern();
}
