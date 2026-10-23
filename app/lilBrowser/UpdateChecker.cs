using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace PxBrowser;

internal sealed record UpdateInfo(string Sha, DateTimeOffset CommittedAt, string Url);

internal static class UpdateChecker
{
    public const string Owner = "Hum1Tab";
    public const string Repository = "lil-browser";

    public static string RepositoryUrl => $"https://github.com/{Owner}/{Repository}";
    public static string ReleasesUrl => $"{RepositoryUrl}/releases";
    public static string ApiUrl => $"https://api.github.com/repos/{Owner}/{Repository}/commits?per_page=1";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("lIlBrowser/0.3");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var response = await http.GetAsync(ApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array ||
            document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var latest = document.RootElement[0];
        var sha = latest.GetProperty("sha").GetString();
        var dateText = latest.GetProperty("commit").GetProperty("committer").GetProperty("date").GetString();
        var url = latest.GetProperty("html_url").GetString();
        if (string.IsNullOrWhiteSpace(sha) || !DateTimeOffset.TryParse(dateText, out var committed) ||
            string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var embedded = EmbeddedCommit();
        if (embedded != null)
        {
            return sha.StartsWith(embedded, StringComparison.OrdinalIgnoreCase)
                ? null
                : new UpdateInfo(sha, committed, url);
        }

        // Fallback for local builds that do not have Git metadata.
        return committed > BuildTimeUtc().AddMinutes(1)
            ? new UpdateInfo(sha, committed, url)
            : null;
    }

    private static string? EmbeddedCommit()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return null;

        var index = informational.IndexOf('+');
        if (index < 0 || index + 1 >= informational.Length) return null;

        var commit = informational[(index + 1)..].Trim();
        return commit.Length >= 7 && !commit.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? commit
            : null;
    }

    private static DateTime BuildTimeUtc()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return File.GetLastWriteTimeUtc(path);
        }

        var assembly = Assembly.GetExecutingAssembly().Location;
        return !string.IsNullOrWhiteSpace(assembly) && File.Exists(assembly)
            ? File.GetLastWriteTimeUtc(assembly)
            : Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }
}
