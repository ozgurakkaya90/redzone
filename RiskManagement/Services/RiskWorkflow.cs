using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Risk iş akışı için izin verilen durum geçişlerini tanımlar.
/// Sabit veri — tüm transition kontrolleri buradan yönetilir.
/// </summary>
public static class RiskWorkflow
{
    private static readonly IReadOnlyDictionary<string, string[]> _allowed =
        new Dictionary<string, string[]>
        {
            [RiskStatus.Proposed]          = [RiskStatus.UnderReview,       RiskStatus.Rejected],
            [RiskStatus.UnderReview]       = [RiskStatus.AwaitingApproval,  RiskStatus.Approved, RiskStatus.Rejected],
            [RiskStatus.AwaitingApproval]  = [RiskStatus.Approved,          RiskStatus.UnderReview, RiskStatus.Rejected],
            [RiskStatus.Approved]          = [RiskStatus.StrategySet,       RiskStatus.Controlled, RiskStatus.ActionPlanned, RiskStatus.Rejected],
            [RiskStatus.StrategySet]       = [RiskStatus.Controlled,        RiskStatus.Rejected],
            [RiskStatus.Controlled]        = [RiskStatus.ResidualEvaluated, RiskStatus.ActionPlanned, RiskStatus.Rejected],
            [RiskStatus.ResidualEvaluated] = [RiskStatus.ActionPlanned,     RiskStatus.RiskAccepted, RiskStatus.Rejected],
            [RiskStatus.ActionPlanned]     = [RiskStatus.Controlled,        RiskStatus.ResidualEvaluated],
            [RiskStatus.RiskAccepted]      = [],
            [RiskStatus.Rejected]          = [RiskStatus.UnderReview],  // risk_manager reddedilen riski yeniden incelemeye alabilir
            [RiskStatus.Closed]            = [],
        };

    /// <summary>Verilen geçişin iş akışı kurallarına göre geçerli olup olmadığını döner.</summary>
    public static bool CanTransition(string from, string to) =>
        _allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Bir durumdan ulaşılabilecek tüm hedef durumları döner.</summary>
    public static IReadOnlyList<string> AllowedTransitions(string from) =>
        _allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Durum bir son-durum mu (terminal state)?</summary>
    public static bool IsTerminal(string status) =>
        status is RiskStatus.RiskAccepted or RiskStatus.Closed;

    /// <summary>Durum aktif bir iş akışı sürecinde mi (kullanıcı aksiyon alabilir)?</summary>
    public static bool IsActive(string status) =>
        !IsTerminal(status);

    /// <summary>İzin verilmeyen geçiş için fırlatılır.</summary>
    public static void EnsureCanTransition(string from, string to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException(
                $"Geçersiz risk durum geçişi: '{from}' → '{to}'");
    }
}
