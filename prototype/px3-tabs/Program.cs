using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text.Json;

namespace Px3WebView2;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var outDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(AppContext.BaseDirectory, "results");

        var tabs = 5;
        var url = "http://127.0.0.1:8766/";
        var holdSeconds = 30;
        var startupOnly = false;
        string? runId = null;
        string? udfPath = null;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--tabs" when i + 1 < args.Length:
                    tabs = int.Parse(args[++i]);
                    break;
                case "--url" when i + 1 < args.Length:
                    url = args[++i];
                    break;
                case "--hold" when i + 1 < args.Length:
                    holdSeconds = int.Parse(args[++i]);
                    break;
                case "--startup-only":
                    startupOnly = true;
                    break;
                case "--run" when i + 1 < args.Length:
                    runId = args[++i];
                    break;
                case "--udf" when i + 1 < args.Length:
                    udfPath = args[++i];
                    break;
            }
        }

        Application.Run(new MainForm(outDir, tabs, url, holdSeconds, startupOnly, runId, udfPath));
    }
}

internal sealed class MainForm : Form
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly string _outDir;
    private readonly int _tabCount;
    private readonly string _url;
    private readonly int _holdSeconds;
    private readonly bool _startupOnly;
    private readonly string? _runId;
    private readonly string? _udfPath;
    private readonly List<WebView2> _webs = new();

    public MainForm(string outDir, int tabCount, string url, int holdSeconds, bool startupOnly, string? runId, string? udfPath)
    {
        _outDir = outDir;
        _tabCount = tabCount;
        _url = url;
        _holdSeconds = holdSeconds;
        _startupOnly = startupOnly;
        _runId = runId;
        _udfPath = udfPath;
        Directory.CreateDirectory(_outDir);

        Text = $"PX3 WebView2 ({tabCount} tabs)";
        Width = 1280;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_tabs);
        Shown += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var process = Process.GetCurrentProcess();
        var startedAt = process.StartTime;
        var udf = GetUdf();
        Directory.CreateDirectory(udf);

        var loadMs = new List<long>();
        var failed = new List<string>();

        if (_startupOnly)
        {
            await RunStartupOnlyAsync(process, startedAt, udf);
            return;
        }

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, udf, null);

            for (var i = 0; i < _tabCount; i++)
            {
                var page = new TabPage($"Tab {i + 1}") { Padding = new Padding(0) };
                var web = new WebView2 { Dock = DockStyle.Fill };
                page.Controls.Add(web);
                _tabs.TabPages.Add(page);
                _tabs.SelectedTab = page;
                await Task.Delay(80);

                await web.EnsureCoreWebView2Async(env);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                web.NavigationCompleted += (_, e) =>
                {
                    if (web.Source?.AbsoluteUri.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        tcs.TrySetResult(e.IsSuccess);
                    }
                };

                var sw = Stopwatch.StartNew();
                web.CoreWebView2.Navigate(BuildUrl(i + 1));
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
                sw.Stop();
                if (completed != tcs.Task)
                {
                    failed.Add($"tab{i + 1}:timeout");
                }
                loadMs.Add(sw.ElapsedMilliseconds);
                _webs.Add(web);
            }

            _tabs.SelectedIndex = 0;
            await Task.Delay(1000);

            var switchMs = new List<long>();
            var probeCount = Math.Min(_tabCount, 5);
            for (var i = 0; i < probeCount; i++)
            {
                _tabs.SelectedIndex = i;
                await Task.Delay(30);
                var sw = Stopwatch.StartNew();
                try
                {
                    await _webs[i].CoreWebView2.ExecuteScriptAsync("document.title");
                }
                catch
                {
                    // best effort
                }
                sw.Stop();
                switchMs.Add(sw.ElapsedMilliseconds);
            }

            _tabs.SelectedIndex = 0;
            var readyAt = DateTime.Now;
            var result = new
            {
                pid = Environment.ProcessId,
                udf,
                tabs = _tabCount,
                url = _url,
                startupOnly = false,
                run = _runId,
                startupMs = (readyAt - startedAt).TotalMilliseconds,
                startedAt = startedAt.ToString("O"),
                readyAt = readyAt.ToString("O"),
                loadMs,
                switchMs,
                failed,
                hostPrivateBytes = process.PrivateMemorySize64,
                hostWorkingSet = process.WorkingSet64,
            };

            await File.WriteAllTextAsync(
                Path.Combine(_outDir, "ready.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

            await Task.Delay(TimeSpan.FromSeconds(_holdSeconds));

            await File.WriteAllTextAsync(
                Path.Combine(_outDir, "done.json"),
                JsonSerializer.Serialize(new { pid = Environment.ProcessId, at = DateTimeOffset.Now.ToString("O") },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(
                Path.Combine(_outDir, "error.txt"),
                ex.ToString());
        }
        finally
        {
            Close();
        }
    }

    private string BuildUrl(int tab)
    {
        var sep = _url.Contains('?') ? '&' : '?';
        var url = $"{_url}{sep}tab={tab}";
        if (!string.IsNullOrWhiteSpace(_runId))
        {
            url += $"&run={Uri.EscapeDataString(_runId)}";
        }
        return url;
    }

    private string GetUdf()
    {
        return !string.IsNullOrWhiteSpace(_udfPath)
            ? _udfPath!
            : Path.Combine(_outDir, "udf");
    }

    private async Task RunStartupOnlyAsync(Process process, DateTime startedAt, string udf)
    {
        var loadMs = new List<long>();
        var failed = new List<string>();
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, udf, null);

            _tabs.TabPages.Add(new TabPage("Tab 1") { Padding = new Padding(0) });

            var page = _tabs.TabPages[0];
            var web = new WebView2 { Dock = DockStyle.Fill };
            page.Controls.Add(web);
            _tabs.SelectedTab = page;
            await Task.Delay(80);

            await web.EnsureCoreWebView2Async(env);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            web.NavigationCompleted += (_, e) =>
            {
                if (web.Source?.AbsoluteUri.StartsWith("http", StringComparison.OrdinalIgnoreCase) == true)
                {
                    tcs.TrySetResult(e.IsSuccess);
                }
            };

            var sw = Stopwatch.StartNew();
            web.CoreWebView2.Navigate(BuildUrl(1));
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            sw.Stop();
            if (completed != tcs.Task)
            {
                failed.Add("tab1:timeout");
            }
            loadMs.Add(sw.ElapsedMilliseconds);
            _webs.Add(web);
            _tabs.SelectedIndex = 0;

            var readyAt = DateTime.Now;
            var result = new
            {
                pid = Environment.ProcessId,
                udf,
                tabs = _tabCount,
                url = _url,
                startupOnly = true,
                run = _runId,
                startupMs = (readyAt - startedAt).TotalMilliseconds,
                startedAt = startedAt.ToString("O"),
                readyAt = readyAt.ToString("O"),
                loadMs,
                switchMs = Array.Empty<long>(),
                failed,
                hostPrivateBytes = process.PrivateMemorySize64,
                hostWorkingSet = process.WorkingSet64,
            };

            await File.WriteAllTextAsync(
                Path.Combine(_outDir, "ready.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

            await Task.Delay(TimeSpan.FromSeconds(_holdSeconds));

            await File.WriteAllTextAsync(
                Path.Combine(_outDir, "done.json"),
                JsonSerializer.Serialize(new { pid = Environment.ProcessId, at = DateTimeOffset.Now.ToString("O") },
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(Path.Combine(_outDir, "error.txt"), ex.ToString());
        }
        finally
        {
            Close();
        }
    }
}
