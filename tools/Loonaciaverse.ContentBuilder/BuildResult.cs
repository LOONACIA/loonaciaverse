namespace Loonaciaverse.ContentBuilder;

public sealed record BuildResult(IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
