namespace RiskManagement.Services.Ai;

public sealed class NullCompletionService : IAiCompletionService
{
    public bool IsAvailable => false;

    public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        => throw new InvalidOperationException("AI servisi yapılandırılmamış. Sistem Yapılandırması → Yapay Zeka bölümünden ayarları girin.");
}
