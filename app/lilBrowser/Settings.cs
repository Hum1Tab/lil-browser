using System.Text.Json;

namespace PxBrowser;

internal sealed class Settings
{
    public const string DefaultHomepage = "https://www.google.com/";

    public string Homepage { get; set; } = DefaultHomepage;
    public bool AdblockEnabled { get; set; } = true;
    public bool StrictTracking { get; set; } = true;
    public bool ClearOnExit { get; set; }
    public bool RestoreSession { get; set; } = true;
    public string SearchEngine { get; set; } = "https://www.google.com/search?q=";
    public string Theme { get; set; } = "dark";
    public Dictionary<string, string> PermissionDecisions { get; set; } = new();

    public static Settings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.SettingsPath));
                if (loaded != null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.Homepage) ||
                        !UrlPolicy.IsAllowedTypedUrl(loaded.Homepage) ||
                        loaded.Homepage.TrimEnd('/').Equals("https://example.com", StringComparison.OrdinalIgnoreCase))
                    {
                        loaded.Homepage = DefaultHomepage;
                    }
                    if (string.IsNullOrWhiteSpace(loaded.SearchEngine) ||
                        !UrlPolicy.IsAllowedTypedUrl(loaded.SearchEngine) ||
                        loaded.SearchEngine.TrimEnd('/').Equals("https://duckduckgo.com/?q=", StringComparison.OrdinalIgnoreCase))
                    {
                        loaded.SearchEngine = "https://www.google.com/search?q=";
                    }
                    if (loaded.Theme is not ("dark" or "light")) loaded.Theme = "dark";
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("settings load failed: " + ex.Message);
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(AppPaths.SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppPaths.Log("settings save failed: " + ex.Message);
        }
    }
}
