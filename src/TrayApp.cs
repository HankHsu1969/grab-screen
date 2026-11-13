using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>常駐在右下角系統匣的主程式：管理快捷鍵、選單、擷取流程。</summary>
    internal class TrayApp : ApplicationContext
    {
        private const int ID_FULL = 1, ID_WINDOW = 2, ID_REGION = 3;

        private readonly Settings settings;
        private readonly HotkeyManager hotkeys;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem miFull, miWindow, miRegion;
        private ToastForm toast;
        private bool busy;
        private bool hotkeyDialogOpen;
        private readonly List<Keys> activeKeys = new List<Keys>(); // 目前成功註冊的快捷鍵

        public TrayApp()
        {
            settings = Settings.Load();
            hotkeys = new HotkeyManager();
            hotkeys.HotkeyPressed += id => RunLater(() => Capture(id), 30);

            var menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft JhengHei UI", 9.5f);
            miFull = new ToolStripMenuItem("全螢幕擷取", null, (s, e) => RunLater(() => Capture(ID_FULL), 300));
            miWindow = new ToolStripMenuItem("視窗 / 物件擷取", null, (s, e) => RunLater(() => Capture(ID_WINDOW), 300));
            miRegion = new ToolStripMenuItem("自行框選擷取", null, (s, e) => RunLater(() => Capture(ID_REGION), 300));
            menu.Items.Add(miFull);
            menu.Items.Add(miWindow);
            menu.Items.Add(miRegion);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("開啟圖片來編輯…", null, (s, e) => OpenFileToEdit()));
            menu.Items.Add(new ToolStripMenuItem("編輯剪貼簿裡的圖片", null, (s, e) => EditClipboard()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("快捷鍵設定…", null, (s, e) => ShowHotkeySettings()));
            menu.Items.Add(new ToolStripMenuItem("其他設定…", null, (s, e) => ShowSettings()));
            menu.Items.Add(new ToolStripMenuItem("結束 GrabScreen", null, (s, e) => ExitThread()));

            tray = new NotifyIcon();
            tray.Icon = Icons.AppIcon(SystemInformation.SmallIconSize.Width);
            tray.Text = "GrabScreen 螢幕擷取";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) RunLater(() => Capture(ID_REGION), 150); };

            RegisterHotkeys(true);

            if (settings.FirstRun)
            {
                settings.FirstRun = false;
                settings.Save();
                tray.ShowBalloonTip(8000, "GrabScreen 已在背景執行",
                    Settings.KeysToText(settings.HotkeyFull) + "：全螢幕\n" +
                    Settings.KeysToText(settings.HotkeyWindow) + "：視窗 / 物件\n" +
                    Settings.KeysToText(settings.HotkeyRegion) + "：自行框選\n右鍵點星星圖示 →「快捷鍵設定」可以改按鍵。",
                    ToolTipIcon.Info);
            }
        }

        private void RegisterHotkeys(bool showErrors)
        {
            var failed = new List<string>();
            activeKeys.Clear();
            int[] ids = { ID_FULL, ID_WINDOW, ID_REGION };
            Keys[] keys = { settings.HotkeyFull, settings.HotkeyWindow, settings.HotkeyRegion };
            for (int i = 0; i < 3; i++)
            {
                if (hotkeys.Register(ids[i], keys[i])) { if (keys[i] != Keys.None) activeKeys.Add(keys[i]); }
                else failed.Add(Settings.KeysToText(keys[i]));
            }
            miFull.ShortcutKeyDisplayString = Settings.KeysToText(settings.HotkeyFull);
            miWindow.ShortcutKeyDisplayString = Settings.KeysToText(settings.HotkeyWindow);
            miRegion.ShortcutKeyDisplayString = Settings.KeysToText(settings.HotkeyRegion);
            if (showErrors && failed.Count > 0)
            {
                tray.ShowBalloonTip(8000, "有快捷鍵被其他程式占用",
                    string.Join("、", failed.ToArray()) + " 無法使用，請到「快捷鍵設定」換一組。", ToolTipIcon.Warning);
            }
        }

        /// <summary>稍等一下再執行（讓選單先關掉、按鍵先放開）。</summary>
        private static void RunLater(Action action, int ms)
        {
            var t = new Timer { Interval = Math.Max(1, ms) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                action();
            };
            t.Start();
        }

        private void Capture(int id)
        {
            if (busy) return;
            busy = true;
            try
            {
                if (toast != null && !toast.IsDisposed) toast.Close();
                Bitmap result = null;
                if (id == ID_FULL)
                {
                    Rectangle r = settings.FullScreenAllMonitors ? ScreenCapture.VirtualScreen : ScreenCapture.CurrentMonitor;
                    result = ScreenCapture.CaptureRect(r);
                }
                else
                {
                    var windows = id == ID_WINDOW ? WindowFinder.GetTopLevelWindows() : null;
                    Rectangle vs = ScreenCapture.VirtualScreen;
                    Bitmap shot = ScreenCapture.CaptureRect(vs);
                    using (var overlay = new CaptureOverlay(shot, vs, id == ID_WINDOW ? CaptureMode.Window : CaptureMode.Region, windows))
                    {
                        if (overlay.ShowDialog() == DialogResult.OK) result = overlay.Result;
                    }
                }
                if (result != null) Deliver(result);
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(5000, "擷取失敗", ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                busy = false;
            }
        }

        private void Deliver(Bitmap bmp)
        {
            bool copied = ScreenCapture.CopyToClipboard(bmp);
            string savedNote = null;
            if (settings.AutoSave && !string.IsNullOrEmpty(settings.AutoSaveFolder))
            {
                try
                {
                    Directory.CreateDirectory(settings.AutoSaveFolder);
                    string path = Path.Combine(settings.AutoSaveFolder, ScreenCapture.DefaultFileName());
                    ScreenCapture.SaveImage(bmp, path);
                    savedNote = "已自動存檔";
                }
                catch { savedNote = "自動存檔失敗"; }
            }

            if (!copied)
                tray.ShowBalloonTip(4000, "GrabScreen", "剪貼簿被其他程式占用，沒有複製成功。", ToolTipIcon.Warning);

            if (!settings.ShowPrompt)
            {
                bmp.Dispose();
                return;
            }

            toast = new ToastForm(bmp, settings.PromptSeconds, savedNote);
            toast.EditClicked += (s, e) => OpenEditor((Bitmap)bmp.Clone());
            toast.SaveClicked += (s, e) => ScreenCapture.SaveWithDialog(bmp, null);
            toast.FormClosed += (s, e) => bmp.Dispose();
            toast.ShowAtCorner();
        }

        private void OpenEditor(Bitmap bmp)
        {
            var editor = new EditorForm(bmp);
            editor.Show();
            editor.Activate();
        }

        private void OpenFileToEdit()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "選擇要編輯的圖片";
                dlg.Filter = "圖片檔|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有檔案|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    Bitmap copy;
                    using (var img = Image.FromFile(dlg.FileName)) copy = new Bitmap(img);
                    OpenEditor(copy);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("無法開啟圖片：" + ex.Message, "GrabScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EditClipboard()
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    tray.ShowBalloonTip(3000, "GrabScreen", "剪貼簿裡沒有圖片。", ToolTipIcon.Info);
                    return;
                }
                using (var img = Clipboard.GetImage()) OpenEditor(new Bitmap(img));
            }
            catch (Exception ex)
            {
                tray.ShowBalloonTip(3000, "GrabScreen", "讀取剪貼簿失敗：" + ex.Message, ToolTipIcon.Warning);
            }
        }

        private void ShowSettings()
        {
            using (var f = new SettingsForm(settings, ShowHotkeySettings))
            {
                f.ShowDialog();
            }
        }

        /// <summary>打開「快捷鍵設定」。設定期間先暫停快捷鍵，免得按下去就開始擷取。</summary>
        private void ShowHotkeySettings()
        {
            if (hotkeyDialogOpen) return;
            hotkeyDialogOpen = true;
            for (int id = 1; id <= 3; id++) hotkeys.Unregister(id);
            activeKeys.Clear();
            try
            {
                using (var f = new HotkeyForm(settings, IsHotkeyAvailable))
                {
                    if (f.ShowDialog() == DialogResult.OK)
                        tray.ShowBalloonTip(4000, "快捷鍵已更新",
                            Settings.KeysToText(settings.HotkeyFull) + "：全螢幕\n" +
                            Settings.KeysToText(settings.HotkeyWindow) + "：視窗 / 物件\n" +
                            Settings.KeysToText(settings.HotkeyRegion) + "：自行框選", ToolTipIcon.Info);
                }
            }
            finally
            {
                hotkeyDialogOpen = false;
                RegisterHotkeys(true);
            }
        }

        /// <summary>試著註冊一下看看，能註冊就代表沒有被其他程式占用（試完馬上放掉）。</summary>
        private bool IsHotkeyAvailable(Keys k)
        {
            if (k == Keys.None || activeKeys.Contains(k)) return true;
            bool ok = hotkeys.Register(99, k);
            if (ok) hotkeys.Unregister(99);
            return ok;
        }

        protected override void ExitThreadCore()
        {
            if (toast != null && !toast.IsDisposed) toast.Close();
            tray.Visible = false;
            tray.Dispose();
            hotkeys.Dispose();
            base.ExitThreadCore();
        }
    }
}
