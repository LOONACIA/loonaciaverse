using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder;

public static class HudStatsCalculator
{
    // Inputs are the summaries actually emitted by this build; project order matches projects.json.
    public static HudStats Calculate(IReadOnlyList<PostSummary> posts, IReadOnlyList<ProjectSummary> projects, AboutProfile about)
    {
        var latest = posts.OrderByDescending(post => post.Date).ThenBy(post => post.Slug, StringComparer.Ordinal).FirstOrDefault();
        var blog = new BlogStats(posts.Count, latest?.Date, TopThree(posts.SelectMany(post => post.Tags)), latest);
        var projectStats = new ProjectStats(projects.Count, TopThree(projects.SelectMany(project => project.Stack)), projects.FirstOrDefault(project => project.Featured));
        var aboutStats = new AboutStats(about.Title, about.Stack, about.Email, about.Github, about.Linkedin);
        return new HudStats(blog, projectStats, aboutStats);
    }

    private static string[] TopThree(IEnumerable<string> values)
    {
        var ranked = values.GroupBy(value => value, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal);
        return ranked.Take(3).Select(group => group.Key).ToArray();
    }
}
