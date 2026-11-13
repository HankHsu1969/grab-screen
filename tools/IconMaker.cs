using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;

// 編譯時用來產生 app.ico（多種尺寸的 PNG 圖示）
public static class IconMaker
{
    public static void Make(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (var bmp = GrabScreen.Icons.AppBitmap(s))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        }
        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
    }
}
