using System.Text;

namespace PxBrowser;

internal static class AppPaths
{
    public static string Root { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lIl Browser");

    public static void Configure(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public static string SettingsPath => Path.Combine(Root, "settings.json");
    public static string HistoryPath => Path.Combine(Root, "history.json");
    public static string BookmarksPath => Path.Combine(Root, "bookmarks.json");
    public static string SessionPath => Path.Combine(Root, "session.json");
    public static string CleanExitPath => Path.Combine(Root, "clean.exit");
    public static string LogPath => Path.Combine(Root, "lilbrowser.log");
    public static string UdfPath => Path.Combine(Root, "WebView2");
    public static string PrivateUdfPath => Path.Combine(Root, "WebView2Private");

    public static void Ensure()
    {
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PxBrowser");
        if (Directory.Exists(legacy) && !Directory.Exists(Root))
        {
            Directory.CreateDirectory(Root);
            foreach (var name in new[] { "settings.json", "history.json", "bookmarks.json", "session.json" })
            {
                try
                {
                    var source = Path.Combine(legacy, name);
                    if (File.Exists(source)) File.Copy(source, Path.Combine(Root, name), false);
                }
                catch { }
            }
        }

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(UdfPath);
        Directory.CreateDirectory(PrivateUdfPath);
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // Logging must never crash the browser.
        }
    }
}
