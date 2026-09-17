using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace jaytwo.Ergonomics.Logging;

/// <summary>
/// Fluent builder for creating a structured logging scope via
/// <see cref="ILogger.BeginScope{TState}(TState)"/>.
/// Obtain an instance via <see cref="LoggerExtensions.BuildScope(ILogger?)"/>.
/// </summary>
public sealed class ScopeBuilder
{
    private readonly ILogger? _logger;
    private readonly Dictionary<string, object?> _fields = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ScopeBuilder"/> class targeting the given logger.
    /// </summary>
    /// <param name="logger">
    /// The logger to open a scope on. A <c>null</c> logger is accepted;
    /// <see cref="BuildScope"/> will return <c>null</c> in that case.
    /// </param>
    public ScopeBuilder(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a structured field that will be included in the log scope.
    /// Duplicate keys are overwritten with the last-written value.
    /// </summary>
    public ScopeBuilder WithField(string key, object? value)
    {
        _fields[key] = value;
        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields(params (string Key, object? Value)[] items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields(params KeyValuePair<string, object?>[] items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields(IEnumerable<KeyValuePair<string, object?>> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields(IDictionary<string, object?> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields<T>(IEnumerable<KeyValuePair<string, T>> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <inheritdoc cref="WithField(string, object?)"/>
    public ScopeBuilder WithFields<T>(IDictionary<string, T> items)
    {
        foreach (var kv in items)
        {
            WithField(kv.Key, kv.Value);
        }

        return this;
    }

    /// <summary>
    /// Opens the log scope and returns an <see cref="IDisposable"/> that closes it when disposed.
    /// Returns <c>null</c> if the logger is <c>null</c> or no fields have been added;
    /// calling <c>using</c> on a <c>null</c> return value is safe.
    /// </summary>
    public IDisposable? BuildScope()
    {
        if (_logger is null || _fields.Count == 0)
        {
            return null;
        }

        return _logger.BeginScope<IDictionary<string, object?>>(_fields);
    }
}
