namespace Loonaciaverse.ContentBuilder;

public sealed class ContentValidationException : Exception
{
    public ContentValidationException(string sourcePath, string description, Exception? innerException = null) : base($"{sourcePath}: {description}", innerException)
    {
    }
}
