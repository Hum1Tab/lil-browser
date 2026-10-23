using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PxBrowser;

internal sealed class MainForm : Form
{
    private readonly bool _private;
    private readonly bool _smoke;
    private readonly string _smokeUrl;
    private readonly string _outDir;
    private readonly bool _restoreSmoke;

    private readonly PxTabHost _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabBarPanel _tabBar = new() { Dock = DockStyle.Top, Height = 40 };
    private int TabWidth
    {
        get
        {
            var count = Math.Max(1, _tabs.TabCount);
            var max = _compact ? 160 : 200;
            var available = Math.Max(0, _tabBar.Width - _windowButtonWidth * 3 - 44);
            return Math.Clamp(available / count, 70, max);
        }
    }
    private bool _compact;
    private readonly RoundedAddressHost _address = new() { Width = 620, AutoSize = false };
    private readonly ToolStripStatusLabel _status = new("") { Spring = true };
    private readonly ToolStripButton _back = new("\uE72B") { ToolTipText = "戻る" };
    private readonly ToolStripButton _forward = new("\uE72A") { ToolTipText = "進む" };
    private readonly ToolStripButton _reload = new("\uE72C") { ToolTipText = "再読み込み" };
    private readonly ToolStripButton _stop = new("\uE711") { ToolTipText = "停止", Enabled = false };
    private readonly ToolStripButton _bookmark = new("\uE734") { ToolTipText = "ブックマーク (Ctrl+D)" };
    private ToolStripButton _home = null!;
    private ToolStripButton _historyButton = null!;
    private ToolStripButton _settingsButton = null!;
    private ToolStripButton _menuButton = null!;
    private ToolStripButton _updateButton = null!;
    private UpdateInfo? _updateInfo;
    private System.Windows.Forms.Timer? _updateTimer;
    private bool _hoverTabPlus;
    private int _hoverTabIndex = -1;
    private BookmarkStore _bookmarks = BookmarkStore.Load();
    private readonly List<DownloadItem> _downloads = new();
    private readonly Dictionary<BrowserTab, WebView2> _views = new();
    private readonly Panel _findPanel = new() { Dock = DockStyle.Bottom, Height = 38, Visible = false };
    private readonly TextBox _findBox = new() { Width = 320 };
    private readonly Label _findLabel = new() { Text = "", AutoSize = true, Padding = new Padding(6, 8, 0, 0) };

    private ToolStrip _strip = null!;
    private StatusStrip _statusStrip = null!;
    private ContextMenuStrip _menu = null!;
    private System.Windows.Forms.Timer _sessionTimer = null!;
    private System.Windows.Forms.Timer _statusTimer = null!;
    private bool _fullscreen;
    private bool _manualMaximized;
    private Rectangle _manualMaxPrevBounds;
    private bool _htmlFullscreen;
    private Rectangle _htmlPrevBounds;
    private FormWindowState _htmlPrevState;
    private FormBorderStyle _htmlPrevBorder;
    private bool _htmlPrevTopMost;
    private readonly Stack<string> _closedTabs = new();
    private FormBorderStyle _prevBorder;
    private FormWindowState _prevState;
    private const double ZoomStep = 0.1;
    private string NewTabUrl
    {
        get
        {
            var search = _settings.SearchEngine ?? "";
            if (search.Contains("google.", StringComparison.OrdinalIgnoreCase))
                return "https://www.google.com/";
            if (search.Contains("duckduckgo.", StringComparison.OrdinalIgnoreCase))
                return "https://duckduckgo.com/";
            if (search.Contains("bing.", StringComparison.OrdinalIgnoreCase))
                return "https://www.bing.com/";
            return _settings.Homepage;
        }
    }


    private readonly Font _tabFont = new("Yu Gothic UI", 9F);
    private readonly Font _tabFontBold = new("Yu Gothic UI", 9F, FontStyle.Bold);
    private readonly Font _windowFont = new("Segoe Fluent Icons", 10F);
    private readonly Font _iconFont = new("Segoe Fluent Icons", 11.5F);
    private const int WindowButtonWidth = 46;
    private int _windowButtonWidth = WindowButtonWidth;
    private WindowControl _hoverWindowControl;
    private enum WindowControl { None, Minimize, Maximize, Close }

    private Settings _settings = new();
    private List<HistoryEntry> _history = new();
    private Adblock _adblock = new(true);
    private CoreWebView2Environment? _env;
    private string _udf = "";
    private bool _allowClose;

    public MainForm(bool privateMode, bool smoke, string smokeUrl, string outDir, bool restoreSmoke = false)
    {
        _private = privateMode;
        _smoke = smoke;
        _smokeUrl = smokeUrl;
        _outDir = outDir;
        _restoreSmoke = restoreSmoke;

        Text = _private ? "lIl Browser — Private" : "lIl Browser";
        FormBorderStyle = FormBorderStyle.None;
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        MinimumSize = new Size(800, 500);

        BuildUi();
        _sessionTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _sessionTimer.Tick += (_, _) =>
        {
            _sessionTimer.Stop();
            SaveSession();
        };
        _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            _status.Text = "";
        };
        Shown += async (_, _) => await StartAsync();
        FormClosing += MainForm_FormClosing;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    private const int GwlStyle = -16;
    private const int WsThickFrame = 0x00040000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int HtCaption = 2;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x00040000; // WS_THICKFRAME: resize
            cp.Style |= 0x00020000; // WS_MINIMIZEBOX
            cp.Style |= 0x00010000; // WS_MAXIMIZEBOX
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != WmNcHitTest || WindowState != FormWindowState.Normal || _fullscreen || _htmlFullscreen || _manualMaximized)
        {
            if (m.Msg == WmNcHitTest && (_fullscreen || _htmlFullscreen || _manualMaximized || WindowState != FormWindowState.Normal))
                m.Result = (IntPtr)1; // HTCLIENT: no resize cursor in fullscreen/manual maximize
            return;
        }

        var value = m.LParam.ToInt64();
        var screenPoint = new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        var point = PointToClient(screenPoint);
        if (point.Y < 0 && point.Y >= -10)
        {
            var tabPoint = new Point(point.X, 0);
            if (HitWindowControl(tabPoint) != WindowControl.None ||
                TabPlusRect().Contains(tabPoint) || IsOverTab(tabPoint))
            {
                m.Result = (IntPtr)1; // tab/control at the very top edge
                return;
            }
        }
        if (point.Y >= 0 && point.Y <= _tabBar.Height &&
            (HitWindowControl(point) != WindowControl.None || TabPlusRect().Contains(point) || IsOverTab(point)))
        {
            m.Result = (IntPtr)1;
            return;
        }

        const int edge = 6;
        var left = point.X <= edge;
        var right = point.X >= ClientSize.Width - edge;
        var top = point.Y <= edge;
        var bottom = point.Y >= ClientSize.Height - edge;

        var hit = left && top ? HtTopLeft
            : right && top ? HtTopRight
            : left && bottom ? HtBottomLeft
            : right && bottom ? HtBottomRight
            : left ? HtLeft
            : right ? HtRight
            : top ? HtTop
            : bottom ? HtBottom
            : 0;
        if (hit != 0) m.Result = (IntPtr)hit;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkTitleBar();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyCompactLayout();
        LayoutToolbar();
        _tabBar.Invalidate();
        ApplyDarkTitleBar();
        StartUpdateChecks();
    }

    private void StartUpdateChecks()
    {
        _ = CheckForUpdatesAsync();
        _updateTimer = new System.Windows.Forms.Timer { Interval = 15 * 60 * 1000 };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        _updateTimer.Start();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updateInfo != null) return;
        try
        {
            var info = await UpdateChecker.CheckAsync();
            if (info == null || IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || info == null) return;
                _updateInfo = info;
                _updateButton.Visible = true;
                _updateButton.ToolTipText = $"GitHub が更新されました（{info.Sha[..7]}）";
                LayoutToolbar();
                _tabBar.Invalidate();
            }));
        }
        catch
        {
            // Update checks must never disturb browsing.
        }
    }

    private void OpenUpdate()
    {
        _ = CreateTabAsync(_updateInfo?.Url ?? UpdateChecker.RepositoryUrl);
    }

    private void ApplyDarkTitleBar()
    {
        try
        {
            var enabled = Theme.IsLight ? 0 : 1;
            DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));

            var caption = ToColorRef(Theme.Ink);
            var text = ToColorRef(Theme.Cream);
            var border = ToColorRef(Theme.Line);
            var corner = 2;
            DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
            DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
        }
        catch
        {
            // Frame colors are a visual nicety; never fail startup for them.
        }
    }

    private static int ToColorRef(Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    private void SetResizeFrame(bool enabled)
    {
        if (!IsHandleCreated) return;
        var style = GetWindowLong(Handle, GwlStyle);
        var next = enabled ? style | WsThickFrame : style & ~WsThickFrame;
        if (next == style) return;
        SetWindowLong(Handle, GwlStyle, next);
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
            SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder);
    }

    private void LayoutToolbar()
    {
        if (_strip == null || _strip.IsDisposed || _address == null) return;
        var fixedWidth = 0;
        foreach (ToolStripItem item in _strip.Items)
        {
            if (item == _address || !item.Visible) continue;
            if (_compact && (item == _historyButton || item == _settingsButton)) continue;
            var width = Math.Max(item.Width, item.Size.Width);
            if (width <= 0) width = item.GetPreferredSize(Size.Empty).Width;
            fixedWidth += width + item.Margin.Horizontal;
        }

        var available = _strip.Width - _strip.Padding.Horizontal - fixedWidth - 32;
        var target = Math.Max(180, Math.Min(available, 1000));
        if (_address.Width != target) _address.Width = target;
    }

    private void ApplyCompactLayout()
    {
        if (_strip == null || _strip.IsDisposed) return;
        var width = ClientSize.Width;
        var compact = width < 1000;
        var tiny = width < 860;
        _compact = compact;

        _strip.Height = tiny ? 40 : compact ? 44 : 50;
        _strip.Padding = tiny
            ? new Padding(6, 3, 6, 3)
            : compact ? new Padding(8, 4, 8, 4) : new Padding(10, 6, 10, 6);
        var buttonHeight = tiny ? 26 : compact ? 28 : 32;
        foreach (var button in _strip.Items.OfType<ToolStripButton>())
        {
            button.Size = new Size(tiny ? 28 : compact ? 30 : 34, buttonHeight);
        }

        _historyButton.Visible = !compact;
        _settingsButton.Visible = !compact;
        _address.Height = tiny ? 26 : compact ? 28 : 32;
        _tabBar.Height = tiny ? 32 : compact ? 34 : 40;
        _windowButtonWidth = tiny ? 36 : compact ? 40 : 46;
        LayoutToolbar();
        _tabBar.Invalidate();
    }

    private void BuildUi()
    {
        Theme.Apply("dark");
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        Font = new Font("Yu Gothic UI", 9F);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var strip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            CanOverflow = false,
            Height = 50,
            Padding = new Padding(10, 6, 10, 6),
        };
        strip.BackColor = Theme.Ink;
        strip.ForeColor = Theme.Cream;
        strip.Renderer = new Hum1ToolStripRenderer();
        _strip = strip;

        void StyleButton(ToolStripButton button, int width = 36)
        {
            button.AutoSize = false;
            button.Size = new Size(width, 34);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = _iconFont;
            button.Margin = new Padding(2, 0, 2, 0);
            button.ForeColor = Theme.Cream;
        }

        StyleButton(_back);
        StyleButton(_forward);
        StyleButton(_reload);
        StyleButton(_stop);

        _address.AutoSize = false;
        _address.Alignment = ToolStripItemAlignment.Left;
        _address.Width = 620;
        _address.Height = 32;
        _address.Margin = new Padding(8, 0, 8, 0);
        _address.BackColor = Theme.InkSoft;
        _address.ForeColor = Theme.Cream;
        _address.TextBox.BackColor = Theme.InkSoft;
        _address.TextBox.ForeColor = Theme.Cream;
        _address.TextBox.BorderStyle = BorderStyle.None;
        _address.TextBox.Font = new Font("Yu Gothic UI", 9.5F);
        _address.ApplyTheme();
        _back.Click += (_, _) => CurrentCore()?.GoBack();
        _forward.Click += (_, _) => CurrentCore()?.GoForward();
        _reload.Click += (_, _) => CurrentCore()?.Reload();
        _stop.Click += (_, _) => CurrentCore()?.Stop();
        _address.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateInput(_address.Text);
            }
        };

        _home = new ToolStripButton("\uE80F") { ToolTipText = "ホーム" };
        _home.Click += (_, _) => NavigateInput(_settings.Homepage);
        _historyButton = new ToolStripButton("\uE81C") { ToolTipText = "履歴 (Ctrl+H)" };
        _historyButton.Click += (_, _) => ShowHistory();
        _settingsButton = new ToolStripButton("\uE713") { ToolTipText = "設定" };
        _settingsButton.Click += (_, _) => ShowSettings();
        _menuButton = new ToolStripButton("\uE700") { ToolTipText = "メニュー" };
        _updateButton = new ToolStripButton("\uE895")
        {
            ToolTipText = "アップデートがあります",
            Visible = false,
            ForeColor = Theme.Gold,
        };
        StyleButton(_home);
        StyleButton(_bookmark);
        StyleButton(_historyButton);
        StyleButton(_settingsButton);
        StyleButton(_menuButton);
        StyleButton(_updateButton);
        _updateButton.ForeColor = Theme.Gold;
        _updateButton.Click += (_, _) => OpenUpdate();
        _bookmark.Click += (_, _) => ToggleBookmark();

        strip.Items.AddRange(new ToolStripItem[]
        {
            _back, _forward, _reload, _stop, _home, new ToolStripSeparator(),
            _address, _bookmark, _historyButton, _settingsButton, _menuButton, _updateButton,
        });
        _bookmark.Alignment = ToolStripItemAlignment.Left;
        _historyButton.Alignment = ToolStripItemAlignment.Left;
        _settingsButton.Alignment = ToolStripItemAlignment.Left;
        _menuButton.Alignment = ToolStripItemAlignment.Left;
        foreach (ToolStripItem item in strip.Items)
        {
            item.ForeColor = Theme.Cream;
        }
        strip.Resize += (_, _) => LayoutToolbar();
        LayoutToolbar();

        var context = new ContextMenuStrip
        {
            BackColor = Theme.Ink,
            ForeColor = Theme.Cream,
            Renderer = new Hum1ToolStripRenderer(),
        };
        _menu = context;
        context.Items.Add("新しいタブ", null, async (_, _) => await CreateTabAsync(NewTabUrl, focusAddress: true));
        context.Items.Add("閉じたタブを開く", null, (_, _) => ReopenClosedTab());
        context.Items.Add("新しいプライベートウィンドウ", null, (_, _) =>
            new MainForm(true, false, _smokeUrl, _outDir).Show());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("履歴", null, (_, _) => ShowHistory());
        context.Items.Add("ブックマーク", null, (_, _) => ShowBookmarks());
        context.Items.Add("ブックマークに追加 / 解除", null, (_, _) => ToggleBookmark());
        context.Items.Add("ダウンロード", null, (_, _) => ShowDownloads());
        context.Items.Add("設定", null, (_, _) => ShowSettings());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("ページ内検索", null, (_, _) => ShowFind());
        context.Items.Add("印刷", null, (_, _) => PrintPage());
        context.Items.Add("名前を付けて保存", null, async (_, _) => await SavePageAsync());
        context.Items.Add("全画面", null, (_, _) => ToggleFullscreen());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("閲覧データを削除", null, async (_, _) => await ClearDataWithConfirmAsync());
        context.Items.Add("このブラウザについて", null, (_, _) => ShowAbout());
        context.Items.Add(new ToolStripSeparator());
        context.Items.Add("終了", null, (_, _) => Close());
        foreach (ToolStripItem item in context.Items)
        {
            item.ForeColor = Theme.Cream;
        }
        _menuButton.Click += (_, _) => context.Show(strip, new Point(strip.Width - 220, strip.Height));

        var findClose = new Button { Text = "×", Dock = DockStyle.Right, Width = 34 };
        findClose.Click += (_, _) => HideFind();
        var findPrev = new Button { Text = "前へ", Dock = DockStyle.Left, Width = 60 };
        findPrev.Click += async (_, _) => await FindAsync(true);
        var findNext = new Button { Text = "次へ", Dock = DockStyle.Left, Width = 60 };
        findNext.Click += async (_, _) => await FindAsync(false);
        var findTitle = new Label { Text = "検索", Dock = DockStyle.Left, Width = 50, TextAlign = ContentAlignment.MiddleLeft };
        _findBox.Dock = DockStyle.Left;
        _findBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await FindAsync(e.Shift);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideFind();
            }
        };
        _findPanel.Controls.Add(findClose);
        _findPanel.Controls.Add(_findLabel);
        _findPanel.Controls.Add(_findBox);
        _findPanel.Controls.Add(findNext);
        _findPanel.Controls.Add(findPrev);
        _findPanel.Controls.Add(findTitle);

        _findPanel.BackColor = Theme.Ink;
        _findBox.BackColor = Theme.InkSoft;
        _findBox.ForeColor = Theme.Cream;
        _findBox.BorderStyle = BorderStyle.FixedSingle;
        _findLabel.ForeColor = Theme.Cream;
        foreach (var button in new[] { findClose, findPrev, findNext })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Theme.InkSoft;
            button.ForeColor = Theme.Cream;
            button.FlatAppearance.BorderColor = Theme.Gold;
        }

        _tabs.BackColor = Theme.Ink;
        _tabBar.BackColor = Theme.Ink;
        _tabBar.Paint += DrawTabBar;
        _tabBar.MouseUp += OnTabBarMouseUp;
        _tabBar.MouseMove += OnTabBarMouseMove;
        _tabBar.MouseLeave += (_, _) =>
        {
            if (_hoverWindowControl == WindowControl.None && !_hoverTabPlus && _hoverTabIndex < 0) return;
            _hoverWindowControl = WindowControl.None;
            _hoverTabPlus = false;
            _hoverTabIndex = -1;
            _tabBar.Cursor = Cursors.Default;
            _tabBar.Invalidate();
        };
        _tabBar.MouseDown += OnTabBarMouseDown;
        _tabBar.DoubleClick += OnTabBarDoubleClick;

        var status = new StatusStrip { BackColor = Theme.Ink, ForeColor = Theme.Cream, SizingGrip = false };
        status.Renderer = new Hum1ToolStripRenderer();
        _status.ForeColor = Theme.Cream;
        status.Items.Add(_status);
        _statusStrip = status;

        Controls.Add(_tabs);
        Controls.Add(strip);
        Controls.Add(_findPanel);
        Controls.Add(_tabBar);
        // The status strip is intentionally not added to the form. The bottom
        // bar created noise ("遮断", "完了") and the page itself shows errors.
        _statusStrip.Visible = false;
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            _tabBar.Invalidate();
            SyncAddress();
            UpdateNav();
        };
        KeyDown += OnKeyDown;
        ClientSizeChanged += (_, _) => ApplyCompactLayout();
        ApplyCompactLayout();
    }

    private void ApplyTheme()
    {
        Theme.Apply(_settings.Theme);
        BackColor = Theme.Ink;
        ForeColor = Theme.Cream;
        _strip.BackColor = Theme.Ink;
        _strip.ForeColor = Theme.Cream;
        _strip.Renderer = new Hum1ToolStripRenderer();
        foreach (ToolStripItem item in _strip.Items)
        {
            item.ForeColor = Theme.Cream;
        }

        _address.BackColor = Theme.InkSoft;
        _address.ForeColor = Theme.Cream;
        _address.TextBox.BackColor = Theme.InkSoft;
        _address.TextBox.ForeColor = Theme.Cream;
        _address.ApplyTheme();

        _findPanel.BackColor = Theme.Ink;
        _findBox.BackColor = Theme.InkSoft;
        _findBox.ForeColor = Theme.Cream;
        _findLabel.ForeColor = Theme.Cream;
        foreach (Control control in _findPanel.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }

        _statusStrip.BackColor = Theme.Ink;
        _statusStrip.ForeColor = Theme.Cream;
        _status.ForeColor = Theme.Cream;

        _tabs.BackColor = Theme.Ink;
        _tabs.Invalidate();
        foreach (BrowserTab page in _tabs.TabPages)
        {
            page.BackColor = Theme.Ink;
            foreach (Control child in page.Controls)
            {
                if (child is Panel panel) panel.BackColor = Theme.Ink;
            }
        }
        _tabBar.BackColor = Theme.Ink;
        _tabBar.Invalidate();

        if (_menu != null)
        {
            _menu.BackColor = Theme.Ink;
            _menu.ForeColor = Theme.Cream;
            _menu.Renderer = new Hum1ToolStripRenderer();
            foreach (ToolStripItem item in _menu.Items)
            {
                item.ForeColor = Theme.Cream;
            }
        }

        ApplyDarkTitleBar();

        foreach (var web in _views.Values)
        {
            try
            {
                web.DefaultBackgroundColor = Theme.Ink;
                if (web.CoreWebView2 != null)
                {
                    web.CoreWebView2.Profile.PreferredColorScheme = Theme.IsLight
                        ? CoreWebView2PreferredColorScheme.Light
                        : CoreWebView2PreferredColorScheme.Dark;
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("theme apply failed: " + ex.Message);
            }
        }
    }

    private async Task StartAsync()
    {
        _settings = Settings.Load();
        Theme.Apply(_settings.Theme);
        ApplyTheme();
        _bookmarks = BookmarkStore.Load();
        _history = _private ? new List<HistoryEntry>() : HistoryStore.Load();
        _adblock = new Adblock(_settings.AdblockEnabled);
        _udf = _private ? AppPaths.PrivateUdfPath : AppPaths.UdfPath;

        if (_smoke) _adblock.Enabled = true;

        if (!_private)
        {
            _env = await CoreWebView2Environment.CreateAsync(null, _udf, null);
        }

        var cleanExit = File.Exists(AppPaths.CleanExitPath);
        if (cleanExit)
        {
            try { File.Delete(AppPaths.CleanExitPath); } catch { }
        }

        if (_smoke && !_restoreSmoke)
        {
            await RunSmokeAsync();
            return;
        }

        var shouldRestore = !_private && File.Exists(AppPaths.SessionPath) &&
                            (_settings.RestoreSession || !cleanExit);
        if (shouldRestore)
        {
            try
            {
                var urls = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(AppPaths.SessionPath)) ?? new();
                foreach (var url in urls.Take(12))
                {
                    await CreateTabAsync(url);
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("session restore failed: " + ex.Message);
            }
        }

        if (_tabs.TabCount == 0)
        {
            await CreateTabAsync(_settings.Homepage);
        }

        if (!cleanExit && !_private && !_restoreSmoke)
        {
            _status.Text = "前回の終了が正常ではありません。タブを復元しました。";
        }

        if (_restoreSmoke)
        {
            await Task.Delay(2000);
            Directory.CreateDirectory(_outDir);
            var urls = _tabs.TabPages.Cast<BrowserTab>()
                .Select(p => _views.TryGetValue(p, out var web) ? web.Source?.AbsoluteUri : null)
                .ToList();
            var result = new Dictionary<string, object?>
            {
                ["tabs"] = _tabs.TabCount,
                ["urls"] = urls,
                ["sessionFile"] = File.Exists(AppPaths.SessionPath),
            };
            File.WriteAllText(Path.Combine(_outDir, "restore-smoke.json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            _allowClose = true;
            Close();
        }
    }

    private async Task CreateTabAsync(string url, bool focusAddress = false)
    {
        var page = new BrowserTab { Text = "New Tab", Padding = new Padding(0), BackColor = Theme.Ink };
        var web = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Theme.Ink,
        };
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Ink,
            Padding = new Padding(2),
        };
        host.Controls.Add(web);
        page.Controls.Add(host);
        _views[page] = web;
        _tabs.AddPage(page);
        _tabs.SelectedTab = page;
        UpdateTabStripFill();

        if (_private)
        {
            web.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = _udf,
                IsInPrivateModeEnabled = true,
            };
            await web.EnsureCoreWebView2Async();
        }
        else
        {
            await web.EnsureCoreWebView2Async(_env);
        }

        Wire(web, page);
        Navigate(web, url);
        SyncAddress();

        if (focusAddress)
        {
            await Task.Delay(60);
            _address.Focus();
            _address.SelectAll();
        }
    }

    private void Wire(WebView2 web, BrowserTab page)
    {
        var core = web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;

        try
        {
            core.Profile.PreferredTrackingPreventionLevel = _settings.StrictTracking
                ? CoreWebView2TrackingPreventionLevel.Strict
                : CoreWebView2TrackingPreventionLevel.Balanced;
            core.Profile.PreferredColorScheme = Theme.IsLight
                ? CoreWebView2PreferredColorScheme.Light
                : CoreWebView2PreferredColorScheme.Dark;
        }
        catch (Exception ex)
        {
            AppPaths.Log("tracking prevention failed: " + ex.Message);
        }

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.PermissionRequested += OnPermissionRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.ContainsFullScreenElementChanged += (_, _) =>
        {
            if (core.ContainsFullScreenElement) EnterHtmlFullscreen();
            else ExitHtmlFullscreen();
        };
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            _ = CreateTabAsync(e.Uri);
        };
        core.NavigationStarting += (_, e) =>
        {
            _stop.Enabled = true;
            _statusTimer.Stop();
            _status.Text = "読み込み中…";
        };
        core.NavigationCompleted += (_, e) =>
        {
            _stop.Enabled = false;
            if (!e.IsSuccess)
            {
                _statusTimer.Stop();
                _status.Text = "失敗: " + e.WebErrorStatus;
                return;
            }

            var url = web.Source?.AbsoluteUri ?? "";
            var title = core.DocumentTitle;
            page.Text = Truncate(string.IsNullOrWhiteSpace(title) ? url : title, 24);
            _tabBar.Invalidate();
            _status.Text = "完了: " + title;
            _statusTimer.Stop();
            _statusTimer.Start();
            if (!_private && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _history = HistoryStore.Add(_history, url, title);
                HistoryStore.Save(_history);
                _sessionTimer.Stop();
                _sessionTimer.Start();
            }
            UpdateNav();
            SyncAddress();
        };
        core.SourceChanged += (_, _) =>
        {
            if (_tabs.SelectedTab == page) SyncAddress();
        };
        core.DocumentTitleChanged += (_, _) =>
        {
            page.Text = Truncate(core.DocumentTitle, 24);
            _tabBar.Invalidate();
        };
        core.ProcessFailed += (_, e) =>
        {
            _status.Text = "プロセス失敗: " + e.ProcessFailedKind;
            AppPaths.Log($"process failed: {e.ProcessFailedKind} {e.Reason}");
        };
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!_settings.AdblockEnabled) return;
        if (e.ResourceContext == CoreWebView2WebResourceContext.Document) return;
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;
        if (!_adblock.ShouldBlock(uri)) return;

        _adblock.CountBlocked();
        if (sender is CoreWebView2 core)
        {
            var empty = new MemoryStream(Array.Empty<byte>());
            e.Response = core.Environment.CreateWebResourceResponse(empty, 204, "No Content", "Cache-Control: no-store");
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        var key = PermissionKey(e);
        if (!_private && _settings.PermissionDecisions.TryGetValue(key, out var saved) &&
            Enum.TryParse<CoreWebView2PermissionState>(saved, out var savedState))
        {
            e.State = savedState;
            return;
        }

        if (_smoke)
        {
            e.State = CoreWebView2PermissionState.Deny;
            return;
        }

        using var dlg = new PermissionForm(e.PermissionKind.ToString(), e.Uri);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            e.State = dlg.Allow ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            if (dlg.Remember && !_private)
            {
                _settings.PermissionDecisions[key] = e.State.ToString();
                _settings.Save();
            }
        }
        else
        {
            e.State = CoreWebView2PermissionState.Deny;
        }
    }

    private static string PermissionKey(CoreWebView2PermissionRequestedEventArgs e)
    {
        var host = Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ? uri.Host : e.Uri;
        return $"{e.PermissionKind}|{host}";
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var operation = e.DownloadOperation;
        var item = new DownloadItem
        {
            Url = operation.Uri,
            FilePath = operation.ResultFilePath,
            State = operation.State.ToString(),
            Total = (long)(operation.TotalBytesToReceive ?? 0),
        };
        _downloads.Insert(0, item);
        if (_downloads.Count > 100) _downloads.RemoveRange(100, _downloads.Count - 100);
        operation.StateChanged += (_, _) =>
        {
            item.State = operation.State.ToString();
            item.FilePath = operation.ResultFilePath;
            item.Received = operation.BytesReceived;
        };
        e.Handled = false;
    }

    private void ShowDownloads()
    {
        var dlg = new PxDialog("ダウンロード", 760, 480);
        var list = new PxListView
        {
            Dock = DockStyle.Fill,
            MultiSelect = false,
        };
        list.Columns.Add("ファイル", 300);
        list.Columns.Add("状態", 120);
        list.Columns.Add("URL", 300);
        void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var item in _downloads)
            {
                var name = string.IsNullOrWhiteSpace(item.FilePath)
                    ? item.Url
                    : Path.GetFileName(item.FilePath);
                var row = new ListViewItem(name) { Tag = item };
                row.SubItems.Add(item.State);
                row.SubItems.Add(item.Url);
                list.Items.Add(row);
            }
            list.EndUpdate();
        }
        RefreshList();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            BackColor = Theme.Ink,
        };
        var open = new Button { Text = "開く", Width = 90 };
        open.Click += (_, _) =>
        {
            if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not DownloadItem item) return;
            try
            {
                if (File.Exists(item.FilePath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath)
                    {
                        UseShellExecute = true,
                    });
            }
            catch { }
        };
        var folder = new Button { Text = "フォルダ", Width = 90 };
        folder.Click += (_, _) =>
        {
            if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not DownloadItem item) return;
            try
            {
                if (File.Exists(item.FilePath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
            }
            catch { }
        };
        var close = new Button { Text = "閉じる", Width = 90, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange(new Control[] { open, folder, close });
        foreach (Control control in buttons.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }
        dlg.Body.Controls.Add(list);
        dlg.Body.Controls.Add(buttons);
        dlg.CancelButton = close;
        dlg.ShowDialog(this);
    }

    private void Navigate(WebView2 web, string url)
    {
        try
        {
            web.CoreWebView2.Navigate(string.IsNullOrWhiteSpace(url) ? _settings.Homepage : url);
        }
        catch (Exception ex)
        {
            _status.Text = "ナビゲーション失敗: " + ex.Message;
        }
    }

    private void NavigateInput(string input)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0) return;

        string url;
        if (text.Contains("://", StringComparison.Ordinal))
        {
            url = text;
        }
        else if (text.Contains('.') && !text.Contains(' '))
        {
            url = "https://" + text;
        }
        else
        {
            url = _settings.SearchEngine + Uri.EscapeDataString(text);
        }
        CurrentCore()?.Navigate(url);
    }

    private CoreWebView2? CurrentCore()
    {
        if (_tabs.SelectedTab is { } page && _views.TryGetValue(page, out var web))
        {
            return web.CoreWebView2;
        }
        return null;
    }

    private WebView2? CurrentWeb()
    {
        return _tabs.SelectedTab is { } page && _views.TryGetValue(page, out var web) ? web : null;
    }

    private void SyncAddress()
    {
        var web = CurrentWeb();
        if (web?.Source is { } source && source.AbsoluteUri != "about:blank" && !_address.Focused)
        {
            _address.Text = source.AbsoluteUri;
        }
        SyncBookmarkButton();
    }

    private void SyncBookmarkButton()
    {
        var url = CurrentCore()?.Source;
        var marked = !string.IsNullOrWhiteSpace(url) && _bookmarks.Contains(url);
        _bookmark.Text = marked ? "\uE735" : "\uE734";
        _bookmark.ToolTipText = marked ? "ブックマークを解除 (Ctrl+D)" : "ブックマークに追加 (Ctrl+D)";
    }

    private void ToggleBookmark()
    {
        var core = CurrentCore();
        var url = core?.Source;
        if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return;
        var title = string.IsNullOrWhiteSpace(core?.DocumentTitle) ? url : core!.DocumentTitle;
        _bookmarks.Toggle(title, url);
        SyncBookmarkButton();
    }

    private void UpdateNav()
    {
        var core = CurrentCore();
        _back.Enabled = core?.CanGoBack == true;
        _forward.Enabled = core?.CanGoForward == true;
    }

    private void CloseCurrentTab()
    {
        if (_tabs.SelectedTab is { } page) CloseTab(page);
    }

    private void CloseTab(BrowserTab page)
    {
        if (_views.Remove(page, out var web))
        {
            var url = web.Source?.AbsoluteUri;
            if (!_private && !string.IsNullOrWhiteSpace(url) &&
                url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _closedTabs.Push(url);
                if (_closedTabs.Count > 20)
                {
                    var kept = _closedTabs.Take(20).Reverse().ToList();
                    _closedTabs.Clear();
                    foreach (var item in kept) _closedTabs.Push(item);
                }
            }
            try { web.Dispose(); } catch { }
        }
        _tabs.RemovePage(page);
        UpdateTabStripFill();
        if (_tabs.TabCount == 0)
        {
            if (_smoke) return;
            Close();
        }
    }

    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0) return;
        _ = CreateTabAsync(_closedTabs.Pop());
    }

    private void SwitchTab(int delta)
    {
        if (_tabs.TabCount < 2) return;
        var index = (_tabs.SelectedIndex + delta + _tabs.TabCount) % _tabs.TabCount;
        _tabs.SelectedTab = _tabs.TabPages[index];
        _tabBar.Invalidate();
    }

    private void SelectTabIndex(int index)
    {
        if (index < 0 || index >= _tabs.TabCount) return;
        _tabs.SelectedTab = _tabs.TabPages[index];
        _tabBar.Invalidate();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.N)
        {
            new MainForm(true, false, _smokeUrl, _outDir).Show();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.Shift && e.KeyCode == Keys.T)
        {
            ReopenClosedTab();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.Tab)
        {
            SwitchTab(e.Shift ? -1 : 1);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
        {
            SelectTabIndex(e.KeyCode - Keys.D1);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.T)
        {
            _ = CreateTabAsync(NewTabUrl, focusAddress: true);
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
        else if (e.Control && e.KeyCode == Keys.D)
        {
            ToggleBookmark();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.Shift && e.KeyCode == Keys.O)
        {
            ShowBookmarks();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.H)
        {
            ShowHistory();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.J)
        {
            ShowDownloads();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.Oemcomma)
        {
            ShowSettings();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.F)
        {
            ShowFind();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            PrintPage();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.S)
        {
            _ = SavePageAsync();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F11)
        {
            ToggleFullscreen();
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_htmlFullscreen) ExitHtmlFullscreen();
            else if (_fullscreen) ToggleFullscreen();
            else HideFind();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
        {
            Zoom(ZoomStep);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
        {
            Zoom(-ZoomStep);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
        {
            var web = CurrentWeb();
            if (web != null) web.ZoomFactor = 1.0;
            _status.Text = "ズーム 100%";
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.R))
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

    private void DrawTabBar(object? sender, PaintEventArgs e)
    {
        using var background = new SolidBrush(Theme.Ink);
        e.Graphics.FillRectangle(background, _tabBar.ClientRectangle);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var controlsLeft = _tabBar.Width - _windowButtonWidth * 3;
        var tabWidth = TabWidth;
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            var slot = new Rectangle(i * tabWidth, 0, tabWidth, _tabBar.Height);
            if (slot.Left >= controlsLeft) break;
            var selected = i == _tabs.SelectedIndex;
            var hover = i == _hoverTabIndex;
            var width = Math.Min(slot.Width, controlsLeft - slot.Left);
            if (width < 24) break;
            var rect = new Rectangle(slot.Left + 4, 5, width - 8, slot.Height - 5);
            using var path = RoundedTopRect(rect, 11);
            using var fill = new SolidBrush(selected || hover ? Theme.InkSoft : Theme.Ink);
            e.Graphics.FillPath(fill, path);
            if (selected)
            {
                using var border = new Pen(Theme.Line);
                e.Graphics.DrawPath(border, path);
                using var accent = new SolidBrush(Theme.Gold);
                e.Graphics.FillRectangle(accent, rect.Left + 16, rect.Bottom - 2, Math.Max(4, rect.Width - 32), 2);
            }

            var showClose = (selected || hover) && rect.Width > 52;
            var textWidth = Math.Max(4, rect.Width - (showClose ? 46 : 24));
            var textRect = new Rectangle(rect.Left + 14, rect.Top + 1, textWidth, rect.Height - 2);
            TextRenderer.DrawText(
                e.Graphics,
                _tabs.TabPages[i].Text,
                selected ? _tabFontBold : _tabFont,
                textRect,
                selected || hover ? Theme.Cream : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            if (showClose)
            {
                var closeRect = new Rectangle(rect.Right - 25, rect.Top + (rect.Height - 18) / 2, 18, 18);
                TextRenderer.DrawText(
                    e.Graphics,
                    "\uE8BB",
                    _windowFont,
                    closeRect,
                    Theme.Cream,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        var plus = TabPlusRect();
        if (plus.Width > 0)
        {
            TextRenderer.DrawText(e.Graphics, "\uE710", _windowFont, plus,
                _hoverTabPlus ? Theme.Gold : Theme.Cream,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        DrawWindowControls(e.Graphics, controlsLeft);
    }

    private Rectangle TabPlusRect()
    {
        var left = _tabs.TabCount * TabWidth + 2;
        var max = _tabBar.Width - _windowButtonWidth * 3;
        var width = 34;
        var height = Math.Max(28, _tabBar.Height - 5);
        if (left + width > max) return Rectangle.Empty;
        return new Rectangle(left, 5, width, height);
    }

    private void DrawWindowControls(Graphics graphics, int left)
    {
        var height = _tabBar.Height;
        DrawWindowButton(graphics, new Rectangle(left, 0, _windowButtonWidth, height),
            "\uE921", WindowControl.Minimize);
        DrawWindowButton(graphics, new Rectangle(left + _windowButtonWidth, 0, _windowButtonWidth, height),
            _manualMaximized ? "\uE923" : "\uE922", WindowControl.Maximize);
        DrawWindowButton(graphics, new Rectangle(left + _windowButtonWidth * 2, 0, _windowButtonWidth, height),
            "\uE8BB", WindowControl.Close);
    }

    private void DrawWindowButton(Graphics graphics, Rectangle rect, string glyph, WindowControl control)
    {
        var hover = _hoverWindowControl == control;
        if (hover)
        {
            using var brush = new SolidBrush(control == WindowControl.Close
                ? Color.FromArgb(196, 43, 28)
                : Theme.InkSoft);
            graphics.FillRectangle(brush, rect);
        }
        TextRenderer.DrawText(graphics, glyph, _windowFont, rect,
            control == WindowControl.Close && hover ? Color.White : Theme.Cream,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private WindowControl HitWindowControl(Point point)
    {
        var left = _tabBar.Width - _windowButtonWidth * 3;
        if (point.X < left || point.Y < 0 || point.Y > _tabBar.Height) return WindowControl.None;
        var index = (point.X - left) / _windowButtonWidth;
        return index switch
        {
            0 => WindowControl.Minimize,
            1 => WindowControl.Maximize,
            2 => WindowControl.Close,
            _ => WindowControl.None,
        };
    }

    private bool IsOverTab(Point point)
    {
        if (point.Y < 0 || point.Y > _tabBar.Height) return false;
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            var slot = new Rectangle(i * TabWidth, 0, TabWidth, _tabBar.Height);
            if (slot.Contains(point)) return true;
        }
        return false;
    }

    private void OnTabBarMouseMove(object? sender, MouseEventArgs e)
    {
        var control = HitWindowControl(e.Location);
        var plus = TabPlusRect();
        var overPlus = plus.Width > 0 && plus.Contains(e.Location);
        var hoverTab = -1;
        if (control == WindowControl.None && !overPlus)
        {
            var tabWidth = TabWidth;
            for (var i = 0; i < _tabs.TabPages.Count; i++)
            {
                if (new Rectangle(i * tabWidth, 0, tabWidth, _tabBar.Height).Contains(e.Location))
                {
                    hoverTab = i;
                    break;
                }
            }
        }
        if (control != _hoverWindowControl || overPlus != _hoverTabPlus || hoverTab != _hoverTabIndex)
        {
            _hoverWindowControl = control;
            _hoverTabPlus = overPlus;
            _hoverTabIndex = hoverTab;
            _tabBar.Cursor = control != WindowControl.None || overPlus ? Cursors.Hand : Cursors.Default;
            _tabBar.Invalidate();
        }
    }

    private void OnTabBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (HitWindowControl(e.Location) != WindowControl.None) return;
        var plus = TabPlusRect();
        if (plus.Width > 0 && plus.Contains(e.Location)) return;
        if (IsOverTab(e.Location)) return;
        if (_manualMaximized) ToggleMaximize();
        ReleaseCapture();
        SendMessage(Handle, WmNcLeftButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
    }

    private void OnTabBarDoubleClick(object? sender, EventArgs e)
    {
        if (IsOverTab(_tabBar.PointToClient(Cursor.Position))) return;
        ToggleMaximize();
    }

    private void PerformWindowControl(WindowControl control)
    {
        switch (control)
        {
            case WindowControl.Minimize:
                WindowState = FormWindowState.Minimized;
                break;
            case WindowControl.Maximize:
                ToggleMaximize();
                break;
            case WindowControl.Close:
                Close();
                break;
        }
    }

    private void ToggleMaximize()
    {
        if (!_manualMaximized)
        {
            _manualMaxPrevBounds = Bounds;
            WindowState = FormWindowState.Normal;
            SetResizeFrame(false);
            var working = Screen.FromControl(this).WorkingArea;
            Bounds = working;
            _manualMaximized = true;
            Activate();
        }
        else
        {
            _manualMaximized = false;
            WindowState = FormWindowState.Normal;
            SetResizeFrame(true);
            Bounds = _manualMaxPrevBounds;
            var working = Screen.FromHandle(Handle).WorkingArea;
            if (Bounds.Width < 240 || Bounds.Height < 180 || !working.IntersectsWith(Bounds))
            {
                Bounds = new Rectangle(
                    working.Left + 80,
                    working.Top + 80,
                    Math.Min(1200, Math.Max(800, working.Width - 160)),
                    Math.Min(800, Math.Max(500, working.Height - 160)));
            }
        }
        _tabBar.Invalidate();
    }

    private void OnTabBarMouseUp(object? sender, MouseEventArgs e)
    {
        var control = HitWindowControl(e.Location);
        if (control != WindowControl.None)
        {
            PerformWindowControl(control);
            _hoverWindowControl = WindowControl.None;
            _tabBar.Invalidate();
            return;
        }

        var plus = TabPlusRect();
        if (plus.Width > 0 && plus.Contains(e.Location))
        {
            _ = CreateTabAsync(NewTabUrl, focusAddress: true);
            _hoverTabPlus = false;
            _tabBar.Invalidate();
            return;
        }

        var tabWidth = TabWidth;
        for (var i = 0; i < _tabs.TabPages.Count; i++)
        {
            var slot = new Rectangle(i * tabWidth, 0, tabWidth, _tabBar.Height);
            var rect = new Rectangle(slot.Left + 4, 5, slot.Width - 8, slot.Height - 5);
            var showClose = (i == _tabs.SelectedIndex || i == _hoverTabIndex) && rect.Width > 52;
            if (showClose)
            {
                var closeRect = new Rectangle(rect.Right - 25, rect.Top + (rect.Height - 18) / 2, 18, 18);
                if (closeRect.Contains(e.Location))
                {
                    CloseTab(_tabs.TabPages[i]);
                    return;
                }
            }
            if (slot.Contains(e.Location))
            {
                _tabs.SelectedTab = _tabs.TabPages[i];
                _tabBar.Invalidate();
                return;
            }
        }
    }

    private void UpdateTabStripFill()
    {
        _tabBar.Invalidate();
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath RoundedTopRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
        path.AddLine(rect.Right, rect.Top + radius, rect.Right, rect.Bottom);
        path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Top + radius);
        path.CloseFigure();
        return path;
    }

    private void ShowFind()
    {
        _findPanel.Visible = true;
        _findBox.Focus();
        _findBox.SelectAll();
    }

    private void HideFind()
    {
        _findPanel.Visible = false;
        CurrentWeb()?.Focus();
    }

    private async Task FindAsync(bool backwards)
    {
        var core = CurrentCore();
        if (core == null) return;
        var text = _findBox.Text.Trim();
        if (text.Length == 0) return;

        var jsText = JsonSerializer.Serialize(text);
        var countScript = "(function(){var t=" + jsText + ";var w=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);var n=0;while(w.nextNode()){var v=w.currentNode.nodeValue||'';var i=0;while((i=v.indexOf(t,i))>=0){n++;i+=t.length;}}return n;})()";
        var countRaw = await EvalAsync(core, countScript);
        int.TryParse(countRaw, out var count);

        var findScript = "window.find(" + jsText + ", false, " + (backwards ? "true" : "false") + ", true, false)";
        await EvalAsync(core, findScript);
        _findLabel.Text = count > 0 ? $"{count} 件" : "0 件";
    }

    private void Zoom(double delta)
    {
        var web = CurrentWeb();
        if (web == null) return;
        var zoom = Math.Clamp(web.ZoomFactor + delta, 0.25, 3.0);
        web.ZoomFactor = zoom;
        _status.Text = $"ズーム {zoom:P0}";
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _fullscreen = true;
            _prevBorder = FormBorderStyle;
            _prevState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _strip.Visible = false;
            _statusStrip.Visible = false;
            _findPanel.Visible = false;
            _tabBar.Visible = false;
        }
        else
        {
            _fullscreen = false;
            FormBorderStyle = _prevBorder;
            WindowState = _prevState;
            _strip.Visible = true;
            _statusStrip.Visible = true;
            _tabBar.Visible = true;
        }
        UpdateTabStripFill();
    }

    private void EnterHtmlFullscreen()
    {
        if (_htmlFullscreen) return;
        _htmlFullscreen = true;
        _htmlPrevBounds = Bounds;
        _htmlPrevState = WindowState;
        _htmlPrevBorder = FormBorderStyle;
        _htmlPrevTopMost = TopMost;

        WindowState = FormWindowState.Normal;
        FormBorderStyle = FormBorderStyle.None;
        SetResizeFrame(false);
        Bounds = Screen.FromControl(this).Bounds;
        TopMost = true;
        _strip.Visible = false;
        _tabBar.Visible = false;
        _findPanel.Visible = false;
        foreach (var candidate in _views.Values)
        {
            if (candidate.Parent is Panel host) host.Padding = Padding.Empty;
        }
    }

    private void ExitHtmlFullscreen()
    {
        if (!_htmlFullscreen) return;
        _htmlFullscreen = false;
        TopMost = _htmlPrevTopMost;
        FormBorderStyle = _htmlPrevBorder;
        SetResizeFrame(!_manualMaximized);
        Bounds = _htmlPrevBounds;
        WindowState = _htmlPrevState;
        _strip.Visible = true;
        _tabBar.Visible = true;
        foreach (var candidate in _views.Values)
        {
            if (candidate.Parent is Panel host) host.Padding = new Padding(2);
        }
    }

    private void PrintPage()
    {
        try
        {
            CurrentCore()?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            _status.Text = "印刷ダイアログを開きました";
        }
        catch (Exception ex)
        {
            _status.Text = "印刷に失敗: " + ex.Message;
        }
    }

    private async Task SavePageAsync()
    {
        var core = CurrentCore();
        if (core == null) return;
        try
        {
            await core.ShowSaveAsUIAsync();
            _status.Text = "保存ダイアログを開きました";
        }
        catch (Exception ex)
        {
            _status.Text = "保存に失敗: " + ex.Message;
        }
    }

    private void ShowBookmarks()
    {
        var dlg = new PxDialog("ブックマーク", 820, 560);
        var list = new PxListView
        {
            Dock = DockStyle.Fill,
            MultiSelect = false,
        };
        list.Columns.Add("タイトル", 300);
        list.Columns.Add("URL", 460);
        void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in _bookmarks.Items)
            {
                var item = new ListViewItem(entry.Title) { Tag = entry };
                item.SubItems.Add(entry.Url);
                list.Items.Add(item);
            }
            list.EndUpdate();
        }
        RefreshList();

        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is BookmarkEntry entry)
            {
                NavigateInput(entry.Url);
                dlg.Close();
            }
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            BackColor = Theme.Ink,
        };
        var open = new Button { Text = "開く", Width = 90 };
        open.Click += (_, _) =>
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is BookmarkEntry entry)
            {
                NavigateInput(entry.Url);
                dlg.Close();
            }
        };
        var remove = new Button { Text = "削除", Width = 90 };
        remove.Click += (_, _) =>
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is BookmarkEntry entry)
            {
                _bookmarks.Remove(entry);
                RefreshList();
                SyncBookmarkButton();
            }
        };
        var close = new Button { Text = "閉じる", Width = 90, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange(new Control[] { open, remove, close });
        foreach (Control control in buttons.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }
        dlg.Body.Controls.Add(list);
        dlg.Body.Controls.Add(buttons);
        dlg.CancelButton = close;
        dlg.ShowDialog(this);
    }

    private void ShowHistory()
    {
        var dlg = new PxDialog("履歴", 920, 600);
        var list = new PxListView
        {
            Dock = DockStyle.Fill,
            MultiSelect = false,
        };
        list.Columns.Add("日時", 140);
        list.Columns.Add("タイトル", 260);
        list.Columns.Add("URL", 460);

        void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in _history.OrderByDescending(e => e.VisitedAt))
            {
                var item = new ListViewItem(entry.VisitedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"))
                {
                    Tag = entry,
                };
                item.SubItems.Add(entry.Title);
                item.SubItems.Add(entry.Url);
                list.Items.Add(item);
            }
            list.EndUpdate();
        }
        RefreshList();

        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is HistoryEntry entry)
            {
                NavigateInput(entry.Url);
                dlg.Close();
            }
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            BackColor = Theme.Ink,
        };
        var open = new Button { Text = "開く", Width = 90 };
        open.Click += (_, _) =>
        {
            if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is HistoryEntry entry)
            {
                NavigateInput(entry.Url);
                dlg.Close();
            }
        };
        var remove = new Button { Text = "選択を削除", Width = 110 };
        remove.Click += (_, _) =>
        {
            var selected = list.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as HistoryEntry)
                .Where(e => e != null)
                .ToHashSet();
            _history = _history.Where(e => !selected.Contains(e)).ToList();
            HistoryStore.Save(_history);
            RefreshList();
        };
        var clear = new Button { Text = "すべて削除", Width = 110 };
        clear.Click += (_, _) =>
        {
            if (!PxMessageBox.Confirm(this, "確認", "履歴をすべて削除しますか？")) return;
            _history.Clear();
            HistoryStore.Save(_history);
            RefreshList();
        };
        var close = new Button { Text = "閉じる", Width = 90 };
        close.Click += (_, _) => dlg.Close();
        buttons.Controls.AddRange(new Control[] { open, remove, clear, close });
        foreach (Control control in buttons.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }

        dlg.Body.Controls.Add(list);
        dlg.Body.Controls.Add(buttons);
        dlg.ShowDialog(this);
    }

    private void ShowSettings()
    {
        var dlg = new PxDialog("設定", 560, 500);
        var homeLabel = new Label { Text = "ホームページ", Left = 20, Top = 20, Width = 120 };
        var home = new TextBox { Text = _settings.Homepage, Left = 150, Top = 18, Width = 380 };
        var searchLabel = new Label { Text = "検索 URL", Left = 20, Top = 60, Width = 120 };
        var search = new TextBox { Text = _settings.SearchEngine, Left = 150, Top = 58, Width = 380 };
        var adblock = new PxCheckBox
        {
            Text = "広告・トラッカーを遮断する（簡易リスト）",
            Left = 20,
            Top = 105,
            Width = 500,
            Checked = _settings.AdblockEnabled,
        };
        var strict = new PxCheckBox
        {
            Text = "追跡防止を Strict にする",
            Left = 20,
            Top = 135,
            Width = 500,
            Checked = _settings.StrictTracking,
        };
        var restore = new PxCheckBox
        {
            Text = "前回のタブを復元する",
            Left = 20,
            Top = 165,
            Width = 500,
            Checked = _settings.RestoreSession,
        };
        var clear = new PxCheckBox
        {
            Text = "終了時に閲覧データを削除する",
            Left = 20,
            Top = 195,
            Width = 500,
            Checked = _settings.ClearOnExit,
        };
        var themeLabel = new Label { Text = "テーマ", Left = 20, Top = 230, Width = 120 };
        var darkTheme = new PxRadio
        {
            Text = "ダーク",
            Left = 150,
            Top = 228,
            Width = 100,
            Checked = _settings.Theme != "light",
        };
        var lightTheme = new PxRadio
        {
            Text = "ホワイト",
            Left = 260,
            Top = 228,
            Width = 120,
            Checked = _settings.Theme == "light",
        };
        var note = new Label
        {
            Text = "このブラウザは Chrome/Edge の完全代替ではありません。\n"
                 + "Chrome 拡張、Google の完全ログイン、Netflix の DRM 再生は対象外です。\n"
                 + "起動は Edge より遅いです。",
            Left = 20,
            Top = 270,
            Width = 510,
            Height = 70,
        };
        var clearPerms = new Button { Text = "権限の記憶を消去", Left = 20, Top = 370, Width = 200 };
        clearPerms.Click += (_, _) =>
        {
            _settings.PermissionDecisions.Clear();
            _settings.Save();
            PxMessageBox.Info(this, "設定", "権限の記憶を消去しました。");
        };
        var save = new Button { Text = "保存", Left = 350, Top = 370, Width = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "キャンセル", Left = 440, Top = 370, Width = 90, DialogResult = DialogResult.Cancel };
        dlg.Body.Controls.AddRange(new Control[]
        {
            homeLabel, home, searchLabel, search, adblock, strict, restore, clear,
            themeLabel, darkTheme, lightTheme, note, clearPerms, save, cancel,
        });
        dlg.AcceptButton = save;
        dlg.CancelButton = cancel;

        foreach (Control control in dlg.Body.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is TextBox textBox)
            {
                textBox.BackColor = Theme.InkSoft;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }
        dlg.BackColor = Theme.Ink;
        dlg.ForeColor = Theme.Cream;
        dlg.Height = 470;

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _settings.Homepage = home.Text.Trim();
        _settings.SearchEngine = search.Text.Trim();
        _settings.AdblockEnabled = adblock.Checked;
        _settings.StrictTracking = strict.Checked;
        _settings.RestoreSession = restore.Checked;
        _settings.ClearOnExit = clear.Checked;
        _settings.Theme = lightTheme.Checked ? "light" : "dark";
        _settings.Save();
        ApplyTheme();
        _adblock.Enabled = _settings.AdblockEnabled;

        foreach (var web in _views.Values)
        {
            try
            {
                if (web.CoreWebView2 == null) continue;
                web.CoreWebView2.Profile.PreferredTrackingPreventionLevel = _settings.StrictTracking
                    ? CoreWebView2TrackingPreventionLevel.Strict
                    : CoreWebView2TrackingPreventionLevel.Balanced;
            }
            catch (Exception ex)
            {
                AppPaths.Log("apply settings failed: " + ex.Message);
            }
        }
        _status.Text = "設定を保存しました";
    }

    private async Task ClearDataWithConfirmAsync()
    {
        if (!PxMessageBox.Confirm(this, "確認", "履歴、Cookie、サイトデータ、キャッシュを削除しますか？")) return;
        await ClearDataAsync();
        _status.Text = "閲覧データを削除しました";
    }

    private async Task ClearDataAsync()
    {
        foreach (var web in _views.Values)
        {
            try
            {
                if (web.CoreWebView2 != null)
                {
                    await web.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("clear data failed: " + ex.Message);
            }
        }
        _history.Clear();
        HistoryStore.Save(_history);
        try { File.Delete(AppPaths.SessionPath); } catch { }
    }

    private void ShowAbout()
    {
        PxMessageBox.Info(this, "lIl Browser について",
            "lIl Browser 0.3.0\n\n"
            + "メモリ軽量・プライバシー・制御に絞った Windows ブラウザ MVP。\n"
            + "Chrome 拡張、Google の完全ログイン、Netflix の DRM 再生は対象外です。\n"
            + "起動は Edge より遅いです。\n\n"
            + "WebView2 Runtime: " + (CurrentCore()?.Environment.BrowserVersionString ?? "unknown"));
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _updateTimer?.Stop();
        if (_allowClose) return;
        e.Cancel = true;
        try
        {
            SaveSession();
            if (!_private && _settings.ClearOnExit)
            {
                await ClearDataAsync();
            }
            File.WriteAllText(AppPaths.CleanExitPath, "ok");
        }
        catch (Exception ex)
        {
            AppPaths.Log("close failed: " + ex.Message);
        }
        finally
        {
            _allowClose = true;
            foreach (var web in _views.Values)
            {
                try { web.Dispose(); } catch { }
            }
            Close();
        }
    }

    private void SaveSession()
    {
        if (_private) return;
        try
        {
            if (!_settings.RestoreSession)
            {
                if (File.Exists(AppPaths.SessionPath)) File.Delete(AppPaths.SessionPath);
                return;
            }
            var urls = _tabs.TabPages.Cast<BrowserTab>()
                .Select(p => _views.TryGetValue(p, out var web) ? web.Source?.AbsoluteUri : null)
                .Where(u => !string.IsNullOrWhiteSpace(u) && u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();
            File.WriteAllText(AppPaths.SessionPath, JsonSerializer.Serialize(urls));
        }
        catch (Exception ex)
        {
            AppPaths.Log("session save failed: " + ex.Message);
        }
    }

    private async Task RunSmokeAsync()
    {
        Directory.CreateDirectory(_outDir);
        if (_smokeUrl.Contains("/fullscreen", StringComparison.OrdinalIgnoreCase))
        {
            await RunFullscreenSmokeAsync();
            return;
        }
        var result = new Dictionary<string, object?>();
        try
        {
            await CreateTabAsync(_smokeUrl);
            await Task.Delay(2500);
            var title = CurrentCore()?.DocumentTitle ?? "";
            var blocked = _adblock.BlockedCount;
            var web = CurrentWeb();
            if (web != null) web.ZoomFactor = 1.2;
            var find = await EvalAsync(CurrentCore(), "window.find('PX', false, false, true, false)");
            ToggleFullscreen();
            var fullscreenOn = _fullscreen;
            ToggleFullscreen();
            var fullscreenOff = !_fullscreen;
            var readUrl = _smokeUrl.TrimEnd('/').EndsWith("/read", StringComparison.OrdinalIgnoreCase)
                ? _smokeUrl
                : _smokeUrl.TrimEnd('/') + "/read";
            await CreateTabAsync(readUrl);
            await Task.Delay(1500);
            var body = await EvalAsync(CurrentCore(), "document.body ? document.body.innerText : ''");
            var normalBounds = Bounds;
            var maxX = ClientSize.Width - (int)(_windowButtonWidth * 1.5);
            var maxY = _tabBar.Height / 2;
            OnTabBarMouseUp(this, new MouseEventArgs(MouseButtons.Left, 1, maxX, maxY, 0));
            await Task.Delay(700);
            var maxState = _manualMaximized ? "Maximized" : WindowState.ToString();
            var maxBounds = Bounds;
            maxX = ClientSize.Width - (int)(_windowButtonWidth * 1.5);
            OnTabBarMouseUp(this, new MouseEventArgs(MouseButtons.Left, 1, maxX, maxY, 0));
            await Task.Delay(700);
            var restoredBounds = Bounds;
            var bookmarkUrl = CurrentCore()?.Source ?? _smokeUrl;
            var bookmarkTitle = CurrentCore()?.DocumentTitle ?? bookmarkUrl;
            _bookmarks.Toggle(bookmarkTitle, bookmarkUrl);
            var bookmarkCount = _bookmarks.Items.Count;
            var bookmarkMarked = _bookmarks.Contains(bookmarkUrl);

            var roundTrip = new Settings
            {
                Homepage = _settings.Homepage,
                AdblockEnabled = _settings.AdblockEnabled,
                StrictTracking = _settings.StrictTracking,
                RestoreSession = _settings.RestoreSession,
                ClearOnExit = _settings.ClearOnExit,
                SearchEngine = _settings.SearchEngine,
                Theme = _settings.Theme,
            };
            roundTrip.Save();
            var reloaded = Settings.Load();

            var beforeClose = _tabs.TabCount;
            OnTabBarMouseUp(this, new MouseEventArgs(MouseButtons.Left, 1, 100, 20, 0));
            var selectedAfterClick = _tabs.SelectedIndex;
            if (_tabs.TabCount > 0)
            {
                var slot = new Rectangle(0, 0, TabWidth, _tabBar.Height);
                var rect = new Rectangle(slot.Left + 4, 5, slot.Width - 8, slot.Height - 5);
                OnTabBarMouseUp(this, new MouseEventArgs(MouseButtons.Left, 1,
                    rect.Right - 15, rect.Top + 15, 0));
            }
            var afterClose = _tabs.TabCount;
            result["tabSelect"] = selectedAfterClick;

            result["private"] = _private;
            result["tabs"] = beforeClose;
            result["tabsAfterClose"] = afterClose;
            result["tabClose"] = $"{beforeClose}->{afterClose}";
            result["history"] = _history.Count;
            result["blocked"] = blocked;
            result["bookmarks"] = bookmarkCount;
            result["bookmarkMarked"] = bookmarkMarked;
            result["maximizeState"] = maxState.ToString();
            result["maximizeBounds"] = $"{maxBounds.Width}x{maxBounds.Height}";
            result["maximizeRestored"] = restoredBounds == normalBounds;
            result["maximizeButtonHit"] = HitWindowControl(new Point(
                ClientSize.Width - (int)(_windowButtonWidth * 1.5), _tabBar.Height / 2)).ToString();
            result["newTabUrl"] = NewTabUrl;
            await CreateTabAsync(NewTabUrl);
            await Task.Delay(1200);
            result["newTabOpened"] = CurrentWeb()?.Source?.AbsoluteUri;
            CloseCurrentTab();
            result["title"] = title;
            result["body"] = body;
            result["zoom"] = web?.ZoomFactor;
            result["find"] = find;
            result["fullscreen"] = $"{fullscreenOn}/{fullscreenOff}";
            result["settingsRoundTrip"] = reloaded.Homepage == roundTrip.Homepage
                                          && reloaded.AdblockEnabled == roundTrip.AdblockEnabled
                                          && reloaded.StrictTracking == roundTrip.StrictTracking;
            await CaptureAsync();
        }
        catch (Exception ex)
        {
            result["error"] = ex.ToString();
            AppPaths.Log("smoke failed: " + ex);
        }

        Directory.CreateDirectory(_outDir);
        var file = _private ? "private-smoke.json" : "smoke.json";
        File.WriteAllText(Path.Combine(_outDir, file),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        _allowClose = true;
        Close();
    }

    private async Task RunFullscreenSmokeAsync()
    {
        var result = new Dictionary<string, object?>();
        try
        {
            await CreateTabAsync(_smokeUrl);
            await Task.Delay(1500);
            var core = CurrentCore();
            if (core != null)
            {
                var args = JsonSerializer.Serialize(new
                {
                    expression = "document.getElementById('btn').click()",
                    userGesture = true,
                });
                await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", args);
            }
            await Task.Delay(1200);
            result["containsFullScreenElement"] = core?.ContainsFullScreenElement;
            result["hostFullscreen"] = _htmlFullscreen;
            result["chromeVisible"] = _strip.Visible;
            result["bounds"] = $"{Bounds.Width}x{Bounds.Height}";
            var edgePoint = new Point(Bounds.Left, Bounds.Top + 10);
            var hitParam = (edgePoint.Y << 16) | (edgePoint.X & 0xFFFF);
            result["edgeHitTest"] = SendMessage(Handle, WmNcHitTest, IntPtr.Zero, (IntPtr)hitParam).ToInt64();
            result["contentPadding"] = (CurrentWeb()?.Parent as Panel)?.Padding.Left;
            if (_htmlFullscreen) ExitHtmlFullscreen();
        }
        catch (Exception ex)
        {
            result["error"] = ex.ToString();
        }

        Directory.CreateDirectory(_outDir);
        File.WriteAllText(Path.Combine(_outDir, "fullscreen-smoke.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        _allowClose = true;
        Close();
    }

    private async Task CaptureAsync()
    {
        try
        {
            using var bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(_outDir, _private ? "private-ui.png" : "ui.png"));
        }
        catch (Exception ex)
        {
            AppPaths.Log("chrome capture failed: " + ex.Message);
        }

        try
        {
            if (CurrentCore() is { } core)
            {
                await using var fs = File.Create(Path.Combine(_outDir, _private ? "private-page.png" : "page.png"));
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            }
        }
        catch (Exception ex)
        {
            AppPaths.Log("page capture failed: " + ex.Message);
        }
    }

    private static async Task<string> EvalAsync(CoreWebView2? core, string script)
    {
        if (core == null) return "";
        try
        {
            var raw = await core.ExecuteScriptAsync(script);
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString() ?? "",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => doc.RootElement.GetRawText(),
            };
        }
        catch (Exception ex)
        {
            return "ERR: " + ex.Message;
        }
    }

    private static string Truncate(string? text, int length)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= length ? text : text[..length] + "…";
    }
}

internal sealed class PermissionForm : PxDialog
{
    private readonly RadioButton _allow;
    private readonly CheckBox _remember;

    public bool Allow { get; private set; }
    public bool Remember => _remember.Checked;

    public PermissionForm(string kind, string uri) : base("権限の確認", 520, 300)
    {
        var label = new Label
        {
            Text = $"{kind} を次のサイトに許可しますか？\n{uri}",
            Left = 20,
            Top = 18,
            Width = 460,
            Height = 54,
        };
        _allow = new PxRadio { Text = "許可", Left = 20, Top = 85, Width = 120 };
        var deny = new PxRadio { Text = "拒否", Left = 150, Top = 85, Width = 120, Checked = true };
        _remember = new PxCheckBox
        {
            Text = "このサイトの選択を記憶する（プライベートでは記憶しません）",
            Left = 20,
            Top = 120,
            Width = 470,
        };
        var ok = new Button { Text = "OK", Left = 300, Top = 180, Width = 80, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "キャンセル", Left = 390, Top = 180, Width = 90, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Allow = _allow.Checked;
        Body.Controls.AddRange(new Control[] { label, _allow, deny, _remember, ok, cancel });
        foreach (Control control in Body.Controls)
        {
            control.ForeColor = Theme.Cream;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Theme.InkSoft;
                button.FlatAppearance.BorderColor = Theme.Gold;
            }
        }
        AcceptButton = ok;
        CancelButton = cancel;
    }
}

internal sealed class Hum1ColorTable : ProfessionalColorTable
{


    public override Color ToolStripGradientBegin => Theme.Ink;
    public override Color ToolStripGradientMiddle => Theme.Ink;
    public override Color ToolStripGradientEnd => Theme.Ink;
    public override Color ToolStripBorder => Theme.Line;
    public override Color ToolStripContentPanelGradientBegin => Theme.Ink;
    public override Color ToolStripContentPanelGradientEnd => Theme.Ink;
    public override Color ToolStripDropDownBackground => Theme.Ink;
    public override Color ImageMarginGradientBegin => Theme.Ink;
    public override Color ImageMarginGradientMiddle => Theme.Ink;
    public override Color ImageMarginGradientEnd => Theme.Ink;
    public override Color ButtonSelectedHighlight => Theme.InkSoft;
    public override Color ButtonSelectedBorder => Theme.Gold;
    public override Color ButtonPressedHighlight => Theme.InkSoft;
    public override Color ButtonPressedBorder => Theme.Gold;
    public override Color ButtonCheckedHighlight => Theme.InkSoft;
    public override Color ButtonCheckedGradientBegin => Theme.InkSoft;
    public override Color ButtonCheckedGradientEnd => Theme.InkSoft;
    public override Color SeparatorDark => Theme.Line;
    public override Color SeparatorLight => Theme.Line;
    public override Color MenuItemSelected => Theme.InkSoft;
    public override Color MenuItemBorder => Theme.Gold;
    public override Color MenuBorder => Theme.Line;
    public override Color MenuItemSelectedGradientBegin => Theme.InkSoft;
    public override Color MenuItemSelectedGradientEnd => Theme.InkSoft;
}

internal sealed class Hum1ToolStripRenderer : ToolStripProfessionalRenderer
{
    public Hum1ToolStripRenderer() : base(new Hum1ColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Theme.Ink);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Flat chrome: no 3D border around the toolbar.
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? e.Item.ForeColor : Theme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed) return;
        var rect = new Rectangle(e.Item.Bounds.Left + 1, e.Item.Bounds.Top + 1,
            e.Item.Bounds.Width - 2, e.Item.Bounds.Height - 2);
        using var path = RoundedRect(rect, 8);
        using var brush = new SolidBrush(Theme.InkSoft);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Line);
        var x = e.Item.Bounds.Left + e.Item.Bounds.Width / 2;
        e.Graphics.DrawLine(pen, x, e.Item.Bounds.Top + 10, x, e.Item.Bounds.Bottom - 10);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rect = new Rectangle(e.Item.Bounds.Left + 4, e.Item.Bounds.Top + 2,
            e.Item.Bounds.Width - 8, e.Item.Bounds.Height - 4);
        using var path = RoundedRect(rect, 7);
        using var brush = new SolidBrush(e.Item.Selected ? Theme.InkSoft : Theme.Ink);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
        if (e.Item.Selected)
        {
            using var border = new Pen(Theme.Gold);
            e.Graphics.DrawPath(border, path);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class TabBarPanel : Panel
{
    public TabBarPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
}

internal sealed class BrowserTab : Panel
{
    public BrowserTab()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        BackColor = Theme.Ink;
    }
}

internal sealed class PxTabHost : Panel
{
    private readonly List<BrowserTab> _pages = new();
    private BrowserTab? _selected;

    public PxTabHost()
    {
        BackColor = Theme.Ink;
    }

    public IReadOnlyList<BrowserTab> TabPages => _pages;
    public int TabCount => _pages.Count;
    public int SelectedIndex { get; private set; } = -1;
    public BrowserTab? SelectedTab
    {
        get => _selected;
        set => SelectPage(value);
    }

    public event EventHandler? SelectedIndexChanged;

    public void AddPage(BrowserTab page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pages.Add(page);
        Controls.Add(page);
        if (_selected == null) SelectPage(page);
    }

    public void RemovePage(BrowserTab page)
    {
        var index = _pages.IndexOf(page);
        if (index < 0) return;
        var wasSelected = _selected == page;
        _pages.RemoveAt(index);
        Controls.Remove(page);
        page.Dispose();
        if (wasSelected)
        {
            _selected = null;
            SelectedIndex = -1;
            if (_pages.Count > 0)
            {
                SelectPage(_pages[Math.Min(index, _pages.Count - 1)]);
            }
            else
            {
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (_selected != null)
        {
            SelectedIndex = _pages.IndexOf(_selected);
        }
    }

    private void SelectPage(BrowserTab? page)
    {
        if (page == null || !_pages.Contains(page)) return;
        _selected = page;
        foreach (var candidate in _pages)
        {
            candidate.Visible = candidate == page;
        }
        page.BringToFront();
        var index = _pages.IndexOf(page);
        if (SelectedIndex == index) return;
        SelectedIndex = index;
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class RoundedAddressHost : ToolStripControlHost
{
    private readonly Panel _panel;
    private readonly TextBox _box;

    public RoundedAddressHost() : base(new Panel())
    {
        _panel = (Panel)Control;
        _panel.BackColor = Theme.Ink;
        _panel.Padding = Padding.Empty;
        _panel.Paint += (_, e) => PaintPill(e.Graphics);
        _panel.Resize += (_, _) => LayoutAddressBox();

        _box = new TextBox
        {
            Dock = DockStyle.None,
            AutoSize = false,
            Height = 24,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.InkSoft,
            ForeColor = Theme.Cream,
            Font = new Font("Yu Gothic UI", 9.5F),
            TextAlign = HorizontalAlignment.Left,
        };
        _box.Enter += (_, _) => _panel.Invalidate();
        _box.Leave += (_, _) => _panel.Invalidate();
        _panel.Controls.Add(_box);
        AutoSize = false;
        Size = new Size(620, 32);
        LayoutAddressBox();
    }

    public TextBox TextBox => _box;
    public new string Text { get => _box.Text; set => _box.Text = value; }
    public new bool Focused => _box.Focused;
    public new event KeyEventHandler? KeyDown
    {
        add => _box.KeyDown += value;
        remove => _box.KeyDown -= value;
    }

    public new bool Focus() => _box.Focus();
    public void SelectAll() => _box.SelectAll();

    public void ApplyTheme()
    {
        _panel.BackColor = Theme.Ink;
        _box.BackColor = Theme.InkSoft;
        _box.ForeColor = Theme.Cream;
        LayoutAddressBox();
        _panel.Invalidate();
    }

    private void LayoutAddressBox()
    {
        if (_panel.ClientSize.Width <= 0 || _panel.ClientSize.Height <= 0) return;
        var height = Math.Min(24, Math.Max(18, _panel.ClientSize.Height - 8));
        _box.Height = height;
        _box.Left = 14;
        _box.Width = Math.Max(0, _panel.ClientSize.Width - 28);
        _box.Top = (_panel.ClientSize.Height - height) / 2 + 1;
    }

    private void PaintPill(Graphics graphics)
    {
        if (_panel.ClientRectangle.Width < 8 || _panel.ClientRectangle.Height < 8) return;
        var rect = _panel.ClientRectangle;
        rect.Inflate(-1, -1);
        using var path = RoundedRect(rect, 16);
        using var fill = new SolidBrush(Theme.InkSoft);
        using var border = new Pen(_box.Focused ? Theme.Gold : Theme.Line,
            _box.Focused ? 1.5F : 1F);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

