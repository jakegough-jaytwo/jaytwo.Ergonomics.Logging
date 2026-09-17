using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace jaytwo.Ergonomics.Logging;

/// <summary>
/// Extension methods for <see cref="ILogger"/> that provide ergonomic entry points
/// for <see cref="LogBuilder"/> and <see cref="ScopeBuilder"/>, plus
/// <c>BeginFieldScope</c> overloads that open a
/// <see cref="ILogger.BeginScope{TState}(TState)"/> from common key-value collection types.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Returns a <see cref="LogBuilder"/> for the given message template with no initial arguments.
    /// </summary>
    /// <param name="logger">The logger to write to. A <c>null</c> logger produces no output.</param>
    /// <param name="message">
    /// The message template. Supports standard <see cref="ILogger"/> positional
    /// placeholders such as <c>{name}</c>.
    /// </param>
    public static LogBuilder BuildMessage(this ILogger? logger, string message)
        => BuildMessage(logger, message, Array.Empty<object?>());

    /// <summary>
    /// Returns a <see cref="LogBuilder"/> for the given message template and arguments.
    /// </summary>
    /// <param name="logger">The logger to write to. A <c>null</c> logger produces no output.</param>
    /// <param name="message">
    /// The message template. Supports standard <see cref="ILogger"/> positional
    /// placeholders such as <c>{name}</c>.
    /// </param>
    /// <param name="messageArgs">Positional arguments corresponding to placeholders in <paramref name="message"/>.</param>
    public static LogBuilder BuildMessage(this ILogger? logger, string message, params object?[] messageArgs)
        => new(logger, message, messageArgs);

    /// <summary>
    /// Returns a <see cref="ScopeBuilder"/> for fluently constructing a structured log scope.
    /// </summary>
    /// <param name="logger">The logger to open the scope on. A <c>null</c> logger is accepted.</param>
    public static ScopeBuilder BuildScope(this ILogger? logger)
        => new(logger);

    /// <summary>
    /// Opens a structured log scope from a tuple array of key-value pairs.
    /// Duplicate keys are overwritten with the last-written value.
    /// Returns <c>null</c> if <paramref name="logger"/> is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not named <c>BeginScope</c>. <see cref="ILogger.BeginScope{TState}(TState)"/>
    /// is an instance method that accepts any single argument, and an instance method always
    /// beats an extension method, so <c>logger.BeginScope(("key", value))</c> would bind to MEL
    /// and open a scope whose state is a tuple. No provider reads fields out of that, so the
    /// field would silently disappear.
    /// </remarks>
    public static IDisposable? BeginFieldScope(this ILogger? logger, params (string Key, object? Value)[] values)
        => BeginFieldScope(logger, ToLastWinDictionary(values));

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope(this ILogger? logger, IEnumerable<KeyValuePair<string, object?>> values)
    {
        if (values is IDictionary<string, object?> dictionary)
        {
            return BeginFieldScope(logger, dictionary);
        }

        return BeginFieldScope(logger, ToLastWinDictionary(values));
    }

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope(this ILogger? logger, params KeyValuePair<string, object?>[] values)
        => BeginFieldScope(logger, ToLastWinDictionary(values));

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope<T>(this ILogger? logger, IEnumerable<KeyValuePair<string, T>> values)
    {
        if (values is IDictionary<string, object?> dictionary)
        {
            return BeginFieldScope(logger, dictionary);
        }

        return BeginFieldScope(logger, ToLastWinDictionary(values));
    }

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope<T>(this ILogger? logger, params KeyValuePair<string, T>[] values)
        => BeginFieldScope(logger, ToLastWinDictionary(values));

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope<T>(this ILogger? logger, IDictionary<string, T> values)
    {
        if (values is IDictionary<string, object?> objectDict)
        {
            return BeginFieldScope(logger, objectDict);
        }

        return BeginFieldScope(logger, ToLastWinDictionary(values));
    }

    /// <inheritdoc cref="BeginFieldScope(ILogger?, ValueTuple{string, object}[])"/>
    public static IDisposable? BeginFieldScope(this ILogger? logger, IDictionary<string, object?> values)
        => logger?.BeginScope<IDictionary<string, object?>>(values);

    // Last-win via indexer. Enumerable.ToDictionary throws ArgumentException on
    // duplicate keys; LogBuilder and ScopeBuilder overwrite. BeginScope must
    // match so composing field lists does not throw.

    private static Dictionary<string, object?> ToLastWinDictionary((string Key, object? Value)[] values)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var kv in values)
        {
            fields[kv.Key] = kv.Value;
        }

        return fields;
    }

    private static Dictionary<string, object?> ToLastWinDictionary<T>(IEnumerable<KeyValuePair<string, T>> values)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var kv in values)
        {
            fields[kv.Key] = kv.Value;
        }

        return fields;
    }
}
