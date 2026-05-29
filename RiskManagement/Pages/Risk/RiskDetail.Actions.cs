// RiskDetail.Actions.cs — Aksiyon planı yönetimi state'i ve metodları (partial class)
using RiskManagement.Models;

namespace RiskManagement.Pages.Risk;

public partial class RiskDetail
{
    // ── Aksiyon formu state ──────────────────────────────────────────────────
    private bool      _showAction;
    private bool      _showAccept;
    private string    _actDesc   = "";
    private int       _actOwnerId;
    private DateTime? _actDue;
    private string    _acceptReason = "";

    // ── Aksiyon düzenleme state ──────────────────────────────────────────────
    private int       _editActionId;
    private string    _editActDesc    = "";
    private int       _editActOwnerId;
    private DateTime? _editActDue;

    // ── Gecikme & kapanış state ──────────────────────────────────────────────
    private int    _editDelayActionId;
    private string _delayReasonDraft = "";
    private string _fClosureNote     = "";
    private string _fDelayReason     = "";

    // ── Metodlar ─────────────────────────────────────────────────────────────

    private void OpenActionForm()
    {
        _showAction = !_showAction;
        if (_showAction && !_actDue.HasValue)
            _actDue = DateTime.Today.AddDays(CfgSvc.GetDefaultActionDueDays());
    }

    private async Task SaveAction()
    {
        if (string.IsNullOrWhiteSpace(_actDesc) || _actOwnerId <= 0)
        { Notify("Açıklama ve sorumlu departman zorunludur.", true); return; }
        if (CfgSvc.IsActionFieldRequired("due_date") && !_actDue.HasValue)
        { Notify("Hedef tarih zorunludur.", true); return; }

        _loading = true;
        var dept = _departments.FirstOrDefault(d => d.Id == _actOwnerId);
        await RiskSvc.AddActionAsync(Id, _actDesc, dept?.Name ?? "",
            _actDue.HasValue ? DateOnly.FromDateTime(_actDue.Value) : null,
            UserId, _actOwnerId);
        Notify("Aksiyon eklendi.");
        _actDesc = ""; _actOwnerId = 0; _actDue = null; _showAction = false;
        Load();
        _loading = false;
    }

    private void StartEditAction(ActionPlan a)
    {
        _editActionId   = a.Id;
        _editActDesc    = a.Description;
        _editActOwnerId = a.OwnerDeptId ?? 0;
        _editActDue     = a.DueDate.HasValue ? a.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null;
        _fClosureNote   = a.ClosureNote ?? "";
        _fDelayReason   = a.DelayReason ?? "";
    }

    private async Task SaveEditAction(int aid)
    {
        var a = _risk?.ActionPlans.FirstOrDefault(x => x.Id == aid);
        if (CfgSvc.IsActionFieldRequired("closure_note")
            && a?.Status == ActionStatus.Completed
            && string.IsNullOrWhiteSpace(_fClosureNote))
        { Notify("Tamamlanan aksiyonlar için kapanış notu zorunludur.", true); return; }

        if (CfgSvc.IsActionFieldRequired("delay_reason")
            && a != null && a.DueDate.HasValue
            && a.DueDate.Value < DateOnly.FromDateTime(DateTime.Today)
            && a.Status is ActionStatus.Planned or ActionStatus.InProgress
            && string.IsNullOrWhiteSpace(_fDelayReason))
        { Notify("Gecikmiş aksiyonlar için gecikme nedeni zorunludur.", true); return; }

        await RiskSvc.EditActionAsync(Id, aid, _editActDesc,
            _editActOwnerId > 0 ? _editActOwnerId : null,
            _editActDue.HasValue ? DateOnly.FromDateTime(_editActDue.Value) : null,
            UserId);

        // ClosureNote ve DelayReason doğrudan DB üzerinden güncelle
        Db.ChangeTracker.Clear();
        var ap = await Db.ActionPlans.FindAsync(aid);
        if (ap != null)
        {
            ap.ClosureNote = NullIfBlank(_fClosureNote);
            ap.DelayReason = NullIfBlank(_fDelayReason);
            Db.ActionPlans.Update(ap);
            await Db.SaveChangesAsync();
        }

        _editActionId = 0;
        Notify("Aksiyon güncellendi.");
        Load();
    }

    private async Task UpdateAction(int aid, string status)
    {
        await RiskSvc.UpdateActionStatusAsync(Id, aid, status, UserId);
        Load();
        Notify(_risk?.Status == RiskStatus.ResidualEvaluated
            ? "Tüm aksiyonlar tamamlandı. Kalan riski yeniden değerlendirin."
            : "Aksiyon güncellendi.");
    }

    private void OpenDelayEdit(ActionPlan a, string? initialText)
    {
        _editDelayActionId = a.Id;
        _delayReasonDraft  = initialText ?? string.Empty;
    }

    private async Task SaveDelayReason(ActionPlan a)
    {
        Db.ChangeTracker.Clear();
        var ap = await Db.ActionPlans.FindAsync(a.Id);
        if (ap != null)
        {
            ap.DelayReason = string.IsNullOrWhiteSpace(_delayReasonDraft) ? null : _delayReasonDraft.Trim();
            Db.ActionPlans.Update(ap);
            await Db.SaveChangesAsync();
        }
        _editDelayActionId = 0;
        Toast.Success("Gecikme nedeni kaydedildi.");
        Load();
    }

    private void AskDeleteAction(int aid)
    {
        _confirmTitle    = "Aksiyonu sil";
        _confirmMessage  = "Bu aksiyon kalıcı olarak silinecek. Devam etmek istiyor musunuz?";
        _confirmedAction = () => DeleteAction(aid);
        _confirmVisible  = true;
    }

    private async Task DeleteAction(int aid)
    {
        await RiskSvc.DeleteActionAsync(Id, aid, UserId);
        Notify("Aksiyon silindi.");
        Load();
    }
}
