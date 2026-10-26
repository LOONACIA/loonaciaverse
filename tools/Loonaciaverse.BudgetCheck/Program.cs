using System.Text.Json;

namespace Loonaciaverse.BudgetCheck;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: Loonaciaverse.BudgetCheck <published-wwwroot>");
            return 2;
        }

        BudgetReport report;
        try
        {
            report = HomeTransferBudget.Measure(args[0]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Unable to measure home transfer: {exception.Message}");
            return 1;
        }

        foreach (var entry in report.Entries.OrderByDescending(entry => entry.Bytes).ThenBy(entry => entry.Path, StringComparer.Ordinal))
        {
            Console.WriteLine($"{entry.Bytes} bytes\t{entry.Path}");
        }

        Console.WriteLine($"Total: {report.TotalBytes} bytes / {HomeTransferBudget.LimitBytes} bytes");
        foreach (var path in report.MissingAssets)
        {
            Console.Error.WriteLine($"Missing asset: {path}");
        }

        if (report.TotalBytes > HomeTransferBudget.LimitBytes)
        {
            Console.Error.WriteLine($"Home transfer budget exceeded by {report.TotalBytes - HomeTransferBudget.LimitBytes} bytes.");
        }

        return report.IsWithinBudget ? 0 : 1;
    }
}
