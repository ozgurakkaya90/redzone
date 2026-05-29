namespace RiskManagement.Models;

// RiskStatus, ActionStatus, Roles → Models/RiskConstants.cs (zaten mevcut, kapsamlı)

/// <summary>Denetim bulgusu durum sabitleri.</summary>
public static class FindingStatus
{
    public const string Open             = "open";
    public const string ClosureRequested = "closure_requested";
    public const string Closed           = "closed";
}

/// <summary>Etik bildirim durum sabitleri.</summary>
public static class EthicsStatus
{
    public const string Pending              = "pending";
    public const string Irrelevant           = "irrelevant";
    public const string EthicsBoardNotified  = "ethics_board_notified";
    public const string DisciplinaryReferred = "disciplinary_referred";
    public const string NoViolation          = "no_violation";
}

/// <summary>Kapanış talebi durum sabitleri.</summary>
public static class ClosureRequestStatus
{
    public const string Pending  = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}
