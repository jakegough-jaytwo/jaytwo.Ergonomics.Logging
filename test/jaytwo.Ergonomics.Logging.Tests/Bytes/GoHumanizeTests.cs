using jaytwo.Ergonomics.Logging.Bytes;
using Xunit;

namespace jaytwo.Ergonomics.Logging.Tests.Bytes;

public class GoHumanizeTests
{
    // Expected values captured from go-humanize v1.1.0 humanize.IBytes (see Bytes/README.md).
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(9L, "9 B")]
    [InlineData(10L, "10 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KiB")]
    [InlineData(1536L, "1.5 KiB")]
    [InlineData(10188L, "9.9 KiB")]
    [InlineData(10189L, "10 KiB")]
    [InlineData(55210L, "54 KiB")]
    [InlineData(1048064L, "1024 KiB")]
    [InlineData(1048575L, "1024 KiB")]
    [InlineData(1048576L, "1.0 MiB")]
    [InlineData(82854982L, "79 MiB")]
    [InlineData(123456789L, "118 MiB")]
    [InlineData(1073740800L, "1024 MiB")]
    [InlineData(1124800395214848L, "1023 TiB")]
    [InlineData(5905580032L, "5.5 GiB")]
    [InlineData(1152921504606846848L, "1.0 EiB")] // 2^60 - 128: float log rounds up into EiB
    [InlineData(long.MaxValue, "8.0 EiB")]
    public void IBytes_matches_go_humanize(long bytes, string expected)
    {
        // Act
        var result = GoHumanize.IBytes(bytes);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1L, "-1 B")]
    [InlineData(-55210L, "-54 KiB")]
    [InlineData(long.MinValue, "-8.0 EiB")]
    public void IBytes_formats_negative_as_signed_magnitude(long bytes, string expected)
    {
        // Act
        var result = GoHumanize.IBytes(bytes);

        // Assert
        Assert.Equal(expected, result);
    }
}
