using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GrabScreen
{
    /// <summary>所有標示（框、箭頭、文字…）的共同基底。座標都是「圖片上的像素座標」。</summary>
    internal abstract class Shape
    {
        public Color Color = Color.Red;
        public int Level = 2; // 粗細/大小等級 0~4

        public static readonly float[] StrokeSizes = { 2f, 3f, 5f, 8f, 12f };
        public static readonly float[] FontSizes = { 14f, 18f, 26f, 36f, 52f };
        public static readonly float[] StepRadius = { 10f, 13f, 17f, 22f, 30f };
        public static readonly int[] MosaicBlocks = { 5, 8, 12, 18, 26 };

        /// <summary>圖片調整尺寸後，線條、文字、編號也要跟著放大縮小的倍率。</summary>
        public float Scale = 1f;

        public float Stroke { get { return StrokeSizes[Level] * Scale; } }

        public abstract void Draw(Graphics g, Bitmap baseImage);
        public abstract RectangleF GetBounds();
        public abstract void Offset(float dx, float dy);
        public abstract Shape Clone();
        protected abstract void ScalePoints(float sx, float sy);

        /// <summary>跟著圖片一起縮放（位置與粗細都會變）。</summary>
        public void Resize(float sx, float sy)
        {
            ScalePoints(sx, sy);
            Scale *= (float)Math.Sqrt(sx * sy);
        }

        protected static PointF Mul(PointF p, float sx, float sy) { return new PointF(p.X * sx, p.Y * sy); }

        public virtual bool HitTest(PointF p, float tol)
        {
            var b = GetBounds();
            b.Inflate(tol, tol);
            return b.Contains(p);
        }

        public virtual PointF[] GetHandles() { return new PointF[0]; }
        public virtual void MoveHandle(int index, PointF p) { }

        protected T CopyBase<T>(T s) where T : Shape
        {
            s.Color = Color;
            s.Level = Level;
            s.Scale = Scale;
            return s;
        }

        protected static bool OutlineHit(GraphicsPath path, PointF p, float width)
        {
            using (var pen = new Pen(Color.Black, Math.Max(1f, width))) return path.IsOutlineVisible(p, pen);
        }
    }

    /// <summary>用兩個點決定的標示：矩形、橢圓、直線、箭頭、馬賽克。</summary>
    internal abstract class TwoPointShape : Shape
    {
        public PointF P1, P2;
        protected virtual bool IsBox { get { return true; } }

        public RectangleF Rect
        {
            get
            {
                return RectangleF.FromLTRB(Math.Min(P1.X, P2.X), Math.Min(P1.Y, P2.Y), Math.Max(P1.X, P2.X), Math.Max(P1.Y, P2.Y));
            }
        }

        public override RectangleF GetBounds()
        {
            var r = Rect;
            r.Inflate(Stroke, Stroke);
            return r;
        }

        public override void Offset(float dx, float dy)
        {
            P1 = new PointF(P1.X + dx, P1.Y + dy);
            P2 = new PointF(P2.X + dx, P2.Y + dy);
        }

        protected override void ScalePoints(float sx, float sy)
        {
            P1 = Mul(P1, sx, sy);
            P2 = Mul(P2, sx, sy);
        }

        public override PointF[] GetHandles()
        {
            if (!IsBox) return new[] { P1, P2 };
            var r = Rect;
            return new[] { new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) };
        }

        public override void MoveHandle(int index, PointF p)
        {
            if (!IsBox)
            {
                if (index == 0) P1 = p; else P2 = p;
                return;
            }
            var h = GetHandles();
            P1 = h[(index + 2) % 4]; // 對角固定不動
            P2 = p;
        }

        protected T CopyPoints<T>(T s) where T : TwoPointShape
        {
            CopyBase(s);
            s.P1 = P1;
            s.P2 = P2;
            return s;
        }
    }

    internal class RectShape : TwoPointShape
    {
        public override void Draw(Graphics g, Bitmap baseImage)
        {
            using (var pen = new Pen(Color, Stroke) { LineJoin = LineJoin.Miter })
            {
                var r = Rect;
                g.DrawRectangle(pen, r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
            }
        }

        public override bool HitTest(PointF p, float tol)
        {
            using (var path = new GraphicsPath())
            {
                var r = Rect;
                path.AddRectangle(new RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)));
                return OutlineHit(path, p, Stroke + tol * 2);
            }
        }

        public override Shape Clone() { return CopyPoints(new RectShape()); }
    }

    internal class EllipseShape : TwoPointShape
    {
        public override void Draw(Graphics g, Bitmap baseImage)
        {
            using (var pen = new Pen(Color, Stroke))
            {
                var r = Rect;
                g.DrawEllipse(pen, r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
            }
        }

        public override bool HitTest(PointF p, float tol)
        {
            using (var path = new GraphicsPath())
            {
                var r = Rect;
                path.AddEllipse(new RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)));
                return OutlineHit(path, p, Stroke + tol * 2);
            }
        }

        public override Shape Clone() { return CopyPoints(new EllipseShape()); }
    }

    internal class LineShape : TwoPointShape
    {
        protected override bool IsBox { get { return false; } }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            using (var pen = new Pen(Color, Stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, P1, P2);
        }

        public override bool HitTest(PointF p, float tol)
        {
            return DistanceToSegment(p, P1, P2) <= Stroke / 2f + tol;
        }

        public static float DistanceToSegment(PointF p, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;
            float t = len2 < 0.0001f ? 0f : Math.Max(0f, Math.Min(1f, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            float cx = a.X + t * dx - p.X, cy = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        public override Shape Clone() { return CopyPoints(new LineShape()); }
    }

    internal class ArrowShape : LineShape
    {
        public override void Draw(Graphics g, Bitmap baseImage)
        {
            float dx = P2.X - P1.X, dy = P2.Y - P1.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.5f) return;
            float ux = dx / len, uy = dy / len;
            float w = Stroke;
            float headLen = Math.Min(len, Math.Max(14f * Scale, w * 4.2f));
            float headW = headLen * 0.75f;
            var basePt = new PointF(P2.X - ux * headLen, P2.Y - uy * headLen);
            var head = new[]
            {
                P2,
                new PointF(basePt.X - uy * headW / 2f, basePt.Y + ux * headW / 2f),
                new PointF(basePt.X + ux * headLen * 0.15f, basePt.Y + uy * headLen * 0.15f),
                new PointF(basePt.X + uy * headW / 2f, basePt.Y - ux * headW / 2f)
            };
            using (var pen = new Pen(Color, w) { StartCap = LineCap.Round, EndCap = LineCap.Flat })
                g.DrawLine(pen, P1, new PointF(basePt.X + ux * headLen * 0.2f, basePt.Y + uy * headLen * 0.2f));
            using (var br = new SolidBrush(Color))
                g.FillPolygon(br, head);
        }

        public override RectangleF GetBounds()
        {
            var r = Rect;
            float m = Math.Max(14f * Scale, Stroke * 4.2f);
            r.Inflate(m, m);
            return r;
        }

        public override Shape Clone() { return CopyPoints(new ArrowShape()); }
    }

    /// <summary>馬賽克：把底圖那一塊變成粗格子，用來遮住個資。</summary>
    internal class MosaicShape : TwoPointShape
    {
        private Bitmap cache;
        private Bitmap cacheBase;
        private Rectangle cacheRect;
        private int cacheBlock;

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            if (baseImage == null) return;
            var rf = Rect;
            var r = Rectangle.Round(rf);
            r.Intersect(new Rectangle(0, 0, baseImage.Width, baseImage.Height));
            if (r.Width < 1 || r.Height < 1) return;
            int block = Math.Max(2, (int)Math.Round(MosaicBlocks[Level] * Scale));
            if (cache == null || cacheBase != baseImage || cacheRect != r || cacheBlock != block)
            {
                if (cache != null) cache.Dispose();
                int sw = Math.Max(1, (int)Math.Ceiling(r.Width / (double)block));
                int sh = Math.Max(1, (int)Math.Ceiling(r.Height / (double)block));
                cache = new Bitmap(sw, sh);
                using (var cg = Graphics.FromImage(cache))
                {
                    cg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    cg.DrawImage(baseImage, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
                }
                cacheBase = baseImage; cacheRect = r; cacheBlock = block;
            }
            var state = g.Save();
            g.SetClip(r, CombineMode.Intersect);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cache, new Rectangle(r.X, r.Y, cache.Width * block, cache.Height * block));
            g.Restore(state);
        }

        public override RectangleF GetBounds() { return Rect; }

        public override bool HitTest(PointF p, float tol)
        {
            var r = Rect;
            r.Inflate(tol, tol);
            return r.Contains(p);
        }

        public override Shape Clone() { return CopyPoints(new MosaicShape()); }
    }

    /// <summary>手繪筆跡；Highlighter=true 時是半透明的螢光筆。</summary>
    internal class PathShape : Shape
    {
        public List<PointF> Points = new List<PointF>();
        public bool Highlighter;

        private float Width
        {
            get { return Highlighter ? Math.Max(12f * Scale, Stroke * 3.5f) : Stroke; }
        }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            if (Points.Count == 0) return;
            Color c = Highlighter ? Color.FromArgb(110, Color) : Color;
            using (var pen = new Pen(c, Width))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = pen.EndCap = Highlighter ? LineCap.Square : LineCap.Round;
                if (Points.Count == 1)
                {
                    var p = Points[0];
                    using (var br = new SolidBrush(c)) g.FillEllipse(br, p.X - Width / 2, p.Y - Width / 2, Width, Width);
                }
                else
                {
                    var mode = g.CompositingMode;
                    g.DrawLines(pen, Points.ToArray());
                }
            }
        }

        public override RectangleF GetBounds()
        {
            if (Points.Count == 0) return RectangleF.Empty;
            float l = float.MaxValue, t = float.MaxValue, r = float.MinValue, b = float.MinValue;
            foreach (var p in Points)
            {
                l = Math.Min(l, p.X); t = Math.Min(t, p.Y); r = Math.Max(r, p.X); b = Math.Max(b, p.Y);
            }
            var rc = RectangleF.FromLTRB(l, t, r, b);
            rc.Inflate(Width / 2f, Width / 2f);
            return rc;
        }

        public override bool HitTest(PointF p, float tol)
        {
            if (Points.Count == 1) return LineShape.DistanceToSegment(p, Points[0], Points[0]) <= Width / 2 + tol;
            for (int i = 1; i < Points.Count; i++)
                if (LineShape.DistanceToSegment(p, Points[i - 1], Points[i]) <= Width / 2 + tol) return true;
            return false;
        }

        protected override void ScalePoints(float sx, float sy)
        {
            for (int i = 0; i < Points.Count; i++) Points[i] = Mul(Points[i], sx, sy);
        }

        public override void Offset(float dx, float dy)
        {
            for (int i = 0; i < Points.Count; i++) Points[i] = new PointF(Points[i].X + dx, Points[i].Y + dy);
        }

        public override Shape Clone()
        {
            var s = CopyBase(new PathShape());
            s.Points = new List<PointF>(Points);
            s.Highlighter = Highlighter;
            return s;
        }
    }

    /// <summary>文字標示：彩色字＋白色描邊，放在任何背景上都看得清楚。</summary>
    internal class TextShape : Shape
    {
        public PointF Location;
        public string Text = "";
        public const string FontName = "Microsoft JhengHei UI";

        public float FontSize { get { return FontSizes[Level] * Scale; } }

        private GraphicsPath BuildPath()
        {
            var path = new GraphicsPath();
            using (var family = new FontFamily(FontName))
            {
                var sf = StringFormat.GenericTypographic;
                path.AddString(Text.Length == 0 ? " " : Text, family, (int)FontStyle.Bold, FontSize, Location, sf);
            }
            return path;
        }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            if (string.IsNullOrEmpty(Text)) return;
            using (var path = BuildPath())
            {
                bool light = Color.GetBrightness() > 0.75f;
                using (var outline = new Pen(light ? Color.FromArgb(220, 30, 30, 30) : Color.White, Math.Max(3f, FontSize / 5f)) { LineJoin = LineJoin.Round })
                    g.DrawPath(outline, path);
                using (var br = new SolidBrush(Color))
                    g.FillPath(br, path);
            }
        }

        public override RectangleF GetBounds()
        {
            using (var path = BuildPath())
            {
                var r = path.GetBounds();
                if (r.Width < 1 || r.Height < 1) r = new RectangleF(Location.X, Location.Y, FontSize, FontSize * 1.3f);
                float m = Math.Max(3f, FontSize / 5f);
                r.Inflate(m, m);
                return r;
            }
        }

        public override void Offset(float dx, float dy)
        {
            Location = new PointF(Location.X + dx, Location.Y + dy);
        }

        protected override void ScalePoints(float sx, float sy) { Location = Mul(Location, sx, sy); }

        public override Shape Clone()
        {
            var s = CopyBase(new TextShape());
            s.Location = Location;
            s.Text = Text;
            return s;
        }
    }

    /// <summary>編號圓圈 ① ② ③，標示操作步驟用。</summary>
    internal class StepShape : Shape
    {
        public PointF Center;
        public int Number = 1;

        public float Radius { get { return StepRadius[Level] * Scale; } }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            float r = Radius;
            var rc = new RectangleF(Center.X - r, Center.Y - r, r * 2, r * 2);
            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(shadow, rc.X + 1.5f, rc.Y + 2f, rc.Width, rc.Height);
            using (var br = new SolidBrush(Color)) g.FillEllipse(br, rc);
            using (var pen = new Pen(Color.White, Math.Max(1.5f, r / 8f))) g.DrawEllipse(pen, rc);
            string text = Number.ToString();
            float fs = r * (text.Length >= 2 ? 0.95f : 1.15f);
            bool light = Color.GetBrightness() > 0.75f;
            using (var f = new Font("Arial", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var tb = new SolidBrush(light ? Color.Black : Color.White))
                g.DrawString(text, f, tb, new RectangleF(rc.X, rc.Y + r * 0.06f, rc.Width, rc.Height), sf);
        }

        public override RectangleF GetBounds()
        {
            float r = Radius + 2;
            return new RectangleF(Center.X - r, Center.Y - r, r * 2, r * 2);
        }

        public override void Offset(float dx, float dy)
        {
            Center = new PointF(Center.X + dx, Center.Y + dy);
        }

        protected override void ScalePoints(float sx, float sy) { Center = Mul(Center, sx, sy); }

        public override Shape Clone()
        {
            var s = CopyBase(new StepShape());
            s.Center = Center;
            s.Number = Number;
            return s;
        }
    }
}
