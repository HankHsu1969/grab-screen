using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace GrabScreen
{
    /// <summary>呼叫 Windows 系統 API 的宣告集中放這裡。</summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x8;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_CLOAKED = 14;
        public const int WM_HOTKEY = 0x0312;
        public const int WM_DPICHANGED = 0x02E0;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    }

    /// <summary>螢幕縮放比例（125%、150%…）的換算工具。</summary>
    internal static class Dpi
    {
        private static float factor = 0f;

        public static void MakeProcessDpiAware()
        {
            bool ok = false;
            try { ok = Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // PER_MONITOR_AWARE_V2
            if (!ok) { try { Native.SetProcessDPIAware(); } catch { } }
        }

        public static float Factor
        {
            get
            {
                if (factor <= 0f)
                {
                    try { factor = Native.GetDpiForSystem() / 96f; }
                    catch
                    {
                        using (var g = Graphics.FromHwnd(IntPtr.Zero)) factor = g.DpiX / 96f;
                    }
                    if (factor <= 0f) factor = 1f;
                }
                return factor;
            }
        }

        public static int S(int v) { return (int)Math.Round(v * Factor); }
        public static float S(float v) { return v * Factor; }
    }
}
