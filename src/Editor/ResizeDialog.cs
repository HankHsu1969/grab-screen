using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>「調整尺寸」視窗：可以用百分比或像素設定新的寬高。</summary>
    internal class ResizeDialog : Form
    {
        public int NewWidth { get; private set; }
        public int NewHeight { get; private set; }

        private const int MaxSide = 20000;
        private const long MaxPixels = 150000000L;

        private readonly int origW, origH;
        private readonly TextBox tbPct, tbW, tbH;
        private readonly CheckBox chkKeep;
        private readonly Label lblResult, lblNote;
        private readonly Button btnOk;
        private bool updating;

        public ResizeDialog(int width, int height)
        {
            origW = width;
            origH = height;
            NewWidth = width;
            NewHeight = height;

            Text = "調整圖片尺寸";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            Icon = Icons.AppIcon(Dpi.S(32));

            int pad = Dpi.S(16), lx = pad, fx = pad + Dpi.S(90), fw = Dpi.S(90), rowH = Dpi.S(34);
            int y = pad;

            Controls.Add(new Label
            {
                Text = "目前尺寸：" + width + " × " + height + " 像素",
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Location = new Point(lx, y)
            });
            y += Dpi.S(32);

            tbPct = AddRow("百分比", "%", lx, fx, fw, ref y, rowH, true);
            tbW = AddRow("寬度", "像素", lx, fx, fw, ref y, rowH, false);
            tbH = AddRow("高度", "像素", lx, fx, fw, ref y, rowH, false);

            chkKeep = new CheckBox
            {
                Text = "維持長寬比例（改寬或高，另一邊自動跟著變）",
                Checked = true,
                AutoSize = true,
                Location = new Point(lx, y)
            };
            Controls.Add(chkKeep);
            y += Dpi.S(36);

            Controls.Add(new Label { Text = "快速選擇：", AutoSize = true, Location = new Point(lx, y + Dpi.S(5)) });
            int bx = fx;
            foreach (int p in new[] { 25, 50, 75, 150, 200 })
            {
                int pct = p;
                var b = SmallButton(p + "%", bx, y);
                b.Click += (s, e) => { chkKeep.Checked = true; tbPct.Text = pct.ToString(); };
                bx += b.Width + Dpi.S(6);
            }
            y += rowH;

            Controls.Add(new Label { Text = "常用寬度：", AutoSize = true, Location = new Point(lx, y + Dpi.S(5)) });
            bx = fx;
            foreach (int w in new[] { 640, 800, 1280, 1920 })
            {
                int target = w;
                var b = SmallButton(w.ToString(), bx, y);
                b.Click += (s, e) => { chkKeep.Checked = true; tbW.Text = target.ToString(); };
                bx += b.Width + Dpi.S(6);
            }
            y += rowH + Dpi.S(6);

            lblResult = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 190),
                Location = new Point(lx, y)
            };
            Controls.Add(lblResult);
            y += Dpi.S(28);

            lblNote = new Label
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(lx, y)
            };
            Controls.Add(lblNote);
            y += Dpi.S(30);

            int W = Math.Max(bx + pad, Dpi.S(440));
            btnOk = new Button { Text = "確定", Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(188), y) };
            var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(90), y) };
            btnOk.Click += (s, e) =>
            {
                int nw, nh;
                if (!TryGetSize(out nw, out nh)) return;
                NewWidth = nw;
                NewHeight = nh;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
            y += Dpi.S(32) + pad;
            ClientSize = new Size(W, y);

            tbPct.TextChanged += (s, e) => OnPercentChanged();
            tbW.TextChanged += (s, e) => OnWidthChanged();
            tbH.TextChanged += (s, e) => OnHeightChanged();
            chkKeep.CheckedChanged += (s, e) => { if (chkKeep.Checked) OnWidthChanged(); };

            updating = true;
            tbPct.Text = "100";
            tbW.Text = width.ToString();
            tbH.Text = height.ToString();
            updating = false;
            UpdatePreview();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            tbPct.Focus();
            tbPct.SelectAll();
        }

        private TextBox AddRow(string label, string unit, int lx, int fx, int fw, ref int y, int rowH, bool allowDot)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(lx, y + Dpi.S(4)) });
            var tb = new TextBox { Location = new Point(fx, y), Width = fw, TextAlign = HorizontalAlignment.Right };
            tb.KeyPress += (s, e) =>
            {
                // 只允許輸入數字（百分比可以有小數點）
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
                if (allowDot && e.KeyChar == '.' && !tb.Text.Contains(".")) return;
                e.Handled = true;
            };
            tb.Enter += (s, e) => tb.SelectAll();
            Controls.Add(tb);
            Controls.Add(new Label { Text = unit, AutoSize = true, Location = new Point(fx + fw + Dpi.S(6), y + Dpi.S(4)) });
            y += rowH;
            return tb;
        }

        private Button SmallButton(string text, int x, int y)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(Dpi.S(text.Length > 3 ? 58 : 52), Dpi.S(28)),
                FlatStyle = FlatStyle.System
            };
            Controls.Add(b);
            return b;
        }

        private static int Clamp(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 1;
            return (int)Math.Max(1, Math.Min(MaxSide, Math.Round(v)));
        }

        private static string FormatPct(double p)
        {
            return p.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void OnPercentChanged()
        {
            if (updating) return;
            double p;
            if (double.TryParse(tbPct.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out p) && p > 0)
            {
                updating = true;
                tbW.Text = Clamp(origW * p / 100.0).ToString();
                tbH.Text = Clamp(origH * p / 100.0).ToString();
                updating = false;
            }
            UpdatePreview();
        }

        private void OnWidthChanged()
        {
            if (updating) return;
            int w;
            if (int.TryParse(tbW.Text, out w) && w > 0)
            {
                updating = true;
                if (chkKeep.Checked) tbH.Text = Clamp(origH * (double)w / origW).ToString();
                tbPct.Text = FormatPct(w * 100.0 / origW);
                updating = false;
            }
            UpdatePreview();
        }

        private void OnHeightChanged()
        {
            if (updating) return;
            int h;
            if (int.TryParse(tbH.Text, out h) && h > 0)
            {
                updating = true;
                if (chkKeep.Checked)
                {
                    int w = Clamp(origW * (double)h / origH);
                    tbW.Text = w.ToString();
                    tbPct.Text = FormatPct(w * 100.0 / origW);
                }
                else tbPct.Text = FormatPct(h * 100.0 / origH);
                updating = false;
            }
            UpdatePreview();
        }

        private bool TryGetSize(out int w, out int h)
        {
            h = 0;
            if (!int.TryParse(tbW.Text, out w) || !int.TryParse(tbH.Text, out h)) return false;
            if (w < 1 || h < 1 || w > MaxSide || h > MaxSide) return false;
            return (long)w * h <= MaxPixels;
        }

        private void UpdatePreview()
        {
            int w, h;
            bool ok = TryGetSize(out w, out h);
            btnOk.Enabled = ok;
            if (!ok)
            {
                lblResult.Text = "新尺寸：—";
                lblResult.ForeColor = Color.FromArgb(200, 60, 40);
                lblNote.Text = "請輸入 1 ～ " + MaxSide + " 之間的數字（太大的圖會讓電腦吃不消）";
                return;
            }
            lblResult.ForeColor = Color.FromArgb(0, 100, 190);
            lblResult.Text = "新尺寸：" + w + " × " + h + " 像素";
            if (w > origW || h > origH)
                lblNote.Text = "提示：放大會讓圖片變得比較模糊，這是正常的。";
            else if (w == origW && h == origH)
                lblNote.Text = "尺寸沒有改變。";
            else
                lblNote.Text = "畫好的標示會跟著一起縮放，之後還可以繼續編輯。";
        }
    }
}
