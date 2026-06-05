using RiskManagement.Models;
using RiskManagement.Services.Ai;

namespace RiskManagement.Pages.Admin;

public partial class SystemConfig
{
    // ── Yapay Zeka Ayarları ───────────────────────────────────────────────
    private AiSettings _aiSettings = new();
    private string _aiProvider = "disabled";
    private string _aiApiKey   = "";
    private string _aiModel    = "";
    private string _aiBaseUrl  = "";
    private bool   _aiSaving, _aiTesting;
    private bool   _aiSaveError, _aiTestOk;
    private string _aiSaveMsg    = "";
    private string _aiTestResult = "";

    private record ProviderPreset(string Name, string BaseUrl, string Model, string KeyHint, string KeyUrl);
    private bool _showProviderTable;
    private readonly ProviderPreset[] _knownProviders =
    [
        new("OpenAI",      "https://api.openai.com/v1",                               "gpt-4o-mini",          "platform.openai.com",   "https://platform.openai.com/api-keys"),
        new("DeepSeek",    "https://api.deepseek.com/v1",                             "deepseek-chat",         "platform.deepseek.com", "https://platform.deepseek.com"),
        new("Google Gemini","https://generativelanguage.googleapis.com/v1beta/openai","gemini-2.0-flash",      "aistudio.google.com",   "https://aistudio.google.com"),
        new("xAI Grok",    "https://api.x.ai/v1",                                    "grok-3-mini",           "console.x.ai",          "https://console.x.ai"),
        new("Groq",        "https://api.groq.com/openai/v1",                          "llama-3.3-70b-versatile","console.groq.com",     "https://console.groq.com"),
        new("OpenRouter",  "https://openrouter.ai/api/v1",                            "openai/gpt-4o-mini",    "openrouter.ai",         "https://openrouter.ai/keys"),
        new("Mistral",     "https://api.mistral.ai/v1",                               "mistral-small-latest",  "console.mistral.ai",    "https://console.mistral.ai"),
        new("Together AI", "https://api.together.xyz/v1",                             "meta-llama/Llama-3-8b-chat-hf","together.ai",   "https://api.together.ai"),
        new("Ollama (Yerel)","http://localhost:11434/v1",                             "llama3.2",              "— (gerekmez)",          "https://ollama.com"),
    ];

    private void FillProvider(ProviderPreset p)
    {
        _aiBaseUrl = p.BaseUrl;
        _aiModel   = p.Model;
        _aiApiKey  = "";
        _showProviderTable = false;
    }

    private void LoadAiSettings()
    {
        _aiSettings  = CfgSvc.GetAiSettings();
        _aiProvider  = _aiSettings.Provider;
        _aiApiKey    = _aiSettings.ApiKey;
        _aiModel     = _aiSettings.Model;
        _aiBaseUrl   = _aiSettings.BaseUrl;
        _aiSaveMsg   = "";
        _aiTestResult = "";
    }

    private void OnProviderChanged()
    {
        _aiModel    = "";
        _aiBaseUrl  = "";
        _aiTestResult = "";
    }

    private void SaveAiSettings()
    {
        _aiSaving  = true;
        _aiSaveMsg = "";
        try
        {
            var settings = new AiSettings(_aiProvider, _aiApiKey.Trim(), _aiModel.Trim(), _aiBaseUrl.Trim());
            CfgSvc.SetAiSettings(settings);
            _aiSettings = settings;
            _aiSaveMsg  = "Ayarlar kaydedildi.";
            _aiSaveError = false;
        }
        catch (Exception ex)
        {
            _aiSaveMsg   = $"Kayıt hatası: {ex.Message}";
            _aiSaveError = true;
        }
        finally { _aiSaving = false; }
    }

    private async Task TestAiConnection()
    {
        _aiTesting    = true;
        _aiTestResult = "";
        try
        {
            var testResult = await AiDraftSvc.TestConnectionAsync(
                _aiProvider, _aiApiKey.Trim(), _aiModel.Trim(), _aiBaseUrl.Trim());
            _aiTestOk     = true;
            _aiTestResult = $"✓ Bağlantı başarılı — {testResult}";
        }
        catch (Exception ex)
        {
            _aiTestOk     = false;
            _aiTestResult = $"✗ {ex.Message}";
        }
        finally { _aiTesting = false; }
    }
}
