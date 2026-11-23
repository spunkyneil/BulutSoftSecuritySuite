using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LabDenetim;

namespace BulutSoftSecuritySuite.Kontroller
{
    public class CanliTaramaKontrolu : UserControl
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly List<VeriPaketi> _paketler = new();
        private readonly Random _rnd = new();
        private readonly Button _btnVazgec;

        private float _aciDis = 0f;
        private float _aciIc = 180f;
        private float _radarAci = 0f;
        private float _gosterilenYuzde = 0f;

        private int _hedefYuzde = 0;
        private int _aktifAsama = 1; // 1..4
        private bool _koyuTema = true;
        private string _baslik = "1/4: Bilgisayarınızdaki programlar ve hizmetler okunuyor...";
        private string _altAciklama = "Güvenlik ayarları, kullanıcı hesapları ve açılışta çalışan programların denetimi (salt okunur)";
        private string _telemetri = "[DENETİM] Sistem servisleri ve başlangıç öğeleri okunuyor...";

        private string[] _asamaEtiketleri =
        {
            "1/4 Sistem",
            "2/4 Ağ & Kapılar",
            "3/4 Güncellemeler",
            "4/4 Güvenlik Karnesi"
        };

        private class VeriPaketi
        {
            public float Aci;
            public float Mesafe; // 1.0 (dış) -> 0.26 (merkez)
            public float Hiz;
            public int RenkTipi;
        }

        public event EventHandler? VazgecTiklandi;

        [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool KoyuTema
        {
            get => _koyuTema;
            set
            {
                _koyuTema = value;
                ButonTemaUygula();
                Invalidate();
            }
        }

        // Dört aşama hapının yazıları (varsayılan: bilgisayar kontrolü). Dış site incelemesi gibi başka akışlar kendi yazılarını verebilir.
        [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string[] AsamaEtiketleri
        {
            get => _asamaEtiketleri;
            set { if (value != null && value.Length == 4) { _asamaEtiketleri = value; Invalidate(); } }
        }

        public CanliTaramaKontrolu()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ResizeRedraw = true;

            for (int i = 0; i < 24; i++)
                _paketler.Add(YeniPaketOlustur(rastgeleMesafe: true));

            _btnVazgec = new EfektliButon
            {
                Text = "Vazgeç",
                Size = new Size(136, 36),
                FlatStyle = FlatStyle.Flat,
                Font = FontProvider.Al(FontRol.Body, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnVazgec.FlatAppearance.BorderSize = 1;
            _btnVazgec.Click += (s, e) => VazgecTiklandi?.Invoke(this, EventArgs.Empty);
            Controls.Add(_btnVazgec);
            ButonTemaUygula();

            _timer = new System.Windows.Forms.Timer { Interval = 30 }; // ~33 FPS
            _timer.Tick += (s, e) =>
            {
                _aciDis = (_aciDis + 1.1f) % 360f;
                _aciIc = (_aciIc - 0.8f + 360f) % 360f;
                _radarAci = (_radarAci + 2.8f) % 360f;

                _gosterilenYuzde += (_hedefYuzde - _gosterilenYuzde) * 0.14f;
                if (Math.Abs(_hedefYuzde - _gosterilenYuzde) < 0.2f)
                    _gosterilenYuzde = _hedefYuzde;

                for (int i = 0; i < _paketler.Count; i++)
                {
                    _paketler[i].Mesafe -= _paketler[i].Hiz;
                    if (_paketler[i].Mesafe <= 0.26f)
                        _paketler[i] = YeniPaketOlustur(rastgeleMesafe: false);
                }

                Invalidate();
            };
            _timer.Start();
        }

        public void DurumGuncelle(int yuzde, int asama, string baslik, string? altAciklama = null, string? telemetri = null)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(() => DurumGuncelle(yuzde, asama, baslik, altAciklama, telemetri));
                return;
            }

            _hedefYuzde = Math.Clamp(yuzde, 0, 100);
            _aktifAsama = Math.Clamp(asama, 1, 4);
            if (!string.IsNullOrWhiteSpace(baslik)) _baslik = baslik;
            if (!string.IsNullOrWhiteSpace(altAciklama)) _altAciklama = altAciklama;
            if (!string.IsNullOrWhiteSpace(telemetri)) _telemetri = telemetri;
            Invalidate();
        }

        private VeriPaketi YeniPaketOlustur(bool rastgeleMesafe) => new()
        {
            Aci = (float)(_rnd.NextDouble() * Math.PI * 2),
            Mesafe = rastgeleMesafe ? 0.35f + (float)_rnd.NextDouble() * 0.65f : 1.0f,
            Hiz = 0.008f + (float)_rnd.NextDouble() * 0.012f,
            RenkTipi = _rnd.Next(0, 3)
        };

        private void ButonTemaUygula()
        {
            // "Vazgeç" düğmesi: Palet'in tehlike rolleriyle (iki tema için tek yol; elle yazılmış renk yok; TemaYonetici bu değerleri olduğu gibi bırakır).
            _btnVazgec.BackColor = Palet.Renk(PaletRol.TehlikeArka, _koyuTema);
            _btnVazgec.ForeColor = Palet.Renk(PaletRol.TehlikeMetin, _koyuTema);
            _btnVazgec.FlatAppearance.BorderColor = Palet.Renk(PaletRol.TehlikeKenar, _koyuTema);
            _btnVazgec.FlatAppearance.MouseOverBackColor = Palet.Renk(PaletRol.TehlikeKenar, _koyuTema);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_btnVazgec != null)
            {
                _btnVazgec.Location = new Point((Width - _btnVazgec.Width) / 2, Math.Max(Height - 62, 10));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 40 || Height <= 40) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Renkler programın TEK palet kaynağından (KTema: Koyu #141B2E / Açık #F0F4F8 zeminli) gelir; ayrı sabit palet yoktur.
            var palet = KTema.Sec(_koyuTema);
            Color arkaPlan = palet.Form;
            Color halkaZemin = palet.Yuksek;
            Color vurguMor = palet.Ana;
            Color vurguCyan = palet.Cam;
            Color vurguYesil = palet.Basari;
            Color metinAna = palet.Metin;
            Color metinSoluk = palet.Soluk;

            g.Clear(arkaPlan);

            float cx = Width / 2f;
            float cy = Height * 0.35f;
            float r = Math.Clamp(Math.Min(Width, Height) * 0.18f, 54f, 110f);
            float maksYayilim = Math.Max(Width, Height) * 0.48f;

            // 1. Arka Plan: Merkeze Akan Neon Veri Paketleri
            foreach (var p in _paketler)
            {
                float px = cx + (float)Math.Cos(p.Aci) * (p.Mesafe * maksYayilim);
                float py = cy + (float)Math.Sin(p.Aci) * (p.Mesafe * maksYayilim);
                float tx = cx + (float)Math.Cos(p.Aci) * ((p.Mesafe + 0.045f) * maksYayilim);
                float ty = cy + (float)Math.Sin(p.Aci) * ((p.Mesafe + 0.045f) * maksYayilim);

                Color pRenk = p.RenkTipi switch
                {
                    0 => vurguMor,
                    1 => vurguCyan,
                    _ => vurguYesil
                };

                int alpha = (int)Math.Clamp((1f - Math.Abs(p.Mesafe - 0.6f)) * (_koyuTema ? 140 : 120), 15, 160);
                using (var izKalem = new Pen(DesignTokens.Saydam(pRenk, alpha), 1.5f))
                    g.DrawLine(izKalem, tx, ty, px, py);

                using (var nokta = new SolidBrush(DesignTokens.Saydam(pRenk, Math.Min(255, alpha + 50))))
                    g.FillEllipse(nokta, px - 2.5f, py - 2.5f, 5f, 5f);
            }

            // 2. Dış Kesikli Yörünge Halkaları
            float rDis = r * 1.28f;
            using (var disKalem = new Pen(DesignTokens.Saydam(vurguMor, _koyuTema ? 90 : 110), 1.5f) { DashStyle = DashStyle.Dash })
            {
                g.DrawArc(disKalem, cx - rDis, cy - rDis, rDis * 2, rDis * 2, _aciDis, 110f);
                g.DrawArc(disKalem, cx - rDis, cy - rDis, rDis * 2, rDis * 2, _aciDis + 180f, 110f);
            }

            float rIc = r * 0.80f;
            using (var icKalem = new Pen(DesignTokens.Saydam(vurguCyan, _koyuTema ? 80 : 100), 1.3f))
            {
                g.DrawArc(icKalem, cx - rIc, cy - rIc, rIc * 2, rIc * 2, _aciIc, 140f);
                g.DrawArc(icKalem, cx - rIc, cy - rIc, rIc * 2, rIc * 2, _aciIc + 180f, 140f);
            }

            // 3. Dönen Yumuşak Radar Konisi
            using (var radarFirca = new SolidBrush(DesignTokens.Saydam(vurguCyan, _koyuTema ? 30 : 26)))
                g.FillPie(radarFirca, cx - r, cy - r, r * 2, r * 2, _radarAci - 42f, 42f);

            // 4. Radyal İlerleme Halkası
            using (var zeminKalem = new Pen(halkaZemin, 8f))
                g.DrawEllipse(zeminKalem, cx - r, cy - r, r * 2, r * 2);

            float taramaAcisi = (_gosterilenYuzde / 100f) * 360f;
            if (taramaAcisi > 0.5f)
            {
                using var gradyan = new LinearGradientBrush(
                    new RectangleF(cx - r - 5, cy - r - 5, r * 2 + 10, r * 2 + 10),
                    vurguMor, vurguYesil, 45f);
                using var ilerlemeKalem = new Pen(gradyan, 8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(ilerlemeKalem, cx - r, cy - r, r * 2, r * 2, -90f, taramaAcisi);
            }

            // 5. Merkez Yüzde Metni
            string yuzdeMetin = $"%{(int)Math.Round(_gosterilenYuzde)}";
            using (var fYuzde = FontProvider.Gecici("Segoe UI", r * 0.30f, FontStyle.Bold))
            using (var bYuzde = new SolidBrush(metinAna))
            {
                var b = g.MeasureString(yuzdeMetin, fYuzde);
                g.DrawString(yuzdeMetin, fYuzde, bYuzde, cx - b.Width / 2f, cy - b.Height / 2f - 4f);
            }

            using (var fAltYuzde = FontProvider.Gecici("Segoe UI", 8.5f, FontStyle.Bold))
            using (var bAltYuzde = new SolidBrush(vurguCyan))
            {
                string etiket = _hedefYuzde >= 100 ? "TAMAMLANDI" : $"AŞAMA {_aktifAsama}/4";
                var b = g.MeasureString(etiket, fAltYuzde);
                g.DrawString(etiket, fAltYuzde, bAltYuzde, cx - b.Width / 2f, cy + r * 0.28f);
            }

            // 6. Aşama Başlığı ve Açıklama
            float metinY = cy + rDis + 18f;
            using (var fBaslik = FontProvider.Gecici("Segoe UI", 12.5f, FontStyle.Bold))
            using (var bBaslik = new SolidBrush(metinAna))
            {
                var b = g.MeasureString(_baslik, fBaslik);
                g.DrawString(_baslik, fBaslik, bBaslik, cx - b.Width / 2f, metinY);
            }

            using (var fAciklama = FontProvider.Gecici("Segoe UI", 9f, FontStyle.Regular))
            using (var bAciklama = new SolidBrush(metinSoluk))
            {
                var b = g.MeasureString(_altAciklama, fAciklama);
                g.DrawString(_altAciklama, fAciklama, bAciklama, cx - b.Width / 2f, metinY + 26f);
            }

            // 7. 4 Aşamalı Durum Hapları
            CizAsamaHaplari(g, cx, metinY + 56f, vurguMor, vurguYesil, halkaZemin, metinAna, metinSoluk);

            // 8. Canlı Telemetri Satırı
            using (var fTel = FontProvider.Gecici("Consolas", 8.5f, FontStyle.Regular))
            using (var bTel = new SolidBrush(DesignTokens.Saydam(vurguCyan, 200)))
            {
                var b = g.MeasureString(_telemetri, fTel);
                g.DrawString(_telemetri, fTel, bTel, cx - b.Width / 2f, metinY + 96f);
            }
        }

        private void CizAsamaHaplari(Graphics g, float cx, float y, Color mor, Color yesil, Color zemin, Color metinAna, Color metinSoluk)
        {
            int hapGenislik = 148;
            int hapYukseklik = 26;
            int bosluk = 10;
            float toplamGenislik = _asamaEtiketleri.Length * hapGenislik + (_asamaEtiketleri.Length - 1) * bosluk;
            float basX = cx - toplamGenislik / 2f;

            using var font = FontProvider.Gecici("Segoe UI", 8.5f, FontStyle.Bold);

            for (int i = 0; i < _asamaEtiketleri.Length; i++)
            {
                int asamaNo = i + 1;
                float x = basX + i * (hapGenislik + bosluk);
                var rect = new RectangleF(x, y, hapGenislik, hapYukseklik);

                bool tamamlandi = asamaNo < _aktifAsama || _hedefYuzde >= 100;
                bool aktif = asamaNo == _aktifAsama && _hedefYuzde < 100;

                Color dolgu = tamamlandi ? DesignTokens.Saydam(yesil, _koyuTema ? 45 : 35)
                            : aktif ? DesignTokens.Saydam(mor, _koyuTema ? 55 : 40)
                            : zemin;

                Color kenar = tamamlandi ? yesil : aktif ? mor : DesignTokens.Saydam(metinSoluk, 80);
                Color yaziRenk = tamamlandi ? yesil : aktif ? metinAna : metinSoluk;

                using (var path = YuvarlakDikdortgen(rect, 13))
                {
                    using (var b = new SolidBrush(dolgu)) g.FillPath(b, path);
                    using (var p = new Pen(kenar, 1f)) g.DrawPath(p, path);
                }

                string metin = (tamamlandi ? "✓ " : aktif ? "● " : "") + _asamaEtiketleri[i];
                var mBoyut = g.MeasureString(metin, font);
                using var yaziFirca = new SolidBrush(yaziRenk);
                g.DrawString(metin, font, yaziFirca, x + (hapGenislik - mBoyut.Width) / 2f, y + (hapYukseklik - mBoyut.Height) / 2f);
            }
        }

        private static GraphicsPath YuvarlakDikdortgen(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
