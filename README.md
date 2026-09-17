# jaytwo.Ergonomics.Logging

Ergonomic structured logging for Microsoft.Extensions.Logging.

[![NuGet Version](https://img.shields.io/nuget/v/jaytwo.Ergonomics.Logging.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/jaytwo.Ergonomics.Logging)
[![NuGet Downloads](https://img.shields.io/nuget/dt/jaytwo.Ergonomics.Logging.svg?style=flat)](https://www.nuget.org/packages/jaytwo.Ergonomics.Logging)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://mit-license.org/)

Source: [github.com/jakegough-jaytwo/jaytwo.Ergonomics.Logging](https://github.com/jakegough-jaytwo/jaytwo.Ergonomics.Logging)

Targets `net8.0`, `net6.0`, and `netstandard2.1`. Requires `Microsoft.Extensions.Logging.Abstractions`.

> **Pre-1.0.** The API is still settling; minor version bumps may break things until 1.0.

## Contents

- [Installation](#installation)
- [Charter](#charter)
- [Which API](#which-api)
- [Applications](#applications)
  - [One-off logs](#one-off-logs)
  - [Request, message, and job scope](#request-message-and-job-scope)
  - [Hosting a library](#hosting-a-library)
  - [Correlating with traces](#correlating-with-traces)
  - [Adding fields to library events](#adding-fields-to-library-events)
  - [App domain catalogs](#app-domain-catalogs)
- [Libraries](#libraries)
- [Canonical events](#canonical-events)
  - [Catalog](#catalog)
  - [EventLogger methods](#eventlogger-methods)
  - [Naming](#naming)
  - [Message vs fields](#message-vs-fields)
  - [What it looks like](#what-it-looks-like)
- [Hosts and sinks](#hosts-and-sinks)
- [Development](#development)
- [License](#license)

## Installation

```bash
dotnet add package jaytwo.Ergonomics.Logging
```

## Charter

It is not a logging framework. It does not replace `ILogger`, invent a backend, or infer events from callers. It is a fluent layer on MEL so libraries can emit named, structured events without boilerplate, and so optional logging is cheap when nobody is listening.

### What it is

- A thin API on `ILogger`: `BuildMessage` / `LogBuilder`, `BuildScope` / `ScopeBuilder`, `BeginFieldScope` overloads, and `EventLogger`
- For library authors first: a null `ILogger` is a no-op; `EventLogger.IsEnabled` is the guard before formatting, hashing, or `extraConfig`
- Opinionated about how events look: named `EventId` from `EventLogger.EventIdFromName` (stable xxHash3 via jaytwo.StableHashing), `[{event_name}]` prefix, last-win field bags, `WithExtraConfig` as the extension hook, Go-style pretty time and decimal seconds
- Explicit about three channels:
  1. Message template and `AppendFieldToMessage` — human-readable line plus structured placeholders
  2. `WithField` — this event only, structured, not in the text
  3. `Scope` / `BeginFieldScope` — ambient context for a `using` block (application code; MEL scopes are factory-global)

### What it is not

- Not a new logger, sink, or `ILogger` wrapper that hides MEL
- Not magical: no caller-name events, no hidden ambient `operation_id`
- Not a reason to open a MEL scope around a library operation (that leaks onto the caller's logs). Per-event properties stay on `WithField`
- Not tied to any one consumer library

## Which API

Extensions are on `ILogger?`. `ILogger` and `ILogger<T>` both work. Get the logger from DI; do not implement `ILogger` yourself.

| You are doing | Use |
| --- | --- |
| One-off app log | `logger.BuildMessage(...)` then a level helper |
| Correlate a request, consumed message, or job | `logger.BeginFieldScope(...)` or `logger.BuildScope()...BuildScope()` |
| Named events in a library, or an app bounded context with a catalog | `EventLogger` subclass + `EventIdFromName` |
| Add a field to a library event from the host | Ambient `BeginFieldScope` (always works). `extraConfig` only if that library's public API forwards it |

Apps mix the first two freely. Libraries do not emit through ad-hoc `BuildMessage`; they go through `EventLogger`. `EventLogger.BuildMessage` always returns a `LogBuilder` (a null `ILogger` is a silent no-op builder). Do not chain with `?.`.

## Applications

### One-off logs

No catalog, no `EventLogger`. The sentence is what you tail. Use `WithField` for queryable context that should not appear in the line.

```csharp
_logger.BuildMessage("Queued job {job_name} for {user_id}.", job.Name, user.Id)
    .WithField("job_id", job.Id)
    .Information();
```

```csharp
_logger.BuildMessage("Failed to queue job {job_name}.", job.Name)
    .WithField("job_id", job.Id)
    .WithException(ex)
    .Error();
```

App-facing events are `Information` / `Warning` / `Error`. Leave `Debug` / `Trace` for library chatter.

A level helper is a no-op when that level is disabled and does **not** consume the builder. If `Information` is off, `.Information()` then `.Error()` still emits at Error. After a write actually emits, later helpers on that instance are ignored.

### Request, message, and job scope

`ILogger.BeginScope` is factory-global AsyncLocal, not per-logger. Open it in the app around a unit of work. Every log in that async flow — including library `EventLogger` events — picks up the fields. For example, that is how `request_id` lands on a data access library's `DB:READER_EXECUTED` without that library knowing anything about HTTP.

```csharp
using (_logger.BeginFieldScope(
    ("request_id", httpContext.TraceIdentifier),
    ("tenant_id", tenantId)))
{
    await _next(httpContext);
}
```

A worker or queue consumer is the same shape:

```csharp
using (_logger.BeginFieldScope(("job_id", job.Id), ("job_name", job.Name)))
{
    await RunAsync(job, cancellationToken);
}
```

> [!IMPORTANT]
> **Why `BeginFieldScope` and not `BeginScope`.** `ILogger.BeginScope<TState>(TState)` is an instance method that accepts any single argument, and an instance method always beats an extension method. An extension named `BeginScope` would therefore lose every one-argument call: `logger.BeginScope(("request_id", id))` would compile, bind to MEL, and open a scope whose state is a tuple. No provider reads fields out of a tuple, so the field would vanish with no warning. The same applies to a lone `KeyValuePair` and to any `Dictionary<string, T>` where `T` is not `object` (there is no variance on a value type, so formatters fall back to `ToString()`). The distinct name keeps every overload reachable and keeps last-win duplicate handling in play. Use `ILogger.BeginScope` directly only when you want to hand MEL a state object of your own.

When construction is conditional, use the builder. The factory and the terminal are both named `BuildScope` today (`logger.BuildScope()...BuildScope()`). A terminal with no fields returns `null`; `using` on `null` is safe.

```csharp
using (_logger.BuildScope()
    .WithField("request_id", httpContext.TraceIdentifier)
    .WithField("tenant_id", tenantId)
    .BuildScope())
{
    await _next(httpContext);
}
```

Duplicate keys last-win, same as `LogBuilder.WithField`.

Do not open this kind of scope inside a library to stamp `operation_id`. That leaks onto the caller's logs. See [Libraries](#libraries).

`EventLogger.Scope` is the same MEL scope if you already hold an `EventLogger` in app code. Prefer `ILogger.BeginFieldScope` in applications.

### Hosting a library

Pass `ILogger` or `ILogger<T>` in. The host owns sinks and levels. Typical ASP.NET / generic-host config:

```json
{
  "Logging": {
    "IncludeScopes": true,
    "LogLevel": {
      "Default": "Information",
      "MyCompany.Data": "Warning",
      "MyCompany.Http": "Debug"
    }
  }
}
```

`IncludeScopes` is what makes Microsoft console (and some other MEL formatters) print `WithField` and `BeginFieldScope` properties. Serilog and OpenTelemetry snapshot scopes at log time and do not need this flag. Default console without it still shows the human line (`[DB:READER_EXECUTED] Executed reader command in 4.2ms.`). That is enough for day-to-day local work. Turn scopes on, or use a JSON console, when you need SQL, hashes, or `operation_id` locally.

Do not ask libraries to put those values in the sentence so the default console looks richer.

Production: app at `Information`, library categories at `Warning` until you are investigating, then drop that category to `Debug` without changing code. Library chatter is `Debug` / `Trace`; library failures stay `Error` regardless of the host's default.

### Correlating with traces

If the app also emits `Activity` spans, the host can stamp trace ids onto every log record. This is a host setting, not a library concern: MEL reads `Activity.Current`, so library events emitted inside a span pick it up without the library knowing anything about tracing.

```csharp
builder.Logging.Configure(x => x.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
```

That gives you log-to-trace. For trace-to-log, the library stamps its own correlation id on the span as a tag, matching the `operation_id` it puts on each event (a data access library might tag the span `db.operation_id`). Do not solve either direction by opening a MEL scope inside the library.

### Adding fields to library events

**Ambient scope** is the usual app tool. Open `BeginFieldScope` for the request or job. Library events in that flow inherit `request_id`. When a library does not forward `extraConfig` through its public API, this is the only way to correlate its events from an application.

**`extraConfig`** is for when you are calling `LogXxx` yourself (wrapping the library, tests, or a public API that takes `Action<LogBuilder>`):

```csharp
httpEvents.LogRequestSent(method, host, status, elapsed, extraConfig: x =>
    x.WithField("user_id", userId));
```

Use `BeginFieldScope` for data that belongs on every log in the unit of work. Use `extraConfig` for a field that belongs on that one event.

### App domain catalogs

If the app has a stable set of named events (orders, payments), subclass `EventLogger` the same way a library does. One-off logs stay on `BuildMessage`. Mixing both in one process is expected.

## Libraries

Accept `ILogger?`. Null is a no-op. Take `ILogger<T>` or `factory.CreateLogger("MyCompany.Http")` so the host can set `"MyCompany.Http": "Debug"` without turning on the whole process. Do not add console or Seq loggers, and do not read host config.

Emit a named catalog through `EventLogger`. Always write both the human line and `WithField` context. Guard with `IsEnabled` at the same `LogLevel` as the terminal `.Trace()` / `.Debug()` / `.Warning()` / `.Error()` before formatting, hashing, or walking payloads.

Stamp correlation (`operation_id`) on each event with `WithField`. Hold that id on the `EventLogger` subclass, set at construction. Do not wrap the operation in `EventLogger.Scope` or any MEL scope.

Expose `Action<LogBuilder>? extraConfig = null` on every `LogXxx` method and call `.WithExtraConfig(extraConfig)` last before the level helper. Forward it from the public API if hosts should add per-event fields without forking. If you do not forward it, hosts still compose via `BeginFieldScope`.

Gate secrets (bodies, tokens, SQL, PII, parameter values) behind a host-supplied flag. Default to off, hashed, or omitted. A data access library gating SQL text and parameter values behind an explicit mode is the same idea.

The pattern is the same for HTTP, queues, caches, or ADO. Prefix the catalog so names do not collide (`HTTP:`, `JOB:`, `DB:`).

```csharp
internal static class Events
{
    public static readonly EventId RequestSent = EventLogger.EventIdFromName("HTTP:REQUEST_SENT");
    public static readonly EventId RequestFailed = EventLogger.EventIdFromName("HTTP:REQUEST_FAILED");
}

public sealed class HttpEventLogger : EventLogger
{
    public HttpEventLogger(ILogger? logger, string operationId)
        : base(logger)
    {
        OperationId = operationId;
    }

    public string OperationId { get; }

    public void LogRequestSent(string method, string host, int statusCode, TimeSpan elapsed, Action<LogBuilder>? extraConfig = null)
    {
        if (!IsEnabled(LogLevel.Debug))
        {
            return;
        }

        BuildMessage(Events.RequestSent, "Sent {http_method} in {elapsed_time}.", method, FormatTimePretty(elapsed))
            .WithFields(
                ("http_method", method),
                ("http_host", host),
                ("http_status", statusCode),
                ("elapsed_time_seconds", FormatTimeSeconds(elapsed)),
                ("operation_id", OperationId))
            .WithExtraConfig(extraConfig)
            .Debug();
    }

    public void LogRequestFailed(string method, string host, TimeSpan elapsed, Exception ex, Action<LogBuilder>? extraConfig = null)
    {
        if (!IsEnabled(LogLevel.Error))
        {
            return;
        }

        BuildMessage(Events.RequestFailed, "Request failed after {elapsed_time}.", FormatTimePretty(elapsed))
            .WithFields(
                ("http_method", method),
                ("http_host", host),
                ("elapsed_time_seconds", FormatTimeSeconds(elapsed)),
                ("operation_id", OperationId))
            .WithException(ex)
            .WithExtraConfig(extraConfig)
            .Error();
    }
}
```

Keep the URL and headers off the line. Put `http_host` / `http_status` on `WithField`. Do not log authorization headers.

## Canonical events

`EventLogger` is meant to pair with a catalog of named events. Ad-hoc `logger.BuildMessage(...)` is for application code. Library instrumentation goes through `EventLogger` and a stable `EventId` catalog.

### Catalog

- `EventId.Name` is the identity. Use `PREFIX:SCREAMING_SNAKE` (`DB:CONNECTION_OPENED`, `HTTP:REQUEST_SENT`). The prefix is the namespace; do not reuse names across libraries.
- `EventId.Id` comes from `EventLogger.EventIdFromName`: a stable positive xxHash3 of the name (jaytwo.StableHashing). Do not hand-number Ids that can drift from the name. `EventIdFromName` throws `ArgumentNullException` if the name is null.
- `Name` is required. `EventLogger.BuildMessage` prepends `[{event_name}]` as positional argument 0. Do not put `{event_name}` in the caller template. Do not construct `new EventId(id, null)` and pass that to `BuildMessage`; use `EventIdFromName`.
- Every catalog entry should have an emitter. If an operation is implemented in terms of another (for example scalar-via-reader), emit the event for the work that actually ran; do not keep a phantom catalog name.

### EventLogger methods

- One public `Log{PascalName}` method per catalog entry. Variants of the same event share the `EventId` and distinguish with `WithField` (a `LogEnumerateRows` and a `LogReadRow` can both emit `DB:ROWS_READ` and tell themselves apart with `operation_name`).
- Guard with `EventLogger.IsEnabled` at the same `LogLevel` as the terminal `.Trace()` / `.Debug()` / `.Warning()` / `.Error()`. Return before `BuildMessage`, `FormatTimePretty`, hashing, or `extraConfig` when it is false.
- Call `BuildMessage(catalogEventId, humanTemplate, ...)` (never null) then chain fields, then `WithExtraConfig(extraConfig)` last, then the level helper.
- The human template is a short sentence. Snake_case placeholders. Do not put SQL, URLs with query strings, hashes, or correlation ids in the sentence.
- Failures use the catalog's failed event (`DB:COMMAND_FAILED`, `HTTP:REQUEST_FAILED`), `WithException`, and `.Error()`. Do not reuse the success event for the failure path.

### Naming

Placeholders and `WithField` keys are `snake_case` (`elapsed_time`, `operation_id`, `http_status`). That is not the EventId (`HTTP:REQUEST_SENT`).

Queryable fields that have a unit take a suffix. Durations on the field channel are always `_seconds` (`elapsed_time_seconds`, `lifetime_seconds`). Do not mix `_ms`, `_millis`, `_ticks`, or a unitless `elapsed_time` on `WithField`. One unit per quantity, for the whole catalog. The pretty string in the sentence already carries its own unit (`12.3ms`); do not suffix `{elapsed_time}`.

Other suffixes stay consistent the same way (`query_text_hash`).

### Message vs fields

| Kind | Channel | Example |
| --- | --- | --- |
| Human duration | message placeholder + `FormatTimePretty` | `{elapsed_time}`, `{lifetime}` |
| Queryable duration | `WithField` + `FormatTimeSeconds` | `elapsed_time_seconds`, `lifetime_seconds` |
| Correlation | `WithField` on every event | `operation_id` |
| Queryable extras | `WithField` | `http_host`, `query_text`, `query_text_hash` |
| Value in the sentence and on the log record | placeholder, or `AppendFieldToMessage` | `http_method`, `attempt`, `row_count` |
| Value also on the WithField channel | placeholder/`AppendFieldToMessage`, and `WithField` | `http_method`, `row_count` |

The line is for a person scanning a console. The fields are for query, sort, and aggregate. Do not use one format for both.

- **Line:** human units and grouping. Durations are Go compact (`12.3ms`, `1m30s`). Counts that appear in the sentence may use thousands separators (`1,234 rows`). Pre-format those placeholder values; do not rely on the current culture's `ToString()`.
- **Fields:** sortable and unambiguous. Durations are invariant decimal seconds (`0.0123`, `90.5`) on `elapsed_time_seconds`. Counts stay a number (`row_count = 1234`), not `"1,234"`. Status codes stay a number (`http_status = 200`). No pretty strings, no mixed units, no locale-dependent decimal commas.

`1m30s` sorts after `12.3ms` as text. `90.5` sorts after `0.0123` as a number. That is why pretty stays in the sentence and `_seconds` stays on the field.

**What belongs on the line.** The sentence is what you grep in a terminal or read in a console sink. Keep it one short line: event prefix (already added), verb, object, human duration. Put a value in the template (or `AppendFieldToMessage`) when a person needs it to understand the event without opening fields: how long it took, how many rows, which HTTP method. Do not put SQL, URLs, parameter values, hashes, or correlation ids in the sentence. Those explode the line and bury the next event.

**What belongs in fields.** Anything you would filter, group, or sort on in Seq, Splunk, or Graylog: `operation_id`, `http_host`, `http_status`, `query_text_hash`, `elapsed_time_seconds`, method names, flags, exceptions. If you would write `query_text_hash = abc` in a search box, it is a field. If you would not shout it across the room, it is not a line.

**Both.** A value that is part of the English sentence and also a query key (`http_method`, `row_count`) can live in the template and on `WithField`. Pretty duration stays in the line; `_seconds` is the field. Do not skip fields because the local console hides them.

Do not stamp `operation_id` (or the equivalent) with `EventLogger.Scope`. MEL scopes are factory-global and would leak onto the caller's logs.

> [!NOTE]
> **Duration helpers.** Durations always go through this pair. `FormatTimePretty` is Go compact (`12.3ms`, `1m30s`) and belongs in the sentence (`{elapsed_time}`, `{lifetime}`). `FormatTimeSeconds` is a culture-invariant decimal seconds string (`0.0123`, `90.5`) and belongs on `WithField` as `elapsed_time_seconds` / `lifetime_seconds`. Standardize on `_seconds`. Do not add `_ms` next to `_seconds` in the same catalog (or on the same event). Do not log a raw `TimeSpan` (its default `ToString()` is not the convention). Do not put the pretty string on the field channel if you want something sortable. Nullable overloads return `null`. Both methods are `virtual` if a subclass needs a different clock face; keep the pair (human vs queryable seconds).

> [!NOTE]
> **Named holes, positional values.** `ILogger.Log(template, args)` does not match `{elapsed_time}` to a parameter named `elapsed_time`. Holes are filled **in order**: the first `{...}` in the final template gets `args[0]`, the second gets `args[1]`. The names are for the provider. Serilog, OpenTelemetry, and JSON formatters use them as property names on the log record. The console formatter uses them only to print the line.
>
> `EventLogger.BuildMessage` prepends `[{event_name}] ` and puts `EventId.Name` at `args[0]`. Placeholders in your sentence must follow that hole, in the same order as the values you pass (`{elapsed_time}` → `FormatTimePretty(elapsed)`). Do not put `{event_name}` in the caller template; it would take `args[0]` and shift everything else.
>
> `AppendFieldToMessage` appends ` key={key}` to the template and the value to the end of `args`, so the extra hole stays aligned. `WithField` is not a placeholder. It is a 1-shot MEL scope. Providers that ignore scopes will not show those properties.

### What it looks like

Same HTTP method, elapsed 12.3ms, `operation_id` `op-1`:

```
[HTTP:REQUEST_SENT] Sent GET in 12.3ms.
```

Context on that event (not in the line):

```
http_method          = GET
http_host            = api.example.com
http_status          = 200
elapsed_time_seconds = 0.0123
operation_id         = op-1
```

`EventId.Name` is `HTTP:REQUEST_SENT`. `EventId.Id` is the stable xxHash3 of that name. The template also binds `event_name`, `http_method`, and `elapsed_time` as MEL placeholders on the log record; they already appear in the line.

`AppendFieldToMessage` adds ` key={value}` to the line and as a MEL placeholder. It does not put the value on the WithField channel:

```csharp
BuildMessage(Events.RequestSent, "Sent {http_method} in {elapsed_time}.", method, FormatTimePretty(elapsed))
    .AppendFieldToMessage("attempt", 2)
    .WithFields(
        ("http_method", method),
        ("elapsed_time_seconds", FormatTimeSeconds(elapsed)),
        ("operation_id", OperationId))
    .Debug();
```

```
[HTTP:REQUEST_SENT] Sent GET in 12.3ms. attempt=2
```

Context (not in the line):

```
http_method          = GET
elapsed_time_seconds = 0.0123
operation_id         = op-1
```

`attempt` is in the line (and on the log record). `operation_id` stays context-only. Use `WithField` when the value must not appear in the text.

A data access catalog is the same split: short line, SQL and hashes in context:

```
[DB:READER_EXECUTED] Executed reader command in 4.2ms.
```

```
db_command_method    = ExecuteReader
command_behavior     = Default
elapsed_time_seconds = 0.0042
operation_id         = op-1
query_text_hash    = ...
query_text         = SELECT @input
```

A failure keeps the catalog name and attaches the exception; still no payload in the line:

```
[HTTP:REQUEST_FAILED] Request failed after 4.2ms.
```

```
http_method          = GET
http_host            = api.example.com
elapsed_time_seconds = 0.0042
operation_id         = op-1
exception            = (the caught Exception)
```

`WithField` is a 1-shot MEL scope around `Log()`. Sinks that snapshot scopes (Serilog, OpenTelemetry, or Microsoft console with `IncludeScopes`) see those properties. Default Microsoft console does not.

## Hosts and sinks

Libraries emit events. The application owns the sink. Do not special-case the host inside `EventLogger` methods. Write the line and the fields every time; `IsEnabled` is the only cheap skip.

**Development / debugging:** log to the console. The human line is the product. `IncludeScopes=false` hides `WithField`; that is a sink setting, not a reason to collapse the two channels.

**Deployed:** log to a structured store (Seq, Splunk, Graylog, OpenTelemetry, and the like). Those sinks snapshot scopes and message-template properties. Filter on `EventId.Name`, `operation_id`, `http_status`, `query_text_hash`, `elapsed_time_seconds`. The line is still stored as the display template; it is not the query surface.

One event, two readers. A developer tails the console and reads the sentence. An operator queries Seq and never looks at the sentence. The library does not know which reader is listening and should not care. Ambient request scope from the app plus per-event fields from the library is the whole picture.

## Development

Requires the .NET SDK. No Docker, database, or network service is needed; the whole suite is in-process. The `Makefile` is GNU make over bash - on Windows run it from Git Bash or WSL, or use the plain `dotnet` commands shown alongside each target.

```bash
dotnet tool restore   # installs reportgenerator and nugetcheck, needed by `make test`
dotnet build
dotnet test
```

```bash
make            # clean deps build test pack-beta nuget-check
make test
```

Pack a local package into `out/packed` with `make pack` or `make pack-beta`.

## License

[MIT](https://mit-license.org/). See [LICENSE](LICENSE).

---

Made with &hearts; by Jake
