namespace RiskManagement.Pages.Admin;

public partial class SystemConfig
{
    // ── MCP API Anahtarları ────────────────────────────────────────────────
    private List<Services.McpApiKeyListItem> _mcpKeys = [];
    private List<Services.UserChoice> _mcpUserChoices = [];
    private bool _mcpShowCreate;
    private string _mcpCreateName = "";
    private string _mcpCreateScopeUserId = "";
    private string? _mcpNewKey;
    private string? _mcpError;
    private string _mcpBaseUrl = "";
    private int _mcpCurrentUserId;

    private void McpLoadKeys()
    {
        try { _mcpKeys = McpKeySvc.GetAll(); }
        catch (Exception ex) { _mcpError = $"API anahtarları yüklenemedi: {ex.Message}"; }
        try { _mcpUserChoices = McpKeySvc.GetUserChoices(); }
        catch (Exception ex) { _mcpError = (_mcpError is null ? "" : _mcpError + " | ") + $"Kullanıcılar yüklenemedi: {ex.Message}"; }
    }

    private void McpCreateKey()
    {
        if (string.IsNullOrWhiteSpace(_mcpCreateName))
        { _mcpError = "Anahtar adı zorunludur."; return; }
        if (_mcpCurrentUserId == 0)
        { _mcpError = "Oturum bilgisi alınamadı. Sayfayı yenileyip tekrar deneyin."; return; }
        try
        {
            int? scopeUserId = int.TryParse(_mcpCreateScopeUserId, out var sid) ? sid : null;
            var (_, plain) = McpKeySvc.Create(_mcpCreateName.Trim(), _mcpCurrentUserId, scopeUserId);
            _mcpNewKey = plain;
            _mcpShowCreate = false;
            _mcpCreateName = "";
            _mcpCreateScopeUserId = "";
            _mcpError = null;
            _mcpKeys = McpKeySvc.GetAll();
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
            _mcpError = $"Anahtar oluşturulamadı: {inner}";
        }
    }

    private void McpRevoke(int id) { McpKeySvc.Revoke(id); _mcpKeys = McpKeySvc.GetAll(); }
    private void McpDelete(int id) { McpKeySvc.Delete(id); _mcpKeys = McpKeySvc.GetAll(); }

    private string GetMcpClaudeConfig()
    {
        var key = _mcpNewKey ?? "<API_KEY>";
        return "{\n" +
               "  \"mcpServers\": {\n" +
               "    \"redzone\": {\n" +
               "      \"type\": \"http\",\n" +
               "      \"url\": \"" + _mcpBaseUrl + "/mcp\",\n" +
               "      \"headers\": {\n" +
               "        \"X-Api-Key\": \"" + key + "\"\n" +
               "      }\n" +
               "    }\n" +
               "  }\n" +
               "}";
    }
}
