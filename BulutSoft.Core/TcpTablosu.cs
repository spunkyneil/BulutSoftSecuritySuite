// Bu dosya Windows'un PID'li TCP bağlantı tablosunu (GetExtendedTcpTable) GÜVENLİ okuyan tek yardımcıyı içerir.
// Eskiden iki yerde kopya vardı ve dönüş kodu hiç denetlenmiyordu: tablo, boyut öğrenilen ilk çağrı ile asıl okuma arasında büyürse
// (yeni bağlantı açılırsa) ikinci çağrı "tampon yetersiz" (122) döner ve veriler SESSİZCE kaybolurdu. Burada tampon büyütülüp yeniden denenir.
using System.ComponentModel;
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// PID'li TCP tablosu okuyucu.
internal static class TcpTablosu
{
    // Windows'un bağlantı tablosunu PID'lerle birlikte veren sistem çağrısı.
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr tablo, ref int boyut, [MarshalAs(UnmanagedType.Bool)] bool sirali, int aile, int tur, uint sifir);

    // Sonuç kodları (0 = başarılı, 122 = tampon yetersiz). Tablo türü 5 = "PID'li tüm TCP bağlantıları".
    private const uint Basarili = 0, YetersizTampon = 122; private const int TcpTabloPidTum = 5, EnFazlaDeneme = 5;

    // Tabloyu okur ve ham belleği isle'ye verir (isle dönünce bellek serbest bırakılır). aile: 2 = IPv4, 23 = IPv6.
    // true: tablo okundu ve işlendi. false: okunamadı (nedeni HataKayit'a yazılır; çağıran boş sonuçla devam edebilir).
    public static bool Oku(int aile, Action<IntPtr> isle)
    {
        int boyut = 0;
        // İlk çağrı yalnızca gereken boyutu öğrenir; beklenen sonuç "tampon yetersiz"dir.
        uint sonuc = GetExtendedTcpTable(IntPtr.Zero, ref boyut, true, aile, TcpTabloPidTum, 0);
        for (int deneme = 0; deneme < EnFazlaDeneme; deneme++)
        {
            if (sonuc != YetersizTampon && sonuc != Basarili) { HataKayit.Sessiz(new Win32Exception((int)sonuc), "TcpTablosu.Oku(boyut)"); return false; }
            if (boyut <= 0) return false;   // tablo yok/boş
            IntPtr tablo = Marshal.AllocHGlobal(boyut);
            try
            {
                sonuc = GetExtendedTcpTable(tablo, ref boyut, true, aile, TcpTabloPidTum, 0);
                if (sonuc == Basarili) { isle(tablo); return true; }
            }
            finally { Marshal.FreeHGlobal(tablo); }
            // İki çağrı arasında tablo büyüdüyse boyut güncellenmiştir: yeniden dene; başka bir hata ise bırak.
            if (sonuc != YetersizTampon) { HataKayit.Sessiz(new Win32Exception((int)sonuc), "TcpTablosu.Oku(tablo)"); return false; }
        }
        HataKayit.Sessiz(new InvalidOperationException("TCP tablosu sürekli büyüdü; okunamadı."), "TcpTablosu.Oku");
        return false;
    }
}
