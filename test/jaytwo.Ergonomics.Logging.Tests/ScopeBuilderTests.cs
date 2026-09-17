using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit;

namespace jaytwo.Ergonomics.Logging.Tests;

public class ScopeBuilderTests
{
    [Fact]
    public void BuildScope_returns_null_when_no_fields_are_present()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using var scope = logger.BuildScope().BuildScope();

        // Assert
        Assert.Null(scope);
    }

    [Fact]
    public void BeginFieldScope_accepts_tuple_values()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using (logger.BeginFieldScope(("request_id", (object?)"abc123"), ("tenant_id", 7)))
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
    }

    [Fact]
    public void BuildScope_adds_dictionary_fields_to_scope()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using (logger.BuildScope()
            .WithFields(new Dictionary<string, object?> { ["request_id"] = "abc123", ["tenant_id"] = 7 })
            .BuildScope())
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
    }

    [Fact]
    public void BuildScope_accepts_all_with_fields_overloads_and_last_value_wins()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using (logger.BuildScope()
            .WithFields(("request_id", (object?)"first"))
            .WithFields(new KeyValuePair<string, object?>("request_id", "second"))
            .WithFields(new[]
            {
                new KeyValuePair<string, object?>("tenant_id", 7),
            })
            .WithFields(new Dictionary<string, object?> { ["request_id"] = "third" })
            .WithFields(new Dictionary<string, int> { ["status_code"] = 200 })
            .WithFields(new[]
            {
                new KeyValuePair<string, int>("attempt", 2),
            })
            .BuildScope())
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("third", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
        Assert.Equal(200, entry.Fields["status_code"]);
        Assert.Equal(2, entry.Fields["attempt"]);
    }

    [Fact]
    public void BeginFieldScope_keeps_last_value_for_duplicate_keys()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using (logger.BeginFieldScope(("request_id", (object?)"first"), ("request_id", "second")))
        {
            logger.BuildMessage("tuple").Debug();
        }

        using (logger.BeginFieldScope(new[]
        {
            new KeyValuePair<string, object?>("request_id", "first"),
            new KeyValuePair<string, object?>("request_id", "second"),
        }))
        {
            logger.BuildMessage("kvp-array").Debug();
        }

        using (logger.BeginFieldScope(new List<KeyValuePair<string, int>>
        {
            new("status_code", 100),
            new("status_code", 200),
        }))
        {
            logger.BuildMessage("typed-list").Debug();
        }

        // Assert
        Assert.Collection(
            logger.Entries,
            x => Assert.Equal("second", x.Fields["request_id"]),
            x => Assert.Equal("second", x.Fields["request_id"]),
            x => Assert.Equal(200, x.Fields["status_code"]));
    }

    [Fact]
    public void BeginFieldScope_overloads_accept_collections()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        using (logger.BeginFieldScope(new Dictionary<string, object?> { ["request_id"] = "abc123" }))
        using (logger.BeginFieldScope(new[]
        {
            new KeyValuePair<string, int>("tenant_id", 7),
        }))
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
    }

    // The overloads below are exercised through an ILogger reference on purpose. A one-argument
    // call is where ILogger.BeginScope<TState> would out-resolve an extension method named
    // BeginScope, so these only prove anything when the instance method is visible.
    [Fact]
    public void BeginFieldScope_adds_a_single_tuple_field_through_an_ILogger_reference()
    {
        // Arrange
        var capturing = new CapturingLogger();
        ILogger logger = capturing;

        // Act
        using (logger.BeginFieldScope(("request_id", (object?)"abc123")))
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(capturing.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
    }

    [Fact]
    public void BeginFieldScope_adds_a_single_key_value_pair_through_an_ILogger_reference()
    {
        // Arrange
        var capturing = new CapturingLogger();
        ILogger logger = capturing;

        // Act
        using (logger.BeginFieldScope(new KeyValuePair<string, object?>("request_id", "abc123")))
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(capturing.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
    }

    [Fact]
    public void BeginFieldScope_adds_typed_dictionary_fields_through_an_ILogger_reference()
    {
        // Arrange
        var capturing = new CapturingLogger();
        ILogger logger = capturing;

        // Act
        using (logger.BeginFieldScope(new Dictionary<string, int> { ["status_code"] = 200 }))
        {
            logger.BuildMessage("Scoped").Debug();
        }

        // Assert
        var entry = Assert.Single(capturing.Entries);
        Assert.Equal(200, entry.Fields["status_code"]);
    }

    [Fact]
    public void BeginFieldScope_returns_null_when_logger_is_null()
    {
        // Arrange
        ILogger? logger = null;

        // Act
        using var scope = logger.BeginFieldScope(("request_id", (object?)"abc123"));

        // Assert
        Assert.Null(scope);
    }
}
