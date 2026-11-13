using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>
    /// 自動測試：不動到真的滑鼠鍵盤，直接在程式內部模擬操作，把結果存成圖片檢查。
    /// 用法：selftest.exe 測試圖片.png 輸出資料夾
    /// </summary>
    internal static class SelfTest
    {
        private static void Call(Control c, string method, EventArgs e)
        {
            typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[] { e });
        }

        private static void Mouse(Control c, string method, Point p, MouseButtons b)
        {
            Call(c, method, new MouseEventArgs(b, method == "OnMouseDoubleClick" ? 2 : 1, p.X, p.Y, 0));
        }

        private static Point P(EditorCanvas cv, float x, float y)
        {
            var s = cv.ToScreen(new PointF(x, y));
            return new Point((int)s.X, (int)s.Y);
        }

        private static void Drag(EditorCanvas cv, float x1, float y1, float x2, float y2)
        {
            Mouse(cv, "OnMouseDown", P(cv, x1, y1), MouseButtons.Left);
            for (int i = 1; i <= 10; i++) Mouse(cv, "OnMouseMove", P(cv, x1 + (x2 - x1) * i / 10, y1 + (y2 - y1) * i / 10), MouseButtons.Left);
            Mouse(cv, "OnMouseUp", P(cv, x2, y2), MouseButtons.Left);
        }

        private static void Click(EditorCanvas cv, float x, float y)
        {
            Mouse(cv, "OnMouseDown", P(cv, x, y), MouseButtons.Left);
            Mouse(cv, "OnMouseUp", P(cv, x, y), MouseButtons.Left);
        }

        private static void Snap(Control c, string path)
        {
            using (var bmp = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static void Check(bool ok, string name)
        {
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        }

        private static void ProbeAll()
        {
            var probe = new ElementProbe();
            var done = new AutoResetEvent(false);
            List<ElementBox> got = null;
            probe.Result += (id, hw, list) => { got = list; done.Set(); };
            int req = 0;
            foreach (var w in WindowFinder.GetTopLevelWindows())
            {
                var sb = new System.Text.StringBuilder(128);
                Native.GetClassName(w.Handle, sb, 128);
                Console.WriteLine("視窗「" + w.Title + "」 class=" + sb + " " + w.Bounds + " ex=0x" + Native.GetWindowLong(w.Handle, Native.GWL_EXSTYLE).ToString("X"));
                var b = w.Bounds;
                foreach (var pt in new[] { new Point(b.X + b.Width / 2, b.Y + b.Height / 2), new Point(b.X + b.Width / 4, b.Y + b.Height / 3), new Point(b.X + b.Width - 60, b.Y + 15), new Point(b.X + b.Width * 2 / 3, b.Y + 12) })
                {
                    Console.WriteLine("  點 " + pt + " 標題列？" + (WindowFinder.IsOnTitleBar(w.Handle, pt, w.Bounds) ? "是" : "否"));
                    got = null;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    probe.Request(++req, w.Handle, w.Bounds, pt);
                    done.WaitOne(10000);
                    Console.WriteLine("  點 " + pt + " → " + (got == null ? "逾時" : got.Count + " 層") + "，" + sw.ElapsedMilliseconds + " ms");
                    if (got != null) foreach (var eb in got) Console.WriteLine("     └ " + eb.Label + " " + eb.Bounds);
                }
            }
            probe.Dispose();
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Dpi.MakeProcessDpiAware();
            Application.EnableVisualStyles();
            if (args.Length > 0 && args[0] == "--probe") { ProbeAll(); return; }
            string img = args[0], outDir = args[1];
            Directory.CreateDirectory(outDir);
            Bitmap src;
            using (var i = Image.FromFile(img)) src = new Bitmap(i);

            // ---------------- 編輯器 ----------------
            var form = new EditorForm((Bitmap)src.Clone());
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-30000, -30000);
            form.Size = new Size(1500, 950);
            var h = form.Handle;
            form.PerformLayout();
            var cv = (EditorCanvas)typeof(EditorForm).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            cv.FitToWindow();

            cv.Tool = Tool.Rect; Drag(cv, 640, 330, 960, 380);
            cv.Tool = Tool.Arrow; Drag(cv, 300, 150, 620, 330);
            cv.Tool = Tool.Step; Click(cv, 820, 225); Click(cv, 820, 680);
            cv.Tool = Tool.Text; Click(cv, 120, 300);
            var tbField = typeof(EditorCanvas).GetField("textBox", BindingFlags.Instance | BindingFlags.NonPublic);
            var tb = (TextBox)tbField.GetValue(cv);
            Check(tb != null, "文字工具會出現輸入框");
            if (tb != null) tb.Text = "這裡要注意！\nSecond line";
            cv.CommitText();
            cv.ApplyColor(Color.FromArgb(30, 136, 229));
            cv.Tool = Tool.Mosaic; Drag(cv, 70, 80, 260, 120);
            cv.ApplyColor(Color.FromArgb(253, 216, 53));
            cv.Tool = Tool.Highlighter; Drag(cv, 560, 520, 840, 520);
            cv.ApplyColor(Color.FromArgb(67, 160, 71));
            cv.ApplyLevel(3);
            cv.Tool = Tool.Ellipse; Drag(cv, 900, 600, 1100, 700);
            cv.ApplyLevel(1);
            cv.ApplyColor(Color.FromArgb(142, 36, 170));
            cv.Tool = Tool.Pen; Drag(cv, 300, 650, 500, 760);
            cv.Tool = Tool.Line; Drag(cv, 1000, 150, 1300, 200);
            Check(cv.Shapes.Count == 10, "畫了 10 個標示 (實際 " + cv.Shapes.Count + ")");
            Check(cv.NextStep == 3, "編號自動 +1");

            using (var r = cv.RenderFinal()) r.Save(Path.Combine(outDir, "editor_result.png"));
            Snap(form, Path.Combine(outDir, "editor_ui.png"));

            // 選取並移動方框
            cv.Tool = Tool.Select;
            var rect = cv.Shapes[0] as RectShape;
            float beforeX = rect.P1.X;
            Drag(cv, 640, 355, 700, 420); // 點在方框左邊線上拖曳
            Check(cv.Selected == rect && Math.Abs(rect.P1.X - beforeX - 60) < 2, "選取工具可以拖曳移動方框");
            Snap(form, Path.Combine(outDir, "editor_select.png"));
            // 拖曳控制點縮放
            var hs = rect.GetHandles();
            Drag(cv, hs[2].X, hs[2].Y, hs[2].X + 80, hs[2].Y + 40);
            Check(Math.Abs(rect.Rect.Right - (hs[2].X + 80)) < 2, "拖曳控制點可以改變大小");
            // 復原 / 重做
            cv.Undo(); cv.Undo();
            rect = cv.Shapes[0] as RectShape;
            Check(Math.Abs(rect.P1.X - beforeX) < 0.01, "復原兩次回到原位置");
            cv.Redo();
            Check(Math.Abs(((RectShape)cv.Shapes[0]).P1.X - beforeX - 60) < 2, "重做");
            // 刪除
            cv.Tool = Tool.Select; Click(cv, 820, 680);
            Check(cv.Selected is StepShape, "點選編號圓圈");
            cv.DeleteSelected();
            Check(cv.Shapes.Count == 9, "刪除選取的標示");
            // 裁切
            int n = cv.Shapes.Count;
            cv.Tool = Tool.Crop; Drag(cv, 50, 50, 1150, 800);
            Check(cv.BaseImage.Width == 1100 && cv.BaseImage.Height == 750, "裁切後尺寸 1100x750 (實際 " + cv.BaseImage.Width + "x" + cv.BaseImage.Height + ")");
            using (var r = cv.RenderFinal()) r.Save(Path.Combine(outDir, "editor_cropped.png"));
            cv.Undo();
            Check(cv.BaseImage.Width == src.Width, "裁切可以復原");
            // 調整尺寸
            var r0 = (RectShape)cv.Shapes[0];
            float rx = r0.P1.X;
            cv.ResizeImage(src.Width / 2, src.Height / 2);
            Check(cv.BaseImage.Width == src.Width / 2 && cv.BaseImage.Height == src.Height / 2, "調整尺寸成 50% (" + cv.BaseImage.Width + "x" + cv.BaseImage.Height + ")");
            var r1 = (RectShape)cv.Shapes[0];
            Check(Math.Abs(r1.P1.X - rx / 2) < 0.5f && Math.Abs(r1.Scale - 0.5f) < 0.01f, "標示的位置和粗細跟著縮放");
            using (var r = cv.RenderFinal()) r.Save(Path.Combine(outDir, "editor_resized.png"));
            cv.Undo();
            Check(cv.BaseImage.Width == src.Width && Math.Abs(((RectShape)cv.Shapes[0]).Scale - 1f) < 0.01f, "調整尺寸可以復原");
            cv.ResizeImage(src.Width * 2, src.Height * 2);
            Check(cv.BaseImage.Width == src.Width * 2, "放大成 200%");
            cv.Undo();
            cv.Dirty = false;
            form.Dispose();

            // ---------------- 擷取畫面：框選模式 ----------------
            var vs = new Rectangle(0, 0, src.Width, src.Height);
            var ov = new CaptureOverlay((Bitmap)src.Clone(), vs, CaptureMode.Region, null);
            ov.ShowInTaskbar = false;
            h = ov.Handle;
            ov.Bounds = vs;
            Mouse(ov, "OnMouseDown", new Point(300, 250), MouseButtons.Left);
            Mouse(ov, "OnMouseMove", new Point(500, 400), MouseButtons.Left);
            Mouse(ov, "OnMouseMove", new Point(760, 520), MouseButtons.Left);
            Snap(ov, Path.Combine(outDir, "overlay_region.png"));
            Mouse(ov, "OnMouseUp", new Point(760, 520), MouseButtons.Left);
            Check(ov.Result != null && ov.Result.Width == 461 && ov.Result.Height == 271, "框選擷取尺寸正確 (" + (ov.Result == null ? "null" : ov.Result.Width + "x" + ov.Result.Height) + ")");
            ov.Dispose();

            // ---------------- 擷取畫面：視窗/物件模式 ----------------
            var fake = new List<WindowInfo>
            {
                new WindowInfo { Handle = new IntPtr(1), Bounds = new Rectangle(120, 90, 700, 480), Title = "A" },
                new WindowInfo { Handle = new IntPtr(2), Bounds = new Rectangle(0, 0, 1400, 850), Title = "B" }
            };
            ov = new CaptureOverlay((Bitmap)src.Clone(), vs, CaptureMode.Window, fake);
            ov.ShowInTaskbar = false;
            h = ov.Handle;
            ov.Bounds = vs;
            Mouse(ov, "OnMouseMove", new Point(400, 300), MouseButtons.None);
            Snap(ov, Path.Combine(outDir, "overlay_window.png"));
            Mouse(ov, "OnMouseDown", new Point(400, 300), MouseButtons.Left);
            Mouse(ov, "OnMouseUp", new Point(400, 300), MouseButtons.Left);
            Check(ov.Result != null && ov.Result.Width == 700 && ov.Result.Height == 480, "點一下擷取滑鼠下的視窗");
            ov.Dispose();

            // ---------------- 物件偵測（只讀取，不操作）----------------
            var wins = WindowFinder.GetTopLevelWindows();
            Console.WriteLine("偵測到可見視窗數：" + wins.Count);
            WindowInfo target = null;
            foreach (var w in wins)
                if (w.Title.Length > 0 && w.Bounds.Width > 500 && w.Bounds.Height > 400) { target = w; break; }
            if (target != null)
            {
                Console.WriteLine("測試視窗：" + target.Title + " " + target.Bounds);
                var done = new ManualResetEvent(false);
                List<ElementBox> got = null;
                var probe = new ElementProbe();
                probe.Result += (id, hw, list) => { got = list; done.Set(); };
                var pt = new Point(target.Bounds.X + target.Bounds.Width / 3, target.Bounds.Y + 60);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                probe.Request(1, target.Handle, target.Bounds, pt);
                bool ok = done.WaitOne(8000);
                Console.WriteLine("物件偵測耗時 " + sw.ElapsedMilliseconds + " ms");
                Check(ok && got != null, "UI 自動化可以找到滑鼠下的物件");
                if (got != null) foreach (var eb in got) Console.WriteLine("   └ " + eb.Label + " " + eb.Bounds);
                // 第二次（有快取）應該很快
                done.Reset(); sw.Restart();
                probe.Request(2, target.Handle, target.Bounds, new Point(pt.X + 3, pt.Y + 2));
                done.WaitOne(8000);
                Console.WriteLine("第二次偵測耗時 " + sw.ElapsedMilliseconds + " ms");
                probe.Dispose();
            }

            // ---------------- 提示小視窗、設定視窗 ----------------
            var toast = new ToastForm(src, 8, null);
            toast.Location = new Point(-30000, -30000);
            toast.Show(); // ToastForm 本身就不會搶焦點
            Application.DoEvents();
            Snap(toast, Path.Combine(outDir, "toast.png"));
            toast.Dispose();
            var sf = new QuietSettings();
            sf.StartPosition = FormStartPosition.Manual;
            sf.Location = new Point(-30000, -30000);
            sf.ShowInTaskbar = false;
            sf.Show();
            Application.DoEvents();
            Snap(sf, Path.Combine(outDir, "settings.png"));
            sf.Dispose();

            // 調整尺寸視窗：輸入 50% 後寬高要自動算好
            var rd = new QuietResize(1400, 850);
            rd.StartPosition = FormStartPosition.Manual;
            rd.Location = new Point(-30000, -30000);
            rd.Show();
            Application.DoEvents();
            var tbPct = (TextBox)typeof(ResizeDialog).GetField("tbPct", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rd);
            var tbW = (TextBox)typeof(ResizeDialog).GetField("tbW", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rd);
            var tbH = (TextBox)typeof(ResizeDialog).GetField("tbH", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rd);
            tbPct.Text = "50";
            Check(tbW.Text == "700" && tbH.Text == "425", "輸入 50% 自動算出 700 × 425 (" + tbW.Text + " × " + tbH.Text + ")");
            tbW.Text = "1920";
            Check(tbH.Text == "1166", "改寬度 1920，高度維持比例變 1166 (" + tbH.Text + ")");
            Application.DoEvents();
            Snap(rd, Path.Combine(outDir, "resize.png"));
            rd.Dispose();

            // 快捷鍵設定視窗：假裝 Ctrl+Alt+3 被別的程式占用
            var hf = new QuietHotkeys(k => k != (Keys.Control | Keys.Alt | Keys.D3));
            hf.StartPosition = FormStartPosition.Manual;
            hf.Location = new Point(-30000, -30000);
            hf.Show();
            Application.DoEvents();
            Snap(hf, Path.Combine(outDir, "hotkeys.png"));
            var save = (Button)typeof(HotkeyForm).GetField("btnSave", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hf);
            Check(!save.Enabled, "有快捷鍵被占用時不能儲存");
            hf.Dispose();

            foreach (int sz in new[] { 16, 32, 256 })
                using (var ib = Icons.AppBitmap(sz)) ib.Save(Path.Combine(outDir, "icon" + sz + ".png"));
            Console.WriteLine("DONE");
        }

        private class QuietSettings : SettingsForm
        {
            public QuietSettings() : base(new Settings(), null) { }
            protected override bool ShowWithoutActivation { get { return true; } }
        }

        private class QuietResize : ResizeDialog
        {
            public QuietResize(int w, int h) : base(w, h) { }
            protected override bool ShowWithoutActivation { get { return true; } }
        }

        private class QuietHotkeys : HotkeyForm
        {
            public QuietHotkeys(Func<Keys, bool> check) : base(new Settings(), check) { TopMost = false; }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override void OnShown(EventArgs e) { } // 測試時不要搶走焦點
        }
    }
}
