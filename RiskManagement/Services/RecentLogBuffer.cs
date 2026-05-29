using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RiskManagement.Services;

/// <summary>Son N adet Warning/Error log girişini bellekte tutan ring buffer.</summary>
public sealed class RecentLogBuffer
{
    public const int Capacity = 50;

    private readonly ConcurrentQueue<LogEntry> _queue = new();

    public void Add(LogEntry entry)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > Capacity)
            _queue.TryDequeue(out _);
    }

    public IReadOnlyList<LogEntry> GetAll() =>
        _queue.Reverse().ToArray(); // en yeni önce
}

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel       Level,
    string         Category,
    string         Message,
    string?        Exception
);

/// <summary>ILoggerProvider — singleton olarak DI'ya kaydedilir.</summary>
public sealed class RecentLogBufferProvider(RecentLogBuffer buffer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new RecentLogBufferLogger(categoryName, buffer);

    public void Dispose() { }
}

internal sealed class RecentLogBufferLogger(string category, RecentLogBuffer buffer) : ILogger
{
    // Maskelenmesi gereken anahtar kelimeler (büyük/küçük harf duyarsız)
    private static readonly string[] _sensitivePatterns =
        ["password", "passwd", "secret", "token", "apikey", "connectionstring", "bindpassword"];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var raw = formatter(state, exception);
        var msg = MaskSensitive(raw);
        var exc = exception is null ? null : MaskSensitive($"{exception.GetType().Name}: {exception.Message}");

        // Microsoft infra kanallarını yalnızca Error ve üstü için tut
        if (logLevel < LogLevel.Error &&
            (category.StartsWith("Microsoft.", StringComparison.Ordinal) ||
             category.StartsWith("System.",    StringComparison.Ordinal)))
            return;

        buffer.Add(new LogEntry(DateTimeOffset.Now, logLevel, ShortCategory(category), msg, exc));
    }

    private static string ShortCategory(string cat)
    {
        // "RiskManagement.Pages.Admin.SystemConfig" → "SystemConfig"
        var last = cat.LastIndexOf('.');
        return last >= 0 ? cat[(last + 1)..] : cat;
    }

    private static string MaskSensitive(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        foreach (var pattern in _sensitivePatterns)
        {
            if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return "[hassas veri maskelendi]";
        }
        return input;
    }
}
