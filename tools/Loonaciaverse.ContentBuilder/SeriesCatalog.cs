using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Loonaciaverse.ContentBuilder;

internal static class SeriesCatalog
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    public static IReadOnlyList<SeriesData> Read(string contentDir)
    {
        var path = Path.Combine(contentDir, "series.yml");
        if (!File.Exists(path))
        {
            return [];
        }

        var file = Deserializer.Deserialize<SeriesFile>(File.ReadAllText(path));
        return file?.Series?.Select(series => series!).ToArray() ?? [];
    }

    public sealed class SeriesFile
    {
        public List<SeriesData?>? Series { get; set; }
    }

    public sealed class SeriesData
    {
        public string Slug { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
    }
}
