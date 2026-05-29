namespace RiskManagement.Models;

public static class RiskStatus
{
    public const string Proposed          = "proposed";
    public const string UnderReview       = "under_review";
    public const string AwaitingApproval  = "awaiting_approval";
    public const string Approved          = "approved";
    public const string StrategySet       = "strategy_set";
    public const string Controlled        = "controlled";
    public const string ResidualEvaluated = "residual_evaluated";
    public const string ActionPlanned     = "action_planned";
    public const string RiskAccepted      = "risk_accepted";
    public const string Rejected          = "rejected";
    public const string Closed            = "closed";

    public static readonly string[] All =
    [
        Proposed, UnderReview, AwaitingApproval, Approved,
        StrategySet, Controlled, ResidualEvaluated, ActionPlanned, RiskAccepted, Rejected, Closed
    ];

    public static string Label(string s) => s switch
    {
        Proposed          => "Risk Önerisi",
        UnderReview       => "İncelemede",
        AwaitingApproval  => "Onay Bekliyor",
        Approved          => "Komite Onaylı",
        StrategySet       => "Strateji Belirlendi",
        Controlled        => "Kontroller Tanımlandı",
        ResidualEvaluated => "Kalan Risk Ölçüldü",
        ActionPlanned     => "Aksiyon Planı Aktif",
        RiskAccepted      => "Risk Kabul Edildi",
        Rejected          => "Reddedildi",
        _                 => s
    };
}

public static class Roles
{
    public const string Admin        = "admin";
    public const string Committee    = "committee";
    public const string RiskManager  = "risk_manager";
    public const string RiskOwner    = "risk_owner";
    public const string User         = "user";
    public const string Auditor      = "auditor";
    public const string AuditManager = "audit_manager";
    public const string FindingOwner = "finding_owner";
    public const string EthicsBoard  = "ethics_board";

    public static readonly string[] All =
    [
        Admin, Committee, RiskManager, RiskOwner, User,
        Auditor, AuditManager, FindingOwner, EthicsBoard
    ];

    public static readonly string[] RiskManagers = [Admin, Committee, RiskManager, AuditManager];
}

public static class ActionStatus
{
    public const string Planned    = "planned";
    public const string InProgress = "in_progress";
    public const string Completed  = "completed";
    public const string Cancelled  = "cancelled";

    public static string Label(string s) => s switch
    {
        Planned    => "Planlandı",
        InProgress => "Devam Ediyor",
        Completed  => "Tamamlandı",
        Cancelled  => "İptal",
        _          => s
    };
}

public static class EvalType
{
    public const string Initial  = "initial";
    public const string Residual = "residual";
}

public static class AuditStatus
{
    public const string Planned    = "planned";
    public const string InProgress = "in_progress";
    public const string Completed  = "completed";
}
