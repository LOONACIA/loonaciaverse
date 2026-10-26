using System.Text.Json;

namespace Loonaciaverse.Core.Content;

public static class ContentJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
