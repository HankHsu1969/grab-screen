using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GrabScreen
{
    internal enum CaptureMode { Window, Region }

    /// <summary>
    /// 擷取時蓋在整個螢幕上的半透明畫面。
    /// Window 模式：滑鼠移到視窗/物件上自動框出，點一下擷取。
    /// Region 模式：自己拖曳框選。
    /// </summary>
    internal class CaptureOverlay : Form
    {
        private readonly Bitmap bright;   // 原始畫面
        private readonly Bitmap dim;      // 變暗的畫面
        private readonly Rectangle vs;    // 所有螢幕的範圍（螢幕座標）
        private readonly CaptureMode mode;
        private readonly List<WindowInfo> windows;
        private ElementProbe probe;

        private Rectangle sel = Rectangle.Empty;   // 目前選取範圍（視窗內座標）
        private string selLabel = "";
        private Rectangle lastDirty = Rectangle.Empty;
        private Point mouse;
        private bool pressed, dragging;
        private Point dragStart;

        private WindowInfo hoverWin;
        private List<ElementBox> chain = new List<ElementBox>();
        private int levelUp = 0;
        private int requestId = 0, lastAppliedId = 0;

        private readonly Font labelFont, hintFont, magFont;
        private Rectangle hintRect;
        private bool hintVisible = true;
        private readonly string hintText;
        private static readonly Color Accent = Color.FromArgb(0, 150, 255);

        public Bitmap Result { get; private set; }

        /// <param name="screenShot">整個螢幕的截圖（交給這個視窗管理，之後會自動釋放）</param>
        public CaptureOverlay(Bitmap screenShot, Rectangle virtualScreen, CaptureMode mode, List<WindowInfo> windows)
        {
            this.vs = virtualScreen;
            this.mode = mode;
            this.windows = windows ?? new List<WindowInfo>();

            bright = new Bitmap(screenShot.Width, screenShot.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bright)) g.DrawImage(screenShot, 0, 0, screenShot.Width, screenShot.Height);
            screenShot.Dispose();
            dim = new Bitmap(bright.Width, bright.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dim))
            {
                g.DrawImage(bright, 0, 0, bright.Width, bright.Height);
                using (var br = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillRectangle(br, 0, 0, dim.Width, dim.Height);
            }

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.Black;
            Text = "GrabScreen";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Bounds = vs;

            labelFont = new Font("Microsoft JhengHei UI", Dpi.S(13f), FontStyle.Bold, GraphicsUnit.Pixel);
            hintFont = new Font("Microsoft JhengHei UI", Dpi.S(15f), FontStyle.Regular, GraphicsUnit.Pixel);
            magFont = new Font("Consolas", Dpi.S(12f), FontStyle.Regular, GraphicsUnit.Pixel);
            hintText = mode == CaptureMode.Window
                ? "移到要擷取的視窗或物件上，按左鍵擷取　｜　滾輪：調整範圍大小　｜　拖曳：自由框選　｜　Esc / 右鍵：取消"
                : "按住左鍵拖曳，框出要擷取的範圍　｜　Esc / 右鍵：取消";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_DPICHANGED) return; // 跨不同縮放比例的螢幕時，不要讓系統改變大小
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Bounds = vs;
            Activate();
            Native.SetForegroundWindow(Handle);

            // 操作提示放在滑鼠所在螢幕的上方中間
            var mon = ToClient(Screen.FromPoint(Cursor.Position).Bounds);
            Size ts = TextRenderer.MeasureText(hintText, hintFont);
            int pad = Dpi.S(14);
            hintRect = new Rectangle(mon.X + (mon.Width - ts.Width) / 2 - pad, mon.Y + Dpi.S(24), ts.Width + pad * 2, ts.Height + Dpi.S(16));

            if (mode == CaptureMode.Window)
            {
                probe = new ElementProbe();
                probe.Result += (id, hwnd, list) =>
                {
                    try { BeginInvoke(new Action(() => OnProbeResult(id, hwnd, list))); } catch { }
                };
            }
            mouse = PointToClient(Cursor.Position);
            if (mode == CaptureMode.Window) UpdateHover();
            Invalidate();
        }

        private Rectangle ToClient(Rectangle screenRect)
        {
            screenRect.Offset(-vs.X, -vs.Y);
            return screenRect;
        }

        // ------------------------------------------------------------ 滑鼠與鍵盤

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            if (hintVisible && Rectangle.Inflate(hintRect, Dpi.S(30), Dpi.S(30)).Contains(mouse))
            {
                hintVisible = false;
                Invalidate(hintRect);
            }
            if (pressed)
            {
                if (!dragging && (Math.Abs(e.X - dragStart.X) > Dpi.S(4) || Math.Abs(e.Y - dragStart.Y) > Dpi.S(4)))
                    dragging = true;
                if (dragging)
                {
                    sel = RectFrom(dragStart, mouse);
                    selLabel = "";
                }
            }
            else if (mode == CaptureMode.Window)
            {
                UpdateHover();
            }
            RefreshDirty();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                if (dragging)
                {
                    dragging = pressed = false;
                    sel = Rectangle.Empty;
                    if (mode == CaptureMode.Window) { hoverWin = null; UpdateHover(); }
                    RefreshDirty();
                }
                else Cancel();
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            pressed = true;
            dragStart = e.Location;
            if (mode == CaptureMode.Region)
            {
                dragging = true;
                sel = RectFrom(dragStart, dragStart);
                RefreshDirty();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pressed) return;
            bool wasDragging = dragging;
            pressed = dragging = false;
            if (wasDragging)
            {
                if (sel.Width >= 3 && sel.Height >= 3) { Finish(sel); return; }
                sel = Rectangle.Empty;
                if (mode == CaptureMode.Window) { hoverWin = null; UpdateHover(); }
                RefreshDirty();
            }
            else if (!sel.IsEmpty)
            {
                Finish(sel);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (mode != CaptureMode.Window || dragging || chain.Count == 0) return;
            levelUp += e.Delta > 0 ? 1 : -1;
            ApplyLevel();
            RefreshDirty();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Cancel();
            else if (e.KeyCode == Keys.Enter && !sel.IsEmpty) Finish(sel);
        }

        private Rectangle RectFrom(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            var r = new Rectangle(x, y, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
            r.Intersect(ClientRectangle);
            return r;
        }

        // ------------------------------------------------------------ 視窗/物件偵測

        private void UpdateHover()
        {
            var sp = new Point(mouse.X + vs.X, mouse.Y + vs.Y);
            WindowInfo w = null;
            foreach (var wi in windows)
            {
                if (wi.Bounds.Contains(sp)) { w = wi; break; }
            }
            if (w != hoverWin || chain.Count == 0)
            {
                hoverWin = w;
                levelUp = 0;
                chain = new List<ElementBox>();
                Rectangle wr = w != null ? w.Bounds : Screen.FromPoint(sp).Bounds;
                wr.Intersect(vs);
                chain.Add(new ElementBox(ToClient(wr), w != null ? "視窗" : "整個螢幕"));
            }
            else
            {
                // 滑鼠已經離開的子物件先拿掉，等背景執行緒算出新的結果
                for (int i = chain.Count - 1; i >= 1; i--)
                    if (!chain[i].Bounds.Contains(mouse)) chain.RemoveAt(i);
            }
            if (w != null && probe != null)
            {
                requestId++;
                probe.Request(requestId, w.Handle, w.Bounds, sp);
            }
            ApplyLevel();
        }

        private void OnProbeResult(int id, IntPtr hwnd, List<ElementBox> list)
        {
            if (IsDisposed || dragging || hoverWin == null || hoverWin.Handle != hwnd || id < lastAppliedId || chain.Count == 0) return;
            lastAppliedId = id;
            var newChain = new List<ElementBox>();
            newChain.Add(chain[0]);
            foreach (var eb in list)
            {
                var r = ToClient(eb.Bounds);
                if (r.Contains(mouse)) newChain.Add(new ElementBox(r, eb.Label));
            }
            chain = newChain;
            ApplyLevel();
            RefreshDirty();
        }

        private void ApplyLevel()
        {
            if (chain.Count == 0) { sel = Rectangle.Empty; selLabel = ""; return; }
            levelUp = Math.Max(0, Math.Min(levelUp, chain.Count - 1));
            int idx = chain.Count - 1 - levelUp;
            sel = chain[idx].Bounds;
            selLabel = chain[idx].Label;
        }

        // ------------------------------------------------------------ 結束

        private void Finish(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, bright.Width, bright.Height));
            if (r.Width < 1 || r.Height < 1) { Cancel(); return; }
            Result = ScreenCapture.Crop(bright, r);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (probe != null) probe.Dispose();
            bright.Dispose();
            dim.Dispose();
            labelFont.Dispose();
            hintFont.Dispose();
            magFont.Dispose();
        }

        // ------------------------------------------------------------ 繪圖

        private bool ShowMagnifier
        {
            get { return mode == CaptureMode.Region || dragging; }
        }

        private static Rectangle Union(Rectangle a, Rectangle b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;
            return Rectangle.Union(a, b);
        }

        private void RefreshDirty()
        {
            Rectangle d = Rectangle.Empty;
            if (!sel.IsEmpty)
            {
                d = Union(d, Rectangle.Inflate(sel, Dpi.S(5), Dpi.S(5)));
                d = Union(d, LabelRect());
            }
            if (ShowMagnifier) d = Union(d, Rectangle.Inflate(MagRect(), 3, 3));
            if (!lastDirty.IsEmpty) Invalidate(lastDirty);
            if (!d.IsEmpty) Invalidate(d);
            lastDirty = d;
        }

        private string LabelText()
        {
            string t = sel.Width + " × " + sel.Height;
            if (!string.IsNullOrEmpty(selLabel)) t += "   " + selLabel;
            return t;
        }

        private Rectangle LabelRect()
        {
            Size ts = TextRenderer.MeasureText(LabelText(), labelFont);
            int w = ts.Width + Dpi.S(12), h = ts.Height + Dpi.S(6);
            int x = sel.X, y = sel.Y - h - Dpi.S(4);
            if (y < 0) y = sel.Y + Dpi.S(4);
            if (x + w > ClientSize.Width) x = ClientSize.Width - w;
            return new Rectangle(Math.Max(0, x), y, w, h);
        }

        private Rectangle MagRect()
        {
            int m = Dpi.S(120), info = Dpi.S(38), off = Dpi.S(22);
            var mon = ToClient(Screen.FromPoint(new Point(mouse.X + vs.X, mouse.Y + vs.Y)).Bounds);
            int x = mouse.X + off, y = mouse.Y + off;
            if (x + m > mon.Right) x = mouse.X - off - m;
            if (y + m + info > mon.Bottom) y = mouse.Y - off - m - info;
            return new Rectangle(x, y, m, m + info);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Rectangle clip = e.ClipRectangle;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(dim, clip, clip, GraphicsUnit.Pixel);
            if (!sel.IsEmpty)
            {
                var r = Rectangle.Intersect(sel, clip);
                if (r.Width > 0 && r.Height > 0) g.DrawImage(bright, r, r, GraphicsUnit.Pixel);
            }
            g.CompositingMode = CompositingMode.SourceOver;

            if (!sel.IsEmpty)
            {
                float pw = Math.Max(2f, Dpi.S(2f));
                using (var pen = new Pen(Accent, pw))
                {
                    g.DrawRectangle(pen, sel.X - pw / 2f, sel.Y - pw / 2f, sel.Width + pw - 1, sel.Height + pw - 1);
                }
                var lr = LabelRect();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Icons.RoundRect(lr, Dpi.S(4f)))
                using (var br = new SolidBrush(Color.FromArgb(220, 20, 20, 25)))
                    g.FillPath(br, path);
                g.SmoothingMode = SmoothingMode.None;
                TextRenderer.DrawText(g, LabelText(), labelFont, lr, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            if (hintVisible && hintRect.IntersectsWith(clip))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Icons.RoundRect(hintRect, Dpi.S(8f)))
                using (var br = new SolidBrush(Color.FromArgb(225, 25, 25, 30)))
                    g.FillPath(br, path);
                g.SmoothingMode = SmoothingMode.None;
                TextRenderer.DrawText(g, hintText, hintFont, hintRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (ShowMagnifier) DrawMagnifier(g);
        }

        private void DrawMagnifier(Graphics g)
        {
            const int cells = 15;
            Rectangle mr = MagRect();
            int m = mr.Width;
            float cell = m / (float)cells;
            var area = new Rectangle(mr.X, mr.Y, m, m);
            using (var br = new SolidBrush(Color.Black)) g.FillRectangle(br, area);

            var src = new Rectangle(mouse.X - cells / 2, mouse.Y - cells / 2, cells, cells);
            var srcIn = Rectangle.Intersect(src, new Rectangle(0, 0, bright.Width, bright.Height));
            if (srcIn.Width > 0 && srcIn.Height > 0)
            {
                var dst = new RectangleF(area.X + (srcIn.X - src.X) * cell, area.Y + (srcIn.Y - src.Y) * cell, srcIn.Width * cell, srcIn.Height * cell);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bright, dst, srcIn, GraphicsUnit.Pixel);
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }
            // 十字準線
            float c0 = (cells / 2) * cell;
            using (var br = new SolidBrush(Color.FromArgb(70, 0, 170, 255)))
            {
                g.FillRectangle(br, area.X, area.Y + c0, m, cell);
                g.FillRectangle(br, area.X + c0, area.Y, cell, m);
            }
            using (var pen = new Pen(Color.White, 1f)) g.DrawRectangle(pen, area.X + c0, area.Y + c0, cell, cell);
            using (var pen = new Pen(Color.White, 2f)) g.DrawRectangle(pen, area.X, area.Y, m - 1, m - 1);

            // 座標與顏色
            var info = new Rectangle(mr.X, mr.Y + m, m, mr.Height - m);
            using (var br = new SolidBrush(Color.FromArgb(235, 20, 20, 25))) g.FillRectangle(br, info);
            string colorText = "";
            if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X < bright.Width && mouse.Y < bright.Height)
            {
                Color c = bright.GetPixel(mouse.X, mouse.Y);
                colorText = "RGB " + c.R + "," + c.G + "," + c.B;
            }
            string text = "X " + (mouse.X + vs.X) + "  Y " + (mouse.Y + vs.Y) + "\n" + colorText;
            TextRenderer.DrawText(g, text, magFont, info, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
