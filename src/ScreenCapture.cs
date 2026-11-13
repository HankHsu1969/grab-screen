using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GrabScreen
{
    /// <summary>抓螢幕畫面、放進剪貼簿、存檔。</summary>
    internal static class ScreenCapture
    {
        /// <summary>所有螢幕合起來的範圍（多螢幕時會包含每一台）。</summary>
        public static Rectangle VirtualScreen
        {
            get { return SystemInformation.VirtualScreen; }
        }

        /// <summary>滑鼠目前所在的那一台螢幕。</summary>
        public static Rectangle CurrentMonitor
        {
            get { return Screen.FromPoint(Cursor.Position).Bounds; }
        }

        public static Bitmap CaptureRect(Rectangle r)
        {
            var bmp = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(r.Left, r.Top, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        /// <summary>從一張大圖裡切出一塊（座標是大圖內的像素座標）。</summary>
        public static Bitmap Crop(Bitmap source, Rectangle r)
        {
            r.Intersect(new Rectangle(0, 0, source.Width, source.Height));
            if (r.Width < 1 || r.Height < 1) r = new Rectangle(0, 0, 1, 1);
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(source, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        /// <summary>把圖片放進剪貼簿（同時放一般圖片與 PNG 兩種格式，貼到各種軟體都比較穩）。</summary>
        public static bool CopyToClipboard(Bitmap bmp)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.Bitmap, true, bmp);
                    var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);
                    data.SetData("PNG", false, ms);
                    Clipboard.SetDataObject(data, true, 5, 100);
                    return true;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
            return false;
        }

        public static string DefaultFileName()
        {
            return "截圖_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        }

        public static void SaveImage(Bitmap bmp, string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            ImageFormat fmt = ImageFormat.Png;
            if (ext == ".jpg" || ext == ".jpeg") fmt = ImageFormat.Jpeg;
            else if (ext == ".bmp") fmt = ImageFormat.Bmp;
            if (fmt == ImageFormat.Jpeg)
            {
                // JPG 不支援透明，先鋪白底
                using (var flat = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(flat))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                    }
                    flat.Save(path, fmt);
                }
            }
            else
            {
                bmp.Save(path, fmt);
            }
        }

        /// <summary>跳出「另存新檔」視窗，成功回傳檔案路徑，取消回傳 null。</summary>
        public static string SaveWithDialog(Bitmap bmp, IWin32Window owner)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "另存圖片";
                dlg.Filter = "PNG 圖片 (*.png)|*.png|JPG 圖片 (*.jpg)|*.jpg|BMP 圖片 (*.bmp)|*.bmp";
                dlg.FileName = DefaultFileName();
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                try
                {
                    SaveImage(bmp, dlg.FileName);
                    return dlg.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(owner, "存檔失敗：" + ex.Message, "GrabScreen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }
        }
    }
}
