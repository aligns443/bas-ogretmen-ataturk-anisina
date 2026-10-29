// 29 Ekim Cumhuriyet Bayramı – Konsolda Atatürk portresi ve
// içi Atatürk'ün bakışlarıyla dolu Türkiye haritası.
//
// Çalıştırma:   dotnet run
// Seçenekler:   dotnet run -- --genislik 140     (çizim genişliği, karakter)
//               dotnet run -- --png onizleme.png (konsol yerine PNG önizleme kaydeder)
//               dotnet run -- --hizli            (animasyonsuz)

using System.Drawing;

int pencere = Konsol.PencereGenisligi();
int? istenenGenislik = null;
string? pngYolu = null;
bool hizli = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--genislik" && i + 1 < args.Length) istenenGenislik = int.Parse(args[++i]);
    else if (args[i] == "--png" && i + 1 < args.Length) pngYolu = args[++i];
    else if (args[i] == "--hizli") hizli = true;
}
if (istenenGenislik is int g) pencere = g + 2;

using var foto = Goruntu.Yukle();
var bayrakKirmizisi = new Rgb(227, 10, 23);

var portre = PortreOlustur(foto, Math.Min(pencere - 2, 110));
var harita = HaritaOlustur(foto, Math.Min(pencere - 2, 150));

if (pngYolu is not null)
{
    Goruntu.PngKaydet(pngYolu, portre, harita);
    Console.WriteLine($"Önizleme kaydedildi: {Path.GetFullPath(pngYolu)}");
    return;
}

Konsol.AnsiEtkinlestir();
try { Console.Clear(); Console.CursorVisible = false; } catch { }

Konsol.Yaz();
Konsol.Ortala($"★  29 EKİM 1923  ★  CUMHURİYETİMİZ {DateTime.Now.Year - 1923} YAŞINDA  ★", bayrakKirmizisi, kalin: true);
Konsol.Yaz();
Konsol.Ciz(portre, (pencere - portre.GetLength(0)) / 2, hizli ? 0 : 35);
Konsol.Yaz();
Konsol.Ciz(harita, (pencere - harita.GetLength(0)) / 2, hizli ? 0 : 60);
Konsol.Yaz();
Konsol.Ortala("\"Yurtta sulh, cihanda sulh.\"", new Rgb(235, 235, 235), kalin: true);
Konsol.Ortala("— Mustafa Kemal Atatürk", new Rgb(160, 160, 160));
Konsol.Yaz();
try { Console.CursorVisible = true; } catch { }

// ---------------------------------------------------------------------------

// Baş ve omuzlar; yüz büyük olsun diye fotoğrafın üst-orta kısmı kırpılır.
static Rgb?[,] PortreOlustur(Bitmap foto, int w)
{
    var kirp = new Rectangle(150, 0, 440, 470);
    int h = (int)Math.Round(w * (double)kirp.Height / kirp.Width);
    h += h % 2;

    var gri = Goruntu.GriTon(foto, kirp, w, h);
    Goruntu.Seviyele(gri, 0.005, 0.995, gama: 0.95);
    gri = Goruntu.Keskinlestir(gri, 0.9);

    var tuval = new Rgb?[w, h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            tuval[x, y] = Rgb.Sepya(gri[x, y]);
    return tuval;
}

// Türkiye silüeti; içi Atatürk'ün göz bölgesiyle doldurulur,
// kenarlar bayrak kırmızısı çerçeve ve içe doğru yumuşak kırmızı gölgeyle biter.
Rgb?[,] HaritaOlustur(Bitmap foto, int w)
{
    double oran = (Harita.MaxBoylam - Harita.MinBoylam) * Math.Cos(39 * Math.PI / 180)
                  / (Harita.MaxEnlem - Harita.MinEnlem);
    int h = (int)Math.Round(w / oran);
    h += h % 2;

    // 1) Hangi pikseller Türkiye'nin içinde?
    var ic = new bool[w, h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            double boylam = Harita.MinBoylam + (x + 0.5) / w * (Harita.MaxBoylam - Harita.MinBoylam);
            double enlem = Harita.MaxEnlem - (y + 0.5) / h * (Harita.MaxEnlem - Harita.MinEnlem);
            ic[x, y] = Harita.Icinde(boylam, enlem);
        }

    // İç boşlukları analiz et: Marmara Denizi'ni koru, sadece küçük raster boşluklarını/noktaları doldur
    var ziyaret = new bool[w, h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (ic[x, y] || ziyaret[x, y]) continue;

            var kume = new List<(int X, int Y)>();
            var kQ = new Queue<(int X, int Y)>();
            ziyaret[x, y] = true;
            kQ.Enqueue((x, y));
            bool kenaraDegiyor = false;

            while (kQ.Count > 0)
            {
                var (cx, cy) = kQ.Dequeue();
                kume.Add((cx, cy));
                if (cx == 0 || cx == w - 1 || cy == 0 || cy == h - 1)
                    kenaraDegiyor = true;

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) != 1) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && !ic[nx, ny] && !ziyaret[nx, ny])
                        {
                            ziyaret[nx, ny] = true;
                            kQ.Enqueue((nx, ny));
                        }
                    }
            }

            // Kenara değmeyen ve küçük olan raster hatalarını/lekeleri doldur (kume.Count < 25)
            // Marmara Denizi (~60+ piksel) böylece olduğu gibi doğal haliyle kalır!
            if (!kenaraDegiyor && kume.Count < 25)
            {
                foreach (var (kx, ky) in kume)
                    ic[kx, ky] = true;
            }
        }

    // 2) Göz bölgesi: Kaşlar, bakışlar ve göz bebeklerini haritaya tam oturtacak hassas kırpma
    var gozKirp = new Rectangle(280, 110, 300, 130);
    var goz = Goruntu.GriTon(foto, gozKirp, w, h);
    Goruntu.Seviyele(goz, 0.02, 0.98, gama: 1.05);
    goz = Goruntu.Keskinlestir(goz, 1.4);

    // 4) Renklendir: Haritanın tamamını pürüzsüz, kırmızı leke veya çerçeve olmadan
    // doğrudan Atatürk'ün bakışlarıyla doldur
    var tuval = new Rgb?[w, h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!ic[x, y]) continue;
            tuval[x, y] = Rgb.Sepya(goz[x, y]);
        }
    return tuval;
}
