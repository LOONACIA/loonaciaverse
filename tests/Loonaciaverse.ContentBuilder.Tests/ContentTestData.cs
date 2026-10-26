namespace Loonaciaverse.ContentBuilder.Tests;

internal static class ContentTestData
{
    public const string AboutMetadata = "name: LOONACIA\ntitle: Game & .NET Developer\nemail: you@example.com";

    public static void WriteAbout(string contentDir, string metadata = AboutMetadata, string body = "소개입니다.")
    {
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "about.md"), $"---\n{metadata}\n---\n{body}");
    }

    public static void WriteProject(string contentDir, string folder, string metadata, string body = "본문입니다.")
    {
        var projectDir = Directory.CreateDirectory(Path.Combine(contentDir, "projects", folder)).FullName;
        File.WriteAllText(Path.Combine(projectDir, "index.md"), $"---\n{metadata}\n---\n{body}");
    }
}
