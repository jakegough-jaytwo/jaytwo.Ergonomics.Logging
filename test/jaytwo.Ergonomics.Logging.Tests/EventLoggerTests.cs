using System;
using Microsoft.Extensions.Logging;
using Xunit;

namespace jaytwo.Ergonomics.Logging.Tests;

public class EventLoggerTests
{
    [Fact]
    public void Scope_adds_fields_to_logged_entry()
    {
        // Arrange
        var logger = new CapturingLogger();
        var eventLogger = new TestEventLogger(logger);

        // Act
        using (eventLogger.Scope(x => x.WithField("request_id", "abc123")))
        {
            eventLogger.LogMessage();
        }

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("abc123", entry.Fields["request_id"]);
    }

    [Fact]
    public void BuildMessage_prefixes_event_name_and_sets_event_id()
    {
        // Arrange
        var logger = new CapturingLogger();
        var eventLogger = new TestEventLogger(logger);

        // Act
        eventLogger.LogMessage();

        // Assert
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(42, entry.EventId.Id);
        Assert.Equal("TEST_EVENT", entry.EventId.Name);
        Assert.Equal("[TEST_EVENT] Hello world", entry.Message);
    }

    [Fact]
    public void FormatBytesPretty_returns_iec_strings()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger());

        // Act
        var pretty = eventLogger.FormatBytesPretty(55210L);
        var prettyNullable = eventLogger.FormatBytesPretty((long?)55210L);

        // Assert
        Assert.Equal("54 KiB", pretty);
        Assert.Equal(pretty, prettyNullable);
    }

    [Fact]
    public void FormatBytesPretty_returns_null_for_null_nullable_bytes()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger());

        // Act
        var pretty = eventLogger.FormatBytesPretty((long?)null);

        // Assert
        Assert.Null(pretty);
    }

    [Fact]
    public void Scope_returns_null_when_logger_is_null()
    {
        // Arrange
        var eventLogger = new TestEventLogger(null);

        // Act
        using var scope = eventLogger.Scope(x => x.WithField("request_id", "abc123"));

        // Assert
        Assert.Null(scope);
    }

    [Fact]
    public void FormatTime_methods_return_expected_strings()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger());
        var duration = TimeSpan.FromSeconds(90.5);

        // Act
        var pretty = eventLogger.FormatTimePretty(duration);
        var prettyNullable = eventLogger.FormatTimePretty((TimeSpan?)duration);
        var seconds = eventLogger.FormatTimeSeconds(duration);
        var secondsNullable = eventLogger.FormatTimeSeconds((TimeSpan?)duration);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(pretty));
        Assert.Equal(pretty, prettyNullable);
        Assert.Equal("90.5", seconds);
        Assert.Equal(seconds, secondsNullable);
    }

    [Fact]
    public void FormatTime_methods_return_null_for_null_nullable_duration()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger());

        // Act
        var pretty = eventLogger.FormatTimePretty((TimeSpan?)null);
        var seconds = eventLogger.FormatTimeSeconds((TimeSpan?)null);

        // Assert
        Assert.Null(pretty);
        Assert.Null(seconds);
    }

    [Fact]
    public void BuildMessage_returns_noop_builder_when_logger_is_null()
    {
        // Arrange
        var eventLogger = new TestEventLogger(null);

        // Act
        var builder = eventLogger.BuildMessageForTest(new EventId(42, "TEST_EVENT"), "Hello {name}", "world");
        var exception = Record.Exception(() => builder.Information());

        // Assert
        Assert.NotNull(builder);
        Assert.Null(exception);
    }

    [Fact]
    public void IsEnabled_returns_false_when_logger_is_null()
    {
        // Arrange
        var eventLogger = new TestEventLogger(null);

        // Act
        var isEnabled = eventLogger.IsEnabled(LogLevel.Information);

        // Assert
        Assert.False(isEnabled);
    }

    [Fact]
    public void IsEnabled_returns_false_when_log_level_is_disabled()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger(isEnabled: false));

        // Act
        var isEnabled = eventLogger.IsEnabled(LogLevel.Information);

        // Assert
        Assert.False(isEnabled);
    }

    [Fact]
    public void IsEnabled_returns_true_when_log_level_is_enabled()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger());

        // Act
        var isEnabled = eventLogger.IsEnabled(LogLevel.Information);

        // Assert
        Assert.True(isEnabled);
    }

    [Fact]
    public void LogMessage_does_not_invoke_extraConfig_when_logging_is_disabled()
    {
        // Arrange
        var eventLogger = new TestEventLogger(new CapturingLogger(isEnabled: false));
        var extraConfigCalled = false;

        // Act
        eventLogger.LogMessage(x => extraConfigCalled = true);

        // Assert
        Assert.False(extraConfigCalled);
    }

    [Fact]
    public void LogMessage_invokes_extraConfig_when_logging_is_enabled()
    {
        // Arrange
        var logger = new CapturingLogger();
        var eventLogger = new TestEventLogger(logger);
        var extraConfigCalled = false;

        // Act
        eventLogger.LogMessage(x => extraConfigCalled = true);

        // Assert
        Assert.True(extraConfigCalled);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void EventIdFromName_uses_name_as_identity_and_a_stable_positive_id()
    {
        // Arrange / Act
        var opened = EventLogger.EventIdFromName("CONN:OPENED");
        var openedAgain = EventLogger.EventIdFromName("CONN:OPENED");
        var closed = EventLogger.EventIdFromName("CONN:CLOSED");

        // Assert
        Assert.Equal("CONN:OPENED", opened.Name);
        Assert.Equal(opened.Id, openedAgain.Id);
        Assert.NotEqual(opened.Id, closed.Id);
        Assert.True(opened.Id > 0);
    }

    [Fact]
    public void EventIdFromName_throws_when_name_is_null()
    {
        // Act
        var exception = Record.Exception(() => EventLogger.EventIdFromName(null!));

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
    }

    private sealed class TestEventLogger : EventLogger
    {
        public TestEventLogger(ILogger? logger)
            : base(logger)
        {
        }

        public void LogMessage(Action<LogBuilder>? extraConfig = null)
        {
            if (!IsEnabled(LogLevel.Information))
            {
                return;
            }

            BuildMessage(new EventId(42, "TEST_EVENT"), "Hello {name}", "world")
                .WithExtraConfig(extraConfig)
                .Information();
        }

        public LogBuilder BuildMessageForTest(EventId eventId, string message, params object?[] args)
            => BuildMessage(eventId, message, args);
    }
}
