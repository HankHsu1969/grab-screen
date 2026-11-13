using System;
using System.Threading;
using System.Windows.Forms;

namespace GrabScreen
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 一定要在建立任何視窗之前設定，截圖才會是螢幕真正的像素（不會因 125%/150% 縮放而模糊或錯位）
            Dpi.MakeProcessDpiAware();

            bool createdNew;
            using (var mutex = new Mutex(true, "GrabScreen_SingleInstance_7F3A", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("GrabScreen 已經在執行了，請看右下角系統匣的圖示。", "GrabScreen",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show("發生錯誤：" + e.Exception.Message, "GrabScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 測試用：GrabScreen.exe --edit 圖片路徑 → 直接開啟編輯器
                if (args.Length >= 2 && args[0] == "--edit")
                {
                    using (var img = System.Drawing.Image.FromFile(args[1]))
                        Application.Run(new EditorForm(new System.Drawing.Bitmap(img)));
                    return;
                }

                Application.Run(new TrayApp());
            }
        }
    }
}
