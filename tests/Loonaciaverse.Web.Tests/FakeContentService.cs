using Loonaciaverse.Core.Content;

namespace Loonaciaverse.Web.Tests;

public sealed class FakeContentService : IContentService
{
    public Func<CancellationToken, Task<Universe>>? GetUniverseAsyncHandler { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<PostSummary>>>? GetPostsAsyncHandler { get; set; }
    public Func<string, CancellationToken, Task<PostDetail?>>? GetPostAsyncHandler { get; set; }
    public Func<string, CancellationToken, Task<SeriesDetail?>>? GetSeriesAsyncHandler { get; set; }
    public Func<CancellationToken, Task<IReadOnlyList<ProjectSummary>>>? GetProjectsAsyncHandler { get; set; }
    public Func<string, CancellationToken, Task<ProjectDetail?>>? GetProjectAsyncHandler { get; set; }
    public Func<CancellationToken, Task<AboutProfile>>? GetAboutAsyncHandler { get; set; }

    public List<CancellationToken> UniverseCalls { get; } = [];
    public List<CancellationToken> PostsCalls { get; } = [];
    public List<(string Slug, CancellationToken Token)> PostCalls { get; } = [];
    public List<(string Slug, CancellationToken Token)> SeriesCalls { get; } = [];
    public List<CancellationToken> ProjectsCalls { get; } = [];
    public List<(string Slug, CancellationToken Token)> ProjectCalls { get; } = [];
    public List<CancellationToken> AboutCalls { get; } = [];

    public Task<Universe> GetUniverseAsync(CancellationToken cancellationToken = default)
    {
        UniverseCalls.Add(cancellationToken);
        return (GetUniverseAsyncHandler ?? throw new InvalidOperationException("GetUniverseAsync is not configured."))(cancellationToken);
    }

    public Task<IReadOnlyList<PostSummary>> GetPostsAsync(CancellationToken cancellationToken = default)
    {
        PostsCalls.Add(cancellationToken);
        return (GetPostsAsyncHandler ?? throw new InvalidOperationException("GetPostsAsync is not configured."))(cancellationToken);
    }

    public Task<PostDetail?> GetPostAsync(string slug, CancellationToken cancellationToken = default)
    {
        PostCalls.Add((slug, cancellationToken));
        return (GetPostAsyncHandler ?? throw new InvalidOperationException("GetPostAsync is not configured."))(slug, cancellationToken);
    }

    public Task<SeriesDetail?> GetSeriesAsync(string slug, CancellationToken cancellationToken = default)
    {
        SeriesCalls.Add((slug, cancellationToken));
        return (GetSeriesAsyncHandler ?? throw new InvalidOperationException("GetSeriesAsync is not configured."))(slug, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        ProjectsCalls.Add(cancellationToken);
        return (GetProjectsAsyncHandler ?? throw new InvalidOperationException("GetProjectsAsync is not configured."))(cancellationToken);
    }

    public Task<ProjectDetail?> GetProjectAsync(string slug, CancellationToken cancellationToken = default)
    {
        ProjectCalls.Add((slug, cancellationToken));
        return (GetProjectAsyncHandler ?? throw new InvalidOperationException("GetProjectAsync is not configured."))(slug, cancellationToken);
    }

    public Task<AboutProfile> GetAboutAsync(CancellationToken cancellationToken = default)
    {
        AboutCalls.Add(cancellationToken);
        return (GetAboutAsyncHandler ?? throw new InvalidOperationException("GetAboutAsync is not configured."))(cancellationToken);
    }
}
