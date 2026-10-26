namespace Loonaciaverse.BudgetCheck.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("Loonaciaverse.Tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
