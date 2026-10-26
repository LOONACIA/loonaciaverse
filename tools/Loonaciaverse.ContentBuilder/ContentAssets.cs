using System.Text.RegularExpressions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Loonaciaverse.ContentBuilder;

public static class ContentAssets
{
    private static readonly Regex SchemePattern = new(@"\A[a-zA-Z][a-zA-Z0-9+.-]*:", RegexOptions.CultureInvariant);

    public static bool IsRelativeUrl(string? url)
    {
        return !string.IsNullOrEmpty(url) && !url.StartsWith('/') && !url.StartsWith('#') && !SchemePattern.IsMatch(url);
    }

    public static string? RewriteUrl(string? url, string assetBaseUrl)
    {
        return IsRelativeUrl(url) ? assetBaseUrl + url : url;
    }

    public static void RewriteUrls(MarkdownDocument document, string assetBaseUrl)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            link.Url = RewriteUrl(link.Url, assetBaseUrl);
        }
    }

    public static bool FileExists(string sourceDir, string url)
    {
        var path = Uri.UnescapeDataString(url.Split('?', '#')[0]);
        var fullPath = Path.GetFullPath(Path.Combine(sourceDir, path));
        var relativePath = Path.GetRelativePath(sourceDir, fullPath);
        return !Path.IsPathRooted(relativePath) && relativePath != ".." && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && File.Exists(fullPath);
    }

    public static void CopyFiles(string sourceDir, string outputDir)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourcePath);
            if (relativePath == "index.md")
            {
                continue;
            }

            var outputPath = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(sourcePath, outputPath, overwrite: true);
        }
    }
}
