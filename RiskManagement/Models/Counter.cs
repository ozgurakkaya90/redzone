using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class Counter
{
    public string Key { get; set; } = string.Empty;

    // ConcurrencyCheck: EF, UPDATE'e WHERE Value = @original ekler.
    // Başka bir transaction aynı satırı güncellemiş olursa SaveChanges
    // DbUpdateConcurrencyException fırlatır — üst katman retry yapar.
    [ConcurrencyCheck]
    public long Value { get; set; }
}
