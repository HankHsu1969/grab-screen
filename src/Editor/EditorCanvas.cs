using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GrabScreen
{
    internal enum Tool { Select, Rect, Ellipse, Arrow, Line, Pen, Highlighter, Text, Step, Mosaic, Crop }

    internal class EditorState
    {
        public Bitmap Base;
        public List<Shape> Shapes;
        public int NextStep;
    }

    /// <summary>編輯器中間的畫布：顯示圖片、處理滑鼠畫標示、復原/重做、縮放。</summary>
    internal class EditorCanvas : Control
    {
        public Bitmap BaseImage { get; private set; }
        public List<Shape> Shapes = new List<Shape>();
        public int NextStep = 1;
        private Color currentColor = Color.FromArgb(229, 57, 53);
        public Color CurrentColor { get { return currentColor; } set { currentColor = value; } }
        public int CurrentLevel = 2;
        public bool Dirty;

        public event EventHandler StateChanged;

        private Tool tool = Tool.Rect;
        private float zoom = 1f;
        private Shape selected;
        private readonly Stack<EditorState> undo = new Stack<EditorState>();
        private readonly Stack<EditorState> redo = new Stack<EditorState>();

        private Shape live;            // 正在畫或正在拖曳的標示（不放進快取）
        private bool mouseDown;
        private PointF downPt, lastPt;
        private int dragHandle = -1;
        private bool moving, moved;
        private EditorState pending;
        private bool cropping;
        private RectangleF cropRect;
        private bool panning;
        private Point panStart, panScrollStart;

        private TextBox textBox;
        private TextShape editingText;
        private bool editingIsNew;

        private Bitmap cache;
        private bool cacheValid;
        private const int ImgMargin = 24;
        private static readonly Color Accent = Color.FromArgb(0, 120, 215);

        public EditorCanvas(Bitmap image)
        {
            BaseImage = image;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.FromArgb(228, 229, 233);
            Cursor = Cursors.Cross;
        }

        // ============================================================ 屬性

        public Tool Tool
        {
            get { return tool; }
            set
            {
                CommitText();
                tool = value;
                if (tool != Tool.Select) selected = null;
                cropping = false;
                Cursor = tool == Tool.Text ? Cursors.IBeam : (tool == Tool.Select ? Cursors.Default : Cursors.Cross);
                Invalidate();
                RaiseChanged();
            }
        }

        public Shape Selected { get { return selected; } }
        public float Zoom { get { return zoom; } }
        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }
        public bool IsEditingText { get { return textBox != null; } }

        private void RaiseChanged()
        {
            var h = StateChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void Changed()
        {
            Dirty = true;
            cacheValid = false;
            Invalidate();
            RaiseChanged();
        }

        // ============================================================ 座標換算與縮放

        private ScrollableControl Host { get { return Parent as ScrollableControl; } }

        private Point Origin
        {
            get
            {
                int iw = (int)Math.Ceiling(BaseImage.Width * zoom), ih = (int)Math.Ceiling(BaseImage.Height * zoom);
                return new Point(Math.Max(0, (Width - iw) / 2), Math.Max(0, (Height - ih) / 2));
            }
        }

        public PointF ToImage(Point p)
        {
            var o = Origin;
            return new PointF((p.X - o.X) / zoom, (p.Y - o.Y) / zoom);
        }

        public PointF ToScreen(PointF p)
        {
            var o = Origin;
            return new PointF(p.X * zoom + o.X, p.Y * zoom + o.Y);
        }

        /// <summary>依外框（可捲動面板）大小調整畫布大小。</summary>
        public void UpdateCanvasSize()
        {
            var host = Host;
            if (host == null) return;
            Size view = host.ClientSize;
            int w = Math.Max(view.Width, (int)Math.Ceiling(BaseImage.Width * zoom) + ImgMargin * 2);
            int h = Math.Max(view.Height, (int)Math.Ceiling(BaseImage.Height * zoom) + ImgMargin * 2);
            if (Size != new Size(w, h)) Size = new Size(w, h);
            Invalidate();
        }

        public void SetZoom(float z, Point? anchor)
        {
            var host = Host;
            float maxZ = Math.Min(8f, 30000f / Math.Max(BaseImage.Width, BaseImage.Height));
            z = Math.Max(0.05f, Math.Min(maxZ, z));
            if (host == null) { zoom = z; return; }
            Point a = anchor.HasValue ? anchor.Value : new Point(-Left + host.ClientSize.Width / 2, -Top + host.ClientSize.Height / 2);
            PointF imgPt = ToImage(a);
            Point aView = new Point(a.X + Left, a.Y + Top);
            zoom = z;
            cacheValid = false;
            UpdateCanvasSize();
            PointF np = ToScreen(imgPt);
            host.AutoScrollPosition = new Point((int)(np.X - aView.X), (int)(np.Y - aView.Y));
            CommitText();
            Invalidate();
            RaiseChanged();
        }

        public void FitToWindow()
        {
            var host = Host;
            if (host == null) return;
            Size v = host.ClientSize;
            float z = Math.Min(1f, Math.Min((v.Width - ImgMargin * 2) / (float)BaseImage.Width, (v.Height - ImgMargin * 2) / (float)BaseImage.Height));
            zoom = Math.Max(0.05f, z);
            cacheValid = false;
            UpdateCanvasSize();
            host.AutoScrollPosition = Point.Empty;
            Invalidate();
            RaiseChanged();
        }

        // ============================================================ 繪圖

        private void RenderScene(Graphics g)
        {
            g.InterpolationMode = zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(BaseImage, 0, 0, BaseImage.Width, BaseImage.Height);
            g.PixelOffsetMode = PixelOffsetMode.Default;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            foreach (var s in Shapes)
            {
                if (s == live || s == editingText) continue;
                s.Draw(g, BaseImage);
            }
        }

        private bool UseCache
        {
            get { return (double)BaseImage.Width * zoom * BaseImage.Height * zoom <= 25e6; }
        }

        private void EnsureCache()
        {
            int cw = Math.Max(1, (int)Math.Ceiling(BaseImage.Width * zoom));
            int ch = Math.Max(1, (int)Math.Ceiling(BaseImage.Height * zoom));
            if (cacheValid && cache != null && cache.Width == cw && cache.Height == ch) return;
            if (cache == null || cache.Width != cw || cache.Height != ch)
            {
                if (cache != null) cache.Dispose();
                cache = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb);
            }
            using (var g = Graphics.FromImage(cache))
            {
                g.Clear(Color.White);
                g.ScaleTransform(zoom, zoom);
                RenderScene(g);
            }
            cacheValid = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            var o = Origin;
            int iw = (int)Math.Ceiling(BaseImage.Width * zoom), ih = (int)Math.Ceiling(BaseImage.Height * zoom);
            using (var sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillRectangle(sh, o.X + 3, o.Y + 3, iw, ih);

            if (UseCache)
            {
                EnsureCache();
                g.DrawImageUnscaled(cache, o.X, o.Y);
            }
            else
            {
                var st0 = g.Save();
                g.SetClip(new Rectangle(o.X, o.Y, iw, ih));
                g.TranslateTransform(o.X, o.Y);
                g.ScaleTransform(zoom, zoom);
                RenderScene(g);
                g.Restore(st0);
            }

            // 正在畫/拖曳中的標示
            if (live != null)
            {
                var st = g.Save();
                g.SetClip(new Rectangle(o.X, o.Y, iw, ih));
                g.TranslateTransform(o.X, o.Y);
                g.ScaleTransform(zoom, zoom);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                live.Draw(g, BaseImage);
                g.Restore(st);
            }

            if (cropping) DrawCrop(g, o, iw, ih);
            if (selected != null && tool == Tool.Select) DrawSelection(g);
        }

        private void DrawCrop(Graphics g, Point o, int iw, int ih)
        {
            var a = ToScreen(cropRect.Location);
            var r = new RectangleF(a.X, a.Y, cropRect.Width * zoom, cropRect.Height * zoom);
            using (var region = new Region(new Rectangle(o.X, o.Y, iw, ih)))
            {
                region.Exclude(r);
                using (var br = new SolidBrush(Color.FromArgb(130, 0, 0, 0))) g.FillRegion(br, region);
            }
            using (var pen = new Pen(Color.White, 1.5f) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            string t = (int)Math.Round(cropRect.Width) + " × " + (int)Math.Round(cropRect.Height);
            using (var f = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold))
            {
                var ts = g.MeasureString(t, f);
                var lr = new RectangleF(r.X, Math.Max(0, r.Y - ts.Height - 4), ts.Width + 8, ts.Height + 2);
                using (var br = new SolidBrush(Color.FromArgb(210, 20, 20, 25))) g.FillRectangle(br, lr);
                g.DrawString(t, f, Brushes.White, lr.X + 4, lr.Y + 1);
            }
        }

        private void DrawSelection(Graphics g)
        {
            var b = selected.GetBounds();
            var a = ToScreen(b.Location);
            var r = new RectangleF(a.X, a.Y, b.Width * zoom, b.Height * zoom);
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(Accent, 1f) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            float hs = Dpi.S(8f);
            foreach (var hp in selected.GetHandles())
            {
                var p = ToScreen(hp);
                var hr = new RectangleF(p.X - hs / 2, p.Y - hs / 2, hs, hs);
                g.FillRectangle(Brushes.White, hr);
                using (var pen = new Pen(Accent, 1.5f)) g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height);
            }
        }

        // ============================================================ 滑鼠操作

        private Shape HitShape(PointF p)
        {
            float tol = Dpi.S(5f) / zoom;
            for (int i = Shapes.Count - 1; i >= 0; i--)
                if (Shapes[i].HitTest(p, tol)) return Shapes[i];
            return null;
        }

        private int HitHandle(Point screen)
        {
            if (selected == null) return -1;
            var hs = selected.GetHandles();
            float tol = Dpi.S(7f);
            for (int i = 0; i < hs.Length; i++)
            {
                var p = ToScreen(hs[i]);
                if (Math.Abs(p.X - screen.X) <= tol && Math.Abs(p.Y - screen.Y) <= tol) return i;
            }
            return -1;
        }

        private PointF ClampToImage(PointF p)
        {
            return new PointF(Math.Max(0, Math.Min(BaseImage.Width, p.X)), Math.Max(0, Math.Min(BaseImage.Height, p.Y)));
        }

        private static PointF Constrain(PointF a, PointF b, bool box)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (box)
            {
                float d = Math.Max(Math.Abs(dx), Math.Abs(dy));
                return new PointF(a.X + Math.Sign(dx) * d, a.Y + Math.Sign(dy) * d);
            }
            double ang = Math.Atan2(dy, dx);
            double snap = Math.Round(ang / (Math.PI / 4)) * (Math.PI / 4);
            double len = Math.Sqrt(dx * dx + dy * dy);
            return new PointF(a.X + (float)(Math.Cos(snap) * len), a.Y + (float)(Math.Sin(snap) * len));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (textBox != null)
            {
                CommitText();
                if (tool == Tool.Text) return;
            }
            Focus();
            if (e.Button == MouseButtons.Middle)
            {
                var host = Host;
                if (host == null) return;
                panning = true;
                panStart = Cursor.Position;
                panScrollStart = new Point(-host.AutoScrollPosition.X, -host.AutoScrollPosition.Y);
                Cursor = Cursors.SizeAll;
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            PointF p = ToImage(e.Location);
            mouseDown = true;
            downPt = lastPt = p;

            switch (tool)
            {
                case Tool.Select:
                    int hi = HitHandle(e.Location);
                    if (hi >= 0)
                    {
                        dragHandle = hi;
                    }
                    else
                    {
                        selected = HitShape(p);
                        if (selected != null) moving = true;
                    }
                    if (selected != null)
                    {
                        pending = Snapshot();
                        live = selected;
                        moved = false;
                        cacheValid = false;
                    }
                    Invalidate();
                    RaiseChanged();
                    break;
                case Tool.Rect: StartTwoPoint(new RectShape(), p); break;
                case Tool.Ellipse: StartTwoPoint(new EllipseShape(), p); break;
                case Tool.Line: StartTwoPoint(new LineShape(), p); break;
                case Tool.Arrow: StartTwoPoint(new ArrowShape(), p); break;
                case Tool.Mosaic: StartTwoPoint(new MosaicShape(), p); break;
                case Tool.Pen:
                case Tool.Highlighter:
                    var ps = new PathShape { Color = CurrentColor, Level = CurrentLevel, Highlighter = tool == Tool.Highlighter };
                    ps.Points.Add(p);
                    live = ps;
                    break;
                case Tool.Text:
                    mouseDown = false;
                    var hitText = HitShape(p) as TextShape;
                    if (hitText != null) BeginEditText(hitText, false);
                    else
                    {
                        var ts = new TextShape { Color = CurrentColor, Level = CurrentLevel };
                        ts.Location = new PointF(p.X, p.Y - ts.FontSize * 0.6f);
                        BeginEditText(ts, true);
                    }
                    break;
                case Tool.Step:
                    mouseDown = false;
                    PushUndo();
                    Shapes.Add(new StepShape { Color = CurrentColor, Level = CurrentLevel, Center = p, Number = NextStep });
                    NextStep++;
                    Changed();
                    break;
                case Tool.Crop:
                    cropping = true;
                    var cp = ClampToImage(p);
                    downPt = cp;
                    cropRect = new RectangleF(cp, SizeF.Empty);
                    Invalidate();
                    break;
            }
        }

        private void StartTwoPoint(TwoPointShape s, PointF p)
        {
            s.Color = CurrentColor;
            s.Level = CurrentLevel;
            s.P1 = s.P2 = p;
            live = s;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (panning)
            {
                var host = Host;
                var cur = Cursor.Position;
                if (host != null) host.AutoScrollPosition = new Point(panScrollStart.X - (cur.X - panStart.X), panScrollStart.Y - (cur.Y - panStart.Y));
                return;
            }
            if (!mouseDown)
            {
                if (tool == Tool.Select)
                    Cursor = (HitHandle(e.Location) >= 0) ? Cursors.Cross : (HitShape(ToImage(e.Location)) != null ? Cursors.SizeAll : Cursors.Default);
                return;
            }

            PointF p = ToImage(e.Location);
            bool shift = (ModifierKeys & Keys.Shift) != 0;

            if (tool == Tool.Select && selected != null)
            {
                if (dragHandle >= 0) selected.MoveHandle(dragHandle, p);
                else if (moving) selected.Offset(p.X - lastPt.X, p.Y - lastPt.Y);
                moved = true;
                lastPt = p;
                Invalidate();
                return;
            }
            if (tool == Tool.Crop && cropping)
            {
                var cp = ClampToImage(p);
                cropRect = RectangleF.FromLTRB(Math.Min(downPt.X, cp.X), Math.Min(downPt.Y, cp.Y), Math.Max(downPt.X, cp.X), Math.Max(downPt.Y, cp.Y));
                Invalidate();
                return;
            }
            var two = live as TwoPointShape;
            if (two != null)
            {
                bool box = !(two is LineShape);
                two.P2 = shift ? Constrain(two.P1, p, box) : p;
                Invalidate();
                return;
            }
            var path = live as PathShape;
            if (path != null)
            {
                if (shift)
                {
                    var first = path.Points[0];
                    path.Points.Clear();
                    path.Points.Add(first);
                    path.Points.Add(p);
                }
                else
                {
                    var last = path.Points[path.Points.Count - 1];
                    float dx = p.X - last.X, dy = p.Y - last.Y;
                    if (dx * dx + dy * dy >= (1f / zoom) * (1f / zoom)) path.Points.Add(p);
                }
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (panning && e.Button == MouseButtons.Middle)
            {
                panning = false;
                Cursor = tool == Tool.Text ? Cursors.IBeam : (tool == Tool.Select ? Cursors.Default : Cursors.Cross);
                return;
            }
            if (!mouseDown || e.Button != MouseButtons.Left) return;
            mouseDown = false;

            if (tool == Tool.Select)
            {
                if (selected != null && moved && pending != null)
                {
                    undo.Push(pending);
                    redo.Clear();
                    Dirty = true;
                }
                pending = null;
                live = null;
                moving = false;
                dragHandle = -1;
                cacheValid = false;
                Invalidate();
                RaiseChanged();
                return;
            }
            if (tool == Tool.Crop)
            {
                cropping = false;
                if (cropRect.Width >= 4 && cropRect.Height >= 4) ApplyCrop(Rectangle.Round(cropRect));
                else Invalidate();
                return;
            }
            var two = live as TwoPointShape;
            if (two != null)
            {
                float dx = two.P2.X - two.P1.X, dy = two.P2.Y - two.P1.Y;
                bool ok = (two is LineShape) ? (dx * dx + dy * dy >= 9) : (Math.Abs(dx) >= 3 && Math.Abs(dy) >= 3);
                live = null;
                if (ok)
                {
                    PushUndo();
                    Shapes.Add(two);
                    Changed();
                }
                else Invalidate();
                return;
            }
            var path = live as PathShape;
            if (path != null)
            {
                live = null;
                PushUndo();
                Shapes.Add(path);
                Changed();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (tool != Tool.Select || e.Button != MouseButtons.Left) return;
            var ts = HitShape(ToImage(e.Location)) as TextShape;
            if (ts != null) BeginEditText(ts, false);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0)
            {
                SetZoom(zoom * (e.Delta > 0 ? 1.2f : 1 / 1.2f), e.Location);
                return;
            }
            var host = Host;
            if (host == null) return;
            var cur = new Point(-host.AutoScrollPosition.X, -host.AutoScrollPosition.Y);
            int step = e.Delta / 120 * Dpi.S(60);
            if ((ModifierKeys & Keys.Shift) != 0) cur.X -= step; else cur.Y -= step;
            host.AutoScrollPosition = cur;
        }

        // ============================================================ 文字輸入

        private void BeginEditText(TextShape ts, bool isNew)
        {
            CommitText();
            editingText = ts;
            editingIsNew = isNew;
            selected = null;
            cacheValid = false;
            Invalidate();

            var tb = new TextBox
            {
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(TextShape.FontName, Math.Max(6f, ts.FontSize * zoom), FontStyle.Bold, GraphicsUnit.Pixel),
                Text = ts.Text,
                ScrollBars = ScrollBars.None,
                WordWrap = false,
                AcceptsReturn = true
            };
            StyleTextBox(tb, ts.Color);
            var sp = ToScreen(ts.Location);
            tb.Location = new Point((int)sp.X - 3, (int)sp.Y - 3);
            tb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    CommitText();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    if (editingIsNew && textBox != null) textBox.Text = "";
                    CommitText();
                }
            };
            tb.TextChanged += (s, e) => ResizeTextBox(tb);
            tb.LostFocus += (s, e) => { if (textBox == tb) CommitText(); };
            textBox = tb;
            ResizeTextBox(tb);
            Controls.Add(tb);
            tb.Focus();
            tb.SelectAll();
            RaiseChanged();
        }

        private static void StyleTextBox(TextBox tb, Color c)
        {
            bool light = c.GetBrightness() > 0.75f;
            tb.ForeColor = c;
            tb.BackColor = light ? Color.FromArgb(60, 60, 64) : Color.FromArgb(255, 255, 250);
        }

        private void ResizeTextBox(TextBox tb)
        {
            string t = tb.Text.Length == 0 ? "輸入文字" : tb.Text + "W";
            Size sz = TextRenderer.MeasureText(t, tb.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int lines = Math.Max(1, tb.Lines.Length);
            tb.Size = new Size(Math.Max(Dpi.S(120), sz.Width + Dpi.S(16)), Math.Max(sz.Height, tb.Font.Height * lines) + Dpi.S(10));
        }

        public void CommitText()
        {
            if (textBox == null) return;
            var tb = textBox;
            textBox = null;
            string t = tb.Text.TrimEnd();
            var ts = editingText;
            editingText = null;
            Controls.Remove(tb);
            BeginInvoke(new Action(tb.Dispose));
            bool changed = false;
            if (ts != null)
            {
                if (editingIsNew)
                {
                    if (t.Length > 0)
                    {
                        PushUndo();
                        ts.Text = t;
                        Shapes.Add(ts);
                        changed = true;
                    }
                }
                else if (t != ts.Text)
                {
                    PushUndo();
                    if (t.Length == 0) Shapes.Remove(ts); else ts.Text = t;
                    changed = true;
                }
            }
            if (changed) Changed();
            else { cacheValid = false; Invalidate(); RaiseChanged(); }
            Focus();
        }

        // ============================================================ 編輯指令

        private EditorState Snapshot()
        {
            var list = new List<Shape>(Shapes.Count);
            foreach (var s in Shapes) list.Add(s.Clone());
            return new EditorState { Base = BaseImage, Shapes = list, NextStep = NextStep };
        }

        private void PushUndo()
        {
            undo.Push(Snapshot());
            redo.Clear();
            if (undo.Count > 100)
            {
                var arr = undo.ToArray();
                undo.Clear();
                for (int i = 99; i >= 0; i--) undo.Push(arr[i]);
            }
        }

        private void Restore(EditorState st)
        {
            bool sizeChanged = st.Base.Size != BaseImage.Size;
            BaseImage = st.Base;
            Shapes = st.Shapes;
            NextStep = st.NextStep;
            selected = null;
            live = null;
            Changed();
            if (sizeChanged) FitToWindow();
        }

        public void Undo()
        {
            CommitText();
            if (undo.Count == 0) return;
            redo.Push(Snapshot());
            Restore(undo.Pop());
        }

        public void Redo()
        {
            CommitText();
            if (redo.Count == 0) return;
            undo.Push(Snapshot());
            Restore(redo.Pop());
        }

        public void DeleteSelected()
        {
            if (selected == null) return;
            PushUndo();
            Shapes.Remove(selected);
            selected = null;
            Changed();
        }

        public void NudgeSelected(float dx, float dy)
        {
            if (selected == null) return;
            PushUndo();
            selected.Offset(dx, dy);
            Changed();
        }

        public void ApplyColor(Color c)
        {
            CurrentColor = c;
            if (editingText != null && textBox != null)
            {
                editingText.Color = c;
                StyleTextBox(textBox, c);
            }
            if (selected != null && tool == Tool.Select)
            {
                PushUndo();
                selected.Color = c;
                Changed();
            }
            RaiseChanged();
        }

        public void ApplyLevel(int level)
        {
            CurrentLevel = Math.Max(0, Math.Min(4, level));
            if (editingText != null && textBox != null)
            {
                editingText.Level = CurrentLevel;
                textBox.Font = new Font(TextShape.FontName, Math.Max(6f, editingText.FontSize * zoom), FontStyle.Bold, GraphicsUnit.Pixel);
                ResizeTextBox(textBox);
            }
            if (selected != null && tool == Tool.Select)
            {
                PushUndo();
                selected.Level = CurrentLevel;
                Changed();
            }
            RaiseChanged();
        }

        public void ClearSelection()
        {
            selected = null;
            Invalidate();
            RaiseChanged();
        }

        public void ResetStepNumber()
        {
            NextStep = 1;
            RaiseChanged();
        }

        private void ApplyCrop(Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, BaseImage.Width, BaseImage.Height));
            if (r.Width < 2 || r.Height < 2) { Invalidate(); return; }
            PushUndo();
            BaseImage = ScreenCapture.Crop(BaseImage, r);
            foreach (var s in Shapes) s.Offset(-r.X, -r.Y);
            selected = null;
            Changed();
            FitToWindow();
        }

        /// <summary>把圖片縮放成新的寬高；已畫好的標示會跟著等比例縮放（可以復原）。</summary>
        public void ResizeImage(int newWidth, int newHeight)
        {
            CommitText();
            newWidth = Math.Max(1, newWidth);
            newHeight = Math.Max(1, newHeight);
            if (newWidth == BaseImage.Width && newHeight == BaseImage.Height) return;
            float sx = newWidth / (float)BaseImage.Width, sy = newHeight / (float)BaseImage.Height;

            var bmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var attr = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                attr.SetWrapMode(WrapMode.TileFlipXY); // 避免邊緣出現一圈淡淡的線
                g.DrawImage(BaseImage, new Rectangle(0, 0, newWidth, newHeight), 0, 0, BaseImage.Width, BaseImage.Height, GraphicsUnit.Pixel, attr);
            }

            PushUndo();
            BaseImage = bmp;
            foreach (var s in Shapes) s.Resize(sx, sy);
            selected = null;
            Changed();
            FitToWindow();
        }

        /// <summary>把底圖和所有標示合成一張最終圖片。</summary>
        public Bitmap RenderFinal()
        {
            CommitText();
            var bmp = new Bitmap(BaseImage.Width, BaseImage.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(BaseImage, 0, 0, BaseImage.Width, BaseImage.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                foreach (var s in Shapes) s.Draw(g, BaseImage);
            }
            return bmp;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && cache != null) { cache.Dispose(); cache = null; }
            base.Dispose(disposing);
        }
    }
}
