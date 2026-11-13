using System;
using System.Drawing;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>「其他設定」視窗：擷取選項、自動存檔、開機啟動；快捷鍵另外用 HotkeyForm 設定。</summary>
    internal class SettingsForm : Form
    {
        private readonly Settings settings;
        private readonly Action openHotkeys;
        private readonly Label lblHotkeys;
        private readonly CheckBox chkAllMonitors, chkPrompt, chkAutoSave, chkStartup;
        private readonly TextBox txtFolder;
        private readonly NumericUpDown numSeconds;

        /// <param name="openHotkeys">按下「變更快捷鍵…」時要做的事（打開快捷鍵設定視窗）</param>
        public SettingsForm(Settings s, Action openHotkeys)
        {
            settings = s;
            this.openHotkeys = openHotkeys;
            Text = "GrabScreen 設定";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            Icon = Icons.AppIcon(Dpi.S(32));

            int pad = Dpi.S(16), rowH = Dpi.S(34);
            int W = pad * 2 + Dpi.S(440);
            int y = pad;

            Controls.Add(Header("快捷鍵", pad, ref y));
            lblHotkeys = new Label { AutoSize = true, Location = new Point(pad + Dpi.S(18), y) };
            Controls.Add(lblHotkeys);
            var btnHotkeys = new Button { Text = "變更快捷鍵…", Size = new Size(Dpi.S(120), Dpi.S(30)), Location = new Point(W - pad - Dpi.S(120), y) };
            btnHotkeys.Click += (o, e) =>
            {
                if (openHotkeys != null) openHotkeys();
                RefreshHotkeyText();
            };
            Controls.Add(btnHotkeys);
            RefreshHotkeyText();
            y += Dpi.S(72);

            Controls.Add(Header("擷取選項", pad, ref y));
            chkAllMonitors = AddCheck("全螢幕擷取時，包含所有螢幕（未勾選＝只抓滑鼠所在的螢幕）", s.FullScreenAllMonitors, pad, ref y);
            chkPrompt = AddCheck("擷取後跳出提示視窗，詢問是否開啟編輯", s.ShowPrompt, pad, ref y);

            Controls.Add(new Label { Text = "提示視窗自動關閉秒數（0 = 不自動關閉）：", AutoSize = true, Location = new Point(pad + Dpi.S(18), y + Dpi.S(3)) });
            numSeconds = new NumericUpDown { Minimum = 0, Maximum = 60, Value = s.PromptSeconds, Location = new Point(pad + Dpi.S(300), y), Width = Dpi.S(60) };
            Controls.Add(numSeconds);
            y += rowH;

            chkAutoSave = AddCheck("擷取後同時自動存檔到資料夾：", s.AutoSave, pad, ref y);
            txtFolder = new TextBox { Text = s.AutoSaveFolder, Location = new Point(pad + Dpi.S(18), y), Width = Dpi.S(330) };
            Controls.Add(txtFolder);
            var btnBrowse = new Button { Text = "選擇…", Location = new Point(pad + Dpi.S(356), y - Dpi.S(1)), Size = new Size(Dpi.S(70), Dpi.S(27)) };
            btnBrowse.Click += (o, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "選擇自動存檔的資料夾";
                    dlg.SelectedPath = txtFolder.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
                }
            };
            Controls.Add(btnBrowse);
            y += rowH + Dpi.S(6);

            Controls.Add(Header("其他", pad, ref y));
            chkStartup = AddCheck("開機時自動啟動 GrabScreen", Settings.StartWithWindows, pad, ref y);
            y += Dpi.S(10);

            var ok = new Button { Text = "儲存", Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(188), y) };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(Dpi.S(90), Dpi.S(32)), Location = new Point(W - pad - Dpi.S(90), y) };
            ok.Click += OnOk;
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            y += Dpi.S(32) + pad;

            ClientSize = new Size(W, y);
        }

        private void RefreshHotkeyText()
        {
            lblHotkeys.Text =
                "全螢幕　　　" + Settings.KeysToText(settings.HotkeyFull) + "\n" +
                "視窗 / 物件　" + Settings.KeysToText(settings.HotkeyWindow) + "\n" +
                "自行框選　　" + Settings.KeysToText(settings.HotkeyRegion);
        }

        private Label Header(string text, int x, ref int y)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 90, 170),
                Location = new Point(x, y)
            };
            y += Dpi.S(28);
            return l;
        }

        private CheckBox AddCheck(string text, bool value, int x, ref int y)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Location = new Point(x + Dpi.S(14), y) };
            Controls.Add(c);
            y += Dpi.S(30);
            return c;
        }

        private void OnOk(object sender, EventArgs e)
        {
            settings.FullScreenAllMonitors = chkAllMonitors.Checked;
            settings.ShowPrompt = chkPrompt.Checked;
            settings.PromptSeconds = (int)numSeconds.Value;
            settings.AutoSave = chkAutoSave.Checked;
            settings.AutoSaveFolder = txtFolder.Text.Trim();
            settings.Save();
            if (chkStartup.Checked != Settings.StartWithWindows) Settings.StartWithWindows = chkStartup.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
