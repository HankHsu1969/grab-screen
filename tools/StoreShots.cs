using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>
    /// 產生 Microsoft Store 商店頁的截圖（selftest.exe --store-shots 輸出資料夾）。
    /// 底圖是程式畫出來的「虛構業績報表」，不含任何真實畫面或個資。
    /// </summary>
    internal static partial class SelfTest
    {
        private const int ShotW = 1920, ShotH = 1080;

        private static void StoreShots(string dir)
        {
            Directory.CreateDirectory(dir);
            using (var app = SampleApp(1500, 860))
            using (var desktop = Desktop(app))
            {
                ShotEditor(app, Path.Combine(dir, "1-editor.png"));
                ShotElement(desktop, Path.Combine(dir, "2-element.png"));
                ShotRegion(desktop, Path.Combine(dir, "3-region.png"));
                ShotToast(desktop, Path.Combine(dir, "4-toast.png"));
                ShotHotkeys(Path.Combine(dir, "5-hotkeys.png"));
            }
            Console.WriteLine("DONE");
        }

        // ------------------------------------------------------------ 範例畫面

        private static Bitmap SampleApp(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.Clear(Color.FromArgb(245, 247, 251));
                // 側邊選單
                using (var br = new SolidBrush(Color.FromArgb(31, 42, 68))) g.FillRectangle(br, 0, 0, 210, h);
                Text(g, "業務儀表板", 28, 26, 22, FontStyle.Bold, Color.White);
                string[] menu = { "總覽", "業績報表", "客戶管理", "訂單", "設定" };
                for (int i = 0; i < menu.Length; i++)
                {
                    if (i == 1) using (var br = new SolidBrush(Color.FromArgb(64, 92, 160))) g.FillRectangle(br, 12, 86 + i * 50, 186, 40);
                    Text(g, menu[i], 36, 94 + i * 50, 17, FontStyle.Regular, Color.FromArgb(220, 228, 245));
                }
                // 標題
                Text(g, "2026 年第三季業績報表", 250, 26, 28, FontStyle.Bold, Color.FromArgb(30, 36, 50));
                Text(g, "更新時間：2026/10/05 09:30", 252, 70, 15, FontStyle.Regular, Color.Gray);
                // 數字卡片
                string[,] kpi = { { "營收", "NT$ 1,284 萬", "▲ 18%" }, { "新客戶", "326 位", "▲ 9%" }, { "訂單數", "2,145 筆", "▲ 12%" }, { "回購率", "42 %", "▼ 2%" } };
                for (int i = 0; i < 4; i++)
                {
                    var rc = new Rectangle(250 + i * 310, 110, 290, 120);
                    Card(g, rc);
                    Text(g, kpi[i, 0], rc.X + 20, rc.Y + 16, 16, FontStyle.Regular, Color.Gray);
                    Text(g, kpi[i, 1], rc.X + 20, rc.Y + 44, 30, FontStyle.Bold, Color.FromArgb(30, 36, 50));
                    Text(g, kpi[i, 2], rc.X + 20, rc.Y + 86, 15, FontStyle.Bold, i == 3 ? Color.FromArgb(210, 60, 50) : Color.FromArgb(30, 150, 80));
                }
                // 長條圖
                var chart = new Rectangle(250, 250, 600, 300);
                Card(g, chart);
                Text(g, "每月營收（萬元）", chart.X + 20, chart.Y + 16, 17, FontStyle.Bold, Color.FromArgb(30, 36, 50));
                int[] vals = { 820, 910, 870, 1020, 1150, 1284 };
                string[] months = { "4月", "5月", "6月", "7月", "8月", "9月" };
                for (int i = 0; i < vals.Length; i++)
                {
                    int bh = vals[i] * 190 / 1300, bx = chart.X + 50 + i * 88;
                    var bar = new Rectangle(bx, chart.Bottom - 40 - bh, 50, bh);
                    using (var br = new LinearGradientBrush(bar, Color.FromArgb(91, 120, 255), Color.FromArgb(64, 164, 255), 90f)) g.FillRectangle(br, bar);
                    Text(g, months[i], bx + 8, chart.Bottom - 32, 14, FontStyle.Regular, Color.Gray);
                    Text(g, vals[i].ToString(), bx + 4, bar.Y - 22, 13, FontStyle.Regular, Color.DimGray);
                }
                // 客戶表格
                var table = new Rectangle(870, 250, 600, 300);
                Card(g, table);
                Text(g, "本月新客戶", table.X + 20, table.Y + 16, 17, FontStyle.Bold, Color.FromArgb(30, 36, 50));
                string[] head = { "姓名", "Email", "電話", "訂單金額" };
                int[] cx = { 20, 120, 340, 480 };
                for (int c = 0; c < 4; c++) Text(g, head[c], table.X + cx[c], table.Y + 56, 14, FontStyle.Bold, Color.Gray);
                string[,] rows =
                {
                    { "王小明", "xiaoming@example.com", "0912-345-678", "12,800" },
                    { "林美玲", "meiling@example.com", "0922-111-222", "8,450" },
                    { "陳志豪", "zhihao@example.com", "0933-456-789", "21,300" },
                    { "張雅婷", "yating@example.com", "0955-246-810", "6,900" },
                    { "李承恩", "chengen@example.com", "0988-135-791", "15,200" }
                };
                for (int r = 0; r < 5; r++)
                {
                    int ry = table.Y + 92 + r * 40;
                    using (var pen = new Pen(Color.FromArgb(235, 237, 242))) g.DrawLine(pen, table.X + 16, ry - 8, table.Right - 16, ry - 8);
                    for (int c = 0; c < 4; c++) Text(g, rows[r, c], table.X + cx[c], ry, 15, FontStyle.Regular, Color.FromArgb(50, 56, 70));
                }
                // 待辦事項
                var todo = new Rectangle(250, 570, 1220, 270);
                Card(g, todo);
                Text(g, "本週待辦", todo.X + 20, todo.Y + 16, 17, FontStyle.Bold, Color.FromArgb(30, 36, 50));
                string[] items = { "整理第三季業績簡報", "回覆陳志豪的報價需求", "安排 10 月客戶拜訪行程", "更新產品價目表" };
                for (int i = 0; i < items.Length; i++)
                {
                    int iy = todo.Y + 64 + i * 46;
                    using (var pen = new Pen(Color.FromArgb(160, 170, 190), 2f)) g.DrawRectangle(pen, todo.X + 24, iy + 2, 18, 18);
                    Text(g, items[i], todo.X + 56, iy, 17, FontStyle.Regular, Color.FromArgb(50, 56, 70));
                }
            }
            return bmp;
        }

        private static void Text(Graphics g, string s, float x, float y, float px, FontStyle st, Color c)
        {
            using (var f = new Font("Microsoft JhengHei UI", px, st, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(c)) g.DrawString(s, f, br, x, y);
        }

        private static void Card(Graphics g, Rectangle rc)
        {
            using (var sh = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
            using (var p0 = Icons.RoundRect(new RectangleF(rc.X + 1, rc.Y + 3, rc.Width, rc.Height), 12)) g.FillPath(sh, p0);
            using (var p = Icons.RoundRect(rc, 12)) g.FillPath(Brushes.White, p);
        }

        private static void Backdrop(Graphics g)
        {
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, ShotW, ShotH), Color.FromArgb(226, 234, 255), Color.FromArgb(246, 249, 255), 45f))
                g.FillRectangle(br, 0, 0, ShotW, ShotH);
        }

        /// <summary>畫一個 Windows 11 風格的視窗外框，把內容放進去。</summary>
        private static void Window(Graphics g, Rectangle client, string title, Image content, bool ourIcon)
        {
            const int tb = 34;
            var frame = new Rectangle(client.X, client.Y - tb, client.Width, client.Height + tb);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 12; i >= 1; i--)
                using (var sh = new SolidBrush(Color.FromArgb(5, 0, 0, 0)))
                using (var p = Icons.RoundRect(new RectangleF(frame.X - i, frame.Y - i + 6, frame.Width + i * 2, frame.Height + i * 2), 10 + i)) g.FillPath(sh, p);
            using (var p = Icons.RoundRect(frame, 8)) g.FillPath(Brushes.White, p);
            var st = g.Save();
            using (var clip = Icons.RoundRect(frame, 8)) g.SetClip(clip);
            g.DrawImage(content, client.X, client.Y, client.Width, client.Height);
            using (var br = new SolidBrush(Color.FromArgb(243, 243, 246))) g.FillRectangle(br, frame.X, frame.Y, frame.Width, tb);
            g.Restore(st);
            int ix = frame.X + 12;
            if (ourIcon) using (var ic = Icons.AppBitmap(18)) g.DrawImage(ic, ix, frame.Y + 8);
            else using (var br = new SolidBrush(Color.FromArgb(64, 92, 160))) g.FillRectangle(br, ix + 2, frame.Y + 10, 14, 14);
            Text(g, title, ix + 28, frame.Y + 8, 14, FontStyle.Regular, Color.FromArgb(40, 40, 46));
            using (var pen = new Pen(Color.FromArgb(70, 70, 76), 1.2f))
            {
                int bx = frame.Right - 138, cy = frame.Y + tb / 2;
                g.DrawLine(pen, bx + 18, cy, bx + 28, cy);
                g.DrawRectangle(pen, bx + 64, cy - 5, 10, 10);
                g.DrawLine(pen, bx + 110, cy - 5, bx + 120, cy + 5);
                g.DrawLine(pen, bx + 120, cy - 5, bx + 110, cy + 5);
            }
        }

        private static readonly Rectangle AppClient = new Rectangle(160, 104, 1500, 860);

        private static Bitmap Desktop(Bitmap app)
        {
            var bmp = new Bitmap(ShotW, ShotH);
            using (var g = Graphics.FromImage(bmp))
            {
                Backdrop(g);
                Window(g, AppClient, "業務儀表板 - 範例", app, false);
            }
            return bmp;
        }

        // ------------------------------------------------------------ 擷取視窗截圖（只取內容區，外框自己畫）

        private static Bitmap ClientShot(Form f)
        {
            using (var full = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(full, new Rectangle(0, 0, f.Width, f.Height));
                Point off = f.PointToScreen(Point.Empty);
                off.Offset(-f.Left, -f.Top);
                var crop = new Bitmap(f.ClientSize.Width, f.ClientSize.Height);
                using (var g = Graphics.FromImage(crop))
                    g.DrawImage(full, new Rectangle(0, 0, crop.Width, crop.Height), new Rectangle(off, f.ClientSize), GraphicsUnit.Pixel);
                return crop;
            }
        }

        private static void Save(Bitmap b, string path)
        {
            b.Save(path, ImageFormat.Png);
            Console.WriteLine("截圖：" + path);
        }

        // ------------------------------------------------------------ 1. 編輯器

        private class QuietEditor : EditorForm
        {
            public QuietEditor(Bitmap b) : base(b) { ShowInTaskbar = false; }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override void OnShown(EventArgs e) { }
        }

        private static void ShotEditor(Bitmap app, string path)
        {
            var form = new QuietEditor((Bitmap)app.Clone());
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(-30000, -30000, 1640, 960);
            form.Show();
            Application.DoEvents();
            var cv = (EditorCanvas)typeof(EditorForm).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            cv.FitToWindow();

            cv.ApplyLevel(2);
            cv.Tool = Tool.Mosaic; Drag(cv, 985, 336, 1203, 538);
            cv.ApplyColor(Color.FromArgb(253, 216, 53));
            cv.Tool = Tool.Highlighter; Drag(cv, 885, 393, 1458, 393);
            cv.ApplyColor(Color.FromArgb(229, 57, 53));
            cv.Tool = Tool.Rect; Drag(cv, 243, 103, 547, 237);
            cv.Tool = Tool.Arrow; Drag(cv, 700, 70, 560, 140);
            cv.Tool = Tool.Text; Click(cv, 712, 38);
            var tb = (TextBox)typeof(EditorCanvas).GetField("textBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cv);
            if (tb != null) tb.Text = "第三季營收創新高！";
            cv.CommitText();
            cv.ApplyColor(Color.FromArgb(67, 160, 71));
            cv.Tool = Tool.Ellipse; Drag(cv, 712, 300, 790, 520);
            cv.ApplyColor(Color.FromArgb(30, 136, 229));
            cv.Tool = Tool.Step; Click(cv, 248, 104); Click(cv, 252, 248); Click(cv, 872, 248);
            cv.Tool = Tool.Arrow;
            cv.Dirty = false;
            Application.DoEvents();

            using (var client = ClientShot(form))
            using (var shot = new Bitmap(ShotW, ShotH))
            {
                using (var g = Graphics.FromImage(shot))
                {
                    Backdrop(g);
                    Window(g, new Rectangle((ShotW - client.Width) / 2, (ShotH - client.Height) / 2 + 16, client.Width, client.Height), "GrabScreen 編輯器", client, true);
                }
                Save(shot, path);
            }
            form.Dispose();
        }

        // ------------------------------------------------------------ 2、3. 擷取畫面

        private static void SetHint(CaptureOverlay ov, Rectangle screen)
        {
            var t = typeof(CaptureOverlay);
            var font = (Font)t.GetField("hintFont", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ov);
            var text = (string)t.GetField("hintText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ov);
            Size ts = TextRenderer.MeasureText(text, font);
            var r = new Rectangle(screen.X + (screen.Width - ts.Width) / 2 - 14, screen.Y + 24, ts.Width + 28, ts.Height + 16);
            t.GetField("hintRect", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ov, r);
            t.GetField("hintVisible", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ov, true);
        }

        private static CaptureOverlay Overlay(Bitmap desktop, CaptureMode mode, List<WindowInfo> wins)
        {
            var vs = new Rectangle(0, 0, ShotW, ShotH);
            var ov = new CaptureOverlay((Bitmap)desktop.Clone(), vs, mode, wins);
            ov.ShowInTaskbar = false;
            var h = ov.Handle;
            ov.Bounds = vs;
            return ov;
        }

        private static void ShotElement(Bitmap desktop, string path)
        {
            var win = new WindowInfo { Handle = new IntPtr(1), Bounds = new Rectangle(AppClient.X, AppClient.Y - 34, AppClient.Width, AppClient.Height + 34), Title = "範例" };
            var ov = Overlay(desktop, CaptureMode.Window, new List<WindowInfo> { win });
            Mouse(ov, "OnMouseMove", new Point(830, 280), MouseButtons.None);
            var card = new Rectangle(AppClient.X + 560, AppClient.Y + 110, 290, 120);
            var main = new Rectangle(AppClient.X + 230, AppClient.Y, AppClient.Width - 230, AppClient.Height);
            var list = new List<ElementBox> { new ElementBox(main, "窗格"), new ElementBox(card, "群組") };
            typeof(CaptureOverlay).GetMethod("OnProbeResult", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ov, new object[] { 0, new IntPtr(1), list });
            SetHint(ov, new Rectangle(0, 0, ShotW, ShotH));
            using (var bmp = new Bitmap(ShotW, ShotH))
            {
                ov.DrawToBitmap(bmp, new Rectangle(0, 0, ShotW, ShotH));
                Save(bmp, path);
            }
            ov.Dispose();
        }

        private static void ShotRegion(Bitmap desktop, string path)
        {
            var ov = Overlay(desktop, CaptureMode.Region, null);
            Mouse(ov, "OnMouseDown", new Point(AppClient.X + 240, AppClient.Y + 240), MouseButtons.Left);
            Mouse(ov, "OnMouseMove", new Point(AppClient.X + 600, AppClient.Y + 400), MouseButtons.Left);
            Mouse(ov, "OnMouseMove", new Point(AppClient.X + 862, AppClient.Y + 562), MouseButtons.Left);
            SetHint(ov, new Rectangle(0, 0, ShotW, ShotH));
            using (var bmp = new Bitmap(ShotW, ShotH))
            {
                ov.DrawToBitmap(bmp, new Rectangle(0, 0, ShotW, ShotH));
                Save(bmp, path);
            }
            ov.Dispose();
        }

        // ------------------------------------------------------------ 4. 擷取後的提示

        private static void ShotToast(Bitmap desktop, string path)
        {
            using (var captured = ScreenCapture.Crop(desktop, new Rectangle(AppClient.X + 240, AppClient.Y + 240, 622, 322)))
            using (var shot = (Bitmap)desktop.Clone())
            {
                var toast = new ToastForm(captured, 8, null);
                toast.Location = new Point(-30000, -30000);
                toast.Show();
                Application.DoEvents();
                using (var tb = new Bitmap(toast.Width, toast.Height))
                {
                    toast.DrawToBitmap(tb, new Rectangle(0, 0, toast.Width, toast.Height));
                    using (var g = Graphics.FromImage(shot))
                    {
                        int x = ShotW - tb.Width - 28, y = ShotH - tb.Height - 28;
                        for (int i = 10; i >= 1; i--)
                            using (var sh = new SolidBrush(Color.FromArgb(6, 0, 0, 0))) g.FillRectangle(sh, x - i, y - i + 4, tb.Width + i * 2, tb.Height + i * 2);
                        g.DrawImage(tb, x, y);
                    }
                }
                toast.Dispose();
                Save(shot, path);
            }
        }

        // ------------------------------------------------------------ 5. 快捷鍵設定

        private static void ShotHotkeys(string path)
        {
            var hf = new QuietHotkeys(k => true);
            hf.StartPosition = FormStartPosition.Manual;
            hf.Location = new Point(-30000, -30000);
            hf.Show();
            Application.DoEvents();
            using (var client = ClientShot(hf))
            using (var shot = new Bitmap(ShotW, ShotH))
            {
                using (var g = Graphics.FromImage(shot))
                {
                    Backdrop(g);
                    int s = 1;
                    if (client.Width < 900) s = 2; // 對話框比較小，放大一點比較看得清楚
                    var rc = new Rectangle((ShotW - client.Width * s) / 2, (ShotH - client.Height * s) / 2 + 17, client.Width * s, client.Height * s);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Window(g, rc, "快捷鍵設定", client, true);
                }
                Save(shot, path);
            }
            hf.Dispose();
        }
    }
}
