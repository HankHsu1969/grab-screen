using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>簡易影像編輯器：上方工具列＋中間畫布＋下方狀態列。</summary>
    internal class EditorForm : Form
    {
        private readonly EditorCanvas canvas;
        private readonly Panel host;
        private readonly ToolStrip bar1, bar2;
        private readonly StatusStrip status;
        private readonly ToolStripStatusLabel lblHint, lblInfo;
        private readonly Dictionary<Tool, ToolStripButton> toolButtons = new Dictionary<Tool, ToolStripButton>();
        private readonly List<ToolStripButton> colorButtons = new List<ToolStripButton>();
        private ToolStripButton btnUndo, btnRedo, btnDelete, btnCustomColor, btnStep;
        private ToolStripDropDownButton ddSize;
        private ToolStripLabel lblZoom;
        private readonly Timer flashTimer = new Timer { Interval = 2500 };

        private static readonly Color[] Palette =
        {
            Color.FromArgb(229, 57, 53),   // 紅
            Color.FromArgb(251, 140, 0),   // 橘
            Color.FromArgb(253, 216, 53),  // 黃
            Color.FromArgb(67, 160, 71),   // 綠
            Color.FromArgb(30, 136, 229),  // 藍
            Color.FromArgb(142, 36, 170),  // 紫
            Color.FromArgb(33, 33, 33),    // 黑
            Color.White                    // 白
        };
        private static readonly string[] PaletteNames = { "紅色", "橘色", "黃色", "綠色", "藍色", "紫色", "黑色", "白色" };

        private static readonly Dictionary<Tool, string> Hints = new Dictionary<Tool, string>
        {
            { Tool.Select, "點選標示可以移動；拖曳白色控制點調整大小；Delete 刪除；雙擊文字可以修改；方向鍵微調位置" },
            { Tool.Rect, "拖曳畫出方框（按住 Shift 畫正方形）" },
            { Tool.Ellipse, "拖曳畫出圓圈（按住 Shift 畫正圓）" },
            { Tool.Arrow, "從起點拖曳到要指的位置（按住 Shift 鎖定 45° 角度）" },
            { Tool.Line, "拖曳畫出直線（按住 Shift 鎖定 45° 角度）" },
            { Tool.Pen, "按住滑鼠自由畫線" },
            { Tool.Highlighter, "像螢光筆一樣畫過要強調的文字（按住 Shift 畫直線）" },
            { Tool.Text, "在圖上點一下開始打字：Enter 完成、Shift+Enter 換行、Esc 取消" },
            { Tool.Step, "點一下放上編號圓圈 ①②③，數字會自動往上加" },
            { Tool.Mosaic, "拖曳框出要打馬賽克的區域（遮住帳號、電話等個資）" },
            { Tool.Crop, "拖曳框出要保留的範圍，放開就會裁切（Ctrl+Z 可以復原）" }
        };

        public EditorForm(Bitmap image)
        {
            Text = "GrabScreen 編輯器";
            Icon = Icons.AppIcon(Dpi.S(32));
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft JhengHei UI", 9f);
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;

            canvas = new EditorCanvas(image);
            host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = canvas.BackColor };
            host.Controls.Add(canvas);
            host.Resize += (s, e) => canvas.UpdateCanvasSize();

            bar1 = MakeBar();
            bar2 = MakeBar();
            BuildBar1();
            BuildBar2();

            status = new StatusStrip { Font = Font, SizingGrip = true };
            lblHint = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            lblInfo = new ToolStripStatusLabel { TextAlign = ContentAlignment.MiddleRight };
            status.Items.Add(lblHint);
            status.Items.Add(lblInfo);

            // 停靠順序：最後加入的會排在最上面
            Controls.Add(host);
            Controls.Add(status);
            Controls.Add(bar2);
            Controls.Add(bar1);

            canvas.StateChanged += (s, e) => UpdateUi();
            flashTimer.Tick += (s, e) => { flashTimer.Stop(); UpdateUi(); };

            // 視窗大小：剛好放得下圖片，但不超過螢幕的 90%
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int w = Math.Min((int)(wa.Width * 0.9), Math.Max(Dpi.S(960), image.Width + Dpi.S(80)));
            int h = Math.Min((int)(wa.Height * 0.9), Math.Max(Dpi.S(640), image.Height + Dpi.S(200)));
            Bounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
            MinimumSize = new Size(Dpi.S(720), Dpi.S(420));

            canvas.Tool = Tool.Rect;
            UpdateUi();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            canvas.FitToWindow();
            canvas.Focus();
            Activate();
        }

        // ============================================================ 工具列

        private ToolStrip MakeBar()
        {
            var b = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                ImageScalingSize = new Size(Dpi.S(22), Dpi.S(22)),
                Padding = new Padding(Dpi.S(6), Dpi.S(2), Dpi.S(6), Dpi.S(2)),
                Font = new Font("Microsoft JhengHei UI", 9f),
                CanOverflow = true,
                Stretch = true
            };
            b.Renderer = new ToolStripProfessionalRenderer(new SoftColors()) { RoundedEdges = false };
            return b;
        }

        private void BuildBar1()
        {
            AddTool(Tool.Select, "選取", "select", "V");
            bar1.Items.Add(new ToolStripSeparator());
            AddTool(Tool.Rect, "方框", "rect", "R");
            AddTool(Tool.Ellipse, "圓圈", "ellipse", "E");
            AddTool(Tool.Arrow, "箭頭", "arrow", "A");
            AddTool(Tool.Line, "直線", "line", "L");
            AddTool(Tool.Pen, "畫筆", "pen", "P");
            AddTool(Tool.Highlighter, "螢光筆", "highlighter", "H");
            AddTool(Tool.Text, "文字", "text", "T");
            AddTool(Tool.Step, "編號", "step", "N");
            AddTool(Tool.Mosaic, "馬賽克", "mosaic", "M");
            AddTool(Tool.Crop, "裁切", "crop", "C");
            AddAction(bar1, "尺寸", "resize", "調整圖片尺寸 (Ctrl+R)", (s, e) => OpenResize());
            bar1.Items.Add(new ToolStripSeparator());
            btnUndo = AddAction(bar1, "復原", "undo", "復原 (Ctrl+Z)", (s, e) => canvas.Undo());
            btnRedo = AddAction(bar1, "重做", "redo", "重做 (Ctrl+Y)", (s, e) => canvas.Redo());

            var done = AddAction(bar1, "完成", "done", "複製到剪貼簿並關閉視窗", (s, e) => DoneAndClose());
            var save = AddAction(bar1, "另存", "save", "另存新檔 (Ctrl+S)", (s, e) => SaveAs());
            var copy = AddAction(bar1, "複製", "copy", "複製到剪貼簿 (Ctrl+C)", (s, e) => CopyResult());
            done.Alignment = save.Alignment = copy.Alignment = ToolStripItemAlignment.Right;
            done.Font = new Font(bar1.Font, FontStyle.Bold);
        }

        private void AddTool(Tool t, string text, string icon, string key)
        {
            var b = new ToolStripButton(text, Icons.Tool(icon))
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ToolTipText = text + " (" + key + ")",
                Padding = new Padding(Dpi.S(3), 0, Dpi.S(3), 0)
            };
            b.Click += (s, e) => canvas.Tool = t;
            toolButtons[t] = b;
            bar1.Items.Add(b);
        }

        private ToolStripButton AddAction(ToolStrip bar, string text, string icon, string tip, EventHandler click)
        {
            var b = new ToolStripButton(text, Icons.Tool(icon))
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ToolTipText = tip,
                Padding = new Padding(Dpi.S(3), 0, Dpi.S(3), 0)
            };
            b.Click += click;
            bar.Items.Add(b);
            return b;
        }

        private void BuildBar2()
        {
            bar2.ImageScalingSize = new Size(Dpi.S(18), Dpi.S(18));
            bar2.Items.Add(new ToolStripLabel("顏色："));
            for (int i = 0; i < Palette.Length; i++)
            {
                Color c = Palette[i];
                var b = new ToolStripButton(Icons.ColorSwatch(c, 32))
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    ToolTipText = PaletteNames[i] + " (Shift+" + (i + 1) + ")",
                    Tag = c
                };
                b.Click += (s, e) => canvas.ApplyColor(c);
                colorButtons.Add(b);
                bar2.Items.Add(b);
            }
            btnCustomColor = new ToolStripButton("其他…") { ToolTipText = "自訂顏色" };
            btnCustomColor.Click += (s, e) => PickCustomColor();
            bar2.Items.Add(btnCustomColor);
            bar2.Items.Add(new ToolStripSeparator());

            ddSize = new ToolStripDropDownButton { ToolTipText = "線條粗細 / 文字大小 / 編號大小（數字鍵 1~5）" };
            string[] names = { "1　最細 / 最小", "2", "3　預設", "4", "5　最粗 / 最大" };
            for (int i = 0; i < 5; i++)
            {
                int lv = i;
                var item = new ToolStripMenuItem(names[i], LineSample(Shape.StrokeSizes[i]));
                item.Click += (s, e) => canvas.ApplyLevel(lv);
                ddSize.DropDownItems.Add(item);
            }
            bar2.Items.Add(ddSize);
            bar2.Items.Add(new ToolStripSeparator());

            btnStep = new ToolStripButton { ToolTipText = "點一下把編號重新從 1 開始" };
            btnStep.Click += (s, e) => canvas.ResetStepNumber();
            bar2.Items.Add(btnStep);
            btnDelete = new ToolStripButton("刪除", Icons.Tool("delete")) { ToolTipText = "刪除選取的標示 (Delete)", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnDelete.Click += (s, e) => canvas.DeleteSelected();
            bar2.Items.Add(btnDelete);

            var zoomFit = new ToolStripButton("符合視窗") { ToolTipText = "Ctrl+0" };
            zoomFit.Click += (s, e) => canvas.FitToWindow();
            var zoom100 = new ToolStripButton("實際大小") { ToolTipText = "Ctrl+1" };
            zoom100.Click += (s, e) => canvas.SetZoom(1f, null);
            var zoomIn = new ToolStripButton("＋") { ToolTipText = "放大（Ctrl+滾輪）" };
            zoomIn.Click += (s, e) => canvas.SetZoom(canvas.Zoom * 1.25f, null);
            lblZoom = new ToolStripLabel("100%") { AutoSize = false, Width = Dpi.S(52), TextAlign = ContentAlignment.MiddleCenter };
            var zoomOut = new ToolStripButton("－") { ToolTipText = "縮小（Ctrl+滾輪）" };
            zoomOut.Click += (s, e) => canvas.SetZoom(canvas.Zoom / 1.25f, null);
            foreach (var it in new ToolStripItem[] { zoomFit, zoom100, zoomIn, lblZoom, zoomOut })
            {
                it.Alignment = ToolStripItemAlignment.Right;
                bar2.Items.Add(it);
            }
        }

        private static Bitmap LineSample(float width)
        {
            var bmp = new Bitmap(48, 24);
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.FromArgb(50, 50, 55), Math.Min(14f, width)))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(pen, 8, 12, 40, 12);
            }
            return bmp;
        }

        private void OpenResize()
        {
            canvas.CommitText();
            using (var dlg = new ResizeDialog(canvas.BaseImage.Width, canvas.BaseImage.Height))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                canvas.ResizeImage(dlg.NewWidth, dlg.NewHeight);
                Flash("✔ 已調整為 " + dlg.NewWidth + " × " + dlg.NewHeight + " 像素（Ctrl+Z 可以復原）");
            }
        }

        private void PickCustomColor()
        {
            using (var dlg = new ColorDialog { Color = canvas.CurrentColor, FullOpen = true })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) canvas.ApplyColor(dlg.Color);
            }
        }

        // ============================================================ 畫面狀態

        private void UpdateUi()
        {
            foreach (var kv in toolButtons) kv.Value.Checked = canvas.Tool == kv.Key;
            bool matched = false;
            foreach (var b in colorButtons)
            {
                bool on = ((Color)b.Tag).ToArgb() == canvas.CurrentColor.ToArgb();
                b.Checked = on;
                matched |= on;
            }
            if (btnCustomColor.Image != null) { var old = btnCustomColor.Image; btnCustomColor.Image = null; old.Dispose(); }
            if (!matched)
            {
                btnCustomColor.Image = Icons.ColorSwatch(canvas.CurrentColor, 32);
                btnCustomColor.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }
            btnCustomColor.Checked = !matched;

            ddSize.Text = "粗細 / 大小：" + (canvas.CurrentLevel + 1);
            for (int i = 0; i < ddSize.DropDownItems.Count; i++)
                ((ToolStripMenuItem)ddSize.DropDownItems[i]).Checked = i == canvas.CurrentLevel;
            btnStep.Text = "下一個編號：" + canvas.NextStep;
            btnUndo.Enabled = canvas.CanUndo;
            btnRedo.Enabled = canvas.CanRedo;
            btnDelete.Enabled = canvas.Selected != null;
            lblZoom.Text = (int)Math.Round(canvas.Zoom * 100) + "%";
            if (!flashTimer.Enabled)
            {
                string hint;
                lblHint.Text = Hints.TryGetValue(canvas.Tool, out hint) ? hint : "";
                lblHint.ForeColor = SystemColors.ControlText;
            }
            lblInfo.Text = canvas.BaseImage.Width + " × " + canvas.BaseImage.Height + " 像素";
            Text = "GrabScreen 編輯器" + (canvas.Dirty ? "  ●" : "");
        }

        private void Flash(string message)
        {
            lblHint.Text = message;
            lblHint.ForeColor = Color.FromArgb(30, 130, 60);
            flashTimer.Stop();
            flashTimer.Start();
        }

        // ============================================================ 複製、存檔、關閉

        private bool CopyResult()
        {
            using (var bmp = canvas.RenderFinal())
            {
                if (ScreenCapture.CopyToClipboard(bmp))
                {
                    canvas.Dirty = false;
                    Flash("✔ 已複製到剪貼簿，可以直接貼到 LINE、Word、簡報…");
                    UpdateUi();
                    return true;
                }
            }
            MessageBox.Show(this, "剪貼簿被其他程式占用，請再試一次。", "GrabScreen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void SaveAs()
        {
            using (var bmp = canvas.RenderFinal())
            {
                string path = ScreenCapture.SaveWithDialog(bmp, this);
                if (path != null)
                {
                    canvas.Dirty = false;
                    Flash("✔ 已儲存：" + path);
                    UpdateUi();
                }
            }
        }

        private void DoneAndClose()
        {
            if (CopyResult()) Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            canvas.CommitText();
            if (canvas.Dirty && e.CloseReason == CloseReason.UserClosing)
            {
                var r = MessageBox.Show(this, "圖片有修改，但還沒複製或存檔。\n\n要先把結果複製到剪貼簿再關閉嗎？",
                    "GrabScreen", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes && !CopyResult()) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            flashTimer.Dispose();
            base.OnFormClosed(e);
        }

        // ============================================================ 鍵盤快捷鍵

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (canvas.IsEditingText) return base.ProcessCmdKey(ref msg, keyData);
            Keys code = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0, shift = (keyData & Keys.Shift) != 0, alt = (keyData & Keys.Alt) != 0;
            if (alt) return base.ProcessCmdKey(ref msg, keyData);

            if (ctrl)
            {
                switch (code)
                {
                    case Keys.Z: if (shift) canvas.Redo(); else canvas.Undo(); return true;
                    case Keys.Y: canvas.Redo(); return true;
                    case Keys.C: CopyResult(); return true;
                    case Keys.S: SaveAs(); return true;
                    case Keys.R: OpenResize(); return true;
                    case Keys.D0: case Keys.NumPad0: canvas.FitToWindow(); return true;
                    case Keys.D1: case Keys.NumPad1: canvas.SetZoom(1f, null); return true;
                    case Keys.Oemplus: case Keys.Add: canvas.SetZoom(canvas.Zoom * 1.25f, null); return true;
                    case Keys.OemMinus: case Keys.Subtract: canvas.SetZoom(canvas.Zoom / 1.25f, null); return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            if (shift && code >= Keys.D1 && code <= Keys.D8)
            {
                canvas.ApplyColor(Palette[code - Keys.D1]);
                return true;
            }

            switch (code)
            {
                case Keys.V: canvas.Tool = Tool.Select; return true;
                case Keys.R: canvas.Tool = Tool.Rect; return true;
                case Keys.E: canvas.Tool = Tool.Ellipse; return true;
                case Keys.A: canvas.Tool = Tool.Arrow; return true;
                case Keys.L: canvas.Tool = Tool.Line; return true;
                case Keys.P: canvas.Tool = Tool.Pen; return true;
                case Keys.H: canvas.Tool = Tool.Highlighter; return true;
                case Keys.T: canvas.Tool = Tool.Text; return true;
                case Keys.N: canvas.Tool = Tool.Step; return true;
                case Keys.M: canvas.Tool = Tool.Mosaic; return true;
                case Keys.C: canvas.Tool = Tool.Crop; return true;
                case Keys.Delete:
                case Keys.Back: canvas.DeleteSelected(); return true;
                case Keys.D1: case Keys.D2: case Keys.D3: case Keys.D4: case Keys.D5:
                    canvas.ApplyLevel(code - Keys.D1); return true;
                case Keys.Escape:
                    canvas.ClearSelection();
                    return true;
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                    if (canvas.Selected == null) break;
                    float step = shift ? 10f : 1f;
                    float dx = code == Keys.Left ? -step : (code == Keys.Right ? step : 0);
                    float dy = code == Keys.Up ? -step : (code == Keys.Down ? step : 0);
                    canvas.NudgeSelected(dx, dy);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>工具列配色：淡灰底、藍色選取，比預設樣式清爽。</summary>
        private class SoftColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Color.FromArgb(248, 248, 250); } }
            public override Color ToolStripGradientMiddle { get { return Color.FromArgb(248, 248, 250); } }
            public override Color ToolStripGradientEnd { get { return Color.FromArgb(243, 243, 246); } }
            public override Color ToolStripBorder { get { return Color.FromArgb(225, 225, 230); } }
            public override Color ButtonSelectedHighlight { get { return Color.FromArgb(225, 238, 252); } }
            public override Color ButtonSelectedGradientBegin { get { return Color.FromArgb(225, 238, 252); } }
            public override Color ButtonSelectedGradientMiddle { get { return Color.FromArgb(225, 238, 252); } }
            public override Color ButtonSelectedGradientEnd { get { return Color.FromArgb(225, 238, 252); } }
            public override Color ButtonSelectedBorder { get { return Color.FromArgb(120, 175, 235); } }
            public override Color ButtonCheckedGradientBegin { get { return Color.FromArgb(204, 228, 250); } }
            public override Color ButtonCheckedGradientMiddle { get { return Color.FromArgb(204, 228, 250); } }
            public override Color ButtonCheckedGradientEnd { get { return Color.FromArgb(204, 228, 250); } }
            public override Color ButtonCheckedHighlight { get { return Color.FromArgb(204, 228, 250); } }
            public override Color ButtonPressedGradientBegin { get { return Color.FromArgb(180, 212, 245); } }
            public override Color ButtonPressedGradientMiddle { get { return Color.FromArgb(180, 212, 245); } }
            public override Color ButtonPressedGradientEnd { get { return Color.FromArgb(180, 212, 245); } }
            public override Color SeparatorDark { get { return Color.FromArgb(215, 215, 220); } }
            public override Color SeparatorLight { get { return Color.FromArgb(250, 250, 252); } }
        }
    }
}
