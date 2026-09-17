using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace jaytwo.Ergonomics.Logging;

/// <summary>
/// Fluent builder for constructing and emitting a single structured log entry.
/// Obtain an instance via <see cref="LoggerExtensions.BuildMessage(ILogger?, string)"/>.
/// </summary>
/// <remarks>
/// Each builder instance is single-use. After any <c>Write</c> or level helper method
/// is called, subsequent calls are silently ignored.
/// </remarks>
public sealed class LogBuilder
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, object?> _fields = new();
    private readonly Dictionary<string, object?> _messageFields = new();

    private string _message;
    private List<object?> _messageArgs;

    private EventId? _eventId;
    private Exception? _exception;
    private bool _written;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogBuilder"/> class targeting the given logger.
    /// </summary>
    /// <param name="logger">
    /// The logger to write to. A <c>null</c> logger is accepted and produces no output.
    /// </param>
    /// <param name="message">
    /// The message template. Supports standard
    /// <see cref="ILogger"/> positional placeholders such as <c>{name}</c>.
    /// </param>
    /// <param name="messageArgs">Positional arguments corresponding to placeholders in <paramref name="message"/>.</param>
    public LogBuilder(ILogger? logger, string message, params object?[] messageArgs)
    {
        _logger = logger;
        _message = message;
        _messageArgs = new(messageArgs ?? Array.Empty<object?>());
    }

    /// <summary>
    /// Invokes an optional configuration delegate on this builder and returns it,
    /// allowing conditional or shared configuration to be applied inline in a fluent chain.
    /// </summary>
    /// <param name="extraConfig">An action to apply additional configuration, or <c>null</c> to skip.</param>
    public LogBuilder WithExtraConfig(Action<LogBuilder>? extraConfig)
    {
        extraConfig?.Invoke(this);
        return this;
    }

    /// <summary>
    /// Associates an exception with the log entry.
    /// </summary>
    public LogBuilder WithException(Exception? ex)
    {
        _exception = ex;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="EventId"/> for the log entry.
    /// </summary>
    public LogBuilder WithEventId(EventId eventId)
    {
        _eventId = eventId;
        return this;
    }

    /// <summary>
    /// Adds a structured field to the log scope. The field appears in structured log sinks
    /// but is NOT appended to the message text. Use
    /// <see cref="AppendFieldToMessage(string, object?)"/> to include the value in
    /// human-readable message output as well.
    /// </summary>
    /// <remarks>
    /// The value is evaluated eagerly. Guard with <c>logger.IsEnabled(level)</c> before
    /// calling if the value is expensive to compute.
    /// </remarks>
    public LogBuilder WithField(string key, object? value)
    {
        _fields[key] = value;
        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields(params (string Key, object? Value)[] items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields(params KeyValuePair<string, object?>[] items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields(IEnumerable<KeyValuePair<string, object?>> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields(IDictionary<string, object?> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields<T>(IEnumerable<KeyValuePair<string, T>> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public LogBuilder WithFields<T>(IDictionary<string, T> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <summary>
    /// Appends additional text and optional positional arguments to the message template.
    /// </summary>
    /// <param name="message">
    /// Text to append to the existing message template. May contain additional
    /// <see cref="ILogger"/> placeholders such as <c>{name}</c>.
    /// </param>
    /// <param name="args">Positional arguments corresponding to any placeholders added in <paramref name="message"/>.</param>
    public LogBuilder AppendToMessage(string message, params object?[] args)
    {
        _message += message;

        if (args != null)
        {
            _messageArgs.AddRange(args);
        }

        return this;
    }

    /// <summary>
    /// Appends <c>key={value}</c> (with a leading space) to the message template and records the value as a
    /// positional structured argument. The field appears in both human-readable message
    /// text and structured log sinks. Use <see cref="WithField(string, object?)"/> for
    /// fields that should appear only in the log scope and not in the message string.
    /// </summary>
    public LogBuilder AppendFieldToMessage(string key, object? value)
    {
        _messageFields[key] = value;
        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage(params (string Key, object? Value)[] items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage(params KeyValuePair<string, object?>[] items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage(IEnumerable<KeyValuePair<string, object?>> items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage(IDictionary<string, object?> items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage<T>(IEnumerable<KeyValuePair<string, T>> items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="AppendFieldToMessage(string, object?)"/>
    public LogBuilder AppendFieldToMessage<T>(IDictionary<string, T> items)
    {
        foreach (var kv in items)
        {
            AppendFieldToMessage(kv.Key, kv.Value);
        }

        return this;
    }

    /// <summary>
    /// Emits the log entry at the specified level. Fields added via
    /// <see cref="WithField(string, object?)"/> are pushed as a log scope for the duration
    /// of the write. This method is a no-op if the logger is <c>null</c>, the log level is
    /// disabled, or the builder has already been written.
    /// </summary>
    public void Write(LogLevel logLevel)
    {
        if (_logger is null || _written || !_logger.IsEnabled(logLevel))
        {
            return;
        }

        _written = true;

        using var scope = _fields.Count > 0
            ? _logger.BeginScope<IDictionary<string, object?>>(_fields)
            : null;

        var final = BuildLog();
        _logger.Log(logLevel, _eventId ?? default, _exception, final.Message, final.Args);
    }

    /// <summary>Emits the log entry at <see cref="LogLevel.Critical"/>.</summary>
    public void Critical() => Write(LogLevel.Critical);

    /// <summary>Emits the log entry at <see cref="LogLevel.Error"/>.</summary>
    public void Error() => Write(LogLevel.Error);

    /// <summary>Emits the log entry at <see cref="LogLevel.Warning"/>.</summary>
    public void Warning() => Write(LogLevel.Warning);

    /// <summary>Emits the log entry at <see cref="LogLevel.Information"/>.</summary>
    public void Information() => Write(LogLevel.Information);

    /// <summary>Emits the log entry at <see cref="LogLevel.Debug"/>.</summary>
    public void Debug() => Write(LogLevel.Debug);

    /// <summary>Emits the log entry at <see cref="LogLevel.Trace"/>.</summary>
    public void Trace() => Write(LogLevel.Trace);

    private (string Message, object?[] Args) BuildLog()
    {
        var message = _message;
        var args = new List<object?>(_messageArgs);

        foreach (var kv in _messageFields)
        {
            message += $" {kv.Key}={{{kv.Key}}}";
            args.Add(kv.Value);
        }

        return (message, args.ToArray());
    }
}
