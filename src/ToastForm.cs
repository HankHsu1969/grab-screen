using System;
using System.Drawing;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>擷取完成後，在右下角跳出的小視窗：「已複製到剪貼簿，要開啟編輯嗎？」</summary>
    internal class ToastForm : Form
    {
        public event EventHandler EditClicked;
        public event EventHandler SaveClicked;

        private readonly Timer timer = new Timer();
        private int secondsLeft;
        private readonly Label countdown;
        private readonly Bitmap thumb;

        public ToastForm(Bitmap image, int seconds, string extraLine)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            Text = "GrabScreen";
            Padding = new Padding(1);

            int W = Dpi.S(330), pad = Dpi.S(14);
            int y = pad;

            var title = new Label
            {
                Text = "✔  已複製到剪貼簿",
                Font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 130, 60),
                AutoSize = false,
                Location = new Point(pad, y),
                Size = new Size(W - pad * 2 - Dpi.S(26), Dpi.S(24))
            };
            Controls.Add(title);

            var close = new Label
            {
                Text = "×",
                Font = new Font("Microsoft JhengHei UI", 13f),
                ForeColor = Color.Gray,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(W - pad - Dpi.S(22), y),
                Size = new Size(Dpi.S(22), Dpi.S(22))
            };
            close.Click += (s, e) => Close();
            Controls.Add(close);
            y += Dpi.S(28);

            string info = image.Width + " × " + image.Height + " 像素";
            if (!string.IsNullOrEmpty(extraLine)) info += "　" + extraLine;
            var sub = new Label
            {
                Text = info,
                ForeColor = Color.DimGray,
                AutoSize = false,
                Location = new Point(pad, y),
                Size = new Size(W - pad * 2, Dpi.S(20))
            };
            Controls.Add(sub);
            y += Dpi.S(24);

            // 縮圖（點一下也可以開啟編輯）
            int maxW = W - pad * 2, maxH = Dpi.S(150);
            float scale = Math.Min(1f, Math.Min(maxW / (float)image.Width, maxH / (float)image.Height));
            int tw = Math.Max(1, (int)(image.Width * scale)), th = Math.Max(1, (int)(image.Height * scale));
            thumb = new Bitmap(tw, th);
            using (var g = Graphics.FromImage(thumb))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, tw, th);
            }
            var pic = new PictureBox
            {
                Image = thumb,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.FromArgb(240, 240, 242),
                Location = new Point(pad, y),
                Size = new Size(maxW, th + Dpi.S(8)),
                Cursor = Cursors.Hand
            };
            pic.Click += (s, e) => RaiseEdit();
            Controls.Add(pic);
            y += pic.Height + Dpi.S(10);

            var ask = new Label
            {
                Text = "要開啟編輯，加上標示嗎？",
                AutoSize = false,
                Location = new Point(pad, y),
                Size = new Size(W - pad * 2, Dpi.S(20))
            };
            Controls.Add(ask);
            y += Dpi.S(26);

            int bh = Dpi.S(32);
            var btnEdit = MakeButton("開啟編輯", true);
            btnEdit.Location = new Point(pad, y);
            btnEdit.Size = new Size(Dpi.S(120), bh);
            btnEdit.Click += (s, e) => RaiseEdit();
            Controls.Add(btnEdit);

            var btnSave = MakeButton("另存新檔", false);
            btnSave.Location = new Point(pad + Dpi.S(128), y);
            btnSave.Size = new Size(Dpi.S(90), bh);
            btnSave.Click += (s, e) =>
            {
                timer.Stop();
                var h = SaveClicked;
                if (h != null) h(this, EventArgs.Empty);
                Close();
            };
            Controls.Add(btnSave);

            var btnNo = MakeButton("不用", false);
            btnNo.Location = new Point(pad + Dpi.S(226), y);
            btnNo.Size = new Size(W - pad * 2 - Dpi.S(226), bh);
            btnNo.Click += (s, e) => Close();
            Controls.Add(btnNo);
            y += bh + Dpi.S(8);

            countdown = new Label
            {
                ForeColor = Color.Gray,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(pad, y),
                Size = new Size(W - pad * 2, Dpi.S(18))
            };
            Controls.Add(countdown);
            y += Dpi.S(18) + pad;

            ClientSize = new Size(W, y);

            secondsLeft = seconds;
            UpdateCountdown();
            timer.Interval = 1000;
            timer.Tick += (s, e) =>
            {
                if (ClientRectangle.Contains(PointToClient(Cursor.Position))) return; // 滑鼠停在上面就暫停倒數
                secondsLeft--;
                UpdateCountdown();
                if (secondsLeft <= 0) Close();
            };
            if (seconds > 0) timer.Start();
        }

        private void UpdateCountdown()
        {
            countdown.Text = secondsLeft > 0 ? secondsLeft + " 秒後自動關閉（滑鼠移上來會暫停）" : "";
        }

        private Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = primary ? Color.FromArgb(0, 120, 215) : Color.FromArgb(242, 242, 244),
                ForeColor = primary ? Color.White : Color.Black
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 214);
            if (primary) b.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
            return b;
        }

        private void RaiseEdit()
        {
            timer.Stop();
            var h = EditClicked;
            if (h != null) h(this, EventArgs.Empty);
            Close();
        }

        /// <summary>顯示在滑鼠所在螢幕的右下角，不搶走目前視窗的焦點。</summary>
        public void ShowAtCorner()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int m = Dpi.S(12);
            Location = new Point(wa.Right - Width - m, wa.Bottom - Height - m);
            Show();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW 陰影
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(200, 200, 205)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            timer.Dispose();
            thumb.Dispose();
            base.OnFormClosed(e);
        }
    }
}
