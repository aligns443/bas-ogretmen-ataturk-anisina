using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Konsola "yarım blok" (▀) karakterleriyle 24-bit renkli çizim yapar.
/// Her karakter hücresi dikeyde 2 piksel taşır: üst piksel = yazı rengi, alt piksel = arka plan rengi.
/// </summary>
static class Konsol
{
    const string Esc = "\u001b[";
    static readonly StreamWriter Cikis = new(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16) { AutoFlush = false };

    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint mode);

    /// <summary>Eski Windows konsolunda (conhost) da ANSI renk kodlarını açar.</summary>
    public static void AnsiEtkinlestir()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var h = GetStdHandle(-11);
        if (GetConsoleMode(h, out uint mod))
            SetConsoleMode(h, mod | 0x0004 /* ENABLE_VIRTUAL_TERMINAL_PROCESSING */);
    }

    public static int PencereGenisligi()
    {
        try { int w = Console.WindowWidth; return w >= 40 ? w : 120; }
        catch { return 120; }
    }

    public static void Yaz(string metin = "") { Cikis.Write(metin + "\n"); Cikis.Flush(); }

    public static void Ortala(string metin, Rgb renk, bool kalin = false)
    {
        int bosluk = Math.Max(0, (PencereGenisligi() - metin.Length) / 2);
        Yaz(new string(' ', bosluk) + (kalin ? Esc + "1m" : "") + On(renk) + metin + Esc + "0m");
    }

    static string On(Rgb c) => $"{Esc}38;2;{c.R};{c.G};{c.B}m";
    static string Arka(Rgb c) => $"{Esc}48;2;{c.R};{c.G};{c.B}m";

    /// <summary>Piksel tuvalini satır satır (animasyonlu) çizer. null = şeffaf.</summary>
    public static void Ciz(Rgb?[,] tuval, int sol, int gecikmeMs)
    {
        int w = tuval.GetLength(0), h = tuval.GetLength(1);
        var sb = new StringBuilder();
        for (int y = 0; y < h; y += 2)
        {
            sb.Clear().Append(' ', Math.Max(0, sol));
            for (int x = 0; x < w; x++)
            {
                Rgb? ust = tuval[x, y];
                Rgb? alt = y + 1 < h ? tuval[x, y + 1] : null;

                if (ust is null && alt is null) sb.Append(Esc).Append("0m ");
                else if (alt is null) sb.Append(Esc).Append("0m").Append(On(ust!.Value)).Append('▀');
                else if (ust is null) sb.Append(Esc).Append("0m").Append(On(alt.Value)).Append('▄');
                else sb.Append(On(ust.Value)).Append(Arka(alt.Value)).Append('▀');
            }
            sb.Append(Esc).Append("0m\n");
            Cikis.Write(sb);
            Cikis.Flush();
            if (gecikmeMs > 0) Thread.Sleep(gecikmeMs);
        }
    }
}
