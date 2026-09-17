using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit;

namespace jaytwo.Ergonomics.Logging.Tests;

public class LogBuilderTests
{
    [Fact]
    public void Write_logs_message_fields_scope_fields_event_id_and_exception()
    {
        // Arrange
        var logger = new CapturingLogger();
        var exception = new InvalidOperationException("boom");

        // Act
        logger.BuildMessage("Hello {name}", "world")
            .WithEventId(new EventId(42, "TEST_EVENT"))
            .WithField("request_id", "abc123")
            .WithFields(new Dictionary<string, object?> { ["tenant_id"] = 7 })
            .AppendToMessage(" {value}", 9)
            .AppendFieldToMessage("elapsed_ms", 15)
            .WithException(exception)
            .Information();

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.LogLevel);
        Assert.Equal(42, entry.EventId.Id);
        Assert.Equal("TEST_EVENT", entry.EventId.Name);
        Assert.Equal(exception, entry.Exception);
        Assert.Equal("Hello world 9 elapsed_ms=15", entry.Message);
        Assert.Equal("abc123", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
    }

    [Fact]
    public void Write_does_nothing_when_logger_is_null()
    {
        // Arrange
        var builder = new LogBuilder(null, "Hello {name}", "world");

        // Act
        var exception = Record.Exception(() => builder.Information());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Write_does_nothing_when_log_level_is_disabled()
    {
        // Arrange
        var logger = new CapturingLogger(isEnabled: false);

        // Act
        logger.BuildMessage("Hello {name}", "world")
            .WithField("request_id", "abc123")
            .Information();

        // Assert
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void WithFields_overloads_and_append_field_overloads_apply_last_value_for_duplicate_keys()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        logger.BuildMessage("Hello")
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
            .AppendFieldToMessage(("elapsed_ms", (object?)10))
            .AppendFieldToMessage(new KeyValuePair<string, object?>("elapsed_ms", 15))
            .AppendFieldToMessage(new[]
            {
                new KeyValuePair<string, object?>("rows", 1),
            })
            .AppendFieldToMessage(new Dictionary<string, object?> { ["elapsed_ms"] = 20 })
            .AppendFieldToMessage(new Dictionary<string, int> { ["rows"] = 2 })
            .AppendFieldToMessage(new[]
            {
                new KeyValuePair<string, int>("retries", 3),
            })
            .Information();

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Hello elapsed_ms=20 rows=2 retries=3", entry.Message);
        Assert.Equal("third", entry.Fields["request_id"]);
        Assert.Equal(7, entry.Fields["tenant_id"]);
        Assert.Equal(200, entry.Fields["status_code"]);
        Assert.Equal(2, entry.Fields["attempt"]);
    }

    [Fact]
    public void Write_is_idempotent_when_called_twice()
    {
        // Arrange
        var logger = new CapturingLogger();
        var builder = logger.BuildMessage("Hello");

        // Act
        builder.Information();
        builder.Information();

        // Assert
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void AppendToMessage_ignores_null_argument_array()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        logger.BuildMessage("Hello")
            .AppendToMessage(" world", (object?[]?)null!)
            .Information();

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Hello world", entry.Message);
    }

    [Fact]
    public void Level_helpers_write_at_expected_log_levels()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        logger.BuildMessage("Critical").Critical();
        logger.BuildMessage("Error").Error();
        logger.BuildMessage("Warning").Warning();
        logger.BuildMessage("Information").Information();
        logger.BuildMessage("Debug").Debug();
        logger.BuildMessage("Trace").Trace();

        // Assert
        Assert.Collection(
            logger.Entries,
            x => Assert.Equal(LogLevel.Critical, x.LogLevel),
            x => Assert.Equal(LogLevel.Error, x.LogLevel),
            x => Assert.Equal(LogLevel.Warning, x.LogLevel),
            x => Assert.Equal(LogLevel.Information, x.LogLevel),
            x => Assert.Equal(LogLevel.Debug, x.LogLevel),
            x => Assert.Equal(LogLevel.Trace, x.LogLevel));
    }
}
