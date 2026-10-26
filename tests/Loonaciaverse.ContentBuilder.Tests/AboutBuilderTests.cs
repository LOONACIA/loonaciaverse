using System.Text.Json;
using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class AboutBuilderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapsProfileAndRendersMarkdown(bool includeOptionalFields)
    {
        using var directory = new TemporaryDirectory();
        var metadata = ContentTestData.AboutMetadata;
        if (includeOptionalFields)
        {
            metadata += "\nstack: [csharp, dotnet, unity]\ngithub: https://github.com/\nlinkedin: https://www.linkedin.com/";
        }

        ContentTestData.WriteAbout(directory.Root, metadata, "**소개**와 ~~이전~~ 이야기입니다.");
        var outputDir = Path.Combine(directory.Root, "output");

        Assert.True(AboutBuilder.Build(directory.Root, outputDir).Success);
        var profile = JsonSerializer.Deserialize<AboutProfile>(File.ReadAllText(Path.Combine(outputDir, "about.json")), ContentJson.Options)!;
        Assert.Equal("LOONACIA", profile.Name);
        Assert.Equal("Game & .NET Developer", profile.Title);
        Assert.Equal("you@example.com", profile.Email);
        Assert.Equal(includeOptionalFields ? new[] { "csharp", "dotnet", "unity" } : [], profile.Stack);
        Assert.Equal(includeOptionalFields ? "https://github.com/" : null, profile.Github);
        Assert.Equal(includeOptionalFields ? "https://www.linkedin.com/" : null, profile.Linkedin);
        Assert.Equal("<p><strong>소개</strong>와 <del>이전</del> 이야기입니다.</p>\n", profile.Html);
    }

    [Theory]
    [InlineData("name", "LOONACIA")]
    [InlineData("title", "Game & .NET Developer")]
    [InlineData("email", "you@example.com")]
    public void RejectsMissingEmptyNullAndWhitespaceRequiredFields(string field, string value)
    {
        using var directory = new TemporaryDirectory();
        var outputDir = Path.Combine(directory.Root, "output");
        foreach (var replacement in new[] { "", $"{field}:", $"{field}: null", $"{field}: '   '" })
        {
            ContentTestData.WriteAbout(directory.Root, ContentTestData.AboutMetadata.Replace($"{field}: {value}", replacement));

            var result = AboutBuilder.Build(directory.Root, outputDir);

            Assert.Equal($"about.md: {field} is required.", Assert.Single(result.Errors));
            Assert.False(Directory.Exists(outputDir));
        }
    }

    [Fact]
    public void MissingAboutFailsAndClearsOtherBuildersOutput()
    {
        using var directory = new TemporaryDirectory();
        var options = new BuildOptions(directory.Root, Path.Combine(directory.Root, "wwwroot"), false);
        ContentTestData.WriteUniverse(options.ContentDir);

        var result = ContentBuildRunner.Run(options);

        Assert.Equal("about.md: File is required.", Assert.Single(result.Errors));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(options.OutputDir, "content")));
    }

    [Theory]
    [InlineData("No front matter")]
    [InlineData("---\nname: [unclosed\n---\nBody")]
    public void ReportsFrontMatterErrors(string markdown)
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "about.md"), markdown);

        var result = AboutBuilder.Build(directory.Root, Path.Combine(directory.Root, "output"));

        Assert.StartsWith("about.md: ", Assert.Single(result.Errors));
        Assert.False(Directory.Exists(Path.Combine(directory.Root, "output")));
    }
}
