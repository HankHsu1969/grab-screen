using System;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>註冊「全域快捷鍵」：不管目前在哪個程式，按下就會通知我們。</summary>
    internal class HotkeyManager : NativeWindow, IDisposable
    {
        public event Action<int> HotkeyPressed;

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        /// <summary>註冊成功回傳 true；跟別的程式衝突時回傳 false。</summary>
        public bool Register(int id, Keys keys)
        {
            Native.UnregisterHotKey(Handle, id);
            if (keys == Keys.None) return true;
            uint mods = Native.MOD_NOREPEAT;
            if ((keys & Keys.Control) != 0) mods |= Native.MOD_CONTROL;
            if ((keys & Keys.Alt) != 0) mods |= Native.MOD_ALT;
            if ((keys & Keys.Shift) != 0) mods |= Native.MOD_SHIFT;
            uint vk = (uint)(keys & Keys.KeyCode);
            return Native.RegisterHotKey(Handle, id, mods, vk);
        }

        public void Unregister(int id)
        {
            Native.UnregisterHotKey(Handle, id);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                var h = HotkeyPressed;
                if (h != null) h(m.WParam.ToInt32());
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(Handle, i);
            DestroyHandle();
        }
    }
}
