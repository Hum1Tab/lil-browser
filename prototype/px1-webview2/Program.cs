using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Px1WebView2;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var outDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(AppContext.BaseDirectory, "results");

        string? scenarioPath = null;
        var adblockMode = "none";
        var inPrivate = false;
        string? udfPath = null;
        var urls = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--scenario" && i + 1 < args.Length)
            {
                scenarioPath = Path.GetFullPath(args[++i]);
            }
            else if (args[i] == "--adblock" && i + 1 < args.Length)
            {
                adblockMode = args[++i].ToLowerInvariant();
            }
            else if (args[i] == "--inprivate")
            {
                inPrivate = true;
            }
            else if (args[i] == "--udf" && i + 1 < args.Length)
            {
                udfPath = Path.GetFullPath(args[++i]);
            }
            else if (!string.IsNullOrWhiteSpace(args[i]))
            {
                urls.Add(args[i]);
            }
        }

        Application.Run(new MainForm(outDir, urls.ToArray(), scenarioPath, adblockMode, inPrivate, udfPath));
    }
}

internal sealed class ScenarioStep
{
    public string? Name { get; set; }
    public string Url { get; set; } = "";
    public int WaitMs { get; set; } = 3000;
    public List<string> Actions { get; set; } = new();
    public int PostWaitMs { get; set; } = 2000;
}

internal sealed class MainForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly TextBox _address = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox _log = new() { Dock = DockStyle.Bottom, Height = 170, ReadOnly = true };
    private readonly StringBuilder _logBuffer = new();
    private readonly string _outDir;
    private readonly string[] _urls;
    private readonly string? _scenarioPath;
    private readonly string _adblockMode;
    private readonly bool _inPrivate;
    private readonly string? _udfPath;

    private TaskCompletionSource<bool>? _navTcs;
    private CoreWebView2WebErrorStatus _lastError = CoreWebView2WebErrorStatus.Unknown;
    private int _index = -1;
    private bool _automation;
    private long _requestCount;
    private long _blockedCount;
    private long _requestAtNavStart;
    private long _blockedAtNavStart;
    private readonly Stopwatch _navStopwatch = new();
    private long _lastNavMs;

    public MainForm(string outDir, string[] urls, string? scenarioPath, string adblockMode, bool inPrivate, string? udfPath)
    {
        _outDir = outDir;
        _urls = urls;
        _scenarioPath = scenarioPath;
        _adblockMode = adblockMode;
        _inPrivate = inPrivate;
        _udfPath = udfPath;
        _automation = urls.Length > 0 || scenarioPath != null;
        Directory.CreateDirectory(_outDir);

        Text = "PX1 WebView2 Probe";
        Width = 1280;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        var bar = new Panel { Dock = DockStyle.Top, Height = 36 };
        var go = new Button { Text = "Go", Dock = DockStyle.Right, Width = 60 };
        var shot = new Button { Text = "Shot", Dock = DockStyle.Right, Width = 60 };
        var probe = new Button { Text = "UVPAA", Dock = DockStyle.Right, Width = 70 };
        bar.Controls.Add(_address);
        bar.Controls.Add(go);
        bar.Controls.Add(shot);
        bar.Controls.Add(probe);

        Controls.Add(_web);
        Controls.Add(bar);
        Controls.Add(_log);

        go.Click += async (_, _) => await NavigateAsync(_address.Text);
        shot.Click += async (_, _) => await CaptureAsync(new Uri(_web.CoreWebView2.Source), "manual", new List<string>());
        probe.Click += async (_, _) => await ProbeUvpaaAsync();
        _address.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await NavigateAsync(_address.Text);
            }
        };

        _web.CoreWebView2InitializationCompleted += (_, e) =>
            Log($"init completed: success={e.IsSuccess} ex={e.InitializationException}");
        Shown += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            var udf = !string.IsNullOrWhiteSpace(_udfPath)
                ? _udfPath!
                : Path.Combine(_outDir, "udf-" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(udf);
            if (_inPrivate)
            {
                _web.CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = udf,
                    IsInPrivateModeEnabled = true,
                };
                await _web.EnsureCoreWebView2Async();
            }
            else
            {
                var env = await CoreWebView2Environment.CreateAsync(null, udf, null);
                await _web.EnsureCoreWebView2Async(env);
            }

            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            ConfigureAdblock(core);

            core.PermissionRequested += OnPermissionRequested;
            core.ProcessFailed += (_, e) => Log($"ProcessFailed: {e.ProcessFailedKind} reason={e.Reason}");
            core.NewWindowRequested += (_, e) =>
            {
                Log($"NewWindowRequested: {e.Uri}");
                e.Handled = true;
            };
            core.DownloadStarting += (_, e) =>
            {
                Log($"DownloadStarting: {e.DownloadOperation.Uri}");
                e.Cancel = true;
            };
            core.NavigationStarting += (_, e) => Log($"NavigationStarting: {e.Uri}");
            core.NavigationCompleted += OnNavigationCompleted;

            Log($"runtime={core.Environment.BrowserVersionString}");
            Log($"udf={udf}");
            Log($"adblock={_adblockMode}");
            Log($"inprivate={_inPrivate} profileInPrivate={core.Profile.IsInPrivateModeEnabled}");
            Log($"mode={(_scenarioPath != null ? "scenario" : _automation ? "sequence" : "interactive")}");

            if (_scenarioPath != null)
            {
                await RunScenarioAsync();
                return;
            }

            if (!_automation)
            {
                _address.Text = "https://example.com/";
                await NavigateAsync(_address.Text);
                return;
            }

            await RunSequenceAsync();
        }
        catch (Exception ex)
        {
            Log("FATAL: " + ex);
            WriteLog();
            Close();
        }
    }

    private void ConfigureAdblock(CoreWebView2 core)
    {
        switch (_adblockMode)
        {
            case "all":
            case "all-allow":
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnWebResourceRequested;
                break;
            case "hybrid":
                core.AddWebResourceRequestedFilter("http://127.0.0.1:8765/ad/*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnWebResourceRequested;
                break;
            case "engine":
                try
                {
                    core.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;
                }
                catch (Exception ex)
                {
                    Log("tracking prevention set failed: " + ex.Message);
                }
                break;
            default:
                break;
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        Interlocked.Increment(ref _requestCount);
        var uri = e.Request.Uri ?? "";
        if ((_adblockMode == "all" || _adblockMode == "hybrid") &&
            uri.Contains("/ad/", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _blockedCount);
            try
            {
                var empty = new MemoryStream(Array.Empty<byte>());
                e.Response = _web.CoreWebView2.Environment.CreateWebResourceResponse(
                    empty, 204, "No Content", "");
            }
            catch (Exception ex)
            {
                Log("block response failed: " + ex.Message);
            }
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        Log($"PermissionRequested: {e.PermissionKind} origin={e.Uri} -> Deny");
        e.State = CoreWebView2PermissionState.Deny;
        e.Handled = true;
    }

    private async Task RunSequenceAsync()
    {
        for (var i = 0; i < _urls.Length; i++)
        {
            _index = i;
            var url = _urls[i];
            _address.Text = url;
            Log($"=== [{i + 1}/{_urls.Length}] {url}");
            await NavigateAsync(url);
        }

        WriteLog();
        Log("sequence done");
        await Task.Delay(500);
        Close();
    }

    private async Task RunScenarioAsync()
    {
        var json = await File.ReadAllTextAsync(_scenarioPath!);
        var steps = JsonSerializer.Deserialize<List<ScenarioStep>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            _index = i;
            _address.Text = step.Url;
            Log($"=== [{i + 1}/{steps.Count}] {step.Name ?? step.Url}");

            await NavigateOnlyAsync(step.Url, step.WaitMs);
            var actionResults = new List<string>();
            foreach (var action in step.Actions)
            {
                var result = await EvalUserGestureAsync(_web.CoreWebView2, action);
                actionResults.Add(result);
                Log("action: " + result);
            }

            if (step.PostWaitMs > 0)
            {
                await Task.Delay(step.PostWaitMs);
            }

            await CaptureAsync(new Uri(_web.CoreWebView2.Source), step.Name ?? $"step{i + 1}", actionResults);
        }

        WriteLog();
        Log("scenario done");
        await Task.Delay(500);
        Close();
    }

    private async Task NavigateAsync(string url)
    {
        await NavigateOnlyAsync(url, 3000);
        await CaptureAsync(new Uri(_web.CoreWebView2.Source), null, new List<string>());
    }

    private async Task NavigateOnlyAsync(string url, int settleMs)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Log($"invalid url: {url}");
            return;
        }

        var core = _web.CoreWebView2;
        _navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _lastError = CoreWebView2WebErrorStatus.Unknown;
        _requestAtNavStart = Interlocked.Read(ref _requestCount);
        _blockedAtNavStart = Interlocked.Read(ref _blockedCount);
        _lastNavMs = 0;
        _navStopwatch.Restart();

        try
        {
            core.Navigate(uri.ToString());
        }
        catch (Exception ex)
        {
            Log("Navigate failed: " + ex.Message);
            _navTcs.TrySetResult(false);
        }

        var completed = await Task.WhenAny(_navTcs.Task, Task.Delay(TimeSpan.FromSeconds(45)));
        if (completed != _navTcs.Task)
        {
            Log("navigation timeout");
            _navStopwatch.Stop();
            _lastNavMs = _navStopwatch.ElapsedMilliseconds;
        }

        if (settleMs > 0)
        {
            await Task.Delay(settleMs);
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navStopwatch.Stop();
        _lastNavMs = _navStopwatch.ElapsedMilliseconds;
        _lastError = e.WebErrorStatus;
        Log($"NavigationCompleted: success={e.IsSuccess} error={e.WebErrorStatus} http={e.HttpStatusCode} navMs={_lastNavMs}");
        _navTcs?.TrySetResult(e.IsSuccess);
    }

    private async Task ProbeUvpaaAsync()
    {
        var core = _web.CoreWebView2;
        var result = await EvalAsync(core,
            "window.__px1={}; " +
            "if (typeof PublicKeyCredential!=='undefined' && PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable) {" +
            "  PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()" +
            "    .then(v=>window.__px1.uvpaa=v)" +
            "    .catch(e=>window.__px1.uvpaa='err:'+e);" +
            "} else { window.__px1.uvpaa='no-api'; } 'started'");
        Log("UVPAA probe: " + result);
        await Task.Delay(1500);
        var value = await EvalAsync(core, "JSON.stringify(window.__px1||{})");
        Log("UVPAA value: " + Unquote(value));
    }

    private async Task CaptureAsync(Uri requested, string? name, List<string> actionResults)
    {
        var core = _web.CoreWebView2;

        var ua = Unquote(await EvalAsync(core, "navigator.userAgent"));
        var chrome = Unquote(await EvalAsync(core, "typeof window.chrome"));
        var chromeWebview = Unquote(await EvalAsync(core,
            "typeof window.chrome!=='undefined' && typeof window.chrome.webview!=='undefined' ? 'yes':'no'"));
        var title = Unquote(await EvalAsync(core, "document.title"));
        var href = Unquote(await EvalAsync(core, "location.href"));
        var body = Unquote(await EvalAsync(core,
            "document.body ? document.body.innerText.slice(0,8000) : ''"));
        var dom = Unquote(await EvalAsync(core,
            "document.documentElement ? document.documentElement.outerHTML : ''"));
        var perfJson = Unquote(await EvalAsync(core,
            "JSON.stringify((()=>{const n=performance.getEntriesByType('navigation')[0]||{};return {duration:n.duration||0,loadEventEnd:n.loadEventEnd||0,domContentLoaded:n.domContentLoadedEventEnd||0,resources:performance.getEntriesByType('resource').length,images:document.images.length};})())"));

        await EvalAsync(core,
            "window.__px1={}; " +
            "if (typeof PublicKeyCredential!=='undefined' && PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable) {" +
            "  PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()" +
            "    .then(v=>window.__px1.uvpaa=v)" +
            "    .catch(e=>window.__px1.uvpaa='err:'+e);" +
            "} else { window.__px1.uvpaa='no-api'; } 'started'");
        await Task.Delay(1500);
        var uvpaaJson = Unquote(await EvalAsync(core, "JSON.stringify(window.__px1||{})"));

        var safe = new string((requested.Host + requested.AbsolutePath)
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (safe.Length > 70) safe = safe[..70];
        var label = string.IsNullOrWhiteSpace(name) ? "" : "_" + new string(name!.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var baseName = $"{_index + 1:00}_{safe}{label}_{DateTime.Now:HHmmss}";

        var png = Path.Combine(_outDir, baseName + ".png");
        try
        {
            await using var fs = File.Create(png);
            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
        }
        catch (Exception ex)
        {
            Log("capture failed: " + ex.Message);
        }

        var htmlPath = Path.Combine(_outDir, baseName + ".html");
        try
        {
            await File.WriteAllTextAsync(htmlPath, dom);
        }
        catch (Exception ex)
        {
            Log("dom save failed: " + ex.Message);
        }

        var record = new
        {
            name,
            requested = requested.ToString(),
            finalUrl = href,
            title,
            userAgent = ua,
            chrome,
            chromeWebview,
            uvpaa = uvpaaJson,
            navError = _lastError.ToString(),
            adblockMode = _adblockMode,
            navMs = _lastNavMs,
            requests = Interlocked.Read(ref _requestCount) - _requestAtNavStart,
            blocked = Interlocked.Read(ref _blockedCount) - _blockedAtNavStart,
            performance = perfJson,
            bodyText = body,
            actionResults,
            screenshot = png,
            dom = htmlPath,
            timestamp = DateTimeOffset.Now.ToString("O"),
        };

        var jsonPath = Path.Combine(_outDir, baseName + ".json");
        await File.WriteAllTextAsync(jsonPath,
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));

        Log($"saved {Path.GetFileName(jsonPath)} ua={ua}");
        Log($"  webview={chromeWebview} uvpaa={uvpaaJson} err={_lastError}");
        _logBuffer.AppendLine(
            $"[{_index + 1}] {requested} -> {href} | {title} | {_lastError} | webview={chromeWebview} | uvpaa={uvpaaJson}");
    }

    private static async Task<string> EvalAsync(CoreWebView2 core, string script)
    {
        try
        {
            return await core.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize("ERR:" + ex.Message);
        }
    }

    private static async Task<string> EvalUserGestureAsync(CoreWebView2 core, string script)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                expression = script,
                userGesture = true,
                awaitPromise = true,
                returnByValue = true,
            });
            var raw = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", payload);
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("value", out var value))
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
            }
            return raw;
        }
        catch (Exception ex)
        {
            return "ERR:" + ex.Message;
        }
    }

    private static string Unquote(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        try
        {
            return JsonSerializer.Deserialize<string>(json) ?? json;
        }
        catch
        {
            return json;
        }
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        _logBuffer.AppendLine(line);
        _log.AppendText(line + Environment.NewLine);
        Debug.WriteLine(line);
    }

    private void WriteLog()
    {
        try
        {
            File.WriteAllText(Path.Combine(_outDir, "px1.log"), _logBuffer.ToString());
        }
        catch
        {
            // best effort
        }
    }
}
