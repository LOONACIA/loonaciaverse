namespace Loonaciaverse.Core.Content;

public interface IContentService
{
    Task<Universe> GetUniverseAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostSummary>> GetPostsAsync(CancellationToken cancellationToken = default);
    Task<PostDetail?> GetPostAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<ProjectDetail?> GetProjectAsync(string slug, CancellationToken cancellationToken = default);
    Task<AboutProfile> GetAboutAsync(CancellationToken cancellationToken = default);
}
