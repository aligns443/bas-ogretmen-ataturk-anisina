using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

/// <summary>24-bit RGB renk.</summary>
readonly record struct Rgb(byte R, byte G, byte B)
{
    static byte B8(double v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);

    /// <summary>Hafif sıcak tonlu (eski fotoğraf hissi veren) gri.</summary>
    public static Rgb Sepya(double v) => new(B8(v), B8(v * 0.97), B8(v * 0.91));

    public static Rgb Karistir(Rgb a, Rgb b, double t) => new(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}

/// <summary>Fotoğraf yükleme ve basit görüntü işleme.</summary>
static class Goruntu
{
    /// <summary>exe'ye gömülü ataturk.jpg dosyasını yükler.</summary>
    public static Bitmap Yukle()
    {
        using var akis = typeof(Goruntu).Assembly.GetManifestResourceStream("ataturk.jpg")
            ?? throw new FileNotFoundException("Gömülü ataturk.jpg bulunamadı.");
        using var gecici = new Bitmap(akis);
        return new Bitmap(gecici); // akıştan bağımsız kopya
    }

    /// <summary>Fotoğrafın bir bölgesini kırpar, w×h boyutuna yüksek kaliteyle küçültür ve parlaklık (0..1) dizisi döndürür.</summary>
    public static double[,] GriTon(Bitmap kaynak, Rectangle kirp, int w, int h)
    {
        using var hedef = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(hedef))
        using (var ayar = new ImageAttributes())
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            ayar.SetWrapMode(WrapMode.TileFlipXY); // kenarlarda koyu çizgi oluşmasın
            g.DrawImage(kaynak, new Rectangle(0, 0, w, h), kirp.X, kirp.Y, kirp.Width, kirp.Height, GraphicsUnit.Pixel, ayar);
        }

        var veri = hedef.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        var baytlar = new byte[veri.Stride * h];
        Marshal.Copy(veri.Scan0, baytlar, 0, baytlar.Length);
        hedef.UnlockBits(veri);

        var sonuc = new double[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * veri.Stride + x * 3; // BGR sırası
                sonuc[x, y] = (0.114 * baytlar[i] + 0.587 * baytlar[i + 1] + 0.299 * baytlar[i + 2]) / 255.0;
            }
        return sonuc;
    }

    /// <summary>Kontrastı açar: en karanlık %alt siyah, en parlak %üst beyaz olur.</summary>
    public static void Seviyele(double[,] a, double alt, double ust, double gama = 1.0)
    {
        var tumu = a.Cast<double>().Order().ToArray();
        double lo = tumu[(int)(alt * (tumu.Length - 1))];
        double hi = tumu[(int)(ust * (tumu.Length - 1))];
        for (int x = 0; x < a.GetLength(0); x++)
            for (int y = 0; y < a.GetLength(1); y++)
                a[x, y] = Math.Pow(Math.Clamp((a[x, y] - lo) / (hi - lo), 0, 1), gama);
    }

    /// <summary>"Unsharp mask" ile keskinleştirme – gözler, kaşlar gibi ince detayları belirginleştirir.</summary>
    public static double[,] Keskinlestir(double[,] a, double miktar)
    {
        int w = a.GetLength(0), h = a.GetLength(1);
        var sonuc = new double[w, h];
        int[] k = [1, 2, 1];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double top = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        top += k[dx + 1] * k[dy + 1] * a[Math.Clamp(x + dx, 0, w - 1), Math.Clamp(y + dy, 0, h - 1)];
                double bulanik = top / 16.0;
                sonuc[x, y] = Math.Clamp(a[x, y] + miktar * (a[x, y] - bulanik), 0, 1);
            }
        return sonuc;
    }

    /// <summary>Tuvalleri alt alta PNG olarak kaydeder (önizleme / hata ayıklama için).</summary>
    public static void PngKaydet(string yol, params Rgb?[][,] tuvaller)
    {
        const int olcek = 4;
        int w = tuvaller.Max(t => t.GetLength(0)), h = tuvaller.Sum(t => t.GetLength(1) + 4);
        using var bmp = new Bitmap(w * olcek, h * olcek);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(12, 12, 12));
        int oy = 0;
        foreach (var t in tuvaller)
        {
            int ox = (w - t.GetLength(0)) / 2;
            for (int y = 0; y < t.GetLength(1); y++)
                for (int x = 0; x < t.GetLength(0); x++)
                    if (t[x, y] is Rgb c)
                    {
                        using var firca = new SolidBrush(Color.FromArgb(c.R, c.G, c.B));
                        g.FillRectangle(firca, (ox + x) * olcek, (oy + y) * olcek, olcek, olcek);
                    }
            oy += t.GetLength(1) + 4;
        }
        bmp.Save(yol, ImageFormat.Png);
    }
}
