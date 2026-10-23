using System.Runtime.InteropServices;
using System.Text;

namespace PxBrowser;

internal static class AppPaths
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);

    private const int MoveFileDelayUntilReboot = 4;

    private static readonly string DefaultRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lIl Browser");

    public static string Root { get; private set; } = DefaultRoot;

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
        if (Root == DefaultRoot && Directory.Exists(legacy) && !Directory.Exists(Root))
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
        CleanupStalePrivateProfiles();
    }

    public static string CreatePrivateProfilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"lIlBrowser-private-{Guid.NewGuid():N}");
    }

    public static void DeletePrivateProfile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch
            {
                Thread.Sleep(250);
            }
        }

        // If a WebView2 process still holds files, ask Windows to remove the
        // directory tree at the next reboot rather than silently keeping it.
        try { MoveFileEx(path, null, MoveFileDelayUntilReboot); } catch { }
    }

    private static void CleanupStalePrivateProfiles()
    {
        try
        {
            foreach (var path in Directory.GetDirectories(Path.GetTempPath(), "lIlBrowser-private-*"))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddMinutes(-10))
                    {
                        DeletePrivateProfile(path);
                    }
                }
                catch { }
            }
        }
        catch { }
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
