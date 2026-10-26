using System.Text.RegularExpressions;

namespace Loonaciaverse.ContentBuilder;

public sealed class ContentValidator
{
    private static readonly Regex SlugPattern = new(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, string> slugPaths = new(StringComparer.Ordinal);
    private readonly List<string> errors = [];

    public IReadOnlyList<string> Errors => errors;

    public void ValidateRequired(string sourcePath, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{sourcePath}: {field} is required.");
        }
    }

    public void ValidateSlug(string sourcePath, string folderName, string? slug)
    {
        ValidateRequired(sourcePath, "slug", slug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        if (!SlugPattern.IsMatch(slug))
        {
            errors.Add($"{sourcePath}: slug '{slug}' must use lowercase letters, digits, and single hyphens between segments.");
        }

        if (!string.Equals(slug, folderName, StringComparison.Ordinal))
        {
            errors.Add($"{sourcePath}: slug '{slug}' must match folder name '{folderName}'.");
        }

        if (!slugPaths.TryAdd(slug, sourcePath))
        {
            errors.Add($"{sourcePath}: Duplicate slug '{slug}' also used in {slugPaths[slug]}.");
        }
    }

    public void ValidateTags(string sourcePath, IEnumerable<string?>? tags)
    {
        foreach (var tag in tags ?? [])
        {
            if (tag is null || !SlugPattern.IsMatch(tag))
            {
                errors.Add($"{sourcePath}: tag '{tag}' must use lowercase letters, digits, and single hyphens between segments.");
            }
        }
    }

    public void ValidateDates(string sourcePath, DateTime? date, DateTime? updated)
    {
        if (date is null)
        {
            errors.Add($"{sourcePath}: date is required.");
        }
        else if (updated is { } updatedDate && updatedDate.Date < date.Value.Date)
        {
            errors.Add($"{sourcePath}: updated must be on or after date.");
        }
    }
}
