using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>Win11 Fluent 深色主题调色板</summary>
    public static class F
    {
        public static class C
        {
            public static readonly Color WindowBg  = Color.FromArgb(32, 32, 32);   // #202020
            public static readonly Color Card      = Color.FromArgb(43, 43, 43);   // #2B2B2B
            public static readonly Color CardAlt   = Color.FromArgb(38, 38, 38);   // #262626
            public static readonly Color InputBg   = Color.FromArgb(48, 48, 48);   // #303030 表单底色
            public static readonly Color RowAlt    = Color.FromArgb(46, 46, 46);   // 列表隔行
            public static readonly Color Border    = Color.FromArgb(61, 61, 61);   // #3D3D3D
            public static readonly Color NavBg     = Color.FromArgb(38, 38, 38);
            public static readonly Color NavHover  = Color.FromArgb(50, 50, 50);
            public static readonly Color Accent    = Color.FromArgb(76, 194, 255); // #4CC2FF
            public static readonly Color AccentHov = Color.FromArgb(111, 219, 255);// #6FDBFF
            public static readonly Color AccentDim = Color.FromArgb(45, 120, 160);
            public static readonly Color AccentSel = Color.FromArgb(38, 76, 96);   // 选中行淡蓝
            public static readonly Color Text      = Color.FromArgb(245, 245, 245);
            public static readonly Color TextDim   = Color.FromArgb(154, 154, 154);
            public static readonly Color TextMuted = Color.FromArgb(110, 110, 110);
            public static readonly Color Danger    = Color.FromArgb(220, 92, 92);
            public static readonly Color DangerHov = Color.FromArgb(235, 110, 110);
            public static readonly Color Green     = Color.FromArgb(110, 200, 120);
            public static readonly Color Amber     = Color.FromArgb(230, 200, 90);
        }

        public static readonly Font BaseFont  = new Font("Segoe UI", 9F);
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 12F);
        public static readonly Font CaptionFont = new Font("Segoe UI Semibold", 8.5F);
        public static readonly Font SmallFont = new Font("Segoe UI", 8F);

        /// <summary>颜色线性插值（动画过渡用）</summary>
        public static Color Lerp(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>圆角路径</summary>
        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>内嵌矢量图标（24×24 viewBox 标准 SVG path 数据，经内置 SVG 渲染器输出）</summary>
    public enum IconKind { None, Screen, Eye, Link, Play, Stop, Fullscreen, Bridge, Refresh }

    public static class FluentIcon
    {
        /// <summary>标准 SVG path 数据（描边风格，24×24 视图坐标）</summary>
        private static string PathOf(IconKind kind)
        {
            switch (kind)
            {
                case IconKind.Screen:    // 显示器（圆角屏幕 + 底座）
                    return "M3 5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2zM8 21h8M12 17v4";
                case IconKind.Eye:       // 眼睛（上下弧 + 瞳孔）
                    return "M2.5 12C5.5 6.5 8.5 4.5 12 4.5S18.5 6.5 21.5 12c-3 5.5-6 7.5-9.5 7.5S5.5 17.5 2.5 12zM12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6";
                case IconKind.Link:      // 链接（链节双弧 + 中轴）
                    return "M9 17H7a5 5 0 0 1 0-10h2M15 7h2a5 5 0 1 1 0 10h-2M8 12h8";
                case IconKind.Play:      // 播放三角
                    return "M7.5 5.5v13l11-6.5z";
                case IconKind.Stop:      // 停止方块
                    return "M7 7h10v10H7z";
                case IconKind.Fullscreen: // 扩角（四角 + 对角箭头）
                    return "M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7";
                case IconKind.Bridge:    // 闪电（雷电/USB4 网桥）
                    return "M13 2L3 14h7l-1 8 10-12h-7l1-8";
                case IconKind.Refresh:   // 刷新（圆弧 + 箭头）
                    return "M21 12a9 9 0 1 1-2.64-6.36M21 3v5h-5";
            }
            return null;
        }

        /// <summary>在 (x,y) 画 size×size 的 SVG 图标（矢量路径，任意 DPI 锐利）</summary>
        public static void Draw(Graphics g, IconKind kind, int x, int y, int size, Color color)
        {
            string d = PathOf(kind);
            if (d == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Svg.Parse(d))
            {
                if (gp.PointCount == 0) return; // 空路径不绘制（PointCount 安全查询）
                GraphicsState st = g.Save();
                g.TranslateTransform(x, y);
                g.ScaleTransform(size / 24f, size / 24f);
                using (Pen pen = new Pen(color, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawPath(pen, gp);
                }
                g.Restore(st);
            }
        }
    }

    /// <summary>Fluent 扁平按钮（圆角 / hover / 主色 / 危险色 / 禁用态 / 矢量图标）</summary>
    public sealed class FluentButton : Control
    {
        private bool _hover, _down;
        private float _hoverP, _downP;
        private System.Windows.Forms.Timer _animT;
        public bool Primary { get; set; }
        public bool Danger { get; set; }
        public IconKind Icon { get; set; }

        public FluentButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(120, 34);
            Font = F.BaseFont;
            ForeColor = F.C.Text;
            Cursor = Cursors.Hand;
        }

        private void EnsureAnim()
        {
            if (_animT == null)
            {
                _animT = new System.Windows.Forms.Timer();
                _animT.Interval = 15;
                _animT.Tick += delegate
                {
                    float step = 0.12f;
                    _hoverP = _hover ? Math.Min(1f, _hoverP + step) : Math.Max(0f, _hoverP - step);
                    _downP = _down ? Math.Min(1f, _downP + step * 1.5f) : Math.Max(0f, _downP - step * 1.5f);
                    if (_hoverP <= 0f && _downP <= 0f && !_hover && !_down) { _animT.Stop(); }
                    Invalidate();
                };
            }
            _animT.Start();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; EnsureAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; EnsureAnim(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; EnsureAnim(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; EnsureAnim(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color idle, hover;
            if (!Enabled) { idle = Color.FromArgb(50, 50, 50); hover = idle; }
            else if (Primary) { idle = F.C.Accent; hover = F.C.AccentHov; }
            else if (Danger) { idle = F.C.Danger; hover = F.C.DangerHov; }
            else { idle = F.C.NavBg; hover = F.C.NavHover; }
            // 悬停颜色平滑过渡 + 按下轻微加深
            Color bg = F.Lerp(idle, hover, _hoverP);
            if (_downP > 0f) bg = ControlPaint.Dark(bg, 0.10f * _downP);

            using (GraphicsPath path = F.RoundRect(r, 6))
            using (SolidBrush b = new SolidBrush(bg))
                g.FillPath(b, path);

            Color tc = Primary ? Color.FromArgb(8, 30, 45) : (Enabled ? F.C.Text : DisabledText);
            // 图标 + 文字整体居中
            int iconSize = 15;
            bool hasIcon = Icon != IconKind.None;
            string text = Text ?? "";
            Size ts = TextRenderer.MeasureText(g, text, Font);
            int gap = 7;
            int totalW = (hasIcon ? iconSize + gap : 0) + ts.Width;
            int sx = (Width - totalW) / 2;
            if (sx < 4) sx = 4;
            int lift = (int)(_downP * 1f); // 按下时内容微降
            if (hasIcon)
                FluentIcon.Draw(g, Icon, sx, (Height - iconSize) / 2 + lift, iconSize, tc);
            Rectangle textArea = new Rectangle(sx + (hasIcon ? iconSize + gap : 0), lift, ts.Width + 4, Height);
            TextRenderer.DrawText(g, text, Font, textArea, tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(p, r.X + 2, r.Y + 2, r.Width - 5, r.Height - 5);
            }
        }

        private static readonly Color DisabledText = Color.FromArgb(110, 110, 110);

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>Fluent 圆角卡片（背景 + 边框 + 顶层标题 Label，不被内容区遮挡）</summary>
    public sealed class FluentCard : Panel
    {
        private readonly Label _cap;
        private string _caption = "";

        public string Caption
        {
            get { return _caption; }
            set { _caption = value; _cap.Text = value; }
        }

        public FluentCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = F.C.Card;
            _cap = new Label();
            _cap.Font = F.CaptionFont;
            _cap.ForeColor = Color.FromArgb(205, 205, 205);
            _cap.BackColor = F.C.Card;
            _cap.AutoSize = true;
            _cap.Location = new Point(14, 10);
            Controls.Add(_cap);
            Controls.SetChildIndex(_cap, 0); // 顶层：不被内容区覆盖
            // 任何后续内容加入后，标题保持顶层
            ControlAdded += delegate { Controls.SetChildIndex(_cap, 0); };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _cap.Location = new Point(14, 10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = F.RoundRect(r, 8))
            using (SolidBrush b = new SolidBrush(F.C.Card))
            using (Pen p = new Pen(F.C.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
        }
    }

    /// <summary>标题栏按钮（最小化 / 最大化 / 关闭，SVG 矢量图标）</summary>
    public sealed class CaptionButton : Control
    {
        public bool Close { get; set; }
        public string SvgGlyph { get; set; }
        private bool _hover;

        public CaptionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 34);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_hover)
            {
                using (SolidBrush b = new SolidBrush(Close ? Color.FromArgb(200, 60, 60) : F.C.NavHover))
                    g.FillRectangle(b, ClientRectangle);
            }

            if (string.IsNullOrEmpty(SvgGlyph)) return;
            using (GraphicsPath gp = Svg.Parse(SvgGlyph))
            {
                if (gp.PointCount == 0) return; // 空路径不绘制
                GraphicsState st = g.Save();
                g.TranslateTransform((Width - 22) / 2f, (Height - 22) / 2f);
                using (Pen p = new Pen(Color.FromArgb(232, 232, 232), 2f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, gp);
                }
                g.Restore(st);
            }
        }
    }

    /// <summary>左侧导航项（Win11 NavigationView 风格，含矢量图标）</summary>
    public sealed class NavItem : Control
    {
        private bool _hover;
        private bool _selected;
        private float _hoverP, _selP;
        private System.Windows.Forms.Timer _animT;
        public bool Selected { get { return _selected; } set { _selected = value; EnsureAnim(); Invalidate(); } }
        public IconKind Icon { get; set; }

        public NavItem()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 42;
            Cursor = Cursors.Hand;
        }

        private void EnsureAnim()
        {
            if (_animT == null)
            {
                _animT = new System.Windows.Forms.Timer();
                _animT.Interval = 15;
                _animT.Tick += delegate
                {
                    float step = 0.12f;
                    _hoverP = _hover ? Math.Min(1f, _hoverP + step) : Math.Max(0f, _hoverP - step);
                    _selP = _selected ? Math.Min(1f, _selP + step) : Math.Max(0f, _selP - step);
                    if (_hoverP <= 0f && _selP <= 0f && !_hover && !_selected) _animT.Stop();
                    Invalidate();
                };
            }
            _animT.Start();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; EnsureAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; EnsureAnim(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(8, 2, Width - 16, Height - 4);
            if (_hoverP > 0.01f || _selP > 0.01f)
            {
                // 悬停背景叠加上选中背景，均平滑过渡
                Color bg = Color.FromArgb((int)(46 * Math.Min(1f, _hoverP + _selP)), 46, 46, 46);
                using (GraphicsPath path = F.RoundRect(r, 6))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, path);
            }
            // 选中强调条：宽度 0→3 动画
            if (_selP > 0.01f)
            {
                int bw = (int)(3f * _selP);
                if (bw > 0)
                    using (SolidBrush b = new SolidBrush(F.C.Accent))
                        g.FillRectangle(b, 8, 12, bw, Height - 24);
            }

            // 图标 + 文字整体在内容区水平居中；颜色按悬停/选中平滑过渡
            string text = Text ?? "";
            Size ts = TextRenderer.MeasureText(e.Graphics, text, F.BaseFont);
            int iconSize = 19;
            bool hasIcon = Icon != IconKind.None;
            int gap = 10;
            int totalW = (hasIcon ? iconSize + gap : 0) + ts.Width;
            int cx = 8 + ((Width - 16) - totalW) / 2;
            if (cx < 14) cx = 14;
            Color tc = F.Lerp(F.Lerp(F.C.TextDim, F.C.Text, _hoverP), F.C.Accent, _selP);
            if (hasIcon)
                FluentIcon.Draw(g, Icon, cx, (Height - iconSize) / 2, iconSize, tc);
            TextRenderer.DrawText(g, text, F.BaseFont,
                new Rectangle(cx + (hasIcon ? iconSize + gap : 0), 0, ts.Width + 4, Height), tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>轻量补间动画器（easeOutCubic；delay + duration，OnFrame 传进度 t 0..1）</summary>
    public sealed class Anim
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();
        private readonly float _delayMs, _durMs;
        private readonly Action<float> _frame;
        private readonly Action _done;

        public Anim(float delayMs, float durMs, Action<float> frame, Action done = null)
        {
            _delayMs = delayMs; _durMs = durMs; _frame = frame; _done = done;
            _sw.Start();
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 16;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            float el = (float)_sw.Elapsed.TotalMilliseconds - _delayMs;
            if (el < 0f) return;
            float raw = el / _durMs;
            if (raw >= 1f)
            {
                _frame(1f);
                _timer.Stop();
                _timer.Dispose();
                if (_done != null) _done();
                return;
            }
            _frame(Ease(raw));
        }

        public static float Ease(float x)
        {
            float t = x < 0f ? 0f : (x > 1f ? 1f : x);
            return 1f - (float)Math.Pow(1f - t, 3);
        }
    }

    /// <summary>Fluent 单选按钮（自绘圆形，同父分组互斥，选中带动画）</summary>
    public sealed class FluentRadio : Control
    {
        public event EventHandler CheckedChanged;
        private bool _checked;
        private bool _hover;
        private float _ckP;
        private System.Windows.Forms.Timer _animT;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                if (value && Parent != null)
                {
                    foreach (Control c in Parent.Controls)
                        if (c is FluentRadio && c != this) ((FluentRadio)c).Silent(false);
                }
                EnsureAnim();
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        private void Silent(bool v) { _checked = v; EnsureAnim(); Invalidate(); }

        private void EnsureAnim()
        {
            if (_animT == null)
            {
                _animT = new System.Windows.Forms.Timer();
                _animT.Interval = 15;
                _animT.Tick += delegate
                {
                    float step = 0.09f;
                    float target = _checked ? 1f : 0f;
                    _ckP = _ckP < target ? Math.Min(target, _ckP + step) : Math.Max(target, _ckP - step);
                    Invalidate();
                };
            }
            _animT.Start();
        }

        public FluentRadio()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 24;
            Font = F.BaseFont;
            BackColor = F.C.Card;
            ForeColor = F.C.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = true; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            Color rim = F.Lerp(_hover ? Color.FromArgb(180, 180, 180) : Color.FromArgb(140, 140, 140), F.C.Accent, _ckP);
            using (Pen pen = new Pen(rim, 2f))
                g.DrawEllipse(pen, 3, cy - 6, 12, 12);
            if (_ckP > 0.01f)
            {
                float r = 4f * _ckP;
                using (SolidBrush b = new SolidBrush(F.C.Accent))
                    g.FillEllipse(b, 7.5f - r, cy - r, r * 2f, r * 2f);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(21, 0, Width - 23, Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Fluent 复选框（自绘圆角勾选框）</summary>
    public sealed class FluentCheck : Control
    {
        public event EventHandler CheckedChanged;
        private bool _checked;
        private bool _hover;
        private float _ckP;
        private System.Windows.Forms.Timer _animT;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                EnsureAnim();
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        private void EnsureAnim()
        {
            if (_animT == null)
            {
                _animT = new System.Windows.Forms.Timer();
                _animT.Interval = 15;
                _animT.Tick += delegate
                {
                    float step = 0.09f;
                    float target = _checked ? 1f : 0f;
                    _ckP = _ckP < target ? Math.Min(target, _ckP + step) : Math.Max(target, _ckP - step);
                    Invalidate();
                };
            }
            _animT.Start();
        }

        public FluentCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 24;
            Font = F.BaseFont;
            BackColor = F.C.Card;
            ForeColor = F.C.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Height / 2;
            Rectangle box = new Rectangle(3, cy - 6, 12, 12);
            using (GraphicsPath path = F.RoundRect(box, 3))
            {
                if (_ckP > 0.01f)
                {
                    Color fill = F.Lerp(Color.Transparent, F.C.Accent, _ckP);
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (Pen pen = new Pen(Color.FromArgb((int)(170 * _ckP), 10, 40, 60), 2f))
                    {
                        pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 6, cy, 9, cy + 3);
                        g.DrawLine(pen, 9, cy + 3, 13, cy - 3);
                    }
                }
                using (Pen pen = new Pen(_hover ? Color.FromArgb(190, 190, 190) : Color.FromArgb(140, 140, 140), 1.6f))
                    g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(21, 0, Width - 23, Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Fluent 输入框（圆角描边 + 聚焦高亮，内部原生 TextBox）</summary>
    public sealed class FluentInput : Panel
    {
        public readonly TextBox Inner;
        public new string Text { get { return Inner.Text; } set { Inner.Text = value; } }

        public FluentInput()
        {
            BackColor = F.C.InputBg;
            Padding = new Padding(10, 5, 10, 5);
            Inner = new TextBox();
            Inner.BorderStyle = BorderStyle.None;
            Inner.BackColor = F.C.InputBg;
            Inner.ForeColor = F.C.Text;
            Inner.Font = F.BaseFont;
            Inner.Dock = DockStyle.Fill;
            Inner.GotFocus += (s, e) => Invalidate();
            Inner.LostFocus += (s, e) => Invalidate();
            Controls.Add(Inner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool focus = Inner.Focused;
            using (GraphicsPath path = F.RoundRect(r, 6))
            using (SolidBrush b = new SolidBrush(F.C.InputBg))
            using (Pen pen = new Pen(focus ? F.C.Accent : F.C.Border, focus ? 1.6f : 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Fluent 数字输入（TextBox + 自绘上下步进按钮）</summary>
    public sealed class FluentNumberUpDown : Control
    {
        private readonly TextBox _box;
        private readonly NumBtn _up;
        private readonly NumBtn _down;

        public int Minimum { get; set; }
        public int Maximum { get; set; }

        public int Value
        {
            get { int v; int.TryParse(_box.Text, out v); return v; }
            set { _box.Text = value.ToString(); }
        }

        public FluentNumberUpDown()
        {
            Height = 28;
            Minimum = 0; Maximum = 100;
            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = F.C.InputBg;
            _box.ForeColor = F.C.Text;
            _box.Font = F.BaseFont;
            _box.TextAlign = HorizontalAlignment.Center;
            _up = new NumBtn(true);
            _up.Click += (s, e) => Step(1);
            _down = new NumBtn(false);
            _down.Click += (s, e) => Step(-1);
            _box.GotFocus += (s, e) => Invalidate();
            _box.LostFocus += (s, e) => Invalidate();
            Controls.Add(_box);
            Controls.Add(_up);
            Controls.Add(_down);
            Resize += (s, e) => LayoutNow();
            LayoutNow();
        }

        private void LayoutNow()
        {
            int bw = 22;
            _down.SetBounds(Width - bw - 1, Height / 2 + 1, bw, Height / 2 - 2);
            _up.SetBounds(Width - bw - 1, 1, bw, Height / 2 - 1);
            _box.SetBounds(3, 1, Width - bw - 5, Height - 2);
        }

        private void Step(int d)
        {
            int v = Value + d;
            if (v < Minimum) v = Minimum;
            if (v > Maximum) v = Maximum;
            Value = v;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool focus = _box.Focused;
            using (GraphicsPath path = F.RoundRect(r, 6))
            using (SolidBrush b = new SolidBrush(F.C.InputBg))
            using (Pen pen = new Pen(focus ? F.C.Accent : F.C.Border, focus ? 1.6f : 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
        }

        private sealed class NumBtn : Control
        {
            private readonly bool _upArrow;
            private bool _hover;

            public NumBtn(bool up)
            {
                _upArrow = up;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (_hover)
                    using (SolidBrush b = new SolidBrush(F.C.NavHover))
                        g.FillRectangle(b, ClientRectangle);
                using (Pen pen = new Pen(F.C.TextDim, 1.4f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    int cx = Width / 2 + 1, cy = Height / 2 + 1;
                    if (_upArrow)
                    {
                        g.DrawLine(pen, cx - 4, cy + 2, cx, cy - 2);
                        g.DrawLine(pen, cx, cy - 2, cx + 4, cy + 2);
                    }
                    else
                    {
                        g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                        g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
                    }
                }
            }
        }
    }

    /// <summary>Fluent 列表（OwnerDraw：深色表头 / 隔行 / 淡蓝选中行）</summary>
    public sealed class FluentListView : ListView
    {
        public FluentListView()
        {
            View = View.Details;
            BorderStyle = BorderStyle.None;
            FullRowSelect = true;
            OwnerDraw = true;
            BackColor = F.C.Card;
            ForeColor = F.C.Text;
            Font = F.BaseFont;
            DrawColumnHeader += OnColumnHeader;
            DrawItem += OnItem;
            DrawSubItem += OnSubItem;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void OnColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(F.C.CardAlt))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, F.CaptionFont,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                F.C.TextDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (Pen pen = new Pen(F.C.Border))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        private void OnItem(object sender, DrawListViewItemEventArgs e)
        {
            // 整行背景（隔行提示是 AlternateColor 不需要，这里统一）
            using (SolidBrush b = new SolidBrush(e.Item.Selected ? F.C.AccentSel : F.C.Card))
                e.Graphics.FillRectangle(b, e.Bounds);
            e.DrawDefault = false;
        }

        private void OnSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool sel = e.Item.Selected;
            using (SolidBrush b = new SolidBrush(sel ? F.C.AccentSel : F.C.Card))
                e.Graphics.FillRectangle(b, e.Bounds);
            if (!sel && (e.ItemIndex % 2) == 1)
                using (SolidBrush b = new SolidBrush(F.C.RowAlt))
                    e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Font,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                sel ? F.C.Accent : F.C.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.DrawDefault = false;
        }
    }

    /// <summary>DWM：Win11 圆角 / Mica / 深色标题栏</summary>
    public static class Dwm
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMWA_BORDER_COLOR = 34;

        private const int CornerRound = 2;
        private const int BackdropMica = 2;

        /// <summary>应用 Win11 风格（圆角 + Mica + 深色标题栏）；老系统/失败时静默跳过</summary>
        public static void Apply(Form form)
        {
            try
            {
                if (form.Handle == IntPtr.Zero) return;
                int dark = 1;
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int corner = CornerRound;
                DwmSetWindowAttribute(form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int backdrop = BackdropMica;
                DwmSetWindowAttribute(form.Handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            }
            catch { }
        }
    }
}
