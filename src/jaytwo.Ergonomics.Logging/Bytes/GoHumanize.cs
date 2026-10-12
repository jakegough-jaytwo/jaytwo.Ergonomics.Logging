using System;
using System.Globalization;

namespace jaytwo.Ergonomics.Logging.Bytes;

/// <summary>
/// Byte counts formatted like go-humanize <c>IBytes</c> (github.com/dustin/go-humanize v1.1.0):
/// IEC units, base 1024, two significant digits.
/// Candidate for extraction into its own package; see the README next to this file.
/// </summary>
internal static class GoHumanize
{
    private const int MinDigits = 2;

    private static readonly string[] IecSizes = { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };

    /// <summary>
    /// Formats <paramref name="bytes"/> as go-humanize <c>IBytes</c> does
    /// (<c>512 B</c>, <c>1.5 KiB</c>, <c>54 KiB</c>, <c>1.0 MiB</c>).
    /// Go takes a <c>uint64</c>; a negative count is formatted as its magnitude with a leading <c>-</c>.
    /// </summary>
    public static string IBytes(long bytes) => bytes < 0
        ? "-" + HumanateBytes((ulong)-(bytes + 1) + 1, 1024, IecSizes)
        : HumanateBytes((ulong)bytes, 1024, IecSizes);

    // Mirrors Go's humanateBytes, float math included, so boundary values (1048575 -> "1024 KiB") match.
    private static string HumanateBytes(ulong s, double @base, string[] sizes)
    {
        if (s < 10)
        {
            return s.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var e = Math.Floor(Math.Log(s) / Math.Log(@base));
        var suffix = sizes[(int)e];
        var val = s / Math.Pow(@base, e);

        var intDigits = CountDigits((long)val);
        var digits = Math.Max(MinDigits - intDigits, 0);
        var rounding = Math.Pow(10, digits);
        val = Math.Floor((val * rounding) + 0.5) / rounding;

        // 9.96 rounds to 10.0, which gains an integer digit, so it drops a decimal.
        var newIntDigits = CountDigits((long)val);
        if (newIntDigits > intDigits)
        {
            digits = Math.Max(digits - (newIntDigits - intDigits), 0);
        }

        return val.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + " " + suffix;

        static int CountDigits(long n)
        {
            var count = 0;
            while (n != 0)
            {
                n /= 10;
                count++;
            }

            return count;
        }
    }
}
