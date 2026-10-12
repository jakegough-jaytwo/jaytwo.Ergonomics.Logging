using System;
using jaytwo.Ergonomics.Logging.Bytes;
using jaytwo.StableHashing;
using jaytwo.TimeExpression.Golang;
using jaytwo.TimeExpression.Seconds;
using Microsoft.Extensions.Logging;

namespace jaytwo.Ergonomics.Logging;

/// <summary>
/// Abstract base class for event-oriented loggers that emit structured log entries
/// identified by a named <see cref="EventId"/>.
/// </summary>
/// <remarks>
/// Subclasses define domain events as methods that call
/// <see cref="BuildMessage(EventId, string, object?[])"/> with a named catalog
/// <see cref="EventId"/> and chain fluent builder calls before emitting at the
/// appropriate log level. Guard those methods with
/// <see cref="IsEnabled(LogLevel)"/> so formatting, hashing, and other work is skipped
/// when logging is off. Pair methods with canonical events as described in the
/// package README. The base class also provides <see cref="EventIdFromName"/>
/// (stable xxHash via jaytwo.StableHashing) and helpers for formatting
/// <see cref="TimeSpan"/> values and byte counts in human-readable form.
/// </remarks>
public abstract class EventLogger
{
    private static readonly IHash32 EventIdHasher = StableHash.XxHash3.Hasher64.Narrow32();

    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventLogger"/> class targeting the given logger.
    /// </summary>
    /// <param name="logger">
    /// The logger to write to. A <c>null</c> logger is accepted; all methods
    /// become no-ops in that case.
    /// </param>
    protected EventLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a canonical <see cref="EventId"/> whose <see cref="EventId.Name"/> is
    /// <paramref name="eventName"/> and whose <see cref="EventId.Id"/> is a stable
    /// positive xxHash of that name.
    /// </summary>
    public static EventId EventIdFromName(string eventName)
        => new(
            EventIdHasher.HashDataAsInt32(
                eventName ?? throw new ArgumentNullException(nameof(eventName)),
                forcePositive: true),
            eventName);

    /// <summary>
    /// Returns whether the underlying logger would write at the given
    /// <paramref name="logLevel"/>. Always <c>false</c> when the logger is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Call this before <see cref="BuildMessage(EventId, string, object?[])"/> when
    /// preparing an event would allocate, format values, or run other expensive work.
    /// </remarks>
    public bool IsEnabled(LogLevel logLevel)
        => _logger?.IsEnabled(logLevel) == true;

    /// <summary>
    /// Opens a structured log scope, optionally configured with additional fields.
    /// Returns <c>null</c> if the logger is <c>null</c>; calling <c>using</c> on a
    /// <c>null</c> return value is safe.
    /// </summary>
    /// <param name="extraConfig">
    /// An optional delegate to add fields to the scope via <see cref="ScopeBuilder"/>.
    /// </param>
    public IDisposable? Scope(Action<ScopeBuilder>? extraConfig = null)
    {
        if (_logger == null)
        {
            return null;
        }

        var builder = _logger.BuildScope();

        extraConfig?.Invoke(builder);

        return builder.BuildScope();
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a compact Go-style duration string
    /// (e.g. <c>1m30.5s</c>).
    /// </summary>
    public virtual string FormatTimePretty(TimeSpan duration) => GoDuration.ToCompactString(duration);

    /// <summary>
    /// Formats a nullable <see cref="TimeSpan"/> as a compact Go-style duration string,
    /// or returns <c>null</c> if the value is <c>null</c>.
    /// </summary>
    public virtual string? FormatTimePretty(TimeSpan? duration) => GoDuration.ToCompactString(duration);

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a decimal seconds string (e.g. <c>90.5</c>).
    /// </summary>
    public virtual string FormatTimeSeconds(TimeSpan duration) => ExactSeconds.ToString(duration);

    /// <summary>
    /// Formats a nullable <see cref="TimeSpan"/> as a decimal seconds string,
    /// or returns <c>null</c> if the value is <c>null</c>.
    /// </summary>
    public virtual string? FormatTimeSeconds(TimeSpan? duration) => ExactSeconds.ToString(duration);

    /// <summary>
    /// Formats a byte count the way go-humanize <c>IBytes</c> does (e.g. <c>512 B</c>, <c>1.5 KiB</c>, <c>54 KiB</c>):
    /// IEC units, base 1024, two significant digits.
    /// </summary>
    public virtual string FormatBytesPretty(long bytes) => GoHumanize.IBytes(bytes);

    /// <summary>
    /// Formats a nullable byte count the way go-humanize <c>IBytes</c> does,
    /// or returns <c>null</c> if the value is <c>null</c>.
    /// </summary>
    public virtual string? FormatBytesPretty(long? bytes) => bytes is null ? null : FormatBytesPretty(bytes.Value);

    /// <summary>
    /// Creates a <see cref="LogBuilder"/> pre-configured with the event name prefix
    /// <c>[{event_name}]</c> and the given <see cref="EventId"/>.
    /// Always returns a builder; a <c>null</c> logger produces a silent no-op builder,
    /// matching <see cref="LoggerExtensions.BuildMessage(ILogger?, string, object?[])"/>.
    /// </summary>
    /// <param name="eventId">
    /// The event identifier. <see cref="EventId.Name"/> is required: it is the
    /// canonical identity, the <c>{event_name}</c> template argument, and the
    /// entry's <see cref="EventId"/>. Do not include <c>{event_name}</c> in
    /// <paramref name="message"/>.
    /// </param>
    /// <param name="message">
    /// The message template, appended after the <c>[{event_name}]</c> prefix.
    /// </param>
    /// <param name="args">Positional arguments corresponding to placeholders in <paramref name="message"/>.</param>
    protected internal virtual LogBuilder BuildMessage(EventId eventId, string message, params object?[] args)
        => _logger.BuildMessage(
                "[{event_name}] " + message,
                PrependArray(eventId.Name, args))
            .WithEventId(eventId);

    private static object?[] PrependArray(object? first, object?[] args)
    {
        if (args.Length == 0)
        {
            return new object?[] { first };
        }

        var arr = new object?[args.Length + 1];
        arr[0] = first;
        Array.Copy(args, 0, arr, 1, args.Length);
        return arr;
    }
}
