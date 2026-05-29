namespace RiskManagement.Services.Ai;

public interface IAiCompletionService
{
    bool IsAvailable { get; }
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default);
}
