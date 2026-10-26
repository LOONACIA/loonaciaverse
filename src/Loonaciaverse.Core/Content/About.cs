namespace Loonaciaverse.Core.Content;

public sealed record AboutProfile(string Name, string Title, IReadOnlyList<string> Stack, string Email, string? Github, string? Linkedin, string Html);
