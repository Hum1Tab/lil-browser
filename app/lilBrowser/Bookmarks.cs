using System.Text.Json;

namespace PxBrowser;

internal sealed class BookmarkEntry
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
}

internal sealed class BookmarkStore
{
    private const int MaxEntries = 2000;
    private readonly List<BookmarkEntry> _items;

    private BookmarkStore(List<BookmarkEntry> items)
    {
        _items = items;
    }

    public IReadOnlyList<BookmarkEntry> Items => _items;

    public static BookmarkStore Load()
    {
        try
        {
            if (!File.Exists(AppPaths.BookmarksPath)) return new BookmarkStore(new List<BookmarkEntry>());
            var items = JsonSerializer.Deserialize<List<BookmarkEntry>>(File.ReadAllText(AppPaths.BookmarksPath));
            return new BookmarkStore(items ?? new List<BookmarkEntry>());
        }
        catch (Exception ex)
        {
            AppPaths.Log("bookmark load failed: " + ex.Message);
            return new BookmarkStore(new List<BookmarkEntry>());
        }
    }

    public bool Contains(string url) =>
        _items.Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));

    public bool Toggle(string title, string url)
    {
        var existing = _items.FirstOrDefault(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _items.Remove(existing);
            Save();
            return false;
        }

        _items.Insert(0, new BookmarkEntry
        {
            Title = string.IsNullOrWhiteSpace(title) ? url : title.Trim(),
            Url = url,
            AddedAt = DateTimeOffset.Now,
        });
        if (_items.Count > MaxEntries) _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
        Save();
        return true;
    }

    public void Remove(BookmarkEntry entry)
    {
        _items.Remove(entry);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(AppPaths.BookmarksPath,
                JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppPaths.Log("bookmark save failed: " + ex.Message);
        }
    }
}
