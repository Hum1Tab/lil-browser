namespace PxBrowser;

/// <summary>
/// Small, conservative host/path blocker. It is not a uBO replacement.
/// The goal is to block common ad/tracker hosts plus a local test path.
/// </summary>
internal sealed class Adblock
{
    private readonly HashSet<string> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _pathMarkers =
    {
        "/pagead/", "/adservice/",
    };

    public long BlockedCount { get; private set; }

    public Adblock(bool enabled)
    {
        Enabled = enabled;
        foreach (var host in new[]
                 {
                     "doubleclick.net", "googlesyndication.com", "googleadservices.com",
                     "adservice.google.com", "amazon-adsystem.com", "scorecardresearch.com",
                     "adnxs.com", "taboola.com", "outbrain.com", "criteo.com",
                     "pubmatic.com", "rubiconproject.com", "openx.net", "quantserve.com",
                     "moatads.com", "adsrvr.org", "casalemedia.com", "bidswitch.net",
                 })
        {
            _hosts.Add(host);
        }
        LoadFiltersFile();
    }

    public bool Enabled { get; set; }

    public void LoadFiltersFile()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "filters.txt");
            if (!File.Exists(path)) return;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!')) continue;
                if (line.StartsWith("||", StringComparison.Ordinal))
                {
                    line = line[2..].TrimEnd('^', '/', '|');
                }
                else if (line.StartsWith("0.0.0.0 ", StringComparison.Ordinal))
                {
                    line = line["0.0.0.0 ".Length..].Trim();
                }
                if (line.Contains(' ') || line.Contains('/')) continue;
                if (line.Contains('.')) _hosts.Add(line);
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("filters load failed: " + ex.Message);
        }
    }

    public bool ShouldBlock(Uri uri)
    {
        if (!Enabled) return false;
        var host = uri.Host;
        foreach (var domain in _hosts)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var path = uri.AbsolutePath;
        foreach (var marker in _pathMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // Local test path only. Never block a real site's generic "/ad/" path
        // outside loopback, to avoid obvious false positives.
        if ((host == "127.0.0.1" || host == "localhost") &&
            path.Contains("/ad/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public void CountBlocked() => BlockedCount++;
}
