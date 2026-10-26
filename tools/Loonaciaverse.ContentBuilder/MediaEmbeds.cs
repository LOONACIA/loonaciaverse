using System.Diagnostics.CodeAnalysis;
using Markdig;
using Markdig.Extensions.MediaLinks;

namespace Loonaciaverse.ContentBuilder;

public static class MediaEmbeds
{
    public static MarkdownPipelineBuilder UseNoCookieMediaLinks(this MarkdownPipelineBuilder builder)
    {
        var options = new MediaOptions();
        for (var index = 0; index < options.Hosts.Count; index++)
        {
            var provider = options.Hosts[index];
            if (provider.Class?.StartsWith("youtube", StringComparison.Ordinal) == true)
            {
                options.Hosts[index] = new NoCookieHostProvider(provider);
            }
        }

        // Register before UseAdvancedExtensions so its default media options do not take precedence.
        return builder.UseMediaLinks(options);
    }

    private sealed class NoCookieHostProvider(IHostProvider provider) : IHostProvider
    {
        public string? Class => provider.Class;
        public bool AllowFullScreen => provider.AllowFullScreen;

        public bool TryHandle(Uri mediaUri, bool isSchemaRelative, [NotNullWhen(true)] out string? iframeUrl)
        {
            if (!provider.TryHandle(mediaUri, isSchemaRelative, out iframeUrl))
            {
                return false;
            }

            const string prefix = "https://www.youtube.com/embed/";
            if (iframeUrl.StartsWith(prefix, StringComparison.Ordinal))
            {
                iframeUrl = "https://www.youtube-nocookie.com/embed/" + iframeUrl[prefix.Length..];
            }

            return true;
        }
    }
}
