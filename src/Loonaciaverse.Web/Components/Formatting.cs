using System.Globalization;

namespace Loonaciaverse.Web.Components;

public static class Formatting
{
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
