using Loonaciaverse.Core.Content;

namespace Loonaciaverse.ContentBuilder.Tests;

public class HudStatsCalculatorTests
{
    private static readonly AboutProfile About = new(
        "LOONACIA", "Developer", ["csharp", "unity"], "hello@example.com", "https://github.com/loonacia", "https://linkedin.com/in/loonacia", "<p>About</p>");

    [Fact]
    public void CountsContentRanksTopThreeByFrequencyThenOrdinalNameAndFindsLatestDate()
    {
        PostSummary[] posts =
        [
            Post("older", new DateOnly(2026, 1, 1), ["zulu", "delta", "beta"]),
            Post("zeta", new DateOnly(2026, 10, 26), ["alpha", "zulu", "charlie"]),
            Post("alpha", new DateOnly(2026, 10, 26), ["zulu"])
        ];
        ProjectSummary[] projects = [Project("first", ["zulu", "delta", "beta"]), Project("second", ["alpha", "zulu", "charlie"])];

        var stats = HudStatsCalculator.Calculate(posts, projects, About);

        Assert.Equal(3, stats.Blog.PostCount);
        Assert.Equal(new DateOnly(2026, 10, 26), stats.Blog.LastPublished);
        Assert.Same(posts[2], stats.Blog.Latest);
        Assert.Equal(new[] { "zulu", "alpha", "beta" }, stats.Blog.TopTags);
        Assert.Equal(2, stats.Projects.ProjectCount);
        Assert.Equal(new[] { "zulu", "alpha", "beta" }, stats.Projects.TopStack);
        Assert.Null(stats.Projects.Featured);
        Assert.Equal(new AboutStats(About.Title, About.Stack, About.Email, About.Github, About.Linkedin), stats.About);
    }

    [Fact]
    public void ChoosesFirstFeaturedInSuppliedIndexOrder()
    {
        ProjectSummary[] projects = [Project("ordinary", []), Project("zeta", []) with { Featured = true }, Project("alpha", []) with { Featured = true }];

        var stats = HudStatsCalculator.Calculate([], projects, About);

        Assert.Same(projects[1], stats.Projects.Featured);
    }

    [Fact]
    public void EmptyCollectionsHaveZeroCountsEmptyRankingsAndNullCardsAndDate()
    {
        var stats = HudStatsCalculator.Calculate([], [], About with { Github = null, Linkedin = null });

        Assert.Equal(0, stats.Blog.PostCount);
        Assert.Null(stats.Blog.LastPublished);
        Assert.Null(stats.Blog.Latest);
        Assert.Empty(stats.Blog.TopTags);
        Assert.Equal(0, stats.Projects.ProjectCount);
        Assert.Null(stats.Projects.Featured);
        Assert.Empty(stats.Projects.TopStack);
        Assert.Equal(About.Title, stats.About.Title);
        Assert.Equal(About.Stack, stats.About.Stack);
        Assert.Equal(About.Email, stats.About.Email);
        Assert.Null(stats.About.Github);
        Assert.Null(stats.About.Linkedin);
    }

    private static PostSummary Post(string slug, DateOnly date, string[] tags)
    {
        return new PostSummary(slug, slug, date, null, "Summary", tags, false, null);
    }

    private static ProjectSummary Project(string slug, string[] stack)
    {
        return new ProjectSummary(slug, slug, "Summary", "2026", "Developer", stack, new Dictionary<string, string>(), null, false, 0, false);
    }
}
