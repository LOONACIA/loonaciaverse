namespace Loonaciaverse.Core.Content;

public sealed record PostSummary(string Slug, string Title, DateOnly Date, DateOnly? Updated, string Summary, IReadOnlyList<string> Tags, bool Draft, string? Cover);

public sealed record PostDetail(PostSummary Meta, string Html);
