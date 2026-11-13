using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GrabScreen
{
    /// <summary>使用者設定：存在 %AppData%\GrabScreen\settings.ini</summary>
    internal class Settings
    {
        public Keys HotkeyFull = Keys.Control | Keys.Alt | Keys.D1;
        public Keys HotkeyWindow = Keys.Control | Keys.Alt | Keys.D2;
        public Keys HotkeyRegion = Keys.Control | Keys.Alt | Keys.D3;
        public bool FullScreenAllMonitors = false;
        public bool ShowPrompt = true;
        public int PromptSeconds = 8;
        public bool AutoSave = false;
        public string AutoSaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "GrabScreen");
        public bool FirstRun = true;

        private static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GrabScreen"); }
        }

        private static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                s.HotkeyFull = GetKeys(map, "HotkeyFull", s.HotkeyFull);
                s.HotkeyWindow = GetKeys(map, "HotkeyWindow", s.HotkeyWindow);
                s.HotkeyRegion = GetKeys(map, "HotkeyRegion", s.HotkeyRegion);
                s.FullScreenAllMonitors = GetBool(map, "FullScreenAllMonitors", s.FullScreenAllMonitors);
                s.ShowPrompt = GetBool(map, "ShowPrompt", s.ShowPrompt);
                s.AutoSave = GetBool(map, "AutoSave", s.AutoSave);
                s.FirstRun = GetBool(map, "FirstRun", s.FirstRun);
                string v;
                if (map.TryGetValue("AutoSaveFolder", out v) && v.Length > 0) s.AutoSaveFolder = v;
                int n;
                if (map.TryGetValue("PromptSeconds", out v) && int.TryParse(v, out n)) s.PromptSeconds = Math.Max(0, Math.Min(60, n));
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("HotkeyFull=" + (int)HotkeyFull);
                sb.AppendLine("HotkeyWindow=" + (int)HotkeyWindow);
                sb.AppendLine("HotkeyRegion=" + (int)HotkeyRegion);
                sb.AppendLine("FullScreenAllMonitors=" + FullScreenAllMonitors);
                sb.AppendLine("ShowPrompt=" + ShowPrompt);
                sb.AppendLine("PromptSeconds=" + PromptSeconds);
                sb.AppendLine("AutoSave=" + AutoSave);
                sb.AppendLine("AutoSaveFolder=" + AutoSaveFolder);
                sb.AppendLine("FirstRun=" + FirstRun);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static Keys GetKeys(Dictionary<string, string> map, string key, Keys def)
        {
            string v; int n;
            if (map.TryGetValue(key, out v) && int.TryParse(v, out n)) return (Keys)n;
            return def;
        }

        private static bool GetBool(Dictionary<string, string> map, string key, bool def)
        {
            string v; bool b;
            if (map.TryGetValue(key, out v) && bool.TryParse(v, out b)) return b;
            return def;
        }

        /// <summary>把快捷鍵轉成好讀的文字，例如 "Ctrl + Alt + 1"。</summary>
        public static string KeysToText(Keys k)
        {
            if (k == Keys.None) return "(未設定)";
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            if ((k & Keys.Shift) != 0) parts.Add("Shift");
            Keys code = k & Keys.KeyCode;
            string name;
            if (code >= Keys.D0 && code <= Keys.D9) name = ((int)(code - Keys.D0)).ToString();
            else if (code >= Keys.NumPad0 && code <= Keys.NumPad9) name = "Num " + (int)(code - Keys.NumPad0);
            else if (code == Keys.PrintScreen) name = "PrtSc";
            else if (code == Keys.Oemtilde) name = "`";
            else name = code.ToString();
            parts.Add(name);
            return string.Join(" + ", parts.ToArray());
        }

        // ---- 開機自動啟動（寫在目前使用者的登錄機碼，不需要系統管理員權限）----
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "GrabScreen";

        public static bool StartWithWindows
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                        return key != null && key.GetValue(RunName) != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (key == null) return;
                        if (value) key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                        else if (key.GetValue(RunName) != null) key.DeleteValue(RunName, false);
                    }
                }
                catch { }
            }
        }
    }
}
