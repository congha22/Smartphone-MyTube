using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SmartphoneMyTube.Playback;

internal static class YouTubeUrlParser
{
    private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

    public static bool TryGetVideoId(string? input, out string videoId)
    {
        videoId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string raw = input.Trim();
        if (VideoIdPattern.IsMatch(raw))
        {
            videoId = raw;
            return true;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
        {
            if (!raw.Contains("://", StringComparison.Ordinal))
                Uri.TryCreate("https://" + raw, UriKind.Absolute, out uri);
        }

        if (uri == null)
            return false;

        string host = uri.Host.ToLowerInvariant();
        string? candidate = null;

        if (host is "youtu.be" or "www.youtu.be")
        {
            string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                candidate = parts[0];
        }
        else if (host.EndsWith("youtube.com", StringComparison.Ordinal))
        {
            string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
                candidate = ParseQuery(uri.Query).GetValueOrDefault("v");
            else if (parts.Length >= 2 && (parts[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
                                           || parts[0].Equals("embed", StringComparison.OrdinalIgnoreCase)
                                           || parts[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
                candidate = parts[1];
        }

        if (candidate != null)
        {
            int separator = candidate.IndexOfAny(new[] { '?', '&', '#', '/' });
            if (separator >= 0)
                candidate = candidate[..separator];

            if (VideoIdPattern.IsMatch(candidate))
            {
                videoId = candidate;
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=');
            string key = equals >= 0 ? pair[..equals] : pair;
            string value = equals >= 0 ? pair[(equals + 1)..] : string.Empty;
            result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        return result;
    }
}
