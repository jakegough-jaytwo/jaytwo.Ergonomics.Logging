# Bytes: candidate for its own package

`GoHumanize` backs `EventLogger.FormatBytesPretty`. It formats byte counts the way go-humanize `IBytes` (github.com/dustin/go-humanize v1.1.0) does: `512 B`, `1.5 KiB`, `54 KiB`, `1.0 MiB`. It lives here because log lines needed it first (S3 `OBJECT_STREAM_CLOSED` used to print `55210 bytes read`). It is `internal` on purpose: if it moves into its own package, nothing public breaks. `FormatBytesPretty` stays on `EventLogger` and forwards to the new package, the same way `FormatTimePretty` forwards to `jaytwo.TimeExpression`.

## Why go-humanize

Durations follow Go (`time.Duration.String()` via jaytwo.TimeExpression), so bytes follow Go too. Go's standard library has no byte formatter, but the ecosystem agrees on the important part:

- **The Go runtime spells sizes in IEC.** `GOMEMLIMIT` accepts only `B`, `KiB`, `MiB`, `GiB`, `TiB`, citing IEC 80000-13.
- **go-humanize is the de facto formatter.** It has `Bytes` (SI, `55 kB`) and `IBytes` (IEC, `54 KiB`). `IBytes` matches the runtime's units.

IEC is also the right call for log lines on its own merits. A bare `K` (GNU `ls -h`) or `KB` means 1000 to some readers and 1024 to others; `KiB` means one thing. GNU `ls -h` was the first choice here and was dropped for that reason: its terse `54K` suits a terminal where everyone knows the convention, not a log line read by anyone. go-humanize also writes `512 B` rather than a bare `512`, which reads better in a sentence, and rounds to nearest rather than up (`du` rounds up so it never understates disk use; logs have no such reason).

## When to extract

One formatter is not a package. Extract when one of these happens:

- A second format is needed (SI `Bytes`, GNU `ls -h`, Windows/JEDEC).
- Parsing is needed (`"10M"` -> bytes for config values such as buffer sizes, multipart thresholds, and cache limits).
- Code outside logging needs it.

Why it could justify a package, using the same test that `jaytwo.TimeExpression` passes:

- **It has an outside reference.** TimeExpression matches Go's `time.Duration.String()`. This matches go-humanize `IBytes`. Matching it exactly takes real work, because the port keeps Go's float math and its quirks: the `< 10` bytes shortcut, two significant digits with a decimal dropped when rounding carries (`9.96 KiB -> 10 KiB`), no carry into the next unit (`1048575 -> 1024 KiB`, not `1.0 MiB`), and a float `log` that rounds `2^60 - 128` up into `1.0 EiB`. A tidier integer version would disagree with Go at those boundaries.
- **It has a family of formats** (see below), the way TimeExpression has Go-style, exact seconds, and exact milliseconds.
- **Parsing is where the value is.** Hand-written parsers for size strings are where bugs show up (see "Parsing traps").
- **Existing packages do not fill the gap.** Humanizer's `ByteSize` is heavy and does not match coreutils. The small `ByteSize` packages each make up their own output format.

**Name:** not `BinaryExpression`. It collides with `System.Linq.Expressions.BinaryExpression`, and "binary" is ambiguous. `jaytwo.ByteExpression` or `jaytwo.SizeExpression` would sit alongside `jaytwo.TimeExpression`.

**Scope:** sizes in bytes. Bits and rates (`Mbps`) are a separate concern. They are almost always decimal and would mostly add naming clutter, so leave them out unless a real consumer needs them.

## The choices behind a size format

There is no fixed list of conventions. Each one is a combination of these choices:

| Choice | Options |
| --- | --- |
| Base | 1000 (SI) or 1024 (binary) |
| Unit spelling | SI `kB` (lowercase k is the official SI spelling), JEDEC `KB` (uppercase K, meaning 1024), IEC `KiB`, bare `K` (GNU), `Ki` without the B (`numfmt`) |
| Bits or bytes | `b` or `B`. Rates are usually in bits (`Mbps`, `Mbit/s`), sizes in bytes |
| Rounding | up (GNU `ls`), to nearest (go-humanize, most tools), down (some) |
| Precision | two significant digits (go-humanize), one decimal under 10 (GNU), three significant digits (Windows), a fixed number of decimals, or whole units only |
| Spacing | `54K` or `54 KB`. SI says to put a space before the unit |

## How tools combine them

| Who | Base | Looks like | Notes |
| --- | --- | --- | --- |
| GNU `ls -h`, `du -h`, `df -h` | 1024 | `54K` | Rounds up, one decimal under 10 |
| GNU `--si` | 1000 | `56k` | Lowercase k |
| `numfmt --to=iec-i` | 1024 | `54Ki` | IEC prefix but no B |
| `dd` | both | `55 kB, 54 KiB` | Prints both forms |
| Windows | 1024 | `53.9 KB` | JEDEC names with binary values. Explorer's list view always shows whole KB, rounded up (a 1-byte file shows `1 KB`) |
| macOS (10.6 and later) | 1000 | `55 KB` | Switched from 1024 to 1000. Uses uppercase K even though the base is SI |
| GNOME | 1000 | `55.2 kB` | |
| KDE | 1024 | `53.9 KiB` | Configurable |
| Disk vendors | 1000 | `1 TB` | Why a new 1 TB disk shows as about 931 GB in Windows |
| RAM | 1024 | `16 GB` | Always binary, under JEDEC names |
| Networking | 1000 | `100 Mbps` | Bits, always decimal |
| Go `humanize` | both | `55 kB` / `54 KiB` | Separate `Bytes()` and `IBytes()` functions. **`IBytes` is what `GoHumanize` ports.** |
| Go runtime `GOMEMLIMIT` | 1024 | `512MiB` | Parse only. Accepts IEC units only |

The 1.44 MB floppy is the worst case: 1,474,560 bytes = 1440 x 1024, so its "MB" is 1000 x 1024 bytes.

## Parsing traps

When you format, you pick one convention. When you parse, you have to accept what other people write:

- **Kubernetes:** `128M` = 128 x 10^6, `128Mi` = 128 x 2^20, and `128m` means **milli**, so a memory limit of `128m` is 0.128 bytes.
- **Java `-Xmx512m`, Docker `--memory 512m`, nginx `10m`:** lowercase, case-insensitive, and 1024-based.
- **`MB` can't be read on its own.** It means 1000^2 or 1024^2 depending on who wrote it, so a parser needs an explicit rule for it.

## A possible API

The formatters would be named after the convention they reproduce, not after a style:

- `GoHumanize.IBytes` (`54 KiB`, this file) and `GoHumanize.Bytes` (`55 kB`)
- `GnuHumanSize` (`ls -h`, `54K`) and `GnuSiSize` (`ls --si`, `56k`)
- `JedecSize` (Windows `53.9 KB`)

A parser would have two modes:

- **Strict:** accept only unambiguous units (`Ki`, `KiB`, `kB` meaning 1000). Reject `K`, `KB`, `m`.
- **Lenient:** accept GNU, Java, Docker, and nginx forms (`K`, `M`, `m` all 1024-based), but only when the caller opts in.

## Verifying against the reference

The test values in `GoHumanizeTests` come from go-humanize v1.1.0 itself:

```go
// go mod init gen && go get github.com/dustin/go-humanize@v1.1.0 && go run .
package main

import (
    "fmt"

    "github.com/dustin/go-humanize"
)

func main() {
    for _, n := range []uint64{0, 9, 10, 1023, 1024, 10188, 10189, 55210, 1048575, 1048576, 1<<60 - 128} {
        fmt.Printf("[InlineData(%dL, %q)]\n", n, humanize.IBytes(n))
    }
}
```

The port was also checked against about 1.3 million `IBytes` outputs (every value up to 20000, dense sweeps around each unit and rounding boundary, the top of the `long` range, and random values) on .NET Core 3.1, .NET 6, and .NET 8, with no mismatches. Go takes a `uint64`, so negative values have no reference. `GoHumanize` formats them as a signed magnitude (`-54 KiB`). Use the same approach for any new format: capture its output from the real tool and test against those values.
