namespace Loonaciaverse.ContentBuilder.Tests;

public class FrontMatterReaderTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ReadDeserializesFieldsAndPreservesBody(string newline)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "index.md");
        var body = $"{newline}# Hello{newline}{newline}Some **Markdown**.  {newline}---{newline}";
        var markdown = string.Join(newline, "---", "title: Hello world", "publishDate: 2026-10-26", "tags:", "  - dotnet", "  - blazor", "unknownKey: ignored", "---") + newline + body;
        File.WriteAllText(path, markdown);

        var result = FrontMatterReader.Read<TestMetadata>(File.ReadAllText(path), path);

        Assert.Equal("Hello world", result.Data.Title);
        Assert.Equal(new DateTime(2026, 10, 26), result.Data.PublishDate);
        Assert.Equal(new[] { "dotnet", "blazor" }, result.Data.Tags);
        Assert.Equal(body, result.Body);
    }

    [Theory]
    [InlineData("# No front matter")]
    [InlineData("---\ntitle: Unclosed")]
    [InlineData("---\ntags: [unclosed\n---\nBody")]
    [InlineData("---\npublishDate: not-a-date\n---\nBody")]
    [InlineData("---\n---\nBody")]
    public void ReadRejectsMissingOrInvalidFrontMatterWithSourcePath(string markdown)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Root, "invalid.md");
        File.WriteAllText(path, markdown);

        var exception = Assert.Throws<ContentValidationException>(() => FrontMatterReader.Read<TestMetadata>(File.ReadAllText(path), path));

        Assert.StartsWith($"{path}: ", exception.Message);
    }

    [Fact]
    public void ReadAllowsAnEmptyBody()
    {
        var result = FrontMatterReader.Read<TestMetadata>("---\ntitle: Hello\n---", "index.md");

        Assert.Equal("Hello", result.Data.Title);
        Assert.Empty(result.Body);
    }

    public sealed class TestMetadata
    {
        public string Title { get; set; } = "";
        public DateTime PublishDate { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
