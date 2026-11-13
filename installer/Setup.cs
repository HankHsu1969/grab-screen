using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using GrabScreen;

[assembly: AssemblyTitle("GrabScreen 安裝程式")]
[assembly: AssemblyDescription("GrabScreen 螢幕擷取 安裝程式")]
[assembly: AssemblyCompany("星方向")]
[assembly: AssemblyProduct("GrabScreen")]
[assembly: AssemblyCopyright("© 2026 星方向")]
// 版本號由 build.ps1 從 src\AssemblyInfo.cs 讀出來，產生在 installer\obj\Version.cs

namespace GrabScreenSetup
{
    /// <summary>要安裝到哪裡。正式安裝用 Real()；自動測試時改成測試資料夾，不會動到真的系統設定。</summary>
    internal class InstallTarget
    {
        public string Dir;
        public string StartMenuDir;
        public string DesktopDir;
        public string RegBase = "Software";
        public bool CloseApp = true;

        public string UninstallKey { get { return RegBase + @"\Microsoft\Windows\CurrentVersion\Uninstall\GrabScreen"; } }
        public string RunKey { get { return RegBase + @"\Microsoft\Windows\CurrentVersion\Run"; } }

        public static InstallTarget Real(string dir)
        {
            return new InstallTarget
            {
                Dir = dir,
                StartMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
        }
    }

    internal static class Installer
    {
        public const string ExeName = "GrabScreen.exe";
        public const string UninstallerName = "Uninstall.exe";
        public const string DisplayName = "GrabScreen 螢幕擷取";
        public const string Publisher = "星方向";
        public const string ShortcutName = "GrabScreen 螢幕擷取.lnk";
        public const string RunValueName = "GrabScreen";
        public const string ExitEventName = "GrabScreen_Exit_7F3A"; // 要跟 TrayApp.ExitEventName 一樣

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        public static string DefaultDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GrabScreen"); }
        }

        /// <summary>以前裝過的話，回傳當時的安裝位置；沒裝過回傳 null。</summary>
        public static string ExistingInstallDir(InstallTarget t)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(t.UninstallKey))
                {
                    if (k == null) return null;
                    var dir = k.GetValue("InstallLocation") as string;
                    return string.IsNullOrEmpty(dir) ? null : dir;
                }
            }
            catch { return null; }
        }

        /// <summary>如果 GrabScreen 正在執行，先請它自己結束；等不到就強制關閉。</summary>
        public static void CloseRunningApp()
        {
            var procs = Process.GetProcessesByName("GrabScreen");
            if (procs.Length == 0) return;
            try
            {
                EventWaitHandle h;
                if (EventWaitHandle.TryOpenExisting(ExitEventName, out h))
                {
                    h.Set();
                    h.Dispose();
                }
            }
            catch { }
            foreach (var p in procs)
            {
                try
                {
                    if (!p.WaitForExit(4000)) { p.Kill(); p.WaitForExit(3000); }
                }
                catch { }
            }
        }

        public static void Install(InstallTarget t, bool desktopShortcut, bool startWithWindows)
        {
            if (t.CloseApp) CloseRunningApp();
            Directory.CreateDirectory(t.Dir);

            // 1. 放主程式（安裝檔裡面內嵌了一份 GrabScreen.exe）
            string exe = Path.Combine(t.Dir, ExeName);
            using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("GrabScreen.exe"))
            {
                if (res == null) throw new Exception("安裝檔不完整，找不到程式檔。請重新下載安裝檔。");
                Retry(() =>
                {
                    res.Position = 0;
                    using (var fs = File.Create(exe)) res.CopyTo(fs);
                });
            }

            // 2. 放一份移除程式（就是這個安裝檔本身）
            string uninstaller = Path.Combine(t.Dir, UninstallerName);
            string self = Application.ExecutablePath;
            if (!SamePath(self, uninstaller)) Retry(() => File.Copy(self, uninstaller, true));

            // 3. 捷徑
            Directory.CreateDirectory(t.StartMenuDir);
            CreateShortcut(Path.Combine(t.StartMenuDir, ShortcutName), exe);
            string desk = Path.Combine(t.DesktopDir, ShortcutName);
            if (desktopShortcut) { Directory.CreateDirectory(t.DesktopDir); CreateShortcut(desk, exe); }
            else if (File.Exists(desk)) File.Delete(desk);

            // 4. 開機自動啟動
            using (var run = Registry.CurrentUser.CreateSubKey(t.RunKey))
            {
                if (startWithWindows) run.SetValue(RunValueName, "\"" + exe + "\"");
                else if (run.GetValue(RunValueName) != null) run.DeleteValue(RunValueName, false);
            }

            // 5. 登記到 Windows「已安裝的應用程式」，才能從設定裡移除
            long sizeKb = (new FileInfo(exe).Length + new FileInfo(uninstaller).Length) / 1024;
            using (var k = Registry.CurrentUser.CreateSubKey(t.UninstallKey))
            {
                k.SetValue("DisplayName", DisplayName);
                k.SetValue("DisplayVersion", Version);
                k.SetValue("Publisher", Publisher);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", t.Dir);
                k.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        /// <summary>移除：只刪我們自己放的檔案、捷徑、登錄機碼，資料夾裡如果有別的東西就保留資料夾。</summary>
        public static void Uninstall(InstallTarget t, bool removeSettings)
        {
            if (t.CloseApp) CloseRunningApp();
            TryDelete(Path.Combine(t.StartMenuDir, ShortcutName));
            TryDelete(Path.Combine(t.DesktopDir, ShortcutName));
            try
            {
                using (var run = Registry.CurrentUser.OpenSubKey(t.RunKey, true))
                    if (run != null && run.GetValue(RunValueName) != null) run.DeleteValue(RunValueName, false);
            }
            catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(t.UninstallKey, false); } catch { }
            TryDelete(Path.Combine(t.Dir, ExeName));

            if (removeSettings)
            {
                try
                {
                    string sd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GrabScreen");
                    if (Directory.Exists(sd)) Directory.Delete(sd, true);
                }
                catch { }
            }

            string uninstaller = Path.Combine(t.Dir, UninstallerName);
            if (SamePath(Application.ExecutablePath, uninstaller))
            {
                // 正在執行的移除程式沒辦法刪除自己：交給 cmd 等我們結束後再刪
                string cmd = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + uninstaller + "\" & rmdir \"" + t.Dir + "\"";
                var psi = new ProcessStartInfo("cmd.exe", cmd)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetTempPath()
                };
                Environment.CurrentDirectory = Path.GetTempPath();
                Process.Start(psi);
            }
            else
            {
                TryDelete(uninstaller);
                try { if (Directory.Exists(t.Dir) && Directory.GetFileSystemEntries(t.Dir).Length == 0) Directory.Delete(t.Dir); } catch { }
            }
        }

        private static void CreateShortcut(string lnkPath, string target)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(t);
            try
            {
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.TargetPath = target;
                sc.WorkingDirectory = Path.GetDirectoryName(target);
                sc.Description = "按快捷鍵擷取螢幕，並加上框框、箭頭、文字等標示";
                sc.IconLocation = target + ",0";
                sc.Save();
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        private static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void Retry(Action action)
        {
            for (int i = 0; ; i++)
            {
                try { action(); return; }
                catch (IOException)
                {
                    if (i >= 10) throw;
                    Thread.Sleep(300);
                }
            }
        }
    }

    /// <summary>安裝畫面。</summary>
    internal class SetupForm : Form
    {
        private readonly Panel pageMain, pageDone;
        private readonly TextBox txtDir;
        private readonly CheckBox chkDesktop, chkStartup, chkRun;
        private readonly Button btnInstall, btnCancel;
        private readonly Label lblStatus;
        private string installedExe;

        public SetupForm()
        {
            Text = "GrabScreen 安裝程式";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft JhengHei UI", 9.5f);
            Icon = Icons.AppIcon(Dpi.S(32));
            BackColor = Color.White;

            int W = Dpi.S(560), pad = Dpi.S(26), headH = Dpi.S(100), pageH = Dpi.S(380);
            string existing = Installer.ExistingInstallDir(InstallTarget.Real(""));

            // ---- 標題區 ----
            var header = new Panel { Location = new Point(0, 0), Size = new Size(W, headH), BackColor = Color.FromArgb(244, 247, 253) };
            header.Controls.Add(new PictureBox { Image = Icons.AppBitmap(Dpi.S(60)), SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(pad, Dpi.S(20)) });
            header.Controls.Add(new Label
            {
                Text = "GrabScreen 螢幕擷取",
                Font = new Font("Microsoft JhengHei UI", 16f, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 46, 60),
                AutoSize = true,
                Location = new Point(pad + Dpi.S(74), Dpi.S(22))
            });
            header.Controls.Add(new Label
            {
                Text = "版本 " + Installer.Version + "　·　星方向" + (existing != null ? "　·　這台電腦已經裝過，會更新成這個版本" : ""),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(pad + Dpi.S(76), Dpi.S(60))
            });
            Controls.Add(header);

            // ---- 安裝設定頁 ----
            pageMain = new Panel { Location = new Point(0, headH), Size = new Size(W, pageH) };
            int y = Dpi.S(20);
            pageMain.Controls.Add(new Label
            {
                Text = "按一個快捷鍵，就能把螢幕畫面抓進剪貼簿：\n" +
                       "　Ctrl+Alt+1 全螢幕　｜　Ctrl+Alt+2 視窗／物件　｜　Ctrl+Alt+3 自行框選\n" +
                       "擷取後還可以加上框框、箭頭、文字、編號、馬賽克，也能裁切和調整尺寸。",
                AutoSize = false,
                Size = new Size(W - pad * 2, Dpi.S(66)),
                Location = new Point(pad, y)
            });
            y += Dpi.S(80);

            pageMain.Controls.Add(new Label { Text = "安裝位置", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Location = new Point(pad, y) });
            y += Dpi.S(26);
            txtDir = new TextBox { Text = existing ?? Installer.DefaultDir, Location = new Point(pad, y), Width = W - pad * 2 - Dpi.S(90) };
            pageMain.Controls.Add(txtDir);
            var btnBrowse = new Button { Text = "瀏覽…", Size = new Size(Dpi.S(82), Dpi.S(27)), Location = new Point(W - pad - Dpi.S(82), y - Dpi.S(1)) };
            btnBrowse.Click += (s, e) => Browse();
            pageMain.Controls.Add(btnBrowse);
            y += Dpi.S(44);

            chkDesktop = AddCheck("在桌面建立捷徑", true, pad, ref y);
            chkStartup = AddCheck("開機時自動啟動（建議勾選，快捷鍵才能隨時使用）", true, pad, ref y);
            chkRun = AddCheck("安裝完成後立即執行", true, pad, ref y);
            y += Dpi.S(8);

            pageMain.Controls.Add(new Label
            {
                Text = "只會安裝給目前的使用者，不需要系統管理員權限。\n如果 GrabScreen 正在執行，安裝時會先自動關閉它（還沒存檔的編輯會遺失）。",
                ForeColor = Color.Gray,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Location = new Point(pad, y)
            });

            int by = pageH - Dpi.S(58);
            lblStatus = new Label { AutoSize = true, ForeColor = Color.FromArgb(0, 100, 190), Location = new Point(pad, by + Dpi.S(8)) };
            pageMain.Controls.Add(lblStatus);
            btnInstall = new Button
            {
                Text = existing != null ? "更新安裝" : "安裝",
                Size = new Size(Dpi.S(110), Dpi.S(36)),
                Location = new Point(W - pad - Dpi.S(212), by),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold)
            };
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Click += (s, e) => DoInstall();
            btnCancel = new Button { Text = "取消", Size = new Size(Dpi.S(92), Dpi.S(36)), Location = new Point(W - pad - Dpi.S(92), by) };
            btnCancel.Click += (s, e) => Close();
            pageMain.Controls.Add(btnInstall);
            pageMain.Controls.Add(btnCancel);
            Controls.Add(pageMain);

            // ---- 完成頁 ----
            pageDone = new Panel { Location = new Point(0, headH), Size = new Size(W, pageH), Visible = false };
            pageDone.Controls.Add(new Label
            {
                Text = "✔  安裝完成！",
                Font = new Font("Microsoft JhengHei UI", 17f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 140, 60),
                AutoSize = true,
                Location = new Point(pad, Dpi.S(24))
            });
            pageDone.Controls.Add(new Label
            {
                Text = "怎麼開始用：\n\n" +
                       "•  右下角時鐘旁邊出現星星圖示，就代表 GrabScreen 正在待命\n" +
                       "•  按 Ctrl + Alt + 3 試試看框選擷取，擷取後直接 Ctrl + V 就能貼上\n" +
                       "•  右鍵點星星圖示，可以改快捷鍵、調整設定或結束程式\n" +
                       "•  開始功能表裡也找得到「GrabScreen 螢幕擷取」\n\n" +
                       "不想用了：到 Windows「設定 → 應用程式 → 已安裝的應用程式」移除 GrabScreen。",
                AutoSize = false,
                Size = new Size(W - pad * 2, Dpi.S(220)),
                Location = new Point(pad, Dpi.S(78))
            });
            var btnDone = new Button { Text = "完成", Size = new Size(Dpi.S(110), Dpi.S(36)), Location = new Point(W - pad - Dpi.S(110), pageH - Dpi.S(58)) };
            btnDone.Click += (s, e) => Close();
            pageDone.Controls.Add(btnDone);
            Controls.Add(pageDone);

            ClientSize = new Size(W, headH + pageH);
            AcceptButton = btnInstall;
        }

        private CheckBox AddCheck(string text, bool value, int x, ref int y)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = true, Location = new Point(x, y) };
            pageMain.Controls.Add(c);
            y += Dpi.S(30);
            return c;
        }

        private void Browse()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "選擇要安裝到哪個資料夾（會在裡面建立 GrabScreen 資料夾）";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string p = dlg.SelectedPath;
                if (!string.Equals(Path.GetFileName(p.TrimEnd('\\')), "GrabScreen", StringComparison.OrdinalIgnoreCase))
                    p = Path.Combine(p, "GrabScreen");
                txtDir.Text = p;
            }
        }

        private void DoInstall()
        {
            string dir = txtDir.Text.Trim();
            if (dir.Length == 0 || !Path.IsPathRooted(dir))
            {
                MessageBox.Show(this, "請選擇一個完整的安裝位置。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnInstall.Enabled = btnCancel.Enabled = false;
            lblStatus.Text = "安裝中，請稍候…";
            Cursor = Cursors.WaitCursor;
            Application.DoEvents();
            try
            {
                Installer.Install(InstallTarget.Real(dir), chkDesktop.Checked, chkStartup.Checked);
                installedExe = Path.Combine(dir, Installer.ExeName);
            }
            catch (UnauthorizedAccessException)
            {
                Fail("沒有權限寫入這個資料夾。\n請改用預設的安裝位置：\n" + Installer.DefaultDir);
                return;
            }
            catch (Exception ex)
            {
                Fail("安裝失敗：" + ex.Message);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (chkRun.Checked)
            {
                try { Process.Start(new ProcessStartInfo(installedExe) { WorkingDirectory = dir }); } catch { }
            }
            ShowDone();
        }

        private void Fail(string message)
        {
            lblStatus.Text = "";
            btnInstall.Enabled = btnCancel.Enabled = true;
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowDone()
        {
            pageMain.Visible = false;
            pageDone.Visible = true;
            AcceptButton = null;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Dpi.MakeProcessDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length >= 2 && args[0] == "/selftest") { SetupSelfTest.Run(args[1]); return; }
            foreach (var a in args)
            {
                if (string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase)) { RunUninstall(); return; }
            }
            Application.Run(new SetupForm());
        }

        private static void RunUninstall()
        {
            const string title = "移除 GrabScreen";
            if (MessageBox.Show("確定要從這台電腦移除「GrabScreen 螢幕擷取」嗎？", title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bool removeSettings = MessageBox.Show("也要一併刪除你的設定（快捷鍵、擷取選項）嗎？\n\n選「否」會保留設定，之後重新安裝可以直接沿用。",
                title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

            var t = InstallTarget.Real(Path.GetDirectoryName(Application.ExecutablePath));
            string regDir = Installer.ExistingInstallDir(t);
            if (regDir != null) t.Dir = regDir;
            try
            {
                Installer.Uninstall(t, removeSettings);
                MessageBox.Show("GrabScreen 已經移除，謝謝使用！", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("移除時發生問題：" + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// 自動測試（setup.exe /selftest 測試資料夾）：在測試資料夾裡安裝、更新、移除一次，
    /// 登錄機碼寫在 HKCU\Software\GrabScreenSetupTest 底下，最後清乾淨，不會動到真的系統設定。
    /// </summary>
    internal static class SetupSelfTest
    {
        private static readonly StringBuilder log = new StringBuilder();

        private static void Check(bool ok, string name)
        {
            log.AppendLine((ok ? "PASS " : "FAIL ") + name);
        }

        private static string ReadReg(string key, string name)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(key))
            {
                if (k == null) return null;
                var v = k.GetValue(name);
                return v == null ? null : v.ToString();
            }
        }

        public static void Run(string root)
        {
            const string regBase = @"Software\GrabScreenSetupTest";
            try
            {
                Directory.CreateDirectory(root);
                var t = new InstallTarget
                {
                    Dir = Path.Combine(root, "app", "GrabScreen"),
                    StartMenuDir = Path.Combine(root, "startmenu"),
                    DesktopDir = Path.Combine(root, "desktop"),
                    RegBase = regBase,
                    CloseApp = false
                };
                string exe = Path.Combine(t.Dir, Installer.ExeName);
                long resLen;
                using (var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("GrabScreen.exe")) resLen = res == null ? -1 : res.Length;
                Check(resLen > 10000, "安裝檔內含 GrabScreen.exe（" + resLen + " bytes）");

                Installer.Install(t, true, true);
                Check(File.Exists(exe) && new FileInfo(exe).Length == resLen, "主程式有放到安裝資料夾，大小正確");
                Check(File.Exists(Path.Combine(t.Dir, Installer.UninstallerName)), "移除程式有放到安裝資料夾");
                Check(File.Exists(Path.Combine(t.StartMenuDir, Installer.ShortcutName)), "開始功能表捷徑");
                Check(File.Exists(Path.Combine(t.DesktopDir, Installer.ShortcutName)), "桌面捷徑");
                Check(ReadReg(t.RunKey, Installer.RunValueName) == "\"" + exe + "\"", "開機自動啟動");
                Check(ReadReg(t.UninstallKey, "DisplayName") == Installer.DisplayName, "登記到已安裝的應用程式");
                Check(ReadReg(t.UninstallKey, "DisplayVersion") == Installer.Version, "版本號 " + Installer.Version);
                Check(Installer.ExistingInstallDir(t) == t.Dir, "可以讀回安裝位置（更新時沿用）");

                // 更新安裝：取消桌面捷徑與開機啟動
                Installer.Install(t, false, false);
                Check(!File.Exists(Path.Combine(t.DesktopDir, Installer.ShortcutName)), "更新時取消勾選會刪掉桌面捷徑");
                Check(ReadReg(t.RunKey, Installer.RunValueName) == null, "更新時取消勾選會關掉開機啟動");

                // 安裝資料夾裡有使用者自己的檔案時，移除後要保留
                string userFile = Path.Combine(t.Dir, "我的筆記.txt");
                File.WriteAllText(userFile, "test");
                Installer.Uninstall(t, false);
                Check(!File.Exists(exe), "移除後主程式不見了");
                Check(!File.Exists(Path.Combine(t.Dir, Installer.UninstallerName)), "移除後移除程式不見了");
                Check(!File.Exists(Path.Combine(t.StartMenuDir, Installer.ShortcutName)), "移除後開始功能表捷徑不見了");
                Check(ReadReg(t.UninstallKey, "DisplayName") == null, "移除後不在已安裝清單裡");
                Check(File.Exists(userFile), "資料夾裡別的檔案不會被刪掉");
                File.Delete(userFile);

                // 空資料夾的情況：移除後資料夾也要刪掉
                Installer.Install(t, true, true);
                Installer.Uninstall(t, false);
                Check(!Directory.Exists(t.Dir), "沒有別的檔案時，安裝資料夾也會刪掉");
                Check(!File.Exists(Path.Combine(t.DesktopDir, Installer.ShortcutName)) && ReadReg(t.RunKey, Installer.RunValueName) == null, "移除後桌面捷徑和開機啟動都清掉");

                // 畫面截圖（放在螢幕外面，不搶焦點）
                var f = new QuietSetupForm();
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-30000, -30000);
                f.Show();
                Application.DoEvents();
                Snap(f, Path.Combine(root, "setup_main.png"));
                f.ShowDone();
                Application.DoEvents();
                Snap(f, Path.Combine(root, "setup_done.png"));
                f.Dispose();
            }
            catch (Exception ex)
            {
                log.AppendLine("FAIL 例外：" + ex);
            }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(regBase, false); } catch { }
                File.WriteAllText(Path.Combine(root, "selftest.log"), log.ToString() + "DONE\r\n", Encoding.UTF8);
            }
        }

        private static void Snap(Control c, string path)
        {
            using (var bmp = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private class QuietSetupForm : SetupForm
        {
            protected override bool ShowWithoutActivation { get { return true; } }
        }
    }
}
