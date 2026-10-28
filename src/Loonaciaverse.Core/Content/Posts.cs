namespace Loonaciaverse.Core.Content;

public sealed record PostSummary(string Slug, string Title, DateOnly Date, DateOnly? Updated, string Summary, IReadOnlyList<string> Tags, bool Draft, string? Cover);

public sealed record TocEntry(int Level, string Id, string Text);

public sealed record SeriesPart(string Slug, string Title, bool Draft);

public sealed record PostSeries(string Slug, string Title, IReadOnlyList<SeriesPart> Parts);

public sealed record SeriesDetail(string Slug, string Title, string Summary, IReadOnlyList<PostSummary> Posts);

public sealed record PostDetail(PostSummary Meta, string Html, IReadOnlyList<TocEntry> Toc, PostSeries? Series = null);
