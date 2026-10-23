using System.Text;

namespace PxBrowser;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppPaths.Log("thread exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppPaths.Log("unhandled: " + e.ExceptionObject);

        var dataDir = Arg(args, "--data-dir");
        if (!string.IsNullOrWhiteSpace(dataDir)) AppPaths.Configure(dataDir);
        AppPaths.Ensure();

        var privateMode = args.Contains("--private") || args.Contains("--private-smoke");
        var smokeUrl = Arg(args, "--smoke-url") ?? "http://127.0.0.1:8769/";
        var outDir = Arg(args, "--out") ?? Path.Combine(AppContext.BaseDirectory, "smoke-results");
        var restoreSmoke = args.Contains("--smoke-restore");
        var smoke = args.Contains("--smoke") || args.Contains("--private-smoke");

        Application.Run(new MainForm(privateMode, smoke, smokeUrl, outDir, restoreSmoke));
    }

    private static string? Arg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
