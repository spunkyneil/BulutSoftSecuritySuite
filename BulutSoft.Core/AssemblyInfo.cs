// Çekirdek yalnızca Windows'ta çalışır (kayıt defteri, DPAPI, WMI, ETW); çözümleyici uyarıları bu işaretle doğru yönetilir.
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
