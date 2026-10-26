namespace Loonaciaverse.ContentBuilder;

internal static class ContentDocuments
{
    public static List<FrontMatterDocument<T>> ReadCollection<T>(string contentDir, string collection, Action<string, string, T> validate, List<string> errors)
    {
        var collectionDir = Path.Combine(contentDir, collection);
        var documents = new List<FrontMatterDocument<T>>();
        if (!Directory.Exists(collectionDir))
        {
            return documents;
        }

        foreach (var sourceDir in Directory.EnumerateDirectories(collectionDir).Order(StringComparer.Ordinal))
        {
            var sourcePath = Path.Combine(sourceDir, "index.md");
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(contentDir, sourcePath).Replace('\\', '/');
            try
            {
                var document = FrontMatterReader.Read<T>(File.ReadAllText(sourcePath), relativePath);
                validate(relativePath, sourceDir, document.Data);
                documents.Add(document);
            }
            catch (ContentValidationException exception)
            {
                errors.Add(exception.Message);
            }
        }

        return documents;
    }
}
