using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Px6Browser;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var outDir = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(AppContext.BaseDirectory, "results");
        var demo = args.Contains("--demo");
        var hold = 0;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--hold" && i + 1 < args.Length) hold = int.Parse(args[++i]);
        }
        Application.Run(new MainForm(outDir, demo, hold));
    }
}

internal sealed record HistoryEntry(string Url, string Title, DateTime At);

internal sealed class MainForm : Form
{
    private readonly string _outDir;
    private readonly bool _demo;
    private readonly int _holdSeconds;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
    private readonly ListBox _historyList = new() { Dock = DockStyle.Fill };
    private readonly ToolStripTextBox _address = new() { Width = 620, AutoSize = false };
    private readonly ToolStripStatusLabel _status = new("Ready");
    private readonly List<HistoryEntry> _history = new();
    private readonly Dictionary<TabPage, WebView2> _views = new();
    private readonly StringBuilder _log = new();
    private readonly string _logPath;
    private CoreWebView2Environment? _env;
    private int _tabSeq;

    public MainForm(string outDir, bool demo, int holdSeconds)
    {
        _outDir = outDir;
        _demo = demo;
        _holdSeconds = holdSeconds;
        Directory.CreateDirectory(_outDir);
        _logPath = Path.Combine(_outDir, "px6.log");

        Text = "PX6 Browser — UI prototype";
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var strip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
        var back = new ToolStripButton("←") { ToolTipText = "戻る" };
        var forward = new ToolStripButton("→") { ToolTipText = "進む" };
        var reload = new ToolStripButton("⟳") { ToolTipText = "再読み込み" };
        var home = new ToolStripButton("⌂") { ToolTipText = "ホーム" };
        var newTab = new ToolStripButton("＋") { ToolTipText = "新しいタブ" };
        var closeTab = new ToolStripButton("×") { ToolTipText = "タブを閉じる" };
        var menu = new ToolStripButton("☰") { ToolTipText = "メニュー" };
        var go = new ToolStripButton("Go");

        back.Click += (_, _) => CurrentCore()?.GoBack();
        forward.Click += (_, _) => CurrentCore()?.GoForward();
        reload.Click += (_, _) => CurrentCore()?.Reload();
        home.Click += (_, _) => NavigateCurrent("https://example.com/");
        newTab.Click += async (_, _) => await NewTabAsync("https://example.com/");
        closeTab.Click += (_, _) => CloseCurrentTab();
        go.Click += (_, _) => NavigateCurrent(_address.Text);
        _address.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateCurrent(_address.Text);
            }
        };

        var historyButton = new ToolStripButton("履歴") { ToolTipText = "履歴" };
        historyButton.Click += (_, _) => ToggleHistory();

        var settingsButton = new ToolStripButton("設定") { ToolTipText = "設定" };
        settingsButton.Click += (_, _) => ShowSettings();

        strip.Items.AddRange(new ToolStripItem[]
        {
            back, forward, reload, home, new ToolStripSeparator(),
            _address, go, new ToolStripSeparator(),
            newTab, closeTab, historyButton, settingsButton, menu
        });

        var context = new ContextMenuStrip();
        context.Items.Add("新しいタブ", null, async (_, _) => await NewTabAsync("https://example.com/"));
        context.Items.Add("タブを閉じる", null, (_, _) => CloseCurrentTab());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("履歴を表示", null, (_, _) => ToggleHistory());
        context.Items.Add("設定", null, (_, _) => ShowSettings());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("終了", null, (_, _) => Close());
        menu.Click += (_, _) => context.Show(strip, new Point(strip.Width - 160, strip.Height));

        _split.Panel1.Controls.Add(_tabs);
        var sideHeader = new Label
        {
            Text = "履歴",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        _split.Panel2.Controls.Add(_historyList);
        _split.Panel2.Controls.Add(sideHeader);
        _split.Panel2Collapsed = true;
        _historyList.DoubleClick += (_, _) =>
        {
            if (_historyList.SelectedIndex >= 0 && _historyList.SelectedIndex < _history.Count)
            {
                NavigateCurrent(_history[_historyList.SelectedIndex].Url);
            }
        };

        var status = new StatusStrip();
        status.Items.Add(_status);
        Controls.Add(_split);
        Controls.Add(strip);
        Controls.Add(status);

        _tabs.SelectedIndexChanged += (_, _) => SyncAddress();
        KeyDown += OnKeyDown;
        Shown += async (_, _) => await StartAsync();
        FormClosing += (_, _) =>
        {
            foreach (var web in _views.Values)
            {
                try { web.Dispose(); } catch { }
            }
            File.WriteAllText(_logPath, _log.ToString());
        };
    }

    private async Task StartAsync()
    {
        try
        {
            var udf = Path.Combine(_outDir, "udf");
            _env = await CoreWebView2Environment.CreateAsync(null, udf, null);
            await NewTabAsync("https://example.com/");
            Log("started");

            if (_demo)
            {
                await Task.Delay(2500);
                await NewTabAsync("https://www.wikipedia.org/");
                await Task.Delay(2500);
                _tabs.SelectedIndex = 0;
                _split.Panel2Collapsed = false;
                await Task.Delay(1000);
                await CaptureAsync();
                Close();
            }
            else if (_holdSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_holdSeconds));
            }
        }
        catch (Exception ex)
        {
            Log("start failed: " + ex);
            _status.Text = "起動失敗: " + ex.Message;
        }
    }

    private async Task NewTabAsync(string url)
    {
        if (_env == null) return;
        var page = new TabPage($"New Tab {++_tabSeq}") { Padding = new Padding(0) };
        var web = new WebView2 { Dock = DockStyle.Fill };
        page.Controls.Add(web);
        _tabs.TabPages.Add(page);
        _views[page] = web;
        _tabs.SelectedTab = page;

        await web.EnsureCoreWebView2Async(_env);
        var core = web.CoreWebView2;
        core.NavigationStarting += (_, e) => _status.Text = "読み込み中: " + e.Uri;
        core.NavigationCompleted += (_, e) =>
        {
            if (e.IsSuccess)
            {
                AddHistory(web.Source?.AbsoluteUri ?? "", core.DocumentTitle);
                _status.Text = "完了: " + core.DocumentTitle;
            }
            else
            {
                _status.Text = "失敗: " + e.WebErrorStatus;
            }
        };
        core.SourceChanged += (_, _) => { if (_tabs.SelectedTab == page) SyncAddress(); };
        core.DocumentTitleChanged += (_, _) => page.Text = Truncate(core.DocumentTitle, 24);
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = NewTabAsync(e.Uri);
        };
        core.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            Log($"permission denied: {e.PermissionKind} {e.Uri}");
            _status.Text = "権限を拒否: " + e.PermissionKind;
        };
        core.DownloadStarting += (_, e) =>
        {
            e.Cancel = true;
            Log($"download cancelled: {e.DownloadOperation.Uri}");
            _status.Text = "ダウンロードを中止: " + e.DownloadOperation.Uri;
        };

        core.Navigate(url);
        SyncAddress();
    }

    private void NavigateCurrent(string input)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0) return;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        CurrentCore()?.Navigate(text);
    }

    private CoreWebView2? CurrentCore()
    {
        if (_tabs.SelectedTab is { } page && _views.TryGetValue(page, out var web) && web.CoreWebView2 != null)
        {
            return web.CoreWebView2;
        }
        return null;
    }

    private void CloseCurrentTab()
    {
        if (_tabs.SelectedTab is not { } page) return;
        if (_views.Remove(page, out var web))
        {
            try { web.Dispose(); } catch { }
        }
        _tabs.TabPages.Remove(page);
        page.Dispose();
        if (_tabs.TabCount == 0)
        {
            _ = NewTabAsync("https://example.com/");
        }
    }

    private void SyncAddress()
    {
        if (_tabs.SelectedTab is { } page && _views.TryGetValue(page, out var web))
        {
            var src = web.Source?.AbsoluteUri ?? "";
            if (!_address.Focused) _address.Text = src;
        }
    }

    private void AddHistory(string url, string title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (_history.Count > 0 && _history[^1].Url == url) return;
        _history.Add(new HistoryEntry(url, title, DateTime.Now));
        _historyList.Items.Add($"{DateTime.Now:HH:mm}  {Truncate(title, 40)}  —  {Truncate(url, 60)}");
    }

    private void ToggleHistory()
    {
        _split.Panel2Collapsed = !_split.Panel2Collapsed;
        if (!_split.Panel2Collapsed) _split.SplitterDistance = Math.Max(400, _split.Width - 340);
    }

    private void ShowSettings()
    {
        using var dlg = new Form
        {
            Text = "設定（プロトタイプ）",
            Width = 460,
            Height = 300,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var adblock = new CheckBox { Text = "広告遮断（未実装）", Left = 20, Top = 20, Width = 400 };
        var privateMode = new CheckBox { Text = "プライベートモード（未実装）", Left = 20, Top = 50, Width = 400 };
        var clear = new CheckBox { Text = "終了時に履歴を削除", Left = 20, Top = 80, Width = 400 };
        var note = new Label
        {
            Text = "この画面は UI のクリック可能なプロトタイプです。\n設定の永続化と実際の遮断は未実装です。",
            Left = 20,
            Top = 115,
            Width = 410,
            Height = 50
        };
        var ok = new Button { Text = "OK", Left = 250, Top = 190, Width = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "キャンセル", Left = 340, Top = 190, Width = 90, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange(new Control[] { adblock, privateMode, clear, note, ok, cancel });
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        dlg.ShowDialog(this);
        Log($"settings: adblock={adblock.Checked} private={privateMode.Checked} clear={clear.Checked}");
    }

    private async Task CaptureAsync()
    {
        try
        {
            using var bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(_outDir, "ui-chrome.png"));
        }
        catch (Exception ex) { Log("chrome capture failed: " + ex.Message); }

        try
        {
            if (CurrentCore() is { } core)
            {
                await using var fs = File.Create(Path.Combine(_outDir, "ui-page.png"));
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            }
        }
        catch (Exception ex) { Log("page capture failed: " + ex.Message); }

        var controls = CountControls(this);
        var result = new
        {
            tabs = _tabs.TabCount,
            history = _history.Count,
            controls,
            title = Text,
            at = DateTimeOffset.Now.ToString("O"),
        };
        await File.WriteAllTextAsync(
            Path.Combine(_outDir, "ui.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Log($"captured: tabs={result.tabs} history={result.history} controls={controls}");
    }

    private static int CountControls(Control root)
    {
        var n = 1;
        foreach (Control c in root.Controls) n += CountControls(c);
        return n;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.T)
        {
            _ = NewTabAsync("https://example.com/");
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.W)
        {
            CloseCurrentTab();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.L)
        {
            _address.Focus();
            _address.SelectAll();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            CurrentCore()?.Reload();
            e.SuppressKeyPress = true;
        }
        else if (e.Alt && e.KeyCode == Keys.Left)
        {
            CurrentCore()?.GoBack();
            e.SuppressKeyPress = true;
        }
        else if (e.Alt && e.KeyCode == Keys.Right)
        {
            CurrentCore()?.GoForward();
            e.SuppressKeyPress = true;
        }
    }

    private void Log(string message)
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
    }

    private static string Truncate(string? s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= n ? s : s[..n] + "…";
    }
}
