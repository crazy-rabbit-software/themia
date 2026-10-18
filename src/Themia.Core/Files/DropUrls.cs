using Themia.Core.Widgets;

namespace Themia.Core.Files;

/// <summary>[S-1402] The web addresses a drag carries as text, as the desktop's drop reads them.</summary>
public static class DropUrls
{
    /// <summary>
    /// [S-1402] Web addresses in dropped text (a browser's tab or address bar offers one per line):
    /// only absolute http/https lines, in order, each once.
    /// </summary>
    public static IReadOnlyList<string> InText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var urls = new List<string>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r').Trim();

            if (line.StartsWith('#'))
            {
                continue; // text/uri-list comments
            }

            if (LaunchTargets.Classify(line) is { Kind: LaunchKind.Web } web
                && !urls.Contains(web.Value, StringComparer.OrdinalIgnoreCase))
            {
                urls.Add(web.Value);
            }
        }

        return urls;
    }
}
