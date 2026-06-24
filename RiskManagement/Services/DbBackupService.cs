using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace RiskManagement.Services;

public record DbBackupResult(bool Success, string? FilePath, long SizeBytes, string? Error);

/// <summary>
/// MySQL veritabanının mantıksal yedeğini alır (mysqldump → zip → saklama). Hem zamanlanmış
/// <see cref="DbBackupWorker"/> hem de admin "Şimdi Yedekle" butonu bu servisi çağırır.
/// Kimlik bilgisi diskte YENİ bir yere yazılmaz: bağlantı dizesinden okunup mysqldump'a
/// MYSQL_PWD ortam değişkeniyle geçirilir (komut satırında / process listesinde görünmez).
/// </summary>
public class DbBackupService(IConfiguration configuration, ConfigService cfg, ILogger<DbBackupService> logger)
{
    public const string DefaultPath = @"D:\RedZone-DBBackups";

    public string ResolvedPath
    {
        get { var p = cfg.Get<string>("backup_path"); return string.IsNullOrWhiteSpace(p) ? DefaultPath : p!; }
    }

    public DbBackupResult Run()
    {
        try
        {
            var connStr = configuration.GetConnectionString("DefaultConnection")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connStr))
                return new(false, null, 0, "Bağlantı dizesi bulunamadı.");

            var dbHost = Field(connStr, "Server") ?? "localhost";
            var dbPort = Field(connStr, "Port") ?? "3306";
            var dbName = Field(connStr, "Database");
            var dbUser = Field(connStr, "User") ?? Field(connStr, "Uid") ?? "root";
            var dbPass = Field(connStr, "Password") ?? Field(connStr, "Pwd") ?? "";
            if (string.IsNullOrWhiteSpace(dbName))
                return new(false, null, 0, "Veritabanı adı bağlantı dizesinde bulunamadı.");

            var dumpExe = ResolveMysqldump();
            if (dumpExe is null)
                return new(false, null, 0, "mysqldump.exe bulunamadı. Yolu ayarlardan girin.");

            var dir = ResolvedPath;
            Directory.CreateDirectory(dir);

            var ts      = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var sqlPath = Path.Combine(dir, $"{dbName}_{ts}.sql");
            var zipPath = Path.Combine(dir, $"{dbName}_{ts}.zip");

            var psi = new ProcessStartInfo
            {
                FileName               = dumpExe,
                UseShellExecute        = false,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            // --no-tablespaces: sınırlı app kullanıcısında PROCESS yetkisi yok; metadata gereksiz.
            // --single-transaction: InnoDB tutarlı, kilitsiz. routines+triggers dahil.
            foreach (var a in new[] { $"--host={dbHost}", $"--port={dbPort}", $"--user={dbUser}",
                "--single-transaction", "--routines", "--triggers", "--no-tablespaces",
                "--databases", dbName, $"--result-file={sqlPath}" })
                psi.ArgumentList.Add(a);
            psi.Environment["MYSQL_PWD"] = dbPass;

            using (var proc = Process.Start(psi) ?? throw new InvalidOperationException("mysqldump başlatılamadı."))
            {
                var stderr = proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(5 * 60 * 1000))
                {
                    try { proc.Kill(); } catch { }
                    SafeDelete(sqlPath);
                    return new(false, null, 0, "mysqldump zaman aşımına uğradı (5 dk).");
                }
                if (proc.ExitCode != 0)
                {
                    SafeDelete(sqlPath);
                    return new(false, null, 0, $"mysqldump hata (exit {proc.ExitCode}): {Trunc(stderr)}");
                }
            }

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(sqlPath, Path.GetFileName(sqlPath), CompressionLevel.Optimal);
            SafeDelete(sqlPath);

            // Saklama: son N yedek tutulur (günde bir → ≈ N gün).
            var keep = cfg.Get<int>("backup_retention_days");
            if (keep < 1) keep = 14;
            foreach (var old in Directory.GetFiles(dir, $"{dbName}_*.zip")
                         .OrderByDescending(f => f).Skip(keep))
                SafeDelete(old);

            var size = new FileInfo(zipPath).Length;
            logger.LogInformation("DB yedeği alındı: {Path} ({Size} byte)", zipPath, size);
            return new(true, zipPath, size, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DB yedekleme hatası");
            return new(false, null, 0, ex.Message);
        }
    }

    private string? ResolveMysqldump()
    {
        var configured = cfg.Get<string>("backup_mysqldump_path");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        return new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
        }.FirstOrDefault(File.Exists);
    }

    private static string? Field(string cs, string name)
    {
        var m = Regex.Match(cs, $@"(?:^|;)\s*{Regex.Escape(name)}\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static void SafeDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    private static string Trunc(string? s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 400 ? s[..400] : s);
}
