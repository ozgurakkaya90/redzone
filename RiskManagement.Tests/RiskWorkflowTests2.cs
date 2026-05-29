using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// RiskWorkflow state machine testleri — geçerli ve geçersiz geçişler.
/// </summary>
public class RiskWorkflowTests2
{
    // ── Geçerli geçişler ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(RiskStatus.Proposed,          RiskStatus.UnderReview)]
    [InlineData(RiskStatus.UnderReview,       RiskStatus.AwaitingApproval)]
    [InlineData(RiskStatus.AwaitingApproval,  RiskStatus.Approved)]
    [InlineData(RiskStatus.Approved,          RiskStatus.StrategySet)]
    [InlineData(RiskStatus.StrategySet,       RiskStatus.Controlled)]
    [InlineData(RiskStatus.Controlled,        RiskStatus.ResidualEvaluated)]
    [InlineData(RiskStatus.ResidualEvaluated, RiskStatus.ActionPlanned)]
    [InlineData(RiskStatus.ResidualEvaluated, RiskStatus.RiskAccepted)]
    public void CanTransition_ValidTransitions_ReturnsTrue(string from, string to)
    {
        Assert.True(RiskWorkflow.CanTransition(from, to));
    }

    // ── Ret geçişleri her aşamadan geçerli ───────────────────────────────────

    [Theory]
    [InlineData(RiskStatus.Proposed)]
    [InlineData(RiskStatus.UnderReview)]
    [InlineData(RiskStatus.AwaitingApproval)]
    [InlineData(RiskStatus.Approved)]
    [InlineData(RiskStatus.StrategySet)]
    public void CanTransition_RejectFromAnyActiveStatus_ReturnsTrue(string from)
    {
        Assert.True(RiskWorkflow.CanTransition(from, RiskStatus.Rejected));
    }

    // ── Geçersiz geçişler ────────────────────────────────────────────────────

    [Theory]
    [InlineData(RiskStatus.Proposed,    RiskStatus.Approved)]   // atlama
    [InlineData(RiskStatus.Rejected,    RiskStatus.Proposed)]   // terminal'den geri
    [InlineData(RiskStatus.Proposed,    RiskStatus.ActionPlanned)] // çok ileri atlama
    public void CanTransition_InvalidTransitions_ReturnsFalse(string from, string to)
    {
        Assert.False(RiskWorkflow.CanTransition(from, to));
    }

    // ── Terminal durumlar ────────────────────────────────────────────────────

    [Theory]
    [InlineData("closed")]
    [InlineData(RiskStatus.RiskAccepted)]
    public void IsTerminal_TerminalStatuses_ReturnsTrue(string status)
    {
        Assert.True(RiskWorkflow.IsTerminal(status));
    }

    [Fact]
    public void IsTerminal_Rejected_ReturnsFalse()
    {
        // Reddedilen riskler risk_manager tarafından yeniden incelemeye alınabilir
        Assert.False(RiskWorkflow.IsTerminal(RiskStatus.Rejected));
    }

    [Theory]
    [InlineData(RiskStatus.Proposed)]
    [InlineData(RiskStatus.Approved)]
    [InlineData(RiskStatus.ActionPlanned)]
    public void IsTerminal_ActiveStatuses_ReturnsFalse(string status)
    {
        Assert.False(RiskWorkflow.IsTerminal(status));
    }

    // ── AllowedTransitions ───────────────────────────────────────────────────

    [Fact]
    public void AllowedTransitions_Proposed_HasTwoOptions()
    {
        var allowed = RiskWorkflow.AllowedTransitions(RiskStatus.Proposed);
        Assert.Contains(RiskStatus.UnderReview, allowed);
        Assert.Contains(RiskStatus.Rejected, allowed);
        Assert.Equal(2, allowed.Count);
    }

    [Fact]
    public void AllowedTransitions_Rejected_OnlyUnderReview()
    {
        // Reddedilen riskler sadece "incelemeye al" adımına döndürülebilir
        var allowed = RiskWorkflow.AllowedTransitions(RiskStatus.Rejected);
        Assert.Single(allowed);
        Assert.Contains(RiskStatus.UnderReview, allowed);
    }

    // ── EnsureCanTransition exception fırlatma ───────────────────────────────

    [Fact]
    public void EnsureCanTransition_InvalidTransition_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RiskWorkflow.EnsureCanTransition(RiskStatus.Rejected, RiskStatus.Proposed));
    }

    [Fact]
    public void EnsureCanTransition_ValidTransition_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            RiskWorkflow.EnsureCanTransition(RiskStatus.Proposed, RiskStatus.UnderReview));
        Assert.Null(ex);
    }
}
