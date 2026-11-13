using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace GrabScreen
{
    internal class WindowInfo
    {
        public IntPtr Handle;
        public Rectangle Bounds;
        public string Title;
    }

    /// <summary>一個可以被選取的範圍（視窗本身，或視窗裡的按鈕、面板等物件）。</summary>
    internal struct ElementBox
    {
        public Rectangle Bounds;
        public string Label;
        public ElementBox(Rectangle r, string label) { Bounds = r; Label = label; }
    }

    internal static class WindowFinder
    {
        // 這些是「看不見但蓋在最上面」的視窗，要跳過
        private static readonly string[] IgnoredClasses = { "CEF-OSC-WIDGET", "WorkerW" };

        /// <summary>列出目前畫面上看得到的視窗，順序是「最上層的在前面」。</summary>
        public static List<WindowInfo> GetTopLevelWindows()
        {
            var list = new List<WindowInfo>();
            uint myPid = (uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                try
                {
                    if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                    uint pid;
                    Native.GetWindowThreadProcessId(h, out pid);
                    if (pid == myPid) return true;
                    int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                    if ((ex & Native.WS_EX_TRANSPARENT) != 0) return true; // 點不到的透明疊加層
                    const int layeredNoActivateTool = 0x80000 | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                    if ((ex & layeredNoActivateTool) == layeredNoActivateTool) return true; // 遊戲/顯卡疊加層之類
                    int cloaked;
                    if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                    var cls = new StringBuilder(64);
                    Native.GetClassName(h, cls, 64);
                    if (Array.IndexOf(IgnoredClasses, cls.ToString()) >= 0) return true;
                    Native.RECT rc;
                    Rectangle r;
                    if (Native.DwmGetWindowAttribute(h, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out rc, 16) == 0) r = rc.ToRectangle();
                    else if (Native.GetWindowRect(h, out rc)) r = rc.ToRectangle();
                    else return true;
                    if (r.Width < 8 || r.Height < 8) return true;
                    var sb = new StringBuilder(256);
                    Native.GetWindowText(h, sb, 256);
                    list.Add(new WindowInfo { Handle = h, Bounds = r, Title = sb.ToString() });
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return list;
        }
    }

    /// <summary>
    /// 在背景執行緒用「UI 自動化」找出滑鼠底下最小的物件（按鈕、輸入框、面板…），
    /// 這樣畫面不會卡住。只處理最新的一個請求。
    /// </summary>
    internal class ElementProbe : IDisposable
    {
        public delegate void ResultHandler(int requestId, IntPtr hwnd, List<ElementBox> chain);
        public event ResultHandler Result;

        private readonly object sync = new object();
        private readonly AutoResetEvent signal = new AutoResetEvent(false);
        private readonly Thread thread;
        private volatile bool stopped;
        private int pendingId = -1;
        private IntPtr pendingHwnd;
        private Rectangle pendingWindow;
        private Point pendingPoint;
        private int budget;
        private readonly Dictionary<string, List<AutomationElement>> childCache = new Dictionary<string, List<AutomationElement>>();
        private readonly Dictionary<string, string> labelCache = new Dictionary<string, string>();

        private struct Candidate
        {
            public AutomationElement Element;
            public Rectangle Rect;
            public long Area;
        }

        public ElementProbe()
        {
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        public void Request(int id, IntPtr hwnd, Rectangle windowBounds, Point screenPoint)
        {
            lock (sync)
            {
                pendingId = id;
                pendingHwnd = hwnd;
                pendingWindow = windowBounds;
                pendingPoint = screenPoint;
            }
            signal.Set();
        }

        private void Run()
        {
            var cr = new CacheRequest();
            cr.Add(AutomationElement.BoundingRectangleProperty);
            cr.Add(AutomationElement.ControlTypeProperty);
            cr.Add(AutomationElement.RuntimeIdProperty);
            cr.TreeScope = TreeScope.Element;
            cr.TreeFilter = Automation.RawViewCondition; // 原始樹：Chrome / Electron 類的程式才找得到內部物件

            while (!stopped)
            {
                signal.WaitOne();
                if (stopped) break;
                int id; IntPtr hwnd; Rectangle win; Point pt;
                lock (sync)
                {
                    id = pendingId; hwnd = pendingHwnd; win = pendingWindow; pt = pendingPoint;
                }
                if (id < 0) continue;
                List<ElementBox> chain = null;
                try
                {
                    using (cr.Activate())
                    {
                        AutomationElement root = AutomationElement.FromHandle(hwnd);
                        budget = 250; // 最多查詢幾次子物件，避免太複雜的程式卡太久
                        chain = Descend(root, "hwnd:" + hwnd.ToInt64(), win, pt, 0);
                    }
                }
                catch { chain = null; }
                if (stopped) break;
                if (chain != null)
                {
                    var h = Result;
                    if (h != null) h(id, hwnd, chain);
                }
            }
        }

        private List<AutomationElement> GetChildren(AutomationElement el, string key)
        {
            List<AutomationElement> children;
            if (childCache.TryGetValue(key, out children)) return children;
            children = new List<AutomationElement>();
            budget--;
            try
            {
                var found = el.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement c in found) children.Add(c);
            }
            catch { }
            childCache[key] = children;
            return children;
        }

        private static string KeyOf(AutomationElement el, string parentKey, Rectangle r)
        {
            try
            {
                var rid = el.GetCachedPropertyValue(AutomationElement.RuntimeIdProperty) as int[];
                if (rid != null && rid.Length > 0) return string.Join(".", Array.ConvertAll(rid, x => x.ToString()));
            }
            catch { }
            return parentKey + "/" + r.ToString();
        }

        private string LabelOf(AutomationElement el, string key)
        {
            string label;
            if (labelCache.TryGetValue(key, out label)) return label;
            label = "";
            try
            {
                var ct = el.Cached.ControlType;
                if (ct != null) label = ct.LocalizedControlType;
            }
            catch { }
            labelCache[key] = label;
            return label;
        }

        /// <summary>
        /// 往下一層一層找「包含滑鼠位置、面積最小」的物件。
        /// 如果某個子物件跟上一層一樣大又沒有東西（例如 Chrome 的繪圖層），就換下一個候選繼續找。
        /// 回傳由大到小的範圍清單（不含 rect 本身）。
        /// </summary>
        private List<ElementBox> Descend(AutomationElement el, string key, Rectangle rect, Point pt, int depth)
        {
            var empty = new List<ElementBox>();
            if (depth > 40 || stopped || budget <= 0) return empty;

            var cands = new List<Candidate>();
            foreach (var c in GetChildren(el, key))
            {
                Rectangle r;
                try
                {
                    var br = c.Cached.BoundingRectangle;
                    if (br.IsEmpty || double.IsInfinity(br.Width) || br.Width < 2 || br.Height < 2) continue;
                    r = new Rectangle((int)Math.Round(br.X), (int)Math.Round(br.Y), (int)Math.Round(br.Width), (int)Math.Round(br.Height));
                }
                catch { continue; }
                r.Intersect(rect); // 子物件不會比上一層大
                if (r.Width < 4 || r.Height < 4 || !r.Contains(pt)) continue;
                cands.Add(new Candidate { Element = c, Rect = r, Area = (long)r.Width * r.Height });
            }
            cands.Sort((a, b) => a.Area.CompareTo(b.Area));

            int tries = 0;
            foreach (var cand in cands)
            {
                if (++tries > 4 || budget <= 0) break;
                string ckey = KeyOf(cand.Element, key, cand.Rect);
                var sub = Descend(cand.Element, ckey, cand.Rect, pt, depth + 1);
                bool smaller = cand.Rect != rect;
                if (!smaller && sub.Count == 0) continue; // 死路，換下一個
                var path = new List<ElementBox>();
                if (smaller) path.Add(new ElementBox(cand.Rect, LabelOf(cand.Element, ckey)));
                path.AddRange(sub);
                return path;
            }
            return empty;
        }

        public void Dispose()
        {
            stopped = true;
            signal.Set();
            Result = null;
        }
    }
}
