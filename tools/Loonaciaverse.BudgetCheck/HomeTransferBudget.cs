using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Loonaciaverse.BudgetCheck;

public sealed record BudgetEntry(string Path, long Bytes);

public sealed record BudgetReport(IReadOnlyList<BudgetEntry> Entries, IReadOnlyList<string> MissingAssets)
{
    public long TotalBytes => Entries.Sum(entry => entry.Bytes);
    public bool IsWithinBudget => MissingAssets.Count == 0 && TotalBytes <= HomeTransferBudget.LimitBytes;
}

public static class HomeTransferBudget
{
    public const long LimitBytes = 3_000_000;

    private const string PretendardCss = "fonts/pretendard/pretendardvariable-dynamic-subset.css";

    public static BudgetReport Measure(string webRoot)
    {
        webRoot = Path.GetFullPath(webRoot);
        var entries = new Dictionary<string, BudgetEntry>(StringComparer.Ordinal);
        var missing = new HashSet<string>(StringComparer.Ordinal);

        void AddAsset(string relativePath, bool required = false)
        {
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath));
            relativePath = Path.GetRelativePath(webRoot, fullPath).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                if (required)
                {
                    missing.Add(relativePath);
                }

                return;
            }

            var transferPath = File.Exists(fullPath + ".br") ? fullPath + ".br" : fullPath;
            entries[relativePath] = new BudgetEntry(relativePath, new FileInfo(transferPath).Length);
        }

        foreach (var path in new[] { "index.html", "css/app.css", "js/scene.js", "content/universe.json", PretendardCss })
        {
            AddAsset(path, required: true);
        }

        AddAsset("Loonaciaverse.Web.styles.css");

        var frameworkDirectory = Path.Combine(webRoot, "_framework");
        var frameworkFiles = Directory.Exists(frameworkDirectory)
            ? Directory.GetFiles(frameworkDirectory, "*", SearchOption.AllDirectories).Where(path => !IsCompressed(path)).ToArray()
            : [];
        if (!frameworkFiles.Any(path => path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase)))
        {
            missing.Add("_framework/*.wasm");
        }

        foreach (var path in frameworkFiles)
        {
            AddAsset(Path.GetRelativePath(webRoot, path));
        }

        foreach (var fontFamily in new[] { "michroma", "rajdhani" })
        {
            var fontDirectory = Path.Combine(webRoot, "fonts", fontFamily);
            if (Directory.Exists(fontDirectory))
            {
                foreach (var path in Directory.GetFiles(fontDirectory, "*.woff2"))
                {
                    AddAsset(Path.GetRelativePath(webRoot, path));
                }
            }
        }

        var universePath = Path.Combine(webRoot, "content/universe.json");
        var cssPath = Path.Combine(webRoot, PretendardCss);
        if (File.Exists(universePath) && File.Exists(cssPath))
        {
            using var universe = JsonDocument.Parse(File.ReadAllText(universePath));
            var codePoints = new HashSet<int>();
            CollectCodePoints(universe.RootElement, codePoints);

            foreach (Match face in Regex.Matches(File.ReadAllText(cssPath), @"@font-face\s*\{(?<body>[^}]*)\}", RegexOptions.IgnoreCase))
            {
                var body = face.Groups["body"].Value;
                var ranges = Regex.Match(body, @"unicode-range\s*:\s*(?<ranges>[^;}]*)", RegexOptions.IgnoreCase);
                if (!ranges.Success || !ContainsCodePoint(ranges.Groups["ranges"].Value, codePoints))
                {
                    continue;
                }

                var source = Regex.Match(body, "src\\s*:\\s*url\\(\\s*['\"]?(?<path>[^'\"\\s)]+)['\"]?\\s*\\)", RegexOptions.IgnoreCase);
                if (source.Success)
                {
                    var fontPath = Path.Combine(Path.GetDirectoryName(cssPath)!, source.Groups["path"].Value);
                    AddAsset(Path.GetRelativePath(webRoot, fontPath), required: true);
                }
            }
        }

        return new BudgetReport(entries.Values.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray(), missing.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsCompressed(string path)
    {
        return path.EndsWith(".br", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
    }

    private static void CollectCodePoints(JsonElement element, HashSet<int> codePoints)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                foreach (var rune in element.GetString()!.EnumerateRunes())
                {
                    codePoints.Add(rune.Value);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectCodePoints(property.Value, codePoints);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectCodePoints(item, codePoints);
                }

                break;
        }
    }

    private static bool ContainsCodePoint(string ranges, HashSet<int> codePoints)
    {
        foreach (var range in ranges.Split(','))
        {
            var match = Regex.Match(range.Trim(), @"^U\+(?<start>[0-9a-f]{1,6})(?:-(?<end>[0-9a-f]{1,6}))?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var start = int.Parse(match.Groups["start"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : start;
            if (codePoints.Any(point => point >= start && point <= end))
            {
                return true;
            }
        }

        return false;
    }
}
