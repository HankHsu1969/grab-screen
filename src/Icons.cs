using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GrabScreen
{
    /// <summary>程式裡用到的小圖示全部用程式畫出來，不需要外部圖檔。</summary>
    internal static class Icons
    {
        private static readonly Color Ink = Color.FromArgb(50, 50, 55);

        // 星方向（Guiding Star）LOGO 的四塊多邊形，座標取自原始 SVG（viewBox 225 65 552 635）
        private static readonly float[][] StarPolys =
        {
            new float[] { 601, 260, 753, 228, 668, 364 },
            new float[] { 668, 364, 767, 520, 603, 470 },
            new float[] { 481, 75, 668, 364, 235, 351, 471, 288 },
            new float[] { 235, 351, 668, 364, 467, 690, 470, 425 }
        };
        private static readonly Color[] StarColors =
        {
            Color.FromArgb(0x42, 0x4A, 0x59),
            Color.FromArgb(0x42, 0x4A, 0x59),
            Color.FromArgb(0x5B, 0x78, 0xFF),
            Color.FromArgb(0x40, 0xA4, 0xFF)
        };

        /// <summary>主程式圖示：星方向 LOGO（透明背景）。</summary>
        public static Bitmap AppBitmap(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                const float vbX = 225, vbY = 65, vbW = 552, vbH = 635;
                float scale = size * 0.96f / vbH;
                float ox = (size - vbW * scale) / 2f - vbX * scale;
                float oy = (size - vbH * scale) / 2f - vbY * scale;
                for (int i = 0; i < StarPolys.Length; i++)
                {
                    var src = StarPolys[i];
                    var pts = new PointF[src.Length / 2];
                    for (int k = 0; k < pts.Length; k++)
                        pts[k] = new PointF(ox + src[k * 2] * scale, oy + src[k * 2 + 1] * scale);
                    using (var br = new SolidBrush(StarColors[i])) g.FillPolygon(br, pts);
                }
            }
            return bmp;
        }

        public static Icon AppIcon(int size)
        {
            using (var bmp = AppBitmap(size))
            {
                IntPtr h = bmp.GetHicon();
                var tmp = Icon.FromHandle(h);
                var icon = (Icon)tmp.Clone();
                tmp.Dispose();
                Native.DestroyIcon(h);
                return icon;
            }
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Bitmap ColorSwatch(Color c, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new RectangleF(1.5f, 1.5f, size - 3, size - 3);
                using (var path = RoundRect(rc, size / 5f))
                {
                    using (var br = new SolidBrush(c)) g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(120, 0, 0, 0), 1f)) g.DrawPath(pen, path);
                }
            }
            return bmp;
        }

        /// <summary>依名稱畫工具列圖示（32×32 為基準）。</summary>
        public static Bitmap Tool(string name)
        {
            const int N = 32;
            var bmp = new Bitmap(N, N);
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Ink, 2.4f))
            using (var fill = new SolidBrush(Ink))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (name)
                {
                    case "select":
                        g.FillPolygon(fill, new[] { new PointF(9, 4), new PointF(9, 25), new PointF(14, 20), new PointF(18, 28), new PointF(21, 27), new PointF(17, 19), new PointF(24, 19) });
                        break;
                    case "rect":
                        using (var p = new Pen(Color.FromArgb(229, 57, 53), 3f)) g.DrawRectangle(p, 5, 8, 22, 16);
                        break;
                    case "ellipse":
                        using (var p = new Pen(Color.FromArgb(229, 57, 53), 3f)) g.DrawEllipse(p, 4, 7, 24, 18);
                        break;
                    case "arrow":
                        using (var p = new Pen(Color.FromArgb(229, 57, 53), 3.2f))
                        {
                            p.StartCap = LineCap.Round;
                            g.DrawLine(p, 6, 26, 21, 11);
                        }
                        using (var br = new SolidBrush(Color.FromArgb(229, 57, 53)))
                            g.FillPolygon(br, new[] { new PointF(27, 5), new PointF(15, 9), new PointF(23, 17) });
                        break;
                    case "line":
                        g.DrawLine(pen, 6, 25, 26, 7);
                        break;
                    case "pen":
                        using (var path = new GraphicsPath())
                        {
                            path.AddBezier(4, 22, 10, 6, 14, 30, 20, 14);
                            path.AddBezier(20, 14, 23, 7, 26, 12, 28, 9);
                            g.DrawPath(pen, path);
                        }
                        break;
                    case "highlighter":
                        using (var p = new Pen(Color.FromArgb(200, 253, 216, 53), 9f))
                        {
                            p.StartCap = p.EndCap = LineCap.Square;
                            g.DrawLine(p, 6, 20, 26, 20);
                        }
                        g.DrawLine(pen, 8, 13, 24, 13);
                        break;
                    case "text":
                        using (var f = new Font("Arial", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("T", f, fill, new RectangleF(0, 1, N, N), sf);
                        }
                        break;
                    case "step":
                        using (var br = new SolidBrush(Color.FromArgb(229, 57, 53))) g.FillEllipse(br, 5, 5, 22, 22);
                        using (var f = new Font("Arial", 14f, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("1", f, Brushes.White, new RectangleF(5, 6, 22, 22), sf);
                        }
                        break;
                    case "mosaic":
                        for (int y = 0; y < 4; y++)
                            for (int x = 0; x < 4; x++)
                            {
                                int v = ((x + y) % 2 == 0) ? 90 : 170;
                                if (x == 1 && y == 2) v = 130;
                                using (var br = new SolidBrush(Color.FromArgb(v, v, v + 10))) g.FillRectangle(br, 5 + x * 5.5f, 5 + y * 5.5f, 5.5f, 5.5f);
                            }
                        break;
                    case "crop":
                        g.DrawLines(pen, new[] { new PointF(9, 3), new PointF(9, 23), new PointF(29, 23) });
                        g.DrawLines(pen, new[] { new PointF(3, 9), new PointF(23, 9), new PointF(23, 29) });
                        break;
                    case "resize":
                        using (var thin = new Pen(Ink, 1.8f) { DashStyle = DashStyle.Dash })
                            g.DrawRectangle(thin, 4, 4, 24, 24);
                        g.DrawRectangle(pen, 4, 16, 12, 12);
                        g.DrawLine(pen, 13, 19, 24, 8);
                        g.FillPolygon(fill, new[] { new PointF(27, 5), new PointF(18, 6), new PointF(26, 14) });
                        break;
                    case "undo":
                    case "redo":
                        {
                            var st = g.Save();
                            if (name == "redo") { g.TranslateTransform(N, 0); g.ScaleTransform(-1, 1); }
                            g.DrawArc(pen, 8, 9, 18, 16, 270, 200);
                            g.DrawLine(pen, 17, 9, 9, 9);
                            g.FillPolygon(fill, new[] { new PointF(5, 9), new PointF(12, 4), new PointF(12, 14) });
                            g.Restore(st);
                        }
                        break;
                    case "copy":
                        g.DrawRectangle(pen, 11, 10, 14, 17);
                        g.DrawLines(pen, new[] { new PointF(7, 22), new PointF(7, 5), new PointF(19, 5) });
                        break;
                    case "save":
                        g.DrawRectangle(pen, 5, 5, 22, 22);
                        g.DrawRectangle(pen, 10, 5, 12, 7);
                        g.DrawRectangle(pen, 9, 17, 14, 10);
                        break;
                    case "done":
                        using (var p = new Pen(Color.FromArgb(46, 160, 67), 4f))
                        {
                            p.StartCap = p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                            g.DrawLines(p, new[] { new PointF(6, 17), new PointF(13, 24), new PointF(26, 9) });
                        }
                        break;
                    case "delete":
                        g.DrawLine(pen, 7, 9, 25, 9);
                        g.DrawLines(pen, new[] { new PointF(9, 9), new PointF(11, 27), new PointF(21, 27), new PointF(23, 9) });
                        g.DrawLine(pen, 13, 5, 19, 5);
                        break;
                }
            }
            return bmp;
        }
    }
}
