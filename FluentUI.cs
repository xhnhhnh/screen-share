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

            Color tc = Primary ? Color.FromArgb(8, 30, 45) : (Enabled ? F.C.Text : (DisabledText));
            // 图标 + 文字整体居中（Icon 与 Text 作为一组度量）
            int iconSize = 15;
            bool hasIcon = Icon != IconKind.None;
            string text = Text ?? "";
            Size ts = TextRenderer.MeasureText(g, text, Font);
            int gap = 7;
            int totalW = (hasIcon ? iconSize + gap : 0) + ts.Width;
            int sx = (Width - totalW) / 2;
            if (sx < 4) sx = 4;
            if (hasIcon)
                FluentIcon.Draw(g, Icon, sx, (Height - iconSize) / 2, iconSize, tc);
            Rectangle textArea = new Rectangle(sx + (hasIcon ? iconSize + gap : 0), 0, ts.Width + 4, Height);
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
