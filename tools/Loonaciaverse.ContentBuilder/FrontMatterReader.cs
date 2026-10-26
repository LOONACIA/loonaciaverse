using Markdig;
using Markdig.Extensions.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Loonaciaverse.ContentBuilder;

public static class FrontMatterReader
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    public static FrontMatterDocument<T> Read<T>(string markdown, string sourcePath)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        var frontMatter = document.OfType<YamlFrontMatterBlock>().FirstOrDefault();
        if (frontMatter is null)
        {
            throw new ContentValidationException(sourcePath, "YAML front matter is missing or not closed.");
        }

        T data;
        try
        {
            data = Deserializer.Deserialize<T>(frontMatter.Lines.ToString());
        }
        catch (YamlException exception)
        {
            throw new ContentValidationException(sourcePath, $"Invalid YAML front matter: {exception.Message}", exception);
        }

        if (data is null)
        {
            throw new ContentValidationException(sourcePath, "YAML front matter is empty.");
        }

        // Consume the closing fence's line ending, preserving the body's original whitespace.
        var bodyStart = frontMatter.Span.End + 1;
        while (bodyStart < markdown.Length && markdown[bodyStart] is not '\r' and not '\n')
        {
            bodyStart++;
        }

        if (bodyStart < markdown.Length && markdown[bodyStart] == '\r')
        {
            bodyStart++;
        }

        if (bodyStart < markdown.Length && markdown[bodyStart] == '\n')
        {
            bodyStart++;
        }

        return new FrontMatterDocument<T>(data, markdown[bodyStart..]);
    }
}
