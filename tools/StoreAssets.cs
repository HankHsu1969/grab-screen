using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 產生 Microsoft Store 套件需要的各種尺寸圖示（星方向 LOGO，透明背景）
public static class StoreAssets
{
    public static void Make(string dir)
    {
        Directory.CreateDirectory(dir);
        int[] scales = { 100, 125, 150, 200, 400 };
        foreach (int s in scales)
        {
            Save(dir, "StoreLogo.scale-" + s, Scale(50, s), Scale(50, s), 0.92f);
            Save(dir, "Square44x44Logo.scale-" + s, Scale(44, s), Scale(44, s), 0.92f);
            Save(dir, "Square150x150Logo.scale-" + s, Scale(150, s), Scale(150, s), 0.62f);
            Save(dir, "Wide310x150Logo.scale-" + s, Scale(310, s), Scale(150, s), 0.62f);
        }
        // 工作列、開始功能表清單用的固定尺寸（unplated＝不加底色）
        foreach (int t in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
        {
            Save(dir, "Square44x44Logo.targetsize-" + t, t, t, 1f);
            Save(dir, "Square44x44Logo.targetsize-" + t + "_altform-unplated", t, t, 1f);
        }
    }

    private static int Scale(int v, int pct) { return (v * pct + 50) / 100; }

    /// <param name="fill">星星佔畫面高度的比例</param>
    private static void Save(string dir, string name, int w, int h, float fill)
    {
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int side = (int)System.Math.Round(System.Math.Min(w, h) * fill);
                if (side < 1) side = 1;
                using (var star = GrabScreen.Icons.AppBitmap(side))
                    g.DrawImage(star, (w - side) / 2, (h - side) / 2, side, side);
            }
            bmp.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
        }
    }
}
