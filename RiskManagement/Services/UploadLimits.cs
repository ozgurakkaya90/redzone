namespace RiskManagement.Services;

/// <summary>
/// Uygulama genelinde dosya yükleme boyut limiti — tek kaynak.
/// Program.cs başlangıçta "AppSettings:MaxUploadSizeMb" değerinden ayarlar.
/// Önceden Audit/Ethics/Import yollarında 10MB ayrı ayrı hardcoded'du ve config
/// (MaxUploadSizeMb) hiç tüketilmiyordu; bu sınıf o tutarsızlığı giderir.
/// </summary>
public static class UploadLimits
{
    /// <summary>İzin verilen maksimum dosya boyutu (byte). Varsayılan 10 MB.</summary>
    public static long MaxBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>Mesajlarda göstermek için MB cinsinden.</summary>
    public static int MaxMb => (int)(MaxBytes / 1024 / 1024);
}
