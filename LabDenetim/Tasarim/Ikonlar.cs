// Bu dosya yeni kabuğun TEK TİP ÇİZGİLİ VEKTÖR İKON SETİDİR (GDI+ GraphicsPath, 24x24 ızgara). Segoe Fluent Icons Windows 10'da bulunmadığı için yazı tipine bağlı kalınmaz;
// her ikon her Windows'ta ve her DPI'da aynı görünür, rengi ve kalınlığı çağıran verir (DesignTokens). Yollar bir kez üretilip önbellekte tutulur.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// İkon adları.
internal enum IkonAdi { Panel, Olay, Tarama, Ag, Kalici, Kalkan, Trafik, Rapor, Arac, Lab, Ara, Zil, Kullanici, Gunes, Ay, Kilit, Menu, ChevronAsagi, ChevronSag, ChevronSol, Uyari, Tamam }

// İkon çizici.
internal static class Ikonlar
{
    private static readonly Dictionary<IkonAdi, GraphicsPath> Onbellek = new();
    private static readonly object Kilit = new();

    // İkonu hedef dikdörtgene çizer. kalinlik: son piksel kalınlığı (0: boyuta göre otomatik).
    public static void Ciz(Graphics g, IkonAdi ad, RectangleF hedef, Color renk, float kalinlik = 0f)
    {
        var yol = Yol(ad); float olcek = Math.Min(hedef.Width, hedef.Height) / 24f; if (olcek <= 0) return;
        float kal = kalinlik > 0 ? kalinlik : Math.Max(1.4f, hedef.Width / 13f);
        var eskiMod = g.SmoothingMode; var eskiDonusum = g.Transform;
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(hedef.X + (hedef.Width - 24f * olcek) / 2f, hedef.Y + (hedef.Height - 24f * olcek) / 2f); g.ScaleTransform(olcek, olcek);
            using var kalem = new Pen(renk, kal / olcek) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawPath(kalem, yol);
        }
        finally { g.Transform = eskiDonusum; eskiDonusum.Dispose(); g.SmoothingMode = eskiMod; }
    }

    // Önbellekli ikon yolu (24x24 ızgara).
    public static GraphicsPath Yol(IkonAdi ad) { lock (Kilit) { if (!Onbellek.TryGetValue(ad, out var p)) Onbellek[ad] = p = Uret(ad); return p; } }

    private static void Cizgi(GraphicsPath p, params float[] xy) { p.StartFigure(); var n = new PointF[xy.Length / 2]; for (int i = 0; i < n.Length; i++) n[i] = new PointF(xy[2 * i], xy[2 * i + 1]); p.AddLines(n); }
    private static void Daire(GraphicsPath p, float cx, float cy, float r) { p.StartFigure(); p.AddEllipse(cx - r, cy - r, 2 * r, 2 * r); }

    private static GraphicsPath Uret(IkonAdi ad)
    {
        var p = new GraphicsPath();
        switch (ad)
        {
            case IkonAdi.Panel: foreach (var (x, y) in new[] { (4f, 4f), (13f, 4f), (4f, 13f), (13f, 13f) }) { p.StartFigure(); p.AddRectangle(new RectangleF(x, y, 7, 7)); } break;
            case IkonAdi.Olay: Cizgi(p, 7, 4, 7, 20); Daire(p, 7, 6, 1.5f); Daire(p, 7, 12, 1.5f); Daire(p, 7, 18, 1.5f); Cizgi(p, 11, 6, 20, 6); Cizgi(p, 11, 12, 20, 12); Cizgi(p, 11, 18, 18, 18); break;
            case IkonAdi.Tarama: case IkonAdi.Ara: Daire(p, 10.5f, 10.5f, 6.5f); Cizgi(p, 15.5f, 15.5f, 20, 20); break;
            case IkonAdi.Ag: Daire(p, 12, 5.5f, 2.5f); Daire(p, 5.5f, 18, 2.5f); Daire(p, 18.5f, 18, 2.5f); Cizgi(p, 11, 8, 6, 15.5f); Cizgi(p, 13, 8, 18, 15.5f); Cizgi(p, 8, 18, 16, 18); break;
            case IkonAdi.Kalici: p.StartFigure(); p.AddArc(4, 4, 16, 16, 30, 300); Cizgi(p, 19, 3.5f, 19, 8.5f, 14, 8.5f); break;
            case IkonAdi.Kalkan:
                p.StartFigure(); p.AddLine(12, 3, 19, 6); p.AddLine(19, 6, 19, 12); p.AddBezier(19, 12, 19, 16.5f, 15.5f, 19.5f, 12, 21); p.AddBezier(12, 21, 8.5f, 19.5f, 5, 16.5f, 5, 12); p.AddLine(5, 12, 5, 6); p.CloseFigure(); break;
            case IkonAdi.Trafik: Cizgi(p, 3, 12, 7, 12, 10, 5, 14, 19, 17, 12, 21, 12); break;
            case IkonAdi.Rapor: p.StartFigure(); p.AddLines(new[] { new PointF(7, 3), new PointF(14, 3), new PointF(19, 8), new PointF(19, 21), new PointF(7, 21) }); p.CloseFigure(); Cizgi(p, 14, 3, 14, 8, 19, 8); Cizgi(p, 10, 13, 16, 13); Cizgi(p, 10, 17, 16, 17); break;
            case IkonAdi.Arac: p.StartFigure(); p.AddRectangle(new RectangleF(3, 8, 18, 12)); Cizgi(p, 8.5f, 8, 8.5f, 5, 15.5f, 5, 15.5f, 8); Cizgi(p, 3, 13, 21, 13); break;
            case IkonAdi.Lab: Cizgi(p, 9, 3, 15, 3); Cizgi(p, 10, 3, 10, 9, 5, 19, 6.5f, 21, 17.5f, 21, 19, 19, 14, 9, 14, 3); Cizgi(p, 7.5f, 15, 16.5f, 15); break;
            case IkonAdi.Zil:
                p.StartFigure(); p.AddArc(6, 4, 12, 12, 180, 180); p.AddLines(new[] { new PointF(18, 10), new PointF(18, 15), new PointF(20, 17), new PointF(4, 17), new PointF(6, 15), new PointF(6, 10) }); p.CloseFigure(); p.StartFigure(); p.AddArc(9.5f, 17.5f, 5, 4, 0, 180); break;
            case IkonAdi.Kullanici: Daire(p, 12, 8, 3.5f); p.StartFigure(); p.AddArc(5, 14, 14, 14, 180, 180); Cizgi(p, 5, 21, 19, 21); break;
            case IkonAdi.Gunes: Daire(p, 12, 12, 4); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; Cizgi(p, (float)(12 + 7 * Math.Cos(a)), (float)(12 + 7 * Math.Sin(a)), (float)(12 + 9.5 * Math.Cos(a)), (float)(12 + 9.5 * Math.Sin(a))); } break;
            case IkonAdi.Ay: p.StartFigure(); p.AddArc(4, 4, 16, 16, 60, 240); p.AddArc(21 - 8.54f, 12 - 8.54f, 17.08f, 17.08f, 234.2f, -108.4f); p.CloseFigure(); break;
            case IkonAdi.Kilit: p.StartFigure(); p.AddRectangle(new RectangleF(6, 11, 12, 9)); p.StartFigure(); p.AddArc(8, 4, 8, 8, 180, 180); Cizgi(p, 8, 8, 8, 11); Cizgi(p, 16, 8, 16, 11); break;
            case IkonAdi.Menu: Cizgi(p, 4, 7, 20, 7); Cizgi(p, 4, 12, 20, 12); Cizgi(p, 4, 17, 20, 17); break;
            case IkonAdi.ChevronAsagi: Cizgi(p, 7, 10, 12, 15, 17, 10); break;
            case IkonAdi.ChevronSag: Cizgi(p, 10, 7, 15, 12, 10, 17); break;
            case IkonAdi.ChevronSol: Cizgi(p, 14, 7, 9, 12, 14, 17); break;
            case IkonAdi.Uyari: p.StartFigure(); p.AddLines(new[] { new PointF(12, 4), new PointF(21, 19), new PointF(3, 19) }); p.CloseFigure(); Cizgi(p, 12, 10, 12, 14); Daire(p, 12, 16.5f, 0.6f); break;
            default: Cizgi(p, 5, 12.5f, 10, 17.5f, 19, 7); break;
        }
        return p;
    }
}
