using System.Text.Json;

namespace PxBrowser;

internal sealed record HistoryEntry(string Url, string Title, DateTimeOffset VisitedAt);

internal static class HistoryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private const int MaxEntries = 5000;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(90);

    public static List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.HistoryPath)) return new List<HistoryEntry>();
            var list = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(AppPaths.HistoryPath))
                       ?? new List<HistoryEntry>();
            var cutoff = DateTimeOffset.Now - MaxAge;
            return list.Where(e => e.VisitedAt >= cutoff).Take(MaxEntries).ToList();
        }
        catch (Exception ex)
        {
            AppPaths.Log("history load failed: " + ex.Message);
            return new List<HistoryEntry>();
        }
    }

    public static void Save(IEnumerable<HistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            var list = entries.OrderByDescending(e => e.VisitedAt).Take(MaxEntries).ToList();
            File.WriteAllText(AppPaths.HistoryPath, JsonSerializer.Serialize(list, Options));
        }
        catch (Exception ex)
        {
            AppPaths.Log("history save failed: " + ex.Message);
        }
    }

    public static List<HistoryEntry> Add(List<HistoryEntry> history, string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return history;
        history.Add(new HistoryEntry(url, title, DateTimeOffset.Now));
        var cutoff = DateTimeOffset.Now - MaxAge;
        return history.Where(e => e.VisitedAt >= cutoff)
            .OrderByDescending(e => e.VisitedAt)
            .Take(MaxEntries)
            .ToList();
    }
}
