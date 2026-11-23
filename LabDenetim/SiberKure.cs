// Bu dosya radarın ortasındaki dönen 3B siber küreyi GDI+ ile çizer (nokta matris kıtalar, radar halkaları, dönen radar kolu, hareketli yaylar).
// Dışarıdan hiçbir dosya/internet kaynağı kullanmaz; tüm veri kodun içindedir. Fareyle sürükleyerek çevrilebilir.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Dönen siber küre.
internal sealed class SiberKure
{
    // Küre açıları (derece): boylam dönüşü ve enlem eğimi. Fareyle değiştirilir.
    public float RotLon = -36f, TiltLat = 24f;
    // Kullanıcı küreyi sürüklüyor mu? (sürüklerken kendi kendine dönmez)
    public bool Suruklenior;
    // Küre üzerindeki hareketli yaylar BENZETİMDİR (dünya şehirleri arası rastgele); gerçek trafik değildir. Kapatmak için false yapın.
    public bool BenzetimYaylar = true;

    private float _zaman;
    private readonly Random _rnd = new();
    private readonly List<Yay> _yaylar = new(); private readonly List<Dalga> _dalgalar = new();
    private readonly (float X, float Y, float R, float Faz)[] _toz;

    public SiberKure()
    {
        _toz = Enumerable.Range(0, 34).Select(_ => ((float)_rnd.NextDouble(), (float)_rnd.NextDouble(), 0.8f + (float)_rnd.NextDouble() * 1.4f, (float)(_rnd.NextDouble() * Math.PI * 2))).ToArray();
        for (int i = 0; i < 14; i++) { Yay? y = YayUret(false); if (y != null) { y.Ilerleme = (float)_rnd.NextDouble(); _yaylar.Add(y); } }
    }

    // ---------- Veri ----------
    private const double D2R = Math.PI / 180.0;
    // Sadeleştirilmiş kıta çokgenleri: [enlem, boylam] çiftleri art arda.
    private static readonly double[][] Kitalar =
    {
        new double[] { 36, -9, 43, -9, 48, -5, 51, 2, 54, 8, 58, 5, 63, 5, 70, 20, 70, 31, 65, 40, 60, 30, 55, 21, 50, 30, 46, 31, 42, 28, 41, 26, 36, 27, 36, 36, 41, 42, 44, 39, 47, 39, 43, 47, 40, 49, 38, 44, 37, 36, 35, 24, 38, 21, 41, 19, 45, 13, 40, 18, 38, 15, 44, 9, 42, 3, 36, -5 },
        new double[] { 50, -5, 55, -5, 58, -4, 57, -2, 52, 2, 50, 0 },
        new double[] { 63, -24, 66, -24, 66, -14, 63, -14 },
        new double[] { 68, 35, 73, 70, 75, 105, 71, 140, 66, 175, 60, 165, 54, 140, 43, 132, 38, 128, 35, 126, 39, 121, 31, 122, 22, 114, 16, 108, 10, 106, 1, 103, 7, 98, 16, 97, 22, 90, 16, 81, 8, 77, 22, 69, 25, 62, 26, 56, 30, 49, 37, 45, 41, 42, 45, 48, 52, 55, 58, 60, 65, 45 },
        new double[] { 30, 35, 32, 40, 29, 48, 22, 59, 13, 44, 16, 42, 28, 35 },
        new double[] { 31, 130, 35, 136, 40, 141, 44, 145, 43, 140, 37, 137, 33, 130 },
        new double[] { -6, 105, -8, 116, -7, 120, -5, 112 },
        new double[] { 6, 120, 18, 120, 18, 123, 8, 126 },
        new double[] { 35, -6, 37, 10, 32, 15, 31, 32, 22, 37, 12, 44, 11, 51, 2, 45, -11, 40, -26, 33, -34, 20, -30, 17, -18, 12, -6, 12, 4, 9, 5, -4, 12, -17, 21, -17, 30, -10 },
        new double[] { -13, 49, -18, 50, -25, 47, -24, 44, -16, 45 },
        new double[] { 70, -162, 71, -130, 65, -95, 60, -65, 52, -56, 44, -66, 38, -75, 25, -80, 30, -85, 26, -97, 18, -95, 15, -84, 9, -79, 16, -98, 23, -106, 32, -117, 40, -124, 50, -127, 60, -142, 62, -165 },
        new double[] { 60, -45, 68, -54, 78, -68, 82, -35, 74, -18, 65, -38 },
        new double[] { 11, -75, 10, -62, 2, -50, -5, -35, -15, -39, -23, -43, -35, -53, -46, -65, -55, -68, -52, -75, -38, -73, -18, -71, -5, -81, 4, -77 },
        new double[] { -12, 131, -12, 142, -20, 149, -33, 152, -38, 145, -35, 137, -32, 126, -34, 115, -22, 114, -15, 124 },
    };
    // Küre üzerinde noktalar (kara olanlar): birim vektör ve "yerel bölge" işareti.
    private static readonly List<(double X, double Y, double Z, bool Yerel)> KaraNoktalari = KaraUret();
    private static readonly (string Ad, double Enlem, double Boylam)[] Merkezler =
    {
        ("Ankara",39.9,32.8),("İstanbul",41.0,29.0),("Frankfurt",50.1,8.6),("Londra",51.5,-0.1),("Moskova",55.7,37.6),("Amsterdam",52.3,4.9),("Dubai",25.2,55.3),("Mumbai",19.0,72.8),
        ("Pekin",39.9,116.4),("Tokyo",35.6,139.7),("Singapur",1.3,103.8),("New York",40.7,-74.0),("San Francisco",37.7,-122.4),("Sao Paulo",-23.5,-46.6),("Johannesburg",-26.2,28.0),("Sidney",-33.8,151.2)
    };

    private static bool Icinde(double enlem, double boylam, double[] c)
    {
        bool ic = false; int n = c.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double yi = c[2 * i], xi = c[2 * i + 1], yj = c[2 * j], xj = c[2 * j + 1];
            if (((yi > enlem) != (yj > enlem)) && (boylam < (xj - xi) * (enlem - yi) / ((yj - yi) == 0 ? 0.00001 : (yj - yi)) + xi)) ic = !ic;
        }
        return ic;
    }

    private static (double X, double Y, double Z) Vek(double enlem, double boylam)
    {
        double phi = (90 - enlem) * D2R, th = boylam * D2R;
        return (Math.Sin(phi) * Math.Sin(th), -Math.Cos(phi), Math.Sin(phi) * Math.Cos(th));
    }

    // Fibonacci küresi üzerinde dengeli örnekleme; kara çokgenlerinin içine düşenler tutulur.
    private static List<(double X, double Y, double Z, bool Yerel)> KaraUret()
    {
        var l = new List<(double X, double Y, double Z, bool Yerel)>(); const int toplam = 4200; double altin = Math.PI * (3 - Math.Sqrt(5));
        for (int i = 0; i < toplam; i++)
        {
            double y = 1 - i / (double)(toplam - 1) * 2, enlem = Math.Asin(y) / D2R, boylam = ((altin * i / D2R) % 360 + 540) % 360 - 180;
            if (!Kitalar.Any(c => Icinde(enlem, boylam, c))) continue;
            var v = Vek(enlem, boylam);
            l.Add((v.X, v.Y, v.Z, enlem >= 35 && enlem <= 43 && boylam >= 25 && boylam <= 45));
        }
        return l;
    }

    // ---------- Renk paleti (stil kılavuzu ile uyumlu) ----------
    private static Color H(string s) => ColorTranslator.FromHtml(s);
    private static Color A(int r, int g, int b, double a) => DesignTokens.Hesapla((int)Math.Round(Math.Clamp(a, 0, 1) * 255), r, g, b);
    private sealed class Pal
    {
        public Color KureIc, KureOrta, KureDis, Hale, Hale2, Kenar, Tel, Halka, Centik, NoktaArka, NoktaVurgu, SweepCizgi, Toz, Golge; public int[] Nokta = new int[3]; public Color[] Yay = Array.Empty<Color>();
    }
    private static readonly Pal Koyu = new()
    {
        KureIc = H("#182A3E"), KureOrta = H("#101B2D"), KureDis = H("#0A1120"), Hale = A(6, 182, 212, 0.22), Hale2 = A(99, 102, 241, 0.16), Kenar = A(99, 102, 241, 0.42), Tel = A(100, 148, 210, 0.12), Halka = A(129, 140, 248, 0.16),
        Centik = A(148, 163, 184, 0.28), NoktaArka = A(56, 189, 248, 0.11), Nokta = new[] { 72, 214, 188 }, NoktaVurgu = H("#38BDF8"), SweepCizgi = A(56, 189, 248, 0.85), Toz = A(148, 163, 184, 0.22), Golge = A(0, 0, 0, 0.45),
        Yay = new[] { H("#06B6D4"), H("#EC4899"), H("#6366F1"), H("#10B981"), H("#EF4444") }
    };
    private static readonly Pal Acik = new()
    {
        KureIc = H("#F9FCFF"), KureOrta = H("#EAF1FB"), KureDis = H("#D6E4F7"), Hale = A(99, 102, 241, 0.13), Hale2 = A(6, 182, 212, 0.10), Kenar = A(99, 102, 241, 0.34), Tel = A(99, 102, 241, 0.13), Halka = A(148, 163, 184, 0.34),
        Centik = A(100, 116, 139, 0.38), NoktaArka = A(100, 116, 139, 0.12), Nokta = new[] { 71, 85, 105 }, NoktaVurgu = H("#4F46E5"), SweepCizgi = A(79, 70, 229, 0.82), Toz = A(99, 102, 241, 0.14), Golge = A(15, 23, 42, 0.10),
        Yay = new[] { H("#4F46E5"), H("#0891B2"), H("#DB2777"), H("#059669"), H("#DC2626") }
    };

    // ---------- Hareketli yaylar ----------
    private sealed class Yay { public (double X, double Y, double Z) V1, V2; public double Yukseklik; public int Renk; public float Ilerleme, Hiz, Kuyruk; public bool Vurdu; }
    private sealed class Dalga { public (double X, double Y, double Z) V; public Color Renk; public float Yas; }

    private Yay? YayUret(bool taramada)
    {
        int a = _rnd.Next(Merkezler.Length), b = _rnd.Next(Merkezler.Length); if (a == b) b = (b + 1) % Merkezler.Length;
        if (taramada && _rnd.NextDouble() < 0.45) { b = _rnd.NextDouble() < 0.5 ? 0 : 1; if (a == b) return null; }
        var v1 = Vek(Merkezler[a].Enlem, Merkezler[a].Boylam); var v2 = Vek(Merkezler[b].Enlem, Merkezler[b].Boylam);
        double nokta = Math.Clamp(v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z, -1, 1), aci = Math.Acos(nokta);
        return new Yay { V1 = v1, V2 = v2, Yukseklik = 0.16 + Math.Min(0.36, aci * 0.18), Renk = _rnd.Next(5), Hiz = (taramada ? 0.011f : 0.0075f) + (float)_rnd.NextDouble() * 0.006f, Kuyruk = 0.32f + (float)_rnd.NextDouble() * 0.18f };
    }

    // Küre açılarına göre 3B noktayı ekrana yansıtır (Z > 0: ön yüz).
    private float _kosY = 1, _sinY, _kosX = 1, _sinX;
    private (float X, float Y, float Z) Yansit((double X, double Y, double Z) v, float cx, float cy, float r)
    {
        double x1 = v.X * _kosY + v.Z * _sinY, z1 = -v.X * _sinY + v.Z * _kosY, y1 = v.Y;
        double y2 = y1 * _kosX - z1 * _sinX, z2 = y1 * _sinX + z1 * _kosX;
        return ((float)(cx + x1 * r), (float)(cy + y2 * r), (float)z2);
    }

    // Kareyi çizer. dt: 60 kare/sn'ye göre geçen süre katsayısı (1 = 16,7 ms). supurmeDerece: radar kolunun açısı (+X ekseninden saat yönünde).
    public void Ciz(Graphics g, float cx, float cy, float r, float alanGenislik, float alanYukseklik, bool koyu, bool taramada, float supurmeDerece, float dt)
    {
        var P = koyu ? Koyu : Acik;
        _zaman += 0.016f * dt;
        if (!Suruklenior) RotLon = (RotLon + (taramada ? 0.24f : 0.11f) * dt) % 360f;
        _kosY = (float)Math.Cos(RotLon * D2R); _sinY = (float)Math.Sin(RotLon * D2R); _kosX = (float)Math.Cos(TiltLat * D2R); _sinX = (float)Math.Sin(TiltLat * D2R);
        var eskiSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;

        // A) Arka plan tozları.
        foreach (var t in _toz) { double al = 0.35 + 0.45 * Math.Sin(_zaman * 1.4 + t.Faz); using var b = new SolidBrush(DesignTokens.Saydam(P.Toz, (int)(P.Toz.A * Math.Clamp(al, 0, 1)))); g.FillEllipse(b, cx - alanGenislik / 2f + t.X * alanGenislik - t.R, cy - alanYukseklik / 2f + t.Y * alanYukseklik - t.R, 2 * t.R, 2 * t.R); }
        // B) Dış radar halkaları ve çentikler.
        HalkalariCiz(g, cx, cy, r, P);
        // C) Atmosfer halesi.
        {
            float R = r * 1.38f; using var yol = new GraphicsPath(); yol.AddEllipse(cx - R, cy - R, 2 * R, 2 * R);
            using var pg = new PathGradientBrush(yol) { CenterPoint = new PointF(cx, cy), SurroundColors = new[] { DesignTokens.Saydam(P.Hale2, 0) } };
            pg.InterpolationColors = new ColorBlend { Positions = new[] { 0f, 0.174f, 0.384f, 1f }, Colors = new[] { DesignTokens.Saydam(P.Hale2, 0), P.Hale2, P.Hale, P.Hale } };
            g.FillEllipse(pg, cx - R, cy - R, 2 * R, 2 * R);
        }
        // D) Gölge ve küre gövdesi.
        for (int i = 6; i >= 1; i--) { using var b = new SolidBrush(DesignTokens.Saydam(P.Golge, (int)(P.Golge.A / 9.0))); float k = i * (koyu ? 5f : 4f); g.FillEllipse(b, cx - r - k + 2, cy - r - k + (koyu ? 8 : 10), 2 * (r + k) - 4, 2 * (r + k) - 4); }
        using (var yol = new GraphicsPath())
        {
            yol.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
            using (var pg = new PathGradientBrush(yol) { CenterPoint = new PointF(cx - r * 0.32f, cy - r * 0.32f), SurroundColors = new[] { P.KureDis } })
            {
                pg.InterpolationColors = new ColorBlend { Positions = new[] { 0f, 0.38f, 1f }, Colors = new[] { P.KureDis, P.KureOrta, P.KureIc } };
                g.FillPath(pg, yol);
            }
            // E-G) Kürenin içinde: tel kafes, kıta noktaları, kenar ışığı.
            var eskiKirp = g.Clip; g.SetClip(yol, CombineMode.Intersect);
            TelKafes(g, cx, cy, r, P); Kitalari(g, cx, cy, r, P);
            using (var k = new Pen(P.Kenar, 1.4f)) g.DrawEllipse(k, cx - r + 0.5f, cy - r + 0.5f, 2 * r - 1, 2 * r - 1);
            g.Clip = eskiKirp;
        }
        // H) Dönen radar kolu.
        Supurme(g, cx, cy, r * 1.28f, supurmeDerece, koyu, P);
        // I) Hareketli yaylar ve çarpma dalgaları (benzetim).
        if (BenzetimYaylar) { YaylariCiz(g, cx, cy, r, P, koyu, taramada, dt); DalgalariCiz(g, cx, cy, r, dt); }
        g.SmoothingMode = eskiSm;
    }

    private static void HalkalariCiz(Graphics g, float cx, float cy, float r, Pal P)
    {
        float[] olcek = { 1.14f, 1.28f, 1.42f };
        for (int i = 0; i < olcek.Length; i++)
        {
            using var k = new Pen(P.Halka, i == 1 ? 1.15f : 0.85f); if (i == 0) k.DashPattern = new[] { 4f, 6f };
            g.DrawEllipse(k, cx - r * olcek[i], cy - r * olcek[i], 2 * r * olcek[i], 2 * r * olcek[i]);
        }
        float dis = r * 1.28f; using var c = new Pen(P.Centik, 1f);
        for (int d = 0; d < 360; d += 10)
        {
            double a = d * D2R; float u = d % 30 == 0 ? 7f : 3.5f;
            g.DrawLine(c, cx + (float)Math.Cos(a) * dis, cy + (float)Math.Sin(a) * dis, cx + (float)Math.Cos(a) * (dis + u), cy + (float)Math.Sin(a) * (dis + u));
        }
    }

    private void TelKafes(Graphics g, float cx, float cy, float r, Pal P)
    {
        using var k = new Pen(P.Tel, 0.8f); var seg = new List<PointF>();
        void Bitir() { if (seg.Count > 1) g.DrawLines(k, seg.ToArray()); seg.Clear(); }
        for (int boy = -180; boy < 180; boy += 30)
        {
            for (int en = -84; en <= 84; en += 6) { var p = Yansit(Vek(en, boy), cx, cy, r); if (p.Z > -0.05f) seg.Add(new PointF(p.X, p.Y)); else Bitir(); }
            Bitir();
        }
        for (int en = -60; en <= 60; en += 30)
        {
            for (int boy = -180; boy <= 180; boy += 6) { var p = Yansit(Vek(en, boy), cx, cy, r); if (p.Z > -0.05f) seg.Add(new PointF(p.X, p.Y)); else Bitir(); }
            Bitir();
        }
    }

    private void Kitalari(Graphics g, float cx, float cy, float r, Pal P)
    {
        float boy = Math.Max(1.25f, r * 0.0115f);
        var firca = new SolidBrush[12]; for (int i = 0; i < 12; i++) firca[i] = new SolidBrush(DesignTokens.Hesapla((int)(255 * Math.Min(1, 0.25 + (i + 0.5) / 12.0 * 0.72)), P.Nokta[0], P.Nokta[1], P.Nokta[2]));
        using var arka = new SolidBrush(P.NoktaArka); using var vurgu = new SolidBrush(P.NoktaVurgu);
        foreach (var d in KaraNoktalari)
        {
            var p = Yansit((d.X, d.Y, d.Z), cx, cy, r);
            if (p.Z < 0) { g.FillRectangle(arka, p.X - 0.7f, p.Y - 0.7f, 1.4f, 1.4f); continue; }
            if (d.Yerel) { float s = boy * 1.2f; g.FillEllipse(vurgu, p.X - s, p.Y - s, 2 * s, 2 * s); }
            else { float s = boy * (0.78f + p.Z * 0.38f); g.FillEllipse(firca[Math.Min(11, (int)(p.Z * 12))], p.X - s, p.Y - s, 2 * s, 2 * s); }
        }
        foreach (var f in firca) f.Dispose();
    }

    private static void Supurme(Graphics g, float cx, float cy, float sr, float bitisDerece, bool koyu, Pal P)
    {
        const float genislik = 61.2f; const int dilim = 18; float adim = genislik / dilim, baslangic = bitisDerece - genislik;
        for (int i = 0; i < dilim; i++)
        {
            double t = (i + 1.0) / dilim, al = Math.Pow(t, 2.1) * (koyu ? 0.28 : 0.22);
            using var b = new SolidBrush(koyu ? A(6, 182, 212, al) : A(99, 102, 241, al)); g.FillPie(b, cx - sr, cy - sr, 2 * sr, 2 * sr, baslangic + i * adim, adim + 0.5f);
        }
        double a = bitisDerece * D2R; float tx = cx + (float)Math.Cos(a) * sr, ty = cy + (float)Math.Sin(a) * sr;
        using (var gr = new LinearGradientBrush(new PointF(cx, cy), new PointF(tx, ty), A(99, 102, 241, 0.08), P.SweepCizgi) { InterpolationColors = new ColorBlend { Positions = new[] { 0f, 0.5f, 1f }, Colors = new[] { A(99, 102, 241, 0.08), P.SweepCizgi, P.SweepCizgi } } })
        using (var k = new Pen(gr, 1.8f)) g.DrawLine(k, cx, cy, tx, ty);
        using (var b = new SolidBrush(P.SweepCizgi)) g.FillEllipse(b, cx - 3.5f, cy - 3.5f, 7, 7);
    }

    private void YaylariCiz(Graphics g, float cx, float cy, float r, Pal P, bool koyu, bool taramada, float dt)
    {
        int hedef = taramada ? 22 : 13;
        if (_yaylar.Count < hedef && _rnd.NextDouble() < (taramada ? 0.18 : 0.08)) { var y = YayUret(taramada); if (y != null) _yaylar.Add(y); }
        using var kalem = new Pen(Color.White, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = _yaylar.Count - 1; i >= 0; i--)
        {
            var a = _yaylar[i]; a.Ilerleme += a.Hiz * dt;
            if (a.Ilerleme - a.Kuyruk > 1) { _yaylar.RemoveAt(i); continue; }
            var renk = P.Yay[a.Renk];
            if (!a.Vurdu && a.Ilerleme >= 1) { a.Vurdu = true; _dalgalar.Add(new Dalga { V = a.V2, Renk = renk }); }
            float bas = Math.Max(0, a.Ilerleme - a.Kuyruk), son = Math.Min(1, a.Ilerleme); if (son <= bas) continue;
            (float X, float Y, float Z)? onceki = null, uc = null; const int adim = 22;
            for (int s = 0; s <= adim; s++)
            {
                double u = bas + (son - bas) * s / adim, ix = a.V1.X * (1 - u) + a.V2.X * u, iy = a.V1.Y * (1 - u) + a.V2.Y * u, iz = a.V1.Z * (1 - u) + a.V2.Z * u, len = Math.Sqrt(ix * ix + iy * iy + iz * iz); if (len == 0) len = 1;
                double yuk = 1 + Math.Sin(Math.PI * u) * a.Yukseklik; var p = Yansit((ix / len * yuk, iy / len * yuk, iz / len * yuk), cx, cy, r);
                if (onceki != null && (p.Z > -0.25f || onceki.Value.Z > -0.25f)) { double al = (double)s / adim * (p.Z > 0 ? 0.92 : 0.25); kalem.Color = DesignTokens.Saydam(renk, (int)(al * 255)); g.DrawLine(kalem, onceki.Value.X, onceki.Value.Y, p.X, p.Y); }
                onceki = p; if (s == adim) uc = p;
            }
            if (uc != null && uc.Value.Z > -0.1f && a.Ilerleme <= 1)
            {
                using (var b = new SolidBrush(DesignTokens.Saydam(renk, 60))) g.FillEllipse(b, uc.Value.X - 7, uc.Value.Y - 7, 14, 14);
                using (var b = new SolidBrush(koyu ? Color.White : renk)) g.FillEllipse(b, uc.Value.X - 3f, uc.Value.Y - 3f, 6, 6);
            }
        }
    }

    private void DalgalariCiz(Graphics g, float cx, float cy, float r, float dt)
    {
        for (int i = _dalgalar.Count - 1; i >= 0; i--)
        {
            var d = _dalgalar[i]; d.Yas += 0.024f * dt; if (d.Yas >= 1) { _dalgalar.RemoveAt(i); continue; }
            var p = Yansit(d.V, cx, cy, r); if (p.Z < 0) continue; float rr = 2 + d.Yas * 16;
            using var k = new Pen(DesignTokens.Saydam(d.Renk, (int)((1 - d.Yas) * 0.85 * 255)), 1.6f); g.DrawEllipse(k, p.X - rr, p.Y - rr, 2 * rr, 2 * rr);
        }
    }
}
