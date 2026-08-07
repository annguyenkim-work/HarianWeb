using Microsoft.Extensions.Logging;
using NewHarian.Domain.Entities;

namespace NewHarian.Infrastructure.Logging;

public sealed class DbLoggerProvider(AppLogQueue queue) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DbLogger(categoryName, queue);

    public void Dispose() { }
}

internal sealed class DbLogger(string categoryName, AppLogQueue queue) : ILogger
{
    private const int MaxMessageLength = 4000;
    private const int MaxExceptionLength = 8 * 1024;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel < LogLevel.Information) return false;
        if (categoryName.StartsWith("Microsoft.", StringComparison.Ordinal)
            || categoryName.StartsWith("System.", StringComparison.Ordinal))
            return false;
        if (!categoryName.StartsWith("NewHarian.", StringComparison.Ordinal)
            && categoryName != "DbSeeder")
            return false;
        if (categoryName.StartsWith("NewHarian.Infrastructure.Logging", StringComparison.Ordinal))
            return false;
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message;
        try
        {
            message = formatter(state, exception) ?? string.Empty;
        }
        catch
        {
            message = state?.ToString() ?? string.Empty;
        }

        if (message.Length > MaxMessageLength)
            message = message[..MaxMessageLength];

        string? exText = null;
        if (exception is not null)
        {
            exText = exception.ToString();
            if (exText.Length > MaxExceptionLength)
                exText = exText[..MaxExceptionLength];
        }

        var entry = new AppLogEntry
        {
            CreatedAtUtc = DateTime.UtcNow,
            Level = (short)logLevel,
            Module = ToModule(categoryName),
            Category = categoryName.Length > 256 ? categoryName[..256] : categoryName,
            Message = message,
            Exception = exText
        };

        queue.TryWrite(entry);
    }

    private static string ToModule(string category)
    {
        var i = category.LastIndexOf('.');
        var name = i >= 0 ? category[(i + 1)..] : category;
        return name.Length > 64 ? name[..64] : name;
    }
}
