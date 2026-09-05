using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>
    /// Win11 Fluent 风格 · 共享 / 观看 二合一主窗口（前端 v2：TableLayoutPanel 流式布局重构）。
    /// 逻辑与协议不变；布局全部改为 AutoSize / Dock / TableLayout，DPI 与尺寸变化不再错位。
    /// </summary>
    public sealed class ScreenShareForm : Form
    {
        // ============ 共享端（A） ============
        private readonly HostEngine _engine = new HostEngine();
        private Label _lblIp;
        private Label _lblPort;
        private FluentRadio _rbPng;
        private FluentRadio _rbJpeg;
        private FluentNumberUpDown _numFps;
        private FluentNumberUpDown _numQuality;
        private FluentButton _btnStart;
        private FluentButton _btnStop;
        private FluentCheck _chkShare;
        private FluentButton _btnBridge;
        private FluentListView _listClients;
        private TextBox _log;

        // ============ 观看端（B） ============
        private class HostEntry
        {
            public string Ip; public string Host; public int Port;
            public override string ToString() { return Host + "  " + Ip + ":" + Port; }
        }

        private readonly object _hostsLock = new object();
        private readonly List<HostEntry> _hosts = new List<HostEntry>();
        private Thread _discThread;
        private Thread _recvThread;
        private Thread _connThread;
        private volatile bool _viewerRunning = true;
        private volatile bool _connecting;
        private volatile bool _connected;
        private bool _autoConnect = true;

        private Label _vStatus;
        private FluentListView _vList;
        private FluentInput _txtManual;
        private PictureBox _pic;
        private Panel _picHostPanel;
        private Label _vInfo;
        private Form _fullForm;
        private int _fpsCount;
        private int _fpsShown;
        private DateTime _fpsTime = DateTime.Now;

        // 窗口化全屏相关
        private TableLayoutPanel _vTopTable;
        private TableLayoutPanel _vBodyTable;
        private Control _cFindCard;
        private Control _vInfoRow;
        private FluentButton _btnWinExit;
        private bool _windowed;
        private Panel _headerPanel;
        private Panel _navPanel;
        private Control _frameCard;
        private Control _canvasParent;   // 画面容器原来的父（TableLayout）

        private Panel _panelShare;
        private Panel _panelViewer;
        private NavItem _navShare;
        private NavItem _navViewer;
        private TableLayoutPanel _shareRoot;

        public ScreenShareForm()
        {
            Text = "屏幕共享";
            ClientSize = new Size(1160, 740);
            Font = F.BaseFont;
            BackColor = F.C.WindowBg;
            ForeColor = F.C.Text;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            // 应用图标（嵌入 exe 的 Fluent 图标；任务栏/Alt-Tab 显示）
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildHeader();
            BuildNav();
            _panelShare = BuildSharePanel();
            _panelViewer = BuildViewerPanel();

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(200, 40, 0, 0);
            content.Controls.Add(_panelShare);
            content.Controls.Add(_panelViewer);
            Controls.Add(content);

            ShowSharePanel();
            _engine.Log += AppLog;
            _engine.ClientCountChanged += OnClients;

            Load += (s, e) =>
            {
                Dwm.Apply(this);
                RefreshIps();
                StartDiscovery();
                PlayEntranceAnimations();
            };
            FormClosing += (s, e) => { _engine.Dispose(); _viewerRunning = false; };
        }

        /// <summary>入场动画：窗口淡入 + 共享页卡片依次上滑（easeOutCubic）</summary>
        private void PlayEntranceAnimations()
        {
            Opacity = 0.0;
            new Anim(0f, 260f, delegate(float t) { Opacity = t; });
            if (_shareRoot != null)
            {
                int i = 0;
                foreach (Control card in _shareRoot.Controls)
                {
                    int idx = i++;
                    int left = card.Margin.Left, right = card.Margin.Right, bottom = card.Margin.Bottom;
                    int baseTop = card.Margin.Top;
                    new Anim(idx * 45f, 300f, delegate(float t)
                    {
                        int top = (int)(baseTop + 24f * (1f - t));
                        card.Margin = new Padding(left, top, right, bottom);
                    });
                }
            }
        }

        /* =================================================================== */
        /* 框架：标题栏 / 导航                                                  */
        /* =================================================================== */
        private void BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 40;
            header.BackColor = F.C.WindowBg;
            header.MouseDown += HeaderDrag;

            Label title = new Label();
            title.Text = "屏幕共享";
            title.Font = F.TitleFont;
            title.ForeColor = F.C.Text;
            title.AutoSize = true;
            title.SetBounds(16, 8, 0, 0);
            title.MouseDown += HeaderDrag;

            CaptionButton btnMin = new CaptionButton();
            btnMin.SvgGlyph = "M5 12h14";
            CaptionButton btnMax = new CaptionButton();
            btnMax.SvgGlyph = "M5.5 5.5h13v13h-13z";
            CaptionButton btnClose = new CaptionButton();
            btnClose.Close = true;
            btnClose.SvgGlyph = "M6.5 6.5l11 11M17.5 6.5l-11 11";

            // 标题栏按钮流式靠右
            TableLayoutPanel tbtns = new TableLayoutPanel();
            tbtns.Dock = DockStyle.Right;
            tbtns.Width = 46 * 3;
            tbtns.Height = 40;
            tbtns.ColumnCount = 3;
            tbtns.RowCount = 1;
            for (int i = 0; i < 3; i++) tbtns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            tbtns.Controls.Add(btnMin, 0, 0);
            tbtns.Controls.Add(btnMax, 1, 0);
            tbtns.Controls.Add(btnClose, 2, 0);
            btnMin.Dock = DockStyle.Top; btnMax.Dock = DockStyle.Top; btnClose.Dock = DockStyle.Top;
            btnMin.Height = 34; btnMax.Height = 34; btnClose.Height = 34;
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;
            btnMax.Click += (s, e) =>
            {
                if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
                else WindowState = FormWindowState.Maximized;
            };
            btnClose.Click += (s, e) => Close();

            header.Controls.Add(title);
            header.Controls.Add(tbtns);
            _headerPanel = header;
            Controls.Add(header);
        }

        private void HeaderDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0); // WM_NCLBUTTONDOWN + HTCAPTION
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void BuildNav()
        {
            Panel nav = new Panel();
            nav.Dock = DockStyle.Left;
            nav.Width = 190;
            nav.BackColor = F.C.NavBg;

            _navShare = new NavItem();
            _navShare.Text = "共享端（发送）";
            _navShare.Icon = IconKind.Screen;
            _navShare.Dock = DockStyle.Top;
            _navShare.Height = 42;

            _navViewer = new NavItem();
            _navViewer.Text = "观看端（接收）";
            _navViewer.Icon = IconKind.Eye;
            _navViewer.Dock = DockStyle.Top;
            _navViewer.Height = 42;

            _navShare.Click += (s, e) => ShowSharePanel();
            _navViewer.Click += (s, e) => ShowViewerPanel();

            Label hint = new Label();
            hint.Text = "同一台电脑可同时开两个窗口\n（一个共享、一个观看）";
            hint.Font = F.SmallFont;
            hint.ForeColor = F.C.TextDim;
            hint.AutoSize = true;
            hint.SetBounds(14, 600, 0, 0);

            // 导航区顶部留白（spacer 最后添加 → Dock 布局时占据最上方）
            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Top;
            spacer.Height = 18;
            spacer.BackColor = F.C.NavBg;

            nav.Controls.Add(hint);
            nav.Controls.Add(_navViewer);
            nav.Controls.Add(_navShare);
            nav.Controls.Add(spacer);
            _navPanel = nav;
            Controls.Add(nav);
        }

        private void ShowSharePanel()
        {
            _navShare.Selected = true;
            _navViewer.Selected = false;
            _panelViewer.Visible = false;
            _panelShare.Visible = true;
        }

        private void ShowViewerPanel()
        {
            _navShare.Selected = false;
            _navViewer.Selected = true;
            _panelShare.Visible = false;
            _panelViewer.Visible = true;
        }

        /* =================================================================== */
        /* 控件工厂                                                            */
        /* =================================================================== */
        private static Label MakeLabel(string text, Color color, Size style)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = F.BaseFont;
            l.ForeColor = color;
            l.AutoSize = true;
            l.Margin = new Padding(0);
            return l;
        }

        private static TableLayoutPanel MakeTable(int cols, int rows)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = cols;
            t.RowCount = rows;
            t.Dock = DockStyle.Fill;
            t.Margin = new Padding(0);
            for (int i = 0; i < cols; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < rows; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return t;
        }

        /// <summary>卡片：标题 + 内容区（Padding 顶部留标题 34px）</summary>
        private static TableLayoutPanel CardBody(FluentCard card, int padL, int padR, int padB)
        {
            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(padL, 32, padR, padB);
            body.Margin = new Padding(0);
            body.BackColor = F.C.Card;
            card.Controls.Add(body);
            return body;
        }

        /* =================================================================== */
        /* 共享端页面（TableLayout 流式）                                       */
        /* =================================================================== */
        private Panel BuildSharePanel()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = F.C.WindowBg;

            TableLayoutPanel root = MakeTable(1, 6);
            _shareRoot = root;
            root.Padding = new Padding(12, 10, 12, 10);
            root.AutoScroll = true;
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 卡1 本机信息
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 卡2 采集设置
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 卡3 雷电网桥
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 按钮行
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60F)); // 列表卡
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F)); // 日志卡
            page.Controls.Add(root);

            // ---- 卡1 本机信息 ----
            FluentCard cInfo = new FluentCard();
            cInfo.Caption = "本机信息（观看端会自动发现，无需填写）";
            cInfo.Dock = DockStyle.Fill;
            cInfo.Margin = new Padding(0, 0, 0, 10);
            TableLayoutPanel t1 = CardBody(cInfo, 26, 26, 14);
            t1.RowCount = 2; t1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _lblIp = MakeLabel("IP：加载中…", F.C.Text, new Size(0, 22));
            _lblPort = MakeLabel("端口：TCP " + Settings.Port + "　UDP 发现 " + Settings.DiscoveryPort, F.C.TextDim, new Size(0, 22));
            t1.Controls.Add(_lblIp, 0, 0);
            t1.Controls.Add(_lblPort, 0, 1);
            root.Controls.Add(cInfo, 0, 0);

            // ---- 卡2 采集设置 ----
            FluentCard cSet = new FluentCard();
            cSet.Caption = "采集设置";
            cSet.Dock = DockStyle.Fill;
            cSet.Margin = new Padding(0, 0, 0, 10);
            TableLayoutPanel t2 = CardBody(cSet, 12, 12, 12);
            t2.ColumnCount = 8;
            t2.ColumnStyles.Clear();
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // PNG
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // JPEG
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 帧率
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 质量
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            t2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); // 提示

            _rbPng = new FluentRadio();
            _rbPng.Text = "PNG 无损（无色差，推荐）";
            _rbPng.Checked = true;
            _rbPng.Width = 190;
            _rbPng.Margin = new Padding(0, 12, 24, 0);
            _rbJpeg = new FluentRadio();
            _rbJpeg.Text = "JPEG 快速";
            _rbJpeg.Width = 110;
            _rbJpeg.Margin = new Padding(0, 12, 0, 0);
            Label lFps = MakeLabel("帧率", F.C.TextDim, new Size(0, 22));
            lFps.Margin = new Padding(0, 17, 4, 0);
            _numFps = new FluentNumberUpDown();
            _numFps.Minimum = 5; _numFps.Maximum = 30; _numFps.Value = Settings.Fps;
            _numFps.Margin = new Padding(0, 14, 0, 0);
            _numFps.Width = 90;
            Label lQ = MakeLabel("质量", F.C.TextDim, new Size(0, 22));
            lQ.Margin = new Padding(0, 17, 4, 0);
            _numQuality = new FluentNumberUpDown();
            _numQuality.Minimum = 40; _numQuality.Maximum = 100; _numQuality.Value = Settings.Quality;
            _numQuality.Margin = new Padding(0, 14, 0, 0);
            _numQuality.Width = 90;
            _numQuality.Enabled = false;
            _rbJpeg.CheckedChanged += (s, e) => _numQuality.Enabled = _rbJpeg.Checked;
            Label lHint = MakeLabel("提示：追求质量用 PNG，追求流畅用 JPEG", F.C.TextDim, new Size(0, 22));
            lHint.Margin = new Padding(10, 17, 0, 0);
            t2.Controls.Add(_rbPng, 0, 0);
            t2.Controls.Add(_rbJpeg, 1, 0);
            t2.Controls.Add(_numFps, 4, 0);
            t2.Controls.Add(_numQuality, 6, 0);
            root.Controls.Add(cSet, 0, 1);

            // ---- 卡3 雷电网桥 ----
            FluentCard cBridge = new FluentCard();
            cBridge.Caption = "雷电网桥（雷电 / USB4 点对点网络）";
            cBridge.Dock = DockStyle.Fill;
            cBridge.Margin = new Padding(0, 0, 0, 10);
            TableLayoutPanel t3 = CardBody(cBridge, 12, 12, 12);
            t3.ColumnCount = 3;
            t3.ColumnStyles.Clear();
            t3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            t3.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _chkShare = new FluentCheck();
            _chkShare.Text = "同时允许对端访问文件共享 (SMB 445)";
            _chkShare.Width = 260;
            _chkShare.Margin = new Padding(0, 12, 0, 0);
            Label lbHint2 = MakeLabel("两台电脑同时点击，3 分钟内自动完成（USB4 连线 + UAC 选「是」）", F.C.TextDim, new Size(0, 22));
            lbHint2.Margin = new Padding(16, 17, 8, 0);
            _btnBridge = new FluentButton();
            _btnBridge.Text = "自动配置雷电网桥";
            _btnBridge.Icon = IconKind.Bridge;
            _btnBridge.Size = new Size(190, 32);
            _btnBridge.Margin = new Padding(0, 7, 0, 0);
            _btnBridge.Click += (s, e) => OnBridge();
            t3.Controls.Add(_chkShare, 0, 0);
            t3.Controls.Add(lbHint2, 1, 0);
            t3.Controls.Add(_btnBridge, 2, 0);
            root.Controls.Add(cBridge, 0, 2);

            // ---- 按钮行（流式） ----
            FlowLayoutPanel fl = new FlowLayoutPanel();
            fl.Dock = DockStyle.Fill;
            fl.Margin = new Padding(0, 0, 0, 10);
            fl.WrapContents = false;
            fl.AutoSize = true;
            _btnStart = new FluentButton();
            _btnStart.Text = "开始共享";
            _btnStart.Primary = true;
            _btnStart.Icon = IconKind.Play;
            _btnStart.Size = new Size(140, 36);
            _btnStart.Margin = new Padding(0, 0, 12, 0);
            _btnStart.Click += (s, e) => StartShare();
            _btnStop = new FluentButton();
            _btnStop.Text = "停止共享";
            _btnStop.Danger = true;
            _btnStop.Icon = IconKind.Stop;
            _btnStop.Size = new Size(140, 36);
            _btnStop.Enabled = false;
            _btnStop.Click += (s, e) => StopShare();
            fl.Controls.Add(_btnStart);
            fl.Controls.Add(_btnStop);
            root.Controls.Add(fl, 0, 3);

            // ---- 卡4 已连接观看端 ----
            FluentCard cCli = new FluentCard();
            cCli.Caption = "已连接观看端";
            cCli.Dock = DockStyle.Fill;
            cCli.Margin = new Padding(0, 0, 0, 10);
            TableLayoutPanel t4 = CardBody(cCli, 12, 12, 12);
            _listClients = new FluentListView();
            _listClients.Columns.Add("地址", 560);
            _listClients.Columns.Add("状态", 240);
            _listClients.Dock = DockStyle.Fill;
            t4.Controls.Add(_listClients, 0, 0);
            root.Controls.Add(cCli, 0, 4);

            // ---- 卡5 日志 ----
            FluentCard cLog = new FluentCard();
            cLog.Caption = "日志";
            cLog.Dock = DockStyle.Fill;
            TableLayoutPanel t5 = CardBody(cLog, 12, 12, 12);
            _log = new TextBox();
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BorderStyle = BorderStyle.None;
            _log.Dock = DockStyle.Fill;
            _log.BackColor = F.C.CardAlt; _log.ForeColor = F.C.TextDim; _log.Font = F.SmallFont;
            t5.Controls.Add(_log, 0, 0);
            root.Controls.Add(cLog, 0, 5);

            return page;
        }

        /* =================================================================== */
        /* 观看端页面（TableLayout 流式）                                       */
        /* =================================================================== */
        private Panel BuildViewerPanel()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = F.C.WindowBg;

            TableLayoutPanel root = MakeTable(1, 3);
            root.Padding = new Padding(12, 10, 12, 10);
            root.RowStyles.Clear();
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 顶栏
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 主体
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 底部信息
            page.Controls.Add(root);

            // ---- 顶栏 ----
            TableLayoutPanel top = MakeTable(5, 1);
            _vTopTable = top;
            top.ColumnStyles.Clear();
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _vStatus = MakeLabel("正在自动发现共享端…", F.C.Amber, new Size(0, 26));
            _vStatus.Margin = new Padding(0, 6, 0, 0);
            _txtManual = new FluentInput();
            _txtManual.Text = "IP:端口";
            _txtManual.Height = 30;
            _txtManual.Dock = DockStyle.Top; _txtManual.Margin = new Padding(0, 4, 8, 0);
            FluentButton btnManual = new FluentButton();
            btnManual.Text = "直连";
            btnManual.Icon = IconKind.Link;
            btnManual.Size = new Size(72, 30);
            btnManual.Margin = new Padding(0, 3, 8, 0);
            btnManual.Click += (s, e) => ManualConnect();
            FluentButton btnFull = new FluentButton();
            btnFull.Text = "全屏";
            btnFull.Icon = IconKind.Fullscreen;
            btnFull.Size = new Size(84, 30);
            btnFull.Margin = new Padding(0, 3, 8, 0);
            btnFull.Click += (s, e) => ToggleFullscreen();
            FluentButton btnWin = new FluentButton();
            btnWin.Text = "窗口化全屏";
            btnWin.Icon = IconKind.Screen;
            btnWin.Size = new Size(120, 30);
            btnWin.Margin = new Padding(0, 3, 0, 0);
            btnWin.Click += (s, e) => ToggleWindowed();
            top.Controls.Add(_vStatus, 0, 0);
            top.Controls.Add(_txtManual, 1, 0);
            top.Controls.Add(btnManual, 2, 0);
            top.Controls.Add(btnFull, 3, 0);
            top.Controls.Add(btnWin, 4, 0);
            root.Controls.Add(top, 0, 0);

            // ---- 主体：左列表 + 画面 ----
            TableLayoutPanel body = MakeTable(2, 1);
            _vBodyTable = body;
            body.Margin = new Padding(0, 8, 0, 8);
            body.ColumnStyles.Clear();
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            FluentCard cFind = new FluentCard();
            cFind.Caption = "自动发现的共享端（双击连接）";
            cFind.Dock = DockStyle.Fill;
            cFind.Margin = new Padding(0, 0, 10, 0);
            _cFindCard = cFind;
            TableLayoutPanel tFind = CardBody(cFind, 12, 12, 12);
            _vList = new FluentListView();
            _vList.Columns.Add("主机", 88);
            _vList.Columns.Add("地址", 120);
            _vList.Dock = DockStyle.Fill;
            _vList.DoubleClick += (s, e) => ConnectSelected();
            tFind.Controls.Add(_vList, 0, 0);
            body.Controls.Add(cFind, 0, 0);

            FluentCard cFrame = new FluentCard();
            cFrame.Caption = "实时画面";
            cFrame.Dock = DockStyle.Fill;
            _frameCard = cFrame;
            TableLayoutPanel tFrame = CardBody(cFrame, 12, 12, 12);
            _picHostPanel = new Panel();
            _picHostPanel.Dock = DockStyle.Fill;
            _picHostPanel.BackColor = Color.Black;
            _pic = new PictureBox();
            _pic.Dock = DockStyle.Fill;
            _pic.SizeMode = PictureBoxSizeMode.Zoom;
            _pic.BackColor = Color.Black;
            _pic.DoubleClick += (s, e) => ToggleFullscreen();
            _picHostPanel.Controls.Add(_pic);
            tFrame.Controls.Add(_picHostPanel, 0, 0);
            // 窗口化全屏模式下的退出按钮（悬浮右上）
            _btnWinExit = new FluentButton();
            _btnWinExit.Text = "退出窗口化";
            _btnWinExit.Icon = IconKind.Screen;
            _btnWinExit.Size = new Size(110, 28);
            _btnWinExit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnWinExit.Location = new Point(cFrame.Width - 122, 6);
            _btnWinExit.Visible = false;
            _btnWinExit.Click += (s, e) => ToggleWindowed();
            cFrame.Resize += (s, e) => _btnWinExit.Location = new Point(cFrame.Width - 122, 6);
            cFrame.Controls.Add(_btnWinExit);
            body.Controls.Add(cFrame, 1, 0);

            root.Controls.Add(body, 0, 1);

            // ---- 底部信息 ----
            Label lHint3 = MakeLabel("双击画面全屏 / Esc 退出；断线自动重连", F.C.TextDim, new Size(0, 20));
            root.Controls.Add(lHint3, 0, 2);

            _vInfo = MakeLabel("等待画面…  （请提醒共享方点击「开始共享」）", F.C.TextDim, new Size(0, 20));
            _vInfo.Dock = DockStyle.Right;
            root.Controls.Add(_vInfo, 0, 2);

            // 顶栏 + 信息独立行（右侧信息行与提示同行）
            FlowLayoutPanel infoRow = new FlowLayoutPanel();
            infoRow.Dock = DockStyle.Fill;
            infoRow.WrapContents = false;
            infoRow.AutoSize = true;
            infoRow.Controls.Add(lHint3);
            lHint3.Margin = new Padding(0, 2, 24, 0);
            infoRow.Controls.Add(_vInfo);
            _vInfo.Margin = new Padding(0, 2, 0, 0);
            root.Controls.Remove(lHint3);
            root.Controls.Remove(_vInfo);
            root.Controls.Add(infoRow, 0, 2);
            _vInfoRow = infoRow;

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F11) { ToggleFullscreen(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape && _windowed) { ToggleWindowed(); e.Handled = true; }
            };
            return page;
        }

        /// <summary>
        /// 窗口化全屏：画面直接占满整个窗口（导航/标题栏/卡片全部隐藏），
        /// 悬浮右上角「退出窗口化」按钮，Esc 退出。窗口边框与 DWM 圆角保留。
        /// </summary>
        private void ToggleWindowed()
        {
            _windowed = !_windowed;
            if (_windowed)
            {
                // 记录画面容器原位，隐藏 chrome，画面占满整个窗体
                _canvasParent = _picHostPanel.Parent;
                _headerPanel.Visible = false;
                _navPanel.Visible = false;
                _panelViewer.Visible = false;

                _picHostPanel.Parent = this;
                _picHostPanel.Dock = DockStyle.Fill;
                _picHostPanel.BringToFront();

                _btnWinExit.Parent = this;
                _btnWinExit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _btnWinExit.Location = new Point(Width - 140, 12);
                _btnWinExit.Visible = true;
                _btnWinExit.BringToFront();
            }
            else
            {
                _headerPanel.Visible = true;
                _navPanel.Visible = true;
                _panelViewer.Visible = true;
                if (_canvasParent != null)
                {
                    _picHostPanel.Parent = _canvasParent;
                    _picHostPanel.Dock = DockStyle.Fill;
                }
                _btnWinExit.Visible = false;
                _btnWinExit.Parent = _frameCard;
                _btnWinExit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _btnWinExit.Location = new Point(_frameCard.Width - 128, 6);
            }
        }

        /* =================================================================== */
        /* 共享端逻辑                                                          */
        /* =================================================================== */
        private void RefreshIps()
        {
            string[] ips = HostEngine.GetLanIps().ToArray();
            _lblIp.Text = "IP：" + (ips.Length == 0 ? "（无局域网地址）" : string.Join("　", ips));
        }

        private void AppLog(string s)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => AppLog(s))); return; }
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss ") + s + Environment.NewLine);
        }

        private void OnClients(int count)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => OnClients(count))); return; }
            _listClients.Items.Clear();
            string[] eps = _engine.GetClientEndpoints().ToArray();
            for (int i = 0; i < count && i < eps.Length; i++)
            {
                ListViewItem it = new ListViewItem(eps.Length > i ? eps[i] : ("观看端 #" + (i + 1)));
                it.SubItems.Add("已连接");
                _listClients.Items.Add(it);
            }
        }

        private void StartShare()
        {
            Settings.Format = _rbPng.Checked ? "png" : "jpeg";
            Settings.Fps = (int)_numFps.Value;
            Settings.Quality = (int)_numQuality.Value;
            try { _engine.Start(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "无法开始共享", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            RefreshIps();
            AppLog("已开始共享（等待观看端接入）");
        }

        private void StopShare()
        {
            _engine.Dispose();
            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            _listClients.Items.Clear();
            AppLog("已停止共享");
        }

        private void OnBridge()
        {
            if (BridgeConfigurer.Run(_chkShare.Checked))
                AppLog("已启动雷电网桥自动配置（新窗口），对端电脑也请点击「自动配置雷电网桥」");
            else
                MessageBox.Show(this, "无法启动配置脚本。", "雷电网桥", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* =================================================================== */
        /* 观看端逻辑                                                          */
        /* =================================================================== */
        private void ManualConnect()
        {
            string t = _txtManual.Text.Trim();
            if (t.Length == 0) return;
            HostEntry h = new HostEntry();
            h.Ip = t; h.Host = "手动"; h.Port = Settings.Port;
            int idx = t.LastIndexOf(':');
            if (idx > 0)
            {
                int p; if (int.TryParse(t.Substring(idx + 1), out p) && p > 0) { h.Port = p; h.Ip = t.Substring(0, idx); }
            }
            StartConnectThread(h);
        }

        private void ConnectSelected()
        {
            if (_vList.SelectedItems.Count == 0) return;
            HostEntry h = (HostEntry)_vList.SelectedItems[0].Tag;
            if (h != null) StartConnectThread(h);
        }

        private static List<IPAddress> GetBroadcastAddresses()
        {
            List<IPAddress> list = new List<IPAddress>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(ip.Address)) continue;
                        byte[] a = ip.Address.GetAddressBytes();
                        byte[] m = ip.IPv4Mask == null ? new byte[4] : ip.IPv4Mask.GetAddressBytes();
                        byte[] b = new byte[4];
                        for (int k = 0; k < 4; k++) b[k] = (byte)(a[k] | (byte)~m[k]);
                        list.Add(new IPAddress(b));
                    }
                }
            }
            catch { }
            return list;
        }

        private void StartDiscovery()
        {
            _recvThread = new Thread(DiscoveryReceiveLoop);
            _recvThread.IsBackground = true;
            _recvThread.Start();
            _discThread = new Thread(DiscoverySendLoop);
            _discThread.IsBackground = true;
            _discThread.Start();
        }

        private void DiscoverySendLoop()
        {
            while (_viewerRunning)
            {
                UdpClient u = null;
                try
                {
                    u = new UdpClient();
                    u.EnableBroadcast = true;
                    u.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    byte[] d = Encoding.UTF8.GetBytes("SCREENSHARE|DISC");
                    foreach (IPAddress b in GetBroadcastAddresses())
                    {
                        try { u.Send(d, d.Length, new IPEndPoint(b, Settings.DiscoveryPort)); }
                        catch { }
                    }
                }
                catch { }
                finally { try { if (u != null) u.Close(); } catch { } }
                for (int i = 0; i < 3 && _viewerRunning; i++) Thread.Sleep(1000);
            }
        }

        private void DiscoveryReceiveLoop()
        {
            while (_viewerRunning)
            {
                UdpClient u = new UdpClient();
                try
                {
                    u.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    u.EnableBroadcast = true;
                    while (_viewerRunning)
                    {
                        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                        byte[] d = u.Receive(ref ep);
                        string s = Encoding.UTF8.GetString(d);
                        if (s.StartsWith("SCREENSHARE|HERE|"))
                        {
                            string[] p = s.Split('|');
                            if (p.Length >= 4)
                            {
                                int port; int.TryParse(p[3], out port);
                                AddHost(ep.Address.ToString(), p[2], port);
                            }
                        }
                    }
                }
                catch { if (_viewerRunning) Thread.Sleep(300); }
                finally { try { u.Close(); } catch { } }
            }
        }

        private void AddHost(string ip, string host, int port)
        {
            if (!_viewerRunning) return;
            lock (_hostsLock)
            {
                foreach (HostEntry h in _hosts) if (h.Ip == ip && h.Port == port) return;
                _hosts.Add(new HostEntry { Ip = ip, Host = host, Port = port });
            }
            if (InvokeRequired) { BeginInvoke((Action)(() => AddHostToList(ip, host, port))); }
            else AddHostToList(ip, host, port);
            if (_autoConnect && !_connecting && !_connected) AutoConnect();
        }

        private void AddHostToList(string ip, string host, int port)
        {
            foreach (ListViewItem it in _vList.Items)
                if (it.Tag != null && ((HostEntry)it.Tag).Ip == ip) return;
            HostEntry h = new HostEntry { Ip = ip, Host = host, Port = port };
            ListViewItem item = new ListViewItem(host);
            item.SubItems.Add(ip + ":" + port);
            item.Tag = h;
            _vList.Items.Add(item);
        }

        private void AutoConnect()
        {
            HostEntry target = null;
            lock (_hostsLock) { if (_hosts.Count > 0) target = _hosts[0]; }
            if (target != null) StartConnectThread(target);
        }

        private void StartConnectThread(HostEntry h)
        {
            if (_connecting) return;
            _connecting = true;
            SetStatus("正在连接 " + h.Ip + ":" + h.Port + " …", F.C.Amber);
            _connThread = new Thread(() => ConnectLoop(h));
            _connThread.IsBackground = true;
            _connThread.Start();
        }

        private void ConnectLoop(HostEntry h)
        {
            while (_viewerRunning)
            {
                try
                {
                    TcpClient tcp = new TcpClient();
                    IAsyncResult ar = tcp.BeginConnect(h.Ip, h.Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(3000)) { try { tcp.Close(); } catch { } throw new Exception("连接超时"); }
                    tcp.EndConnect(ar);

                    _connected = true;
                    _connecting = false;
                    SetStatus("已连接 " + h.Ip + ":" + h.Port + "（" + h.Host + "）", F.C.Green);

                    NetworkStream ns = tcp.GetStream();
                    byte[] header = new byte[13];
                    while (_viewerRunning)
                    {
                        if (!ReadExact(ns, header, 13)) break;
                        int w = (header[1] << 24) | (header[2] << 16) | (header[3] << 8) | header[4];
                        int ht = (header[5] << 24) | (header[6] << 16) | (header[7] << 8) | header[8];
                        int len = (header[9] << 24) | (header[10] << 16) | (header[11] << 8) | header[12];
                        if (len <= 0 || len > 64 * 1024 * 1024) break;
                        byte[] data = new byte[len];
                        if (!ReadExact(ns, data, len)) break;
                        try
                        {
                            using (MemoryStream ms = new MemoryStream(data))
                            {
                                Bitmap bmp = new Bitmap(ms);
                                ShowFrame(bmp, w, ht);
                            }
                        }
                        catch { }
                    }
                    try { tcp.Close(); } catch { }
                }
                catch { }
                finally
                {
                    if (_viewerRunning) { _connected = false; _connecting = true; }
                }
                if (!_viewerRunning) break;
                SetStatus("连接已断开，正在重新发现并重连…", F.C.Danger);
                Thread.Sleep(3000);
            }
            _connecting = false;
        }

        private static bool ReadExact(NetworkStream ns, byte[] buf, int need)
        {
            int off = 0;
            while (off < need)
            {
                int n = ns.Read(buf, off, need - off);
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        private void ShowFrame(Bitmap bmp, int w, int h)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => ShowFrame(bmp, w, h))); return; }
            Image old = _pic.Image;
            _pic.Image = bmp;
            if (old != null) old.Dispose();
            _fpsCount++;
            DateTime now = DateTime.Now;
            double s = (now - _fpsTime).TotalSeconds;
            if (s >= 1.0) { _fpsShown = (int)(_fpsCount / s); _fpsCount = 0; _fpsTime = now; }
            string id = _pic.Image == null ? "" : "; " + _pic.Image.Width + "×" + _pic.Image.Height;
            _vInfo.Text = "实时画面" + id + " · " + _fpsShown + " fps";
        }

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetStatus(text, color))); return; }
            _vStatus.Text = text;
            _vStatus.ForeColor = color;
        }

        private void ToggleFullscreen()
        {
            if (_fullForm == null)
            {
                _fullForm = new Form();
                _fullForm.FormBorderStyle = FormBorderStyle.None;
                _fullForm.WindowState = FormWindowState.Maximized;
                _fullForm.BackColor = Color.Black;
                _fullForm.KeyPreview = true;
                _fullForm.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) ToggleFullscreen(); };
                _fullForm.Controls.Add(_pic);
                _pic.Dock = DockStyle.Fill;
                Dwm.Apply(_fullForm);
                _fullForm.Show();
            }
            else
            {
                _fullForm.Controls.Remove(_pic);
                _fullForm.Close();
                _fullForm = null;
                _picHostPanel.Controls.Add(_pic);
                _pic.Dock = DockStyle.Fill;
            }
        }
    }
}
