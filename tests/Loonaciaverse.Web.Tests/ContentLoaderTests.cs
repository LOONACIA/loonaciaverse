using Bunit;
using Loonaciaverse.Core.Content;
using Loonaciaverse.Web.Components;
using Microsoft.AspNetCore.Components;

namespace Loonaciaverse.Web.Tests;

public class ContentLoaderTests
{
    [Fact]
    public void ParameterChangesDoNotReloadContent()
    {
        using var context = new BunitContext();
        var calls = 0;
        var profile = TestContent.About();
        Func<CancellationToken, Task<AboutProfile?>> load = _ =>
        {
            calls++;
            return Task.FromResult<AboutProfile?>(profile);
        };
        RenderFragment<AboutProfile> content = item => builder => builder.AddContent(0, item.Name);
        var component = context.Render<ContentLoader<AboutProfile>>(parameters => parameters.Add(p => p.Load, load).Add(p => p.ChildContent, content));

        component.Render(parameters => parameters.Add(p => p.Load, _ =>
        {
            calls++;
            return Task.FromResult<AboutProfile?>(profile with { Name = "Replacement" });
        }));

        Assert.Equal(1, calls);
        Assert.Equal(profile.Name, component.Markup);
    }
}
