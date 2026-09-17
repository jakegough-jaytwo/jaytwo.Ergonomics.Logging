#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace jaytwo.Ergonomics.Logging.Tests;

internal sealed class CapturingLogger : ILogger
{
    private readonly AsyncLocal<Scope> _currentScope = new();
    private readonly bool _isEnabled;

    public CapturingLogger(bool isEnabled = true)
    {
        _isEnabled = isEnabled;
    }

    public List<LogEntry> Entries { get; } = new();

    // Implemented implicitly on purpose. An explicit implementation hides this from a
    // CapturingLogger-typed variable, which lets extension methods named BeginScope win in
    // tests when they would lose to this instance method in real calling code.
    public IDisposable BeginScope<TState>(TState state)
    {
        var scope = new Scope(this, state, _currentScope.Value);
        _currentScope.Value = scope;
        return scope;
    }

    public bool IsEnabled(LogLevel logLevel) => _isEnabled;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var fields = new Dictionary<string, object>(StringComparer.Ordinal);
        var scopes = new Stack<Scope>();

        for (var scope = _currentScope.Value; scope is not null; scope = scope.Parent)
        {
            scopes.Push(scope);
        }

        while (scopes.Count > 0)
        {
            if (scopes.Pop().State is IEnumerable<KeyValuePair<string, object>> items)
            {
                foreach (var item in items)
                {
                    fields[item.Key] = item.Value;
                }
            }
        }

        Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception, fields));
    }

    private void RestoreScope(Scope scope)
        => _currentScope.Value = scope;

    internal sealed class LogEntry
    {
        public LogEntry(LogLevel logLevel, EventId eventId, string message, Exception exception, IReadOnlyDictionary<string, object> fields)
        {
            LogLevel = logLevel;
            EventId = eventId;
            Message = message;
            Exception = exception;
            Fields = fields;
        }

        public LogLevel LogLevel { get; }

        public EventId EventId { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public IReadOnlyDictionary<string, object> Fields { get; }
    }

    private sealed class Scope : IDisposable
    {
        private readonly CapturingLogger _logger;

        public Scope(CapturingLogger logger, object state, Scope parent)
        {
            _logger = logger;
            State = state;
            Parent = parent;
        }

        public Scope Parent { get; }

        public object State { get; }

        public void Dispose()
            => _logger.RestoreScope(Parent);
    }
}
