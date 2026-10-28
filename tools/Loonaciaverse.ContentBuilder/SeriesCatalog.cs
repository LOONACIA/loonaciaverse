using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Loonaciaverse.ContentBuilder;

internal static class SeriesCatalog
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    public static IReadOnlyList<SeriesData>? Read(string contentDir, List<string> errors)
    {
        var path = Path.Combine(contentDir, "series.yml");
        if (!File.Exists(path))
        {
            return [];
        }

        SeriesFile? file;
        try
        {
            file = Deserializer.Deserialize<SeriesFile>(File.ReadAllText(path));
        }
        catch (YamlException exception)
        {
            errors.Add($"series.yml: Invalid YAML: {exception.Message}");
            return null;
        }

        var validator = new ContentValidator();
        var entries = file?.Series ?? [];
        var series = new List<SeriesData>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var sourcePath = $"series.yml: series[{index}]";
            if (entry is null)
            {
                errors.Add($"{sourcePath}: Series must be a mapping.");
                continue;
            }

            entry.SourceIndex = index;
            validator.ValidateSlug(sourcePath, null, entry.Slug);
            validator.ValidateRequired(sourcePath, "title", entry.Title);
            validator.ValidateRequired(sourcePath, "summary", entry.Summary);
            if (!string.IsNullOrWhiteSpace(entry.Slug))
            {
                series.Add(entry);
            }
        }

        errors.AddRange(validator.Errors);
        return series;
    }

    public sealed class SeriesFile
    {
        public List<SeriesData?>? Series { get; set; }
    }

    public sealed class SeriesData
    {
        internal int SourceIndex { get; set; }
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
    }
}
