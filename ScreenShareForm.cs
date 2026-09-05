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
    /// Win11 Fluent 风格 · 共享 / 观看 二合一主窗口。
    /// 无边框圆角窗体 + 自定义标题栏 + 左侧导航 + 圆角卡片，全部逻辑与旧版一致。
    /// </summary>
    public sealed class ScreenShareForm : Form
    {
        // ============ 共享端（A） ============
        private readonly HostEngine _engine = new HostEngine();
        private Label _lblIp;
        private Label _lblPort;
        private RadioButton _rbPng;
        private RadioButton _rbJpeg;
        private NumericUpDown _numFps;
        private NumericUpDown _numQuality;
        private FluentButton _btnStart;
        private FluentButton _btnStop;
        private CheckBox _chkShare;
        private FluentButton _btnBridge;
        private ListView _listClients;
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
        private ListView _vList;
        private TextBox _txtManual;
        private PictureBox _pic;
        private Panel _picHostPanel;
        private Label _vInfo;
        private Form _fullForm;
        private int _fpsCount;
        private int _fpsShown;
        private DateTime _fpsTime = DateTime.Now;

        private Panel _panelShare;
        private Panel _panelViewer;
        private NavItem _navShare;
        private NavItem _navViewer;

        public ScreenShareForm()
        {
            Text = "屏幕共享";
            ClientSize = new Size(1160, 740);
            Font = F.BaseFont;
            BackColor = F.C.WindowBg;
            ForeColor = F.C.Text;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;

            // 高 DPI：配合 manifest 的 PerMonitorV2，让运行时添加的控件随 DPI 缩放
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            BuildHeader();
            BuildNav();
            ConfigureSharing();
            _panelShare = BuildSharePanel();
            _panelViewer = BuildViewerPanel();
            ConfigureViewer();

            // 内容区容器
            Panel content = new Panel();
            content.SetBounds(190, 40, 1160 - 190, 740 - 40);
            content.Controls.Add(_panelShare);
            content.Controls.Add(_panelViewer);
            Controls.Add(content);

            ShowSharePanel();
            _engine.Log += AppLog;
            _engine.ClientCountChanged += OnClients;

            Load += (s, e) => { Dwm.Apply(this); RefreshIps(); StartDiscovery(); };
            FormClosing += (s, e) => { _engine.Dispose(); _viewerRunning = false; };
        }

        /* =================================================================== */
        /* 框架：标题栏 / 导航                                                  */
        /* =================================================================== */
        private void BuildHeader()
        {
            Panel header = new Panel();
            header.SetBounds(0, 0, 1160, 40);
            header.BackColor = F.C.WindowBg;
            header.MouseDown += HeaderDrag;

            Label title = new Label();
            title.Text = "屏幕共享";
            title.Font = F.TitleFont;
            title.ForeColor = F.C.Text;
            title.SetBounds(16, 8, 140, 24);
            title.MouseDown += HeaderDrag;

            Label sub = new Label();
            sub.Text = "局域网 · 零配置 · 自动联系";
            sub.Font = F.SmallFont;
            sub.ForeColor = F.C.TextDim;
            sub.SetBounds(150, 13, 220, 20);
            sub.MouseDown += HeaderDrag;

            CaptionButton btnMin = new CaptionButton();
            btnMin.Text = "_";
            btnMin.SetBounds(1160 - 46 * 3, 3, 46, 34);
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            CaptionButton btnMax = new CaptionButton();
            btnMax.Text = "□";
            btnMax.SetBounds(1160 - 46 * 2, 3, 46, 34);
            btnMax.Click += (s, e) =>
            {
                if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
                else WindowState = FormWindowState.Maximized;
            };

            CaptionButton btnClose = new CaptionButton();
            btnClose.Close = true;
            btnClose.SetBounds(1160 - 46, 3, 46, 34);
            btnClose.Click += (s, e) => Close();

            header.Controls.AddRange(new Control[] { title, sub, btnMin, btnMax, btnClose });
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
            nav.SetBounds(0, 40, 190, 740 - 40);
            nav.BackColor = F.C.NavBg;

            _navShare = new NavItem();
            _navShare.Text = "共享端（发送）";
            _navShare.Icon = IconKind.Screen;
            _navShare.SetBounds(0, 16, 190, 42);
            _navShare.Click += (s, e) => ShowSharePanel();

            _navViewer = new NavItem();
            _navViewer.Text = "观看端（接收）";
            _navViewer.Icon = IconKind.Eye;
            _navViewer.SetBounds(0, 62, 190, 42);
            _navViewer.Click += (s, e) => ShowViewerPanel();

            Label hint = new Label();
            hint.Text = "同一台电脑可同时开两个窗口\n（一个共享、一个观看）";
            hint.Font = F.SmallFont;
            hint.ForeColor = F.C.TextDim;
            hint.SetBounds(14, 620, 170, 44);

            nav.Controls.AddRange(new Control[] { _navShare, _navViewer, hint });
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

        private static Label MakeLabel(string text, int x, int y, int w, int h, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = F.BaseFont;
            l.ForeColor = color;
            l.SetBounds(x, y, w, h);
            return l;
        }

        /* =================================================================== */
        /* 共享端页面                                                          */
        /* =================================================================== */
        private Panel BuildSharePanel()
        {
            Panel page = new Panel();
            page.SetBounds(0, 0, 970, 700);
            page.BackColor = F.C.WindowBg;

            // 卡1 本机信息
            FluentCard cInfo = new FluentCard();
            cInfo.Caption = "本机信息（观看端会自动发现，无需填写）";
            cInfo.SetBounds(12, 10, 946, 92);
            _lblIp = MakeLabel("IP：加载中…", 26, 34, 900, 22, F.C.Text);
            _lblPort = MakeLabel("端口：TCP " + Settings.Port + "　UDP 发现 " + Settings.DiscoveryPort, 26, 60, 900, 22, F.C.TextDim);
            cInfo.Controls.Add(_lblIp);
            cInfo.Controls.Add(_lblPort);
            page.Controls.Add(cInfo);

            // 卡2 采集设置
            FluentCard cSet = new FluentCard();
            cSet.Caption = "采集设置";
            cSet.SetBounds(12, 112, 946, 92);
            _rbPng = new RadioButton();
            _rbPng.Text = "PNG 无损（无色差，推荐）";
            _rbPng.Checked = true;
            _rbPng.SetBounds(26, 36, 220, 22);
            _rbPng.BackColor = F.C.Card; _rbPng.ForeColor = F.C.Text;
            _rbJpeg = new RadioButton();
            _rbJpeg.Text = "JPEG 快速";
            _rbJpeg.SetBounds(254, 36, 130, 22);
            _rbJpeg.BackColor = F.C.Card; _rbJpeg.ForeColor = F.C.Text;
            Label lFps = MakeLabel("帧率", 396, 38, 40, 22, F.C.TextDim);
            _numFps = new NumericUpDown();
            _numFps.Minimum = 5; _numFps.Maximum = 30; _numFps.Value = Settings.Fps;
            _numFps.SetBounds(436, 34, 62, 26);
            _numFps.BackColor = F.C.CardAlt; _numFps.ForeColor = F.C.Text; _numFps.BorderStyle = BorderStyle.FixedSingle;
            Label lQ = MakeLabel("质量", 510, 38, 40, 22, F.C.TextDim);
            _numQuality = new NumericUpDown();
            _numQuality.Minimum = 40; _numQuality.Maximum = 100; _numQuality.Value = Settings.Quality;
            _numQuality.SetBounds(550, 34, 62, 26);
            _numQuality.BackColor = F.C.CardAlt; _numQuality.ForeColor = F.C.Text; _numQuality.BorderStyle = BorderStyle.FixedSingle;
            _numQuality.Enabled = false;
            _rbJpeg.CheckedChanged += (s, e) => _numQuality.Enabled = _rbJpeg.Checked;
            Label lHint = MakeLabel("提示：追求质量用 PNG，追求流畅用 JPEG", 640, 38, 280, 22, F.C.TextDim);
            cSet.Controls.AddRange(new Control[] { _rbPng, _rbJpeg, lFps, _numFps, lQ, _numQuality, lHint });
            page.Controls.Add(cSet);

            // 卡3 雷电网桥
            FluentCard cBridge = new FluentCard();
            cBridge.Caption = "雷电网桥（雷电 / USB4 点对点网络）";
            cBridge.SetBounds(12, 214, 946, 84);
            _chkShare = new CheckBox();
            _chkShare.Text = "同时允许对端访问文件共享 (SMB 445)";
            _chkShare.SetBounds(26, 38, 260, 22);
            _chkShare.BackColor = F.C.Card; _chkShare.ForeColor = F.C.Text;
            Label lbHint2 = MakeLabel("两台电脑同时点击，3 分钟内自动完成（USB4 连线 + UAC 选「是」）", 296, 40, 380, 22, F.C.TextDim);
            _btnBridge = new FluentButton();
            _btnBridge.Text = "自动配置雷电网桥";
            _btnBridge.Icon = IconKind.Bridge;
            _btnBridge.SetBounds(700, 36, 220, 34);
            _btnBridge.Click += (s, e) => OnBridge();
            cBridge.Controls.Add(_chkShare);
            cBridge.Controls.Add(lbHint2);
            cBridge.Controls.Add(_btnBridge);
            page.Controls.Add(cBridge);

            // 操作按钮
            _btnStart = new FluentButton();
            _btnStart.Text = "开始共享";
            _btnStart.Primary = true;
            _btnStart.Icon = IconKind.Play;
            _btnStart.Size = new Size(148, 38);
            _btnStart.SetBounds(12, 310, 148, 38);
            _btnStart.Click += (s, e) => StartShare();
            _btnStop = new FluentButton();
            _btnStop.Text = "停止共享";
            _btnStop.Danger = true;
            _btnStop.Icon = IconKind.Stop;
            _btnStop.Size = new Size(148, 38);
            _btnStop.SetBounds(170, 310, 148, 38);
            _btnStop.Enabled = false;
            _btnStop.Click += (s, e) => StopShare();
            page.Controls.Add(_btnStart);
            page.Controls.Add(_btnStop);

            // 卡4 已连接观看端
            FluentCard cCli = new FluentCard();
            cCli.Caption = "已连接观看端";
            cCli.SetBounds(12, 362, 946, 168);
            _listClients = new ListView();
            _listClients.View = View.Details;
            _listClients.FullRowSelect = true;
            _listClients.BorderStyle = BorderStyle.None;
            _listClients.Columns.Add("地址", 540);
            _listClients.Columns.Add("状态", 360);
            _listClients.SetBounds(14, 34, 918, 122);
            _listClients.BackColor = F.C.CardAlt; _listClients.ForeColor = F.C.Text;
            cCli.Controls.Add(_listClients);
            page.Controls.Add(cCli);

            // 日志卡
            FluentCard cLog = new FluentCard();
            cLog.Caption = "日志";
            cLog.SetBounds(12, 540, 946, 150);
            _log = new TextBox();
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BorderStyle = BorderStyle.None;
            _log.SetBounds(14, 34, 918, 106);
            _log.BackColor = F.C.CardAlt; _log.ForeColor = F.C.TextDim; _log.Font = F.SmallFont;
            cLog.Controls.Add(_log);
            page.Controls.Add(cLog);

            return page;
        }

        /* =================================================================== */
        /* 观看端页面                                                          */
        /* =================================================================== */
        private Panel BuildViewerPanel()
        {
            Panel page = new Panel();
            page.SetBounds(0, 0, 970, 700);
            page.BackColor = F.C.WindowBg;

            _vStatus = MakeLabel("正在自动发现共享端…", 14, 12, 500, 22, F.C.Amber);
            page.Controls.Add(_vStatus);

            _txtManual = new TextBox();
            _txtManual.Text = "IP:端口";
            _txtManual.SetBounds(620, 9, 170, 27);
            _txtManual.BackColor = F.C.CardAlt; _txtManual.ForeColor = F.C.Text; _txtManual.BorderStyle = BorderStyle.FixedSingle;
            page.Controls.Add(_txtManual);

            FluentButton btnManual = new FluentButton();
            btnManual.Text = "直连";
            btnManual.Icon = IconKind.Link;
            btnManual.SetBounds(798, 8, 70, 30);
            btnManual.Click += (s, e) => ManualConnect();
            page.Controls.Add(btnManual);

            FluentButton btnFull = new FluentButton();
            btnFull.Text = "全屏";
            btnFull.Icon = IconKind.Fullscreen;
            btnFull.SetBounds(876, 8, 84, 30);
            btnFull.Click += (s, e) => ToggleFullscreen();
            page.Controls.Add(btnFull);

            Label lHint3 = MakeLabel("双击画面全屏 / Esc 退出；断线自动重连", 14, 44, 320, 20, F.C.TextDim);
            page.Controls.Add(lHint3);

            // 左侧发现列表卡片
            FluentCard cFind = new FluentCard();
            cFind.Caption = "自动发现的共享端（双击连接）";
            cFind.SetBounds(12, 72, 250, 546);
            _vList = new ListView();
            _vList.View = View.Details;
            _vList.FullRowSelect = true;
            _vList.BorderStyle = BorderStyle.None;
            _vList.Columns.Add("主机", 90);
            _vList.Columns.Add("地址", 140);
            _vList.SetBounds(14, 34, 222, 496);
            _vList.BackColor = F.C.CardAlt; _vList.ForeColor = F.C.Text;
            _vList.DoubleClick += (s, e) => ConnectSelected();
            cFind.Controls.Add(_vList);
            page.Controls.Add(cFind);

            // 画面区
            FluentCard cFrame = new FluentCard();
            cFrame.Caption = "实时画面";
            cFrame.SetBounds(274, 72, 684, 560);
            _picHostPanel = new Panel();
            _picHostPanel.SetBounds(12, 30, 660, 518);
            _pic = new PictureBox();
            _pic.Dock = DockStyle.Fill;
            _pic.SizeMode = PictureBoxSizeMode.Zoom;
            _pic.BackColor = Color.Black;
            _pic.DoubleClick += (s, e) => ToggleFullscreen();
            _picHostPanel.Controls.Add(_pic);
            cFrame.Controls.Add(_picHostPanel);
            page.Controls.Add(cFrame);

            _vInfo = MakeLabel("等待画面…  （请提醒共享方点击「开始共享」）", 14, 644, 940, 22, F.C.TextDim);
            page.Controls.Add(_vInfo);

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F11) { ToggleFullscreen(); e.Handled = true; } };
            return page;
        }

        private void ConfigureViewer() { } // 占位（与旧版合并的构建接口一致）

        private void ConfigureSharing() { } // 占位

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
            _engine.Start();
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
