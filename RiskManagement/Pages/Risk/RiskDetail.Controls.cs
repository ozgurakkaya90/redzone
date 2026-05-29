// RiskDetail.Controls.cs — Kontrol yönetimi state'i ve metodları (partial class)
namespace RiskManagement.Pages.Risk;

public partial class RiskDetail
{
    // ── Kontrol formu state ──────────────────────────────────────────────────
    private bool   _showControl;
    private string _ctrlDesc = "", _ctrlType = "Önleyici", _ctrlEff = "", _ctrlFreq = "";
    private int    _ctrlOwnerId;

    // ── Kontrol düzenleme state ──────────────────────────────────────────────
    private int    _editControlId;
    private string _editCtrlDesc = "", _editCtrlType = "Önleyici", _editCtrlEff = "", _editCtrlFreq = "";
    private int    _editCtrlOwnerId;

    // ── Metodlar ─────────────────────────────────────────────────────────────

    private void ToggleControlForm() => _showControl = !_showControl;

    private async Task SaveControl()
    {
        if (string.IsNullOrWhiteSpace(_ctrlDesc)) { Notify("Açıklama zorunludur.", true); return; }
        _loading = true;
        await RiskSvc.AddControlAsync(Id, _ctrlDesc, _ctrlType, _ctrlEff, _ctrlFreq, UserId, _ctrlOwnerId);
        Notify("Kontrol eklendi.");
        _ctrlDesc = ""; _ctrlEff = ""; _ctrlFreq = ""; _ctrlOwnerId = 0; _showControl = false;
        Load();
        _loading = false;
    }

    private void StartEditControl(Models.Control c)
    {
        _editControlId   = c.Id;
        _editCtrlDesc    = c.Description;
        _editCtrlType    = c.ControlType;
        _editCtrlEff     = c.Effectiveness ?? "";
        _editCtrlFreq    = c.Frequency ?? "";
        _editCtrlOwnerId = c.OwnerDeptId ?? 0;
    }

    private async Task SaveEditControl(int cid)
    {
        await RiskSvc.EditControlAsync(Id, cid, _editCtrlDesc, _editCtrlType,
            NullIfBlank(_editCtrlEff), NullIfBlank(_editCtrlFreq),
            _editCtrlOwnerId > 0 ? _editCtrlOwnerId : null, UserId);
        _editControlId = 0;
        Notify("Kontrol güncellendi.");
        Load();
    }

    private void AskDeleteControl(int cid)
    {
        _confirmTitle    = "Kontrolü sil";
        _confirmMessage  = "Bu kontrol kalıcı olarak silinecek. Devam etmek istiyor musunuz?";
        _confirmedAction = () => DeleteControl(cid);
        _confirmVisible  = true;
    }

    private async Task DeleteControl(int cid)
    {
        await RiskSvc.DeleteControlAsync(Id, cid, UserId);
        Notify("Kontrol silindi.");
        Load();
    }
}
