namespace Loonaciaverse.Core.Content;

public sealed record SiteInfo(string Title, string BaseUrl, string Author, string Description);

public sealed record BodyVisual(string BaseColor, string AtmosphereColor, double NoiseScale, double GlowIntensity);

public sealed record BodyOrbit(double Radius, double PeriodSeconds, double Phase);

public sealed record CelestialBody(string Id, string Name, string Subtitle, string Tagline, string? Route, bool Locked, BodyVisual Visual, BodyOrbit? Orbit);

public sealed record BlogStats(int PostCount, DateOnly? LastPublished, IReadOnlyList<string> TopTags, PostSummary? Latest);

public sealed record ProjectStats(int ProjectCount, IReadOnlyList<string> TopStack, ProjectSummary? Featured);

public sealed record AboutStats(string Title, IReadOnlyList<string> Stack, string Email, string? Github, string? Linkedin);

public sealed record HudStats(BlogStats Blog, ProjectStats Projects, AboutStats About);

public sealed record Universe(SiteInfo Site, IReadOnlyList<CelestialBody> Bodies, HudStats Stats);
