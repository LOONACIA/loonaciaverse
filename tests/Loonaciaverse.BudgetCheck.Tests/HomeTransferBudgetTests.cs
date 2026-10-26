namespace Loonaciaverse.BudgetCheck.Tests;

public sealed class HomeTransferBudgetTests
{
    private const string PretendardCss = "fonts/pretendard/pretendardvariable-dynamic-subset.css";

    [Fact]
    public void MeasureCountsHomeAssetsUsingBrotliSizesWithoutCountingCompressedCopies()
    {
        using var directory = CreatePublishedRoot();
        WriteBytes(directory.Root, "index.html.br", 3);
        WriteBytes(directory.Root, "index.html.gz", 4);
        WriteBytes(directory.Root, "_framework/runtime.wasm.br", 5);
        WriteBytes(directory.Root, "_framework/runtime.wasm.gz", 6);
        WriteBytes(directory.Root, "_framework/nested/app.wasm", 30);
        WriteBytes(directory.Root, "_framework/nested/app.wasm.br", 7);
        WriteBytes(directory.Root, "_framework/blazor.webassembly.js", 11);
        WriteBytes(directory.Root, "_framework/blazor.boot.json", 12);
        WriteBytes(directory.Root, "_framework/orphan.br", 50);
        WriteBytes(directory.Root, "_framework/orphan.gz", 50);
        WriteBytes(directory.Root, "css/app.css.gz", 1);
        WriteBytes(directory.Root, "Loonaciaverse.Web.styles.css", 13);
        WriteBytes(directory.Root, "js/scene.js.br", 8);
        WriteBytes(directory.Root, "content/universe.json.br", 1);
        WriteBytes(directory.Root, "fonts/michroma/regular.woff2", 14);
        WriteBytes(directory.Root, "fonts/rajdhani/regular.woff2", 15);
        WriteBytes(directory.Root, "fonts/rajdhani/bold.woff2", 16);
        WriteBytes(directory.Root, "fonts/rajdhani/bold.woff2.br", 9);

        var report = HomeTransferBudget.Measure(directory.Root);

        BudgetEntry[] expected =
        [
            new("index.html", 3),
            new("_framework/runtime.wasm", 5),
            new("_framework/nested/app.wasm", 7),
            new("_framework/blazor.webassembly.js", 11),
            new("_framework/blazor.boot.json", 12),
            new("css/app.css", 10),
            new("Loonaciaverse.Web.styles.css", 13),
            new("js/scene.js", 8),
            new("content/universe.json", 1),
            new(PretendardCss, 0),
            new("fonts/michroma/regular.woff2", 14),
            new("fonts/rajdhani/regular.woff2", 15),
            new("fonts/rajdhani/bold.woff2", 9)
        ];
        Assert.Equal(expected.OrderBy(entry => entry.Path, StringComparer.Ordinal), report.Entries);
        Assert.Equal(108, report.TotalBytes);
        Assert.Empty(report.MissingAssets);
        Assert.True(report.IsWithinBudget);
    }

    [Fact]
    public void MeasureExcludesDetailAssetsAndUnrelatedFiles()
    {
        using var directory = CreatePublishedRoot();
        var baseline = HomeTransferBudget.Measure(directory.Root);
        foreach (var path in new[] { "js/highlight.js", "js/toc.js", "js/other.js", "content/posts.json", "content/about.json", "og-image.png", "fonts/michroma/LICENSE" })
        {
            WriteBytes(directory.Root, path, 100);
            WriteBytes(directory.Root, path + ".br", 50);
        }

        Assert.Equal(baseline.Entries, HomeTransferBudget.Measure(directory.Root).Entries);
    }

    [Theory]
    [InlineData("""{"nested":{"title":"가"},"array":[{"title":"난"},false,42,null]}""", "single.woff2", "interval.woff2")]
    [InlineData("""{"array":[["힣"]],"nested":{"title":"A"}}""", "multiple.woff2", "multiple.woff2")]
    [InlineData("""{"가":"unmatched","난":17,"힣":true}""", null, null)]
    public void MeasureSelectsPretendardChunksFromNestedStringValuesOnly(string universe, string? firstChunk, string? secondChunk)
    {
        using var directory = CreatePublishedRoot();
        WriteText(directory.Root, "content/universe.json", universe);
        WriteText(directory.Root, PretendardCss, """
            @font-face {
                src: url(./chunks/single.woff2) format('woff2');
                unicode-range: U+AC00;
            }
            @font-face {
                src: url('./chunks/interval.woff2') format('woff2');
                unicode-range: U+B098-B0A0;
            }
            @font-face {
                src: url("./chunks/multiple.woff2") format('woff2');
                unicode-range: U+0041, U+d7a0-d7a3, U+0030-0039;
            }
            """);
        foreach (var chunk in new[] { "single.woff2", "interval.woff2", "multiple.woff2" })
        {
            WriteBytes(directory.Root, "fonts/pretendard/chunks/" + chunk, 100);
            WriteBytes(directory.Root, "fonts/pretendard/chunks/" + chunk + ".br", 20);
        }

        var report = HomeTransferBudget.Measure(directory.Root);
        var chunks = report.Entries.Where(entry => entry.Path.EndsWith(".woff2", StringComparison.Ordinal)).ToArray();
        var expected = new[] { firstChunk, secondChunk }.Where(chunk => chunk is not null).Distinct().Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected.Select(chunk => "fonts/pretendard/chunks/" + chunk), chunks.Select(entry => entry.Path));
        Assert.All(chunks, entry => Assert.Equal(20, entry.Bytes));
        Assert.Empty(report.MissingAssets);
    }

    [Fact]
    public void MeasureUsesUnicodeRunesAndResolvesFontUrlsRelativeToCss()
    {
        using var directory = CreatePublishedRoot();
        WriteText(directory.Root, "content/universe.json", """{"title":"\uD83D\uDE80"}""");
        WriteText(directory.Root, PretendardCss, """
            @font-face { src: url(../shared/rocket.woff2); unicode-range: U+1F680; }
            @font-face { src: url(./surrogate.woff2); unicode-range: U+D83D; }
            @font-face { src: url(../shared/rocket.woff2); unicode-range: U+1F680-1F6FF; }
            """);
        WriteBytes(directory.Root, "fonts/shared/rocket.woff2", 25);
        WriteBytes(directory.Root, "fonts/pretendard/surrogate.woff2", 30);

        var chunks = HomeTransferBudget.Measure(directory.Root).Entries.Where(entry => entry.Path.EndsWith(".woff2", StringComparison.Ordinal));

        Assert.Equal(new[] { new BudgetEntry("fonts/shared/rocket.woff2", 25) }, chunks);
    }

    [Theory]
    [InlineData("index.html", "index.html")]
    [InlineData("css/app.css", "css/app.css")]
    [InlineData("js/scene.js", "js/scene.js")]
    [InlineData("content/universe.json", "content/universe.json")]
    [InlineData(PretendardCss, PretendardCss)]
    [InlineData("_framework/runtime.wasm", "_framework/*.wasm")]
    public void MeasureRejectsMissingRequiredAssetsEvenWithCompressedCopies(string path, string expectedMissing)
    {
        using var directory = CreatePublishedRoot();
        File.Delete(Path.Combine(directory.Root, path));
        WriteBytes(directory.Root, path + ".br", 5);
        WriteBytes(directory.Root, path + ".gz", 5);

        var report = HomeTransferBudget.Measure(directory.Root);

        Assert.Equal(new[] { expectedMissing }, report.MissingAssets);
        Assert.False(report.IsWithinBudget);
    }

    [Fact]
    public void MeasureRejectsAnEmptyDirectory()
    {
        using var directory = new TemporaryDirectory();

        var report = HomeTransferBudget.Measure(directory.Root);

        Assert.Empty(report.Entries);
        Assert.Equal(6, report.MissingAssets.Count);
        Assert.Contains("_framework/*.wasm", report.MissingAssets);
        Assert.False(report.IsWithinBudget);
    }

    [Fact]
    public void MeasureRejectsMissingSelectedFontChunk()
    {
        using var directory = CreatePublishedRoot();
        WriteText(directory.Root, "content/universe.json", """{"title":"가"}""");
        WriteText(directory.Root, PretendardCss, "@font-face { src: url(./missing.woff2); unicode-range: U+AC00; }");

        var report = HomeTransferBudget.Measure(directory.Root);

        Assert.Equal(new[] { "fonts/pretendard/missing.woff2" }, report.MissingAssets);
        Assert.False(report.IsWithinBudget);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MeasureEnforcesDecimalMegabyteLimitInclusively(int excessBytes, bool expectedWithinBudget)
    {
        using var directory = CreatePublishedRoot();
        var baseline = HomeTransferBudget.Measure(directory.Root).TotalBytes;
        WriteBytes(directory.Root, "_framework/runtime.wasm.br", HomeTransferBudget.LimitBytes - baseline + 10 + excessBytes);

        var report = HomeTransferBudget.Measure(directory.Root);

        Assert.Equal(HomeTransferBudget.LimitBytes + excessBytes, report.TotalBytes);
        Assert.Equal(expectedWithinBudget, report.IsWithinBudget);
    }

    [Fact]
    public void MainReturnsZeroAndPrintsDescendingEntriesFollowedByTotal()
    {
        using var directory = CreatePublishedRoot();
        WriteBytes(directory.Root, "js/scene.js", 50);

        var result = RunProgram([directory.Root]);
        var lines = result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal("50 bytes\tjs/scene.js", lines[0]);
        Assert.Equal("Total: 82 bytes / 3000000 bytes", lines[^1]);
        var sizes = lines[..^1].Select(line => long.Parse(line.Split(' ')[0])).ToArray();
        Assert.Equal(sizes.OrderDescending(), sizes);
    }

    [Fact]
    public void MainReturnsOneAndReportsMissingAssets()
    {
        using var directory = new TemporaryDirectory();

        var result = RunProgram([directory.Root]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Missing asset: index.html", result.Error);
        Assert.Contains("Missing asset: _framework/*.wasm", result.Error);
        Assert.Contains("Total: 0 bytes / 3000000 bytes", result.Output);
    }

    [Fact]
    public void MainReturnsOneAndReportsExceededBudget()
    {
        using var directory = CreatePublishedRoot();
        WriteBytes(directory.Root, "js/scene.js.br", HomeTransferBudget.LimitBytes);

        var result = RunProgram([directory.Root]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Home transfer budget exceeded by 32 bytes.", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MainReturnsTwoAndPrintsUsageForInvalidArguments(int argumentCount)
    {
        using var directory = new TemporaryDirectory();
        var nonexistentPath = Path.Combine(directory.Root, "missing");
        string[] args = argumentCount switch
        {
            0 => [],
            1 => [nonexistentPath],
            _ => [directory.Root, directory.Root]
        };

        var result = RunProgram(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Usage: Loonaciaverse.BudgetCheck <published-wwwroot>", result.Error);
    }

    [Fact]
    public void MainReturnsOneForMalformedUniverseJson()
    {
        using var directory = CreatePublishedRoot();
        WriteText(directory.Root, "content/universe.json", "{");

        var result = RunProgram([directory.Root]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unable to measure home transfer:", result.Error);
    }

    private static TemporaryDirectory CreatePublishedRoot()
    {
        var directory = new TemporaryDirectory();
        foreach (var path in new[] { "index.html", "css/app.css", "js/scene.js", "_framework/runtime.wasm" })
        {
            WriteBytes(directory.Root, path, 10);
        }

        WriteText(directory.Root, "content/universe.json", "{}");
        WriteText(directory.Root, PretendardCss, "");
        return directory;
    }

    private static void WriteBytes(string root, string path, long length)
    {
        var fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = File.Create(fullPath);
        stream.SetLength(length);
    }

    private static void WriteText(string root, string path, string text)
    {
        var fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }

    private static (int ExitCode, string Output, string Error) RunProgram(string[] args)
    {
        var previousOutput = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = Program.Main(args);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
            Console.SetError(previousError);
        }
    }
}
