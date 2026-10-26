namespace Loonaciaverse.Core.Content;

public sealed record ProjectSummary(
    string Slug, string Title, string Summary, string Period, string Role, IReadOnlyList<string> Stack,
    IReadOnlyDictionary<string, string> Links, string? Thumbnail, bool Featured, int Order, bool Draft);

public sealed record ProjectDetail(ProjectSummary Meta, string Html);
