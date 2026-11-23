// Bu dosya her bulguya bir "MITRE ATT&CK" etiketi yapıştırır. MITRE ATT&CK, saldırganların kullandığı yöntemlerin dünyaca bilinen
// numaralı listesidir (ör. T1021 = uzaktan erişim hizmetleri). Böylece bulgunuzu profesyonel raporlarda ortak dille anlatabilirsiniz.
// Eşleştirme yalnızca bir YAKLAŞIK rehberdir: kesin saldırı kanıtı değil, "bu zayıflık şu yöntemin kapısını açar" bilgisidir.
namespace LabDenetim;

// MITRE etiketi: numara, kısa ad ve taktik (saldırının hangi aşaması).
internal sealed record MitreEtiketi(string Kimlik, string Ad, string Taktik)
{
    // Ekranda ve raporda görünen kısa yazı.
    public override string ToString() => $"{Kimlik} {Ad}";
}

// Bulguyu MITRE etiketine çeviren yardımcı.
// SÜRÜM: ATT&CK v20 (13 Ekim 2026; Enterprise: 15 taktik). v20'de eski "Defense Evasion" taktiği "Stealth" ve "Defense Impairment" (TA0112) olarak ikiye ayrıldı;
// savunma araçlarını/güvenlik duvarını bozma teknikleri Defense Impairment altına taşındı: T1562.001 → T1685 (Disable or Modify Tools), T1562.004 → T1686.003 (Windows Host Firewall).
// Kaynak: attack.mitre.org/resources/updates ve attack.mitre.org/tactics/TA0112. Bu sürüm raporlarda ve kanıt paketinde belirtilir; yeni ATT&CK sürümünde bu dosya ve MitreTestleri güncellenmelidir.
internal static class Mitre
{
    // Etiketlerin dayandığı ATT&CK sürümü (raporlara yazılır).
    public const string Surum = "MITRE ATT&CK Enterprise v20 (Ekim 2026)";

    // Metinde anahtar kelimelerden biri geçiyor mu? (büyük/küçük harf fark etmez)
    private static bool Var(string metin, params string[] anahtarlar)
    {
        // Her anahtar kelimeyi dene.
        foreach (var a in anahtarlar)
            // Bulunursa evet.
            if (metin.Contains(a, StringComparison.OrdinalIgnoreCase)) return true;
        // Hiçbiri yok.
        return false;
    }

    // Bir bulgunun MITRE etiketini verir; bilgi amaçlı/yeşil sonuçlar ve eşleşmeyenler için boş döner.
    public static MitreEtiketi? Bul(Bulgu b)
    {
        // Yeşil (sorun olmayan) ve atlanan sonuçlara etiket yapıştırma.
        if (b.Onem == Onem.Bilgi || b.Atlandi) return null;
        // Derin denetim (Finding v2) bulguları kendi MITRE etiketini taşır.
        if (V2.V2Kayit.Etiket(b) != null) return V2.V2Kayit.MitreEtiketi(b);
        // Aranacak metin: başlık + detay.
        string m = b.Baslik + " " + b.Detay;
        // Modül adına göre karar ver.
        switch (b.Modul)
        {
            // Açık kapılar: hangi hizmet olduğuna bakılır.
            case "Port":
                // Uzak masaüstü.
                if (Var(m, "3389", "RDP", "uzak masaüstü")) return new("T1021.001", "Remote Services: RDP", "Lateral Movement");
                // Dosya paylaşımı.
                if (Var(m, "445", "139", "SMB")) return new("T1021.002", "Remote Services: SMB/Windows Admin Shares", "Lateral Movement");
                // VNC.
                if (Var(m, "5900", "VNC")) return new("T1021.005", "Remote Services: VNC", "Lateral Movement");
                // Şifresiz eski protokoller.
                if (Var(m, "telnet", "ftp", "23 ", " 21 ")) return new("T1040", "Network Sniffing (şifresiz protokol)", "Credential Access");
                // Diğer açık kapılar.
                return new("T1190", "Exploit Public-Facing Application", "Initial Access");
            // Web/HTTP kontrolleri.
            case "HTTP": return new("T1190", "Exploit Public-Facing Application", "Initial Access");
            // Şifreleme (TLS) zayıflıkları: trafik dinlenebilir.
            case "TLS": return new("T1040", "Network Sniffing", "Credential Access");
            // Girdi doğrulama: enjeksiyon saldırıları.
            case "Girdi": return new("T1190", "Exploit Public-Facing Application (enjeksiyon)", "Initial Access");
            // DNS bilgi sızdırma / keşif.
            case "DNS": return new("T1590.002", "Gather Victim Network Information: DNS", "Reconnaissance");
            // hosts dosyası oynamaları.
            case "Hosts": return new("T1565.001", "Data Manipulation: Stored Data (hosts)", "Impact");
            // Kalıcılık: hangi yöntem?
            case "Kalıcılık":
                // Zamanlanmış görev.
                if (Var(m, "zamanlanmış", "görev", "task")) return new("T1053.005", "Scheduled Task/Job: Scheduled Task", "Persistence");
                // Hizmet.
                if (Var(m, "hizmet", "service")) return new("T1543.003", "Create or Modify System Process: Windows Service", "Persistence");
                // Diğerleri: Run anahtarı / Başlangıç klasörü.
                return new("T1547.001", "Boot or Logon Autostart: Registry Run Keys / Startup Folder", "Persistence");
            // Güvenlik karnesi: ne kapalı?
            case "Karne":
                // Defender.
                if (Var(m, "defender", "antivir", "virüs")) return new("T1685", "Disable or Modify Tools", "Defense Impairment");
                // Güvenlik duvarı.
                if (Var(m, "duvar", "firewall")) return new("T1686.003", "Disable or Modify System Firewall: Windows Host Firewall", "Defense Impairment");
                // UAC.
                if (Var(m, "UAC", "hesap denetimi")) return new("T1548.002", "Abuse Elevation Control: Bypass UAC", "Privilege Escalation");
                // Eski SMB.
                if (Var(m, "SMB")) return new("T1210", "Exploitation of Remote Services", "Lateral Movement");
                // RDP.
                if (Var(m, "RDP", "uzak masaüstü")) return new("T1021.001", "Remote Services: RDP", "Lateral Movement");
                // Disk şifreleme yok.
                if (Var(m, "bitlocker", "şifreleme")) return new("T1005", "Data from Local System", "Collection");
                // Yama eski.
                if (Var(m, "yama", "güncelleme", "patch")) return new("T1190", "Exploit Public-Facing Application (eksik yama)", "Initial Access");
                // Diğer karne maddeleri.
                return new("T1685", "Disable or Modify Tools (genel savunma zayıflığı)", "Defense Impairment");
            // Wi-Fi: ağ dinleme/ortadaki adam.
            case "Wi-Fi": return new("T1557", "Adversary-in-the-Middle", "Credential Access");
            // Hesaplar: geçerli hesapların kötüye kullanımı.
            case "Hesap": return new("T1078", "Valid Accounts", "Initial Access");
            // Yapılandırma: genel zayıf ayar.
            case "Yapılandırma": return new("T1190", "Exploit Public-Facing Application", "Initial Access");
            // Zincirleme risk: birden çok yöntemin birleşimi.
            case "Zincir": return new("T1210", "Exploitation of Remote Services (zincir)", "Lateral Movement");
            // Referans (altın profil) sapması: yetkisiz değişiklik.
            case "Referans": return new("T1565", "Data Manipulation", "Impact");
        }
        // Eşleşme yoksa boş.
        return null;
    }

    // Etiketi tek satır yazıya çevirir (yoksa tire).
    public static string Yazi(Bulgu b) => Bul(b)?.ToString() ?? "-";
}
