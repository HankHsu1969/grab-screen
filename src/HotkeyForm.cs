using System;
using System.Drawing;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>可以直接按下組合鍵來設定快捷鍵的輸入框。</summary>
    internal class HotkeyBox : TextBox
    {
        private Keys keys;
        public event EventHandler KeysChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
        }

        public Keys Keys
        {
            get { return keys; }
            set
            {
                keys = value;
                Text = Settings.KeysToText(value);
                var h = KeysChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private static string ModText(Keys mods)
        {
            string t = "";
            if ((mods & Keys.Control) != 0) t += "Ctrl + ";
            if ((mods & Keys.Alt) != 0) t += "Alt + ";
            if ((mods & Keys.Shift) != 0) t += "Shift + ";
            return t;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;
            if ((code == Keys.Tab || code == Keys.Escape || code == Keys.Enter) && mods == Keys.None)
                return base.ProcessCmdKey(ref msg, keyData); // 讓 Tab 換欄位、Esc 取消、Enter 儲存
            if ((code == Keys.Back || code == Keys.Delete) && mods == Keys.None) { Keys = Keys.None; return true; }
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu)
            {
                Text = ModText(mods) + "…"; // 先顯示已按住的 Ctrl / Alt / Shift
                return true;
            }
            bool special = (code >= Keys.F1 && code <= Keys.F24) || code == Keys.PrintScreen || code == Keys.Pause || code == Keys.Scroll;
            if (mods == Keys.None && !special)
            {
                Text = "請搭配 Ctrl / Alt / Shift";
                return true;
            }
            Keys = code | mods;
            return true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            // PrtSc 鍵通常只會送出「放開」的訊號，所以在這裡處理
            if (e.KeyCode == Keys.PrintScreen) { Keys = Keys.PrintScreen | e.Modifiers; return; }
            if (e.Modifiers == Keys.None && Text.EndsWith("…")) Text = Settings.KeysToText(keys);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            BackColor = Color.FromArgb(255, 250, 220);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            BackColor = Color.White;
            Text = Settings.KeysToText(keys);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true;
        }
    }

    /// <summary>「快捷鍵設定」視窗：三種擷取模式各自設定一組快捷鍵，並即時檢查能不能用。</summary>
    internal class HotkeyForm : Form
    {
        private readonly Settings settings;
        private readonly Func<Keys, bool> isAvailable;
        private readonly HotkeyBox[] boxes = new HotkeyBox[3];
        private readonly Label[] status = new Label[3];
        private readonly Button btnSave;

        /// <param name="isAvailable">檢查某組按鍵有沒有被其他程式占用（可以是 null）</param>
        public HotkeyForm(Settings s, Func<Keys, bool> isAvailable)
        {
            settings = s;
            this.isAvailable = isAvailable;
            Text = "快捷鍵設定";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            Icon = Icons.AppIcon(Dpi.S(32));
            TopMost = true;

            int pad = Dpi.S(18), lw = Dpi.S(130), bw = Dpi.S(200), sw = Dpi.S(170), rowH = Dpi.S(40);
            int W = pad * 2 + lw + bw + Dpi.S(12) + sw;
            int y = pad;

            var logo = new PictureBox { Image = Icons.AppBitmap(Dpi.S(40)), SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(pad, y) };
            Controls.Add(logo);
            Controls.Add(new Label
            {
                Text = "設定你習慣的快捷鍵",
                Font = new Font("Microsoft JhengHei UI", 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(pad + Dpi.S(50), y)
            });
            Controls.Add(new Label
            {
                Text = "點一下輸入框，直接按下想要的組合鍵（例如 Ctrl + Shift + A）。\n按 Delete 或 Backspace 可以清除。",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(pad + Dpi.S(50), y + Dpi.S(26))
            });
            y += Dpi.S(76);

            string[] names = { "全螢幕擷取", "視窗 / 物件擷取", "自行框選擷取" };
            Keys[] values = { s.HotkeyFull, s.HotkeyWindow, s.HotkeyRegion };
            for (int i = 0; i < 3; i++)
            {
                Controls.Add(new Label { Text = (i + 1) + ".  " + names[i], AutoSize = true, Location = new Point(pad, y + Dpi.S(5)) });
                var box = new HotkeyBox { Location = new Point(pad + lw, y), Width = bw, Font = new Font("Microsoft JhengHei UI", 10.5f) };
                box.Keys = values[i];
                box.KeysChanged += (o, e) => UpdateStatus();
                boxes[i] = box;
                Controls.Add(box);
                var st = new Label { AutoSize = false, Size = new Size(sw, Dpi.S(26)), Location = new Point(pad + lw + bw + Dpi.S(12), y + Dpi.S(4)) };
                status[i] = st;
                Controls.Add(st);
                y += rowH;
            }
            y += Dpi.S(4);

            Controls.Add(new Label
            {
                Text = "小提示：想用 PrtSc 鍵的話，要先到 Windows「設定 → 協助工具 → 鍵盤」，\n關閉「使用 Print Screen 鍵開啟螢幕擷取」，否則會被 Windows 內建的截圖搶走。",
                ForeColor = Color.Gray,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Location = new Point(pad, y)
            });
            y += Dpi.S(46);

            var btnDefault = new Button { Text = "恢復預設值", Size = new Size(Dpi.S(110), Dpi.S(32)), Location = new Point(pad, y) };
            btnDefault.Click += (o, e) =>
            {
                var d = new Settings();
                boxes[0].Keys = d.HotkeyFull;
                boxes[1].Keys = d.HotkeyWindow;
                boxes[2].Keys = d.HotkeyRegion;
            };
            btnSave = new Button { Text = "儲存", Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(188), y) };
            var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(90), y) };
            btnSave.Click += OnSave;
            Controls.Add(btnDefault);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
            y += Dpi.S(32) + pad;
            ClientSize = new Size(W, y);
            UpdateStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            boxes[0].Focus();
        }

        private void UpdateStatus()
        {
            bool allOk = true;
            for (int i = 0; i < 3; i++)
            {
                if (boxes[i] == null || status[i] == null) return;
                Keys k = boxes[i].Keys;
                string text; Color color;
                bool dup = false;
                for (int j = 0; j < 3; j++)
                    if (j != i && boxes[j] != null && k != Keys.None && boxes[j].Keys == k) dup = true;
                if (k == Keys.None) { text = "未設定（只能從選單使用）"; color = Color.Gray; }
                else if (dup) { text = "✖ 跟另一組重複了"; color = Color.FromArgb(210, 50, 40); allOk = false; }
                else if (isAvailable != null && !isAvailable(k)) { text = "⚠ 已被其他程式使用"; color = Color.FromArgb(220, 120, 0); allOk = false; }
                else { text = "✔ 可以使用"; color = Color.FromArgb(30, 140, 60); }
                status[i].Text = text;
                status[i].ForeColor = color;
            }
            if (btnSave != null) btnSave.Enabled = allOk;
        }

        private void OnSave(object sender, EventArgs e)
        {
            settings.HotkeyFull = boxes[0].Keys;
            settings.HotkeyWindow = boxes[1].Keys;
            settings.HotkeyRegion = boxes[2].Keys;
            settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
