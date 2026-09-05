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
            public static readonly Color Border    = Color.FromArgb(61, 61, 61);   // #3D3D3D
            public static readonly Color NavBg     = Color.FromArgb(38, 38, 38);
            public static readonly Color NavHover  = Color.FromArgb(50, 50, 50);
            public static readonly Color Accent    = Color.FromArgb(76, 194, 255); // #4CC2FF
            public static readonly Color AccentHov = Color.FromArgb(111, 219, 255);// #6FDBFF
            public static readonly Color AccentDim = Color.FromArgb(45, 120, 160);
            public static readonly Color Text      = Color.FromArgb(255, 255, 255);
            public static readonly Color TextDim   = Color.FromArgb(154, 154, 154);
            public static readonly Color Danger    = Color.FromArgb(220, 92, 92);
            public static readonly Color DangerHov = Color.FromArgb(235, 110, 110);
            public static readonly Color Green     = Color.FromArgb(110, 200, 120);
            public static readonly Color Amber     = Color.FromArgb(230, 200, 90);
        }

        public static readonly Font BaseFont  = new Font("Segoe UI", 9F);
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 12F);
        public static readonly Font CaptionFont = new Font("Segoe UI Semibold", 8.5F);
        public static readonly Font SmallFont = new Font("Segoe UI", 8F);

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

    /// <summary>内嵌矢量图标（GDI+ 线条绘制，等价 SVG path，任意 DPI 锐利）</summary>
    public enum IconKind { None, Screen, Eye, Link, Play, Stop, Fullscreen, Bridge, Refresh }

    public static class FluentIcon
    {
        /// <summary>在 (x,y) 画 size×size 图标（24 逻辑坐标设计）</summary>
        public static void Draw(Graphics g, IconKind kind, int x, int y, int size, Color color)
        {
            if (kind == IconKind.None) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(color, 1.7f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                float k = size / 24f;
                Func<float, float> X = delegate(float v) { return x + v * k; };
                Func<float, float> Y = delegate(float v) { return y + v * k; };
                Action<float, float, float, float> L = delegate(float x1, float y1, float x2, float y2)
                {
                    g.DrawLine(p, X(x1), Y(y1), X(x2), Y(y2));
                };

                switch (kind)
                {
                    case IconKind.Screen: // 显示器
                        g.DrawRectangle(p, X(3), Y(4), 18 * k, 12 * k);
                        L(9, 20, 15, 20);
                        L(12, 16, 12, 20);
                        break;
                    case IconKind.Eye: // 眼睛
                        g.DrawBezier(p, X(2.5f), Y(12), X(8), Y(5), X(16), Y(5), X(21.5f), Y(12));
                        g.DrawBezier(p, X(2.5f), Y(12), X(8), Y(19), X(16), Y(19), X(21.5f), Y(12));
                        g.FillEllipse(p.Brush, X(10.6f), Y(10.6f), 2.8f * k, 2.8f * k);
                        break;
                    case IconKind.Link: // 链接（两环）
                        g.DrawArc(p, X(3), Y(9), 10 * k, 10 * k, 130, 190);
                        g.DrawArc(p, X(11), Y(9), 10 * k, 10 * k, -50, 190);
                        break;
                    case IconKind.Play: // 播放三角
                        L(8, 5.5f, 8, 18.5f);
                        L(8, 5.5f, 19, 12);
                        L(8, 18.5f, 19, 12);
                        break;
                    case IconKind.Stop: // 停止方块
                        g.DrawRectangle(p, X(6.5f), Y(6.5f), 11 * k, 11 * k);
                        break;
                    case IconKind.Fullscreen: // 四角
                        L(4, 9, 4, 4); L(4, 4, 9, 4);
                        L(15, 4, 20, 4); L(20, 4, 20, 9);
                        L(20, 15, 20, 20); L(20, 20, 15, 20);
                        L(9, 20, 4, 20); L(4, 20, 4, 15);
                        break;
                    case IconKind.Bridge: // 双端箭头（点对点）
                        L(4, 12, 20, 12);
                        L(15, 7, 20, 12); L(15, 17, 20, 12);
                        L(9, 7, 4, 12); L(9, 17, 4, 12);
                        break;
                    case IconKind.Refresh: // 刷新
                        g.DrawArc(p, X(6), Y(6), 12 * k, 12 * k, -30, 250);
                        L(18, 3, 18.5f, 8); L(18, 8, 13.5f, 8);
                        break;
                }
            }
        }
    }

    /// <summary>Fluent 扁平按钮（圆角 / hover / 主色 / 危险色 / 禁用态 / 矢量图标）</summary>
    public sealed class FluentButton : Control
    {
        private bool _hover, _down;
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

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg;
            if (!Enabled) bg = Color.FromArgb(50, 50, 50);
            else if (Primary) bg = _hover ? F.C.AccentHov : F.C.Accent;
            else if (Danger) bg = _hover ? F.C.DangerHov : F.C.Danger;
            else bg = _hover ? F.C.NavHover : F.C.NavBg;
            if (_down && Enabled) bg = ControlPaint.Dark(bg, 0.08f);

            using (GraphicsPath path = F.RoundRect(r, 6))
            using (SolidBrush b = new SolidBrush(bg))
                g.FillPath(b, path);

            Color tc = Primary ? Color.FromArgb(8, 30, 45) : (Enabled ? F.C.Text : F.C.TextDim);
            Rectangle textArea = ClientRectangle;
            if (Icon != IconKind.None)
            {
                int iconSize = 15;
                int ix = 14;
                int iy = (Height - iconSize) / 2;
                FluentIcon.Draw(g, Icon, ix, iy, iconSize, tc);
                textArea = new Rectangle(ix + iconSize + 8, 0, Width - (ix + iconSize + 8) - 12, Height);
            }
            TextRenderer.DrawText(g, Text, Font, textArea, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (Focused && ShowFocusCues)
            {
                using (Pen p = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(p, r.X + 2, r.Y + 2, r.Width - 5, r.Height - 5);
            }
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>Fluent 圆角卡片（背景 + 边框 + 左上角小标题）</summary>
    public sealed class FluentCard : Panel
    {
        private string _caption = "";
        public string Caption { get { return _caption; } set { _caption = value; Invalidate(); } }

        public FluentCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = F.C.Card;
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
            if (Caption.Length > 0)
            {
                TextRenderer.DrawText(g, Caption, F.CaptionFont, new Rectangle(14, 10, Width - 28, 20),
                    Color.FromArgb(200, 200, 200), TextFormatFlags.Left);
            }
        }
    }

    /// <summary>标题栏按钮（最小化 / 最大化 / 关闭）</summary>
    public sealed class CaptionButton : Control
    {
        public bool Close { get; set; }
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

            int cx = Width / 2, cy = Height / 2;
            using (Pen p = new Pen(Color.FromArgb(230, 230, 230)))
            {
                if (Text == "_")
                {
                    g.DrawLine(p, cx - 7, cy + 3, cx + 7, cy + 3);
                }
                else if (Text == "□")
                {
                    g.DrawRectangle(p, cx - 7, cy - 7, 14, 14);
                }
                else if (Close)
                {
                    g.DrawLine(p, cx - 6, cy - 6, cx + 6, cy + 6);
                    g.DrawLine(p, cx - 6, cy + 6, cx + 6, cy - 6);
                }
            }
        }
    }

    /// <summary>左侧导航项（Win11 NavigationView 风格，含矢量图标）</summary>
    public sealed class NavItem : Control
    {
        private bool _hover;
        private bool _selected;
        public bool Selected { get { return _selected; } set { _selected = value; Invalidate(); } }
        public IconKind Icon { get; set; }

        public NavItem()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 42;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(8, 2, Width - 16, Height - 4);
            if (Selected)
            {
                using (GraphicsPath path = F.RoundRect(r, 6))
                using (SolidBrush b = new SolidBrush(F.C.NavHover))
                    g.FillPath(b, path);
                using (SolidBrush b = new SolidBrush(F.C.Accent))
                    g.FillRectangle(b, 8, 12, 3, Height - 24);
            }
            else if (_hover)
            {
                using (GraphicsPath path = F.RoundRect(r, 6))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(46, 46, 46)))
                    g.FillPath(b, path);
            }

            Color tc = Selected ? F.C.Accent : (_hover ? F.C.Text : F.C.TextDim);
            if (Icon != IconKind.None)
                FluentIcon.Draw(g, Icon, 27, (Height - 19) / 2, 19, tc);
            TextRenderer.DrawText(g, Text, F.BaseFont, new Rectangle(56, 0, Width - 62, Height), tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
