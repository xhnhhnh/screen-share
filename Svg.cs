using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenShare
{
    /// <summary>
    /// 迷你 SVG path 渲染器：解析标准 SVG path 数据（M/L/H/V/C/S/Q/T/Z/A 及相对命令，
    /// 支持隐式重复参数），输出 System.Drawing GraphicsPath —— 图标即 SVG 矢量，任意 DPI 锐利。
    /// </summary>
    public static class Svg
    {
        private static readonly Dictionary<string, GraphicsPath> Cache = new Dictionary<string, GraphicsPath>();

        public static GraphicsPath Parse(string d)
        {
            GraphicsPath gp;
            if (Cache.TryGetValue(d, out gp))
            {
                // 缓存持有原件；调用方（using 释放）拿到克隆，避免销毁共享缓存对象
                return (GraphicsPath)gp.Clone();
            }
            gp = new GraphicsPath();
            Build(d, gp);
            Cache[d] = gp;
            return (GraphicsPath)gp.Clone();
        }

        private static List<string> Tokenize(string d)
        {
            List<string> tokens = new List<string>();
            int n = d.Length;
            for (int i = 0; i < n; )
            {
                char ch = d[i];
                if (char.IsWhiteSpace(ch) || ch == ',') { i++; continue; }
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
                {
                    tokens.Add(ch.ToString());
                    i++;
                    continue;
                }
                int start = i;
                if (d[i] == '+' || d[i] == '-') i++;
                while (i < n && (char.IsDigit(d[i]) || d[i] == '.')) i++;
                if (i < n && (d[i] == 'e' || d[i] == 'E'))
                {
                    i++;
                    if (i < n && (d[i] == '+' || d[i] == '-')) i++;
                    while (i < n && char.IsDigit(d[i])) i++;
                }
                if (i == start) { i++; continue; }
                tokens.Add(d.Substring(start, i - start));
            }
            return tokens;
        }

        private static bool IsCmd(string t)
        {
            if (t.Length != 1) return false;
            char c = t[0];
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static float F(List<string> t, ref int i)
        {
            float v;
            float.TryParse(t[i], out v);
            i++;
            return v;
        }

        private static void Build(string d, GraphicsPath p)
        {
            List<string> t = Tokenize(d);
            int i = 0;
            float x = 0, y = 0, sx = 0, sy = 0;
            float c1x = 0, c1y = 0, c2x = 0, c2y = 0, qx = 0, qy = 0;
            char cur = '\0';
            bool hasC = false, hasQ = false;

            while (i < t.Count)
            {
                if (IsCmd(t[i])) { cur = t[i][0]; i++; }
                else if (cur == '\0') break;

                char upper = char.ToUpperInvariant(cur);
                bool rel = char.IsLower(cur);

                switch (upper)
                {
                    case 'M':
                    {
                        float nx = F(t, ref i) + (rel ? x : 0);
                        float ny = F(t, ref i) + (rel ? y : 0);
                        x = nx; y = ny; sx = nx; sy = ny;
                        p.StartFigure();
                        cur = rel ? 'l' : 'L'; // SVG：M 之后的数字视为 L
                        break;
                    }
                    case 'L':
                    {
                        float x0 = x, y0 = y;
                        x = F(t, ref i) + (rel ? x : 0);
                        y = F(t, ref i) + (rel ? y : 0);
                        p.AddLine(x0, y0, x, y);
                        break;
                    }
                    case 'H':
                    {
                        float x0 = x;
                        x = F(t, ref i) + (rel ? x : 0);
                        p.AddLine(x0, y, x, y);
                        break;
                    }
                    case 'V':
                    {
                        float y0 = y;
                        y = F(t, ref i) + (rel ? y : 0);
                        p.AddLine(x, y0, x, y);
                        break;
                    }
                    case 'C':
                    {
                        float x1 = F(t, ref i) + (rel ? x : 0), y1 = F(t, ref i) + (rel ? y : 0);
                        float x2 = F(t, ref i) + (rel ? x : 0), y2 = F(t, ref i) + (rel ? y : 0);
                        float nx = F(t, ref i) + (rel ? x : 0), ny = F(t, ref i) + (rel ? y : 0);
                        p.AddBezier(x, y, x1, y1, x2, y2, nx, ny);
                        c1x = x1; c1y = y1; c2x = x2; c2y = y2;
                        hasC = true; hasQ = false;
                        x = nx; y = ny;
                        break;
                    }
                    case 'S':
                    {
                        float x1, y1, x2, y2, nx, ny;
                        if (hasC) { x1 = 2 * x - c2x; y1 = 2 * y - c2y; }
                        else { x1 = x; y1 = y; }
                        x2 = F(t, ref i) + (rel ? x : 0);
                        y2 = F(t, ref i) + (rel ? y : 0);
                        nx = F(t, ref i) + (rel ? x : 0);
                        ny = F(t, ref i) + (rel ? y : 0);
                        p.AddBezier(x, y, x1, y1, x2, y2, nx, ny);
                        c1x = x1; c1y = y1; c2x = x2; c2y = y2;
                        x = nx; y = ny;
                        break;
                    }
                    case 'Q':
                    {
                        float q1x = F(t, ref i) + (rel ? x : 0), q1y = F(t, ref i) + (rel ? y : 0);
                        float nx = F(t, ref i) + (rel ? x : 0), ny = F(t, ref i) + (rel ? y : 0);
                        // 二次 → 三次贝塞尔
                        float c1 = x + 2f / 3f * (q1x - x);
                        float d1 = y + 2f / 3f * (q1y - y);
                        float c2 = nx + 2f / 3f * (q1x - nx);
                        float d2 = ny + 2f / 3f * (q1y - ny);
                        p.AddBezier(x, y, c1, d1, c2, d2, nx, ny);
                        qx = q1x; qy = q1y;
                        hasQ = true; hasC = false;
                        x = nx; y = ny;
                        break;
                    }
                    case 'T':
                    {
                        float nx = F(t, ref i) + (rel ? x : 0), ny = F(t, ref i) + (rel ? y : 0);
                        float q1x, q1y;
                        if (hasQ) { q1x = 2 * x - qx; q1y = 2 * y - qy; }
                        else { q1x = x; q1y = y; }
                        float c1 = x + 2f / 3f * (q1x - x);
                        float d1 = y + 2f / 3f * (q1y - y);
                        float c2 = nx + 2f / 3f * (q1x - nx);
                        float d2 = ny + 2f / 3f * (q1y - ny);
                        p.AddBezier(x, y, c1, d1, c2, d2, nx, ny);
                        qx = q1x; qy = q1y;
                        x = nx; y = ny;
                        break;
                    }
                    case 'A':
                    {
                        float rx = Math.Abs(F(t, ref i)), ry = Math.Abs(F(t, ref i));
                        float rot = F(t, ref i);
                        bool large = F(t, ref i) != 0;
                        bool sweep = F(t, ref i) != 0;
                        float nx = F(t, ref i) + (rel ? x : 0);
                        float ny = F(t, ref i) + (rel ? y : 0);
                        ArcTo(p, x, y, rx, ry, rot, large, sweep, nx, ny);
                        x = nx; y = ny;
                        break;
                    }
                    case 'Z':
                    {
                        p.CloseFigure();
                        x = sx; y = sy;
                        break;
                    }
                    default:
                        i++;
                        break;
                }
            }
        }

        private static PointF Last(GraphicsPath p)
        {
            try
            {
                PointF[] pts = p.PathPoints;
                return pts.Length > 0 ? pts[pts.Length - 1] : PointF.Empty;
            }
            catch
            {
                return PointF.Empty; // 空路径无点可查
            }
        }

        // ---- SVG 椭圆弧（端点→中心参数化）→ 分贝塞尔段 ----
        private static void ArcTo(GraphicsPath p, float x1, float y1, float rx, float ry,
            float phiDeg, bool largeArc, bool sweep, float x2, float y2)
        {
            if (rx <= 0f || ry <= 0f || (x1 == x2 && y1 == y2))
            {
                p.AddLine(x1, y1, x2, y2);
                return;
            }
            float phi = phiDeg * (float)Math.PI / 180f;
            float cosP = (float)Math.Cos(phi), sinP = (float)Math.Sin(phi);
            float dx = (x1 - x2) / 2f, dy = (y1 - y2) / 2f;
            float x1p = cosP * dx + sinP * dy;
            float y1p = -sinP * dx + cosP * dy;

            float rx2 = rx * rx, ry2 = ry * ry;
            float lambda = x1p * x1p / rx2 + y1p * y1p / ry2;
            if (lambda > 1f)
            {
                float s = (float)Math.Sqrt(lambda);
                rx *= s; ry *= s; rx2 = rx * rx; ry2 = ry * ry;
            }

            float sign = (largeArc != sweep) ? 1f : -1f;
            float num = rx2 * ry2 - rx2 * y1p * y1p - ry2 * x1p * x1p;
            float den = rx2 * y1p * y1p + ry2 * x1p * x1p;
            float coef = sign * (float)Math.Sqrt(Math.Max(0f, num / (den == 0f ? 1e-6f : den)));
            float cxp = coef * (rx * y1p / ry);
            float cyp = -coef * (ry * x1p / rx);
            float cx = cosP * cxp - sinP * cyp + (x1 + x2) / 2f;
            float cy = sinP * cxp + cosP * cyp + (y1 + y2) / 2f;

            float ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
            float vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;
            float theta1 = (float)Math.Atan2(uy, ux);
            float dtheta = (float)Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            if (dtheta == 0f) return;
            if (!sweep && dtheta > 0f) dtheta -= 2f * (float)Math.PI;
            else if (sweep && dtheta < 0f) dtheta += 2f * (float)Math.PI;

            int segs = (int)Math.Ceiling(Math.Abs(dtheta) / (Math.PI / 2.0));
            float delta = dtheta / segs;
            float t = theta1;
            PointF a = E(cx, cy, rx, ry, phi, t);
            for (int k = 0; k < segs; k++)
            {
                float t2 = t + delta;
                PointF b = E(cx, cy, rx, ry, phi, t2);
                float alpha = 4f / 3f * (float)Math.Tan(delta / 4f);
                PointF d1 = D(cx, cy, rx, ry, phi, t);
                PointF d2 = D(cx, cy, rx, ry, phi, t2);
                p.AddBezier(a.X, a.Y, a.X + alpha * d1.X, a.Y + alpha * d1.Y,
                            b.X - alpha * d2.X, b.Y - alpha * d2.Y, b.X, b.Y);
                a = b;
                t = t2;
            }
        }

        private static PointF E(float cx, float cy, float rx, float ry, float phi, float t)
        {
            float cosP = (float)Math.Cos(phi), sinP = (float)Math.Sin(phi);
            float e = (float)Math.Cos(t) * rx;
            float f = (float)Math.Sin(t) * ry;
            return new PointF(cx + cosP * e - sinP * f, cy + sinP * e + cosP * f);
        }

        private static PointF D(float cx, float cy, float rx, float ry, float phi, float t)
        {
            float cosP = (float)Math.Cos(phi), sinP = (float)Math.Sin(phi);
            float e = -(float)Math.Sin(t) * rx;
            float f = (float)Math.Cos(t) * ry;
            return new PointF(cosP * e - sinP * f, sinP * e + cosP * f);
        }
    }
}

