using Serilog.Core;
using Serilog.Events;

namespace MrSixResultsComparator.Core.Services;

/// <summary>
/// Serilog sink that keeps the most recent Warning+ events in a bounded ring buffer
/// so the UI can surface them without opening the log file. Singleton-friendly:
/// register once in DI and re-use across LoggingService.Initialize calls.
/// </summary>
public class InMemoryLogSink : ILogEventSink
{
    private readonly int _capacity;
    private readonly LinkedList<CapturedLogEvent> _events = new();
    private readonly object _lock = new();

    public InMemoryLogSink(int capacity = 500)
    {
        _capacity = capacity;
    }

    /// <summary>Fires whenever a new event is added (UI-bindable).</summary>
    public event Action? EventAdded;

    public void Emit(LogEvent logEvent)
    {
        // Only capture Errors (and Fatals) - Warnings are too chatty to be useful as a UI list.
        // Users who want the full picture should open the JSON log file.
        if (logEvent.Level < LogEventLevel.Error)
            return;

        var captured = new CapturedLogEvent(
            TimestampUtc: logEvent.Timestamp.UtcDateTime,
            Level: logEvent.Level,
            Message: logEvent.RenderMessage(),
            ExceptionTypeName: logEvent.Exception?.GetType().FullName,
            ExceptionMessage: logEvent.Exception?.Message,
            ExceptionToString: logEvent.Exception?.ToString());

        lock (_lock)
        {
            _events.AddLast(captured);
            while (_events.Count > _capacity)
                _events.RemoveFirst();
        }

        EventAdded?.Invoke();
    }

    /// <summary>Most-recent-first snapshot.</summary>
    public IReadOnlyList<CapturedLogEvent> Snapshot()
    {
        lock (_lock)
        {
            var list = _events.ToList();
            list.Reverse();
            return list;
        }
    }

    public int Count
    {
        get { lock (_lock) { return _events.Count; } }
    }

    public int ErrorCount
    {
        get { lock (_lock) { return _events.Count(e => e.Level >= LogEventLevel.Error); } }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
        EventAdded?.Invoke();
    }
}

/// <summary>
/// Immutable snapshot of a Serilog event captured by <see cref="InMemoryLogSink"/>.
/// We intentionally flatten the data (rendered message + exception strings) so the UI
/// doesn't hold on to the original LogEvent, its structured properties, or message
/// template machinery.
/// </summary>
public record CapturedLogEvent(
    DateTime TimestampUtc,
    LogEventLevel Level,
    string Message,
    string? ExceptionTypeName,
    string? ExceptionMessage,
    string? ExceptionToString);
