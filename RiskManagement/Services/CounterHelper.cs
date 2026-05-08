using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Atomik sıralı sayaç üretimi. ConcurrencyCheck + retry ile eşzamanlılık güvenli.
///
/// Senaryo 1 — eş zamanlı güncelleme:
///   İki request aynı Value=N'i okur. İlki N+1 yazar (başarı). İkincisi
///   SaveChanges sırasında WHERE Value=N artık 0 satır etkilediği için
///   DbUpdateConcurrencyException alır; tracker temizlenir, N+1 okunarak N+2 yazılır.
///
/// Senaryo 2 — eş zamanlı ilk ekleme:
///   Her ikisi de counter=null görür, her ikisi de INSERT dener. Biri PK çakışmasıyla
///   DbUpdateException alır; tracker temizlenir, eklenen satır okunarak 2 yazılır.
/// </summary>
internal static class CounterHelper
{
    internal static long GetNext(AppDbContext db, string key, int maxRetries = 5)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                db.ChangeTracker.Clear(); // retry'da stale entry'leri temizle
                using var tx = db.Database.BeginTransaction();

                var counter = db.Counters.FirstOrDefault(c => c.Key == key);
                if (counter is null)
                {
                    db.Counters.Add(new Counter { Key = key, Value = 1 });
                    db.SaveChanges();
                    tx.Commit();
                    return 1;
                }

                counter.Value++;
                db.SaveChanges(); // ConcurrencyCheck başarısızsa exception fırlatır
                tx.Commit();
                return counter.Value;
            }
            catch (DbUpdateException) when (attempt < maxRetries - 1)
            {
                // DbUpdateConcurrencyException (eş zamanlı güncelleme) veya
                // unique constraint ihlali (eş zamanlı ilk ekleme) — bir sonraki
                // iterasyonda ChangeTracker.Clear() + taze okuma yapılır.
            }
        }

        throw new InvalidOperationException(
            $"Sıralı kod üretilemedi ('{key}'): {maxRetries} denemede çakışma çözülemedi.");
    }
}
