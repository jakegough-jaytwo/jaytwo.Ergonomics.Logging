# Repo Instructions

## Workflow

- Do not ask for permission before read-only exploration inside the repo. It is fine to inspect context with commands like `ls`, `grep`, `find`, `rg`, `Get-Content`, and `git diff`.
- Prefer read-only inspection first when gathering context.
- Only stop to ask before destructive actions, network access, or commands that require escalation outside the sandbox.
- Prefer minimal patches over full-file rewrites.

## File editing and formatting

- Preserve a file's existing line endings when editing.
- Honor `.editorconfig`, especially `end_of_line` and `insert_final_newline`, when present.
- If `end_of_line` is not specified, detect the file's dominant existing line ending and keep it.
- Do not introduce mixed line endings within a file.
- Do not normalize or rewrite line endings unless explicitly requested.
- Prefer ASCII in source and docs unless a file already intentionally uses non-ASCII characters. Avoid introducing typographic punctuation that can turn into mojibake.
- Keep comments sparse and practical. Short "why" comments are fine; avoid tutorial-style narration.

## Product charter

Ergonomic structured logging on Microsoft.Extensions.Logging. It is not a logging framework. See `README.md`.

Consumers (jaytwo.Ergonomics.Ado is the first) are not the product. Do not let a consumer's vocabulary leak into this package.

- Wrap `ILogger`; do not replace it or hide it behind a new abstraction.
- Null `ILogger` is a first-class no-op. `BuildMessage` always returns a builder.
- Event methods that format, hash, or run `extraConfig` must guard with `IsEnabled`.
- Keep the three channels distinct: message template / `AppendFieldToMessage`; `WithField` (this event, structured-only, not in the text); `Scope` / `BeginScope` (ambient `using`-block; MEL scopes are factory-global).
- Do not implement per-event properties (`operation_id`, `query_text`) as `EventLogger.Scope` or a MEL `BeginScope` around a library operation.
- Duration formatting (`FormatTimePretty` / `FormatTimeSeconds`) byte formatting (`FormatBytesPretty`), and count formatting (`FormatCountPretty`) stay on `EventLogger`. `Bytes/GoHumanize` stays `internal` until it is extracted into its own package (see `src/jaytwo.Ergonomics.Logging/Bytes/README.md`).
- Field bags last-win on duplicate keys.
- Prefer `WithExtraConfig` as the extension hook.
- Pair `EventLogger` methods with a named `EventId` catalog as described in the README. Create catalog entries with `EventLogger.EventIdFromName` (jaytwo.StableHashing). Do not emit library events through ad-hoc `logger.BuildMessage`. Do not hand-number `EventId.Id`.
- Field and placeholder names are `snake_case`. Queryable durations use a `_seconds` suffix only; do not mix `_ms` and `_seconds`.
- Log lines are human-readable (Go compact durations, grouped counts). Fields are sortable and unambiguous (invariant `_seconds`, raw numbers). Do not use one format for both.
- Libraries always write both the human line and `WithField` context. They do not configure sinks. Apps use console locally and a structured store (Seq, Splunk, Graylog, and the like) when deployed. Do not put SQL or correlation ids in the sentence to compensate for `IncludeScopes=false`.

## Code style and structure

- Match the existing public API style: prefer small overload families that forward to one core implementation instead of collapsing everything into one clever method.
- Stay explicit rather than magical. Do not add inference or hidden behavior.
- Use expression-bodied members for trivial pass-through wrappers; use block bodies when there is meaningful control flow, disposal, logging, or validation.
- When logic is only relevant to one method, prefer a local helper or local function near the call site rather than extracting a broader private method.
- Use file-scoped namespace declarations (`namespace jaytwo.Ergonomics.Logging;`), not block-scoped.
- Prefix private instance fields with underscore: `_logger`, `_fields`.
- Mark concrete implementation classes `internal` when they are not part of the intended public API surface.
- Mark leaf implementation classes `sealed` unless there is an explicit reason to allow inheritance. `EventLogger` is the exception: subclassing it is the intended catalog pattern.
- Preserve nullable intent. Keep `?`, null guards, and `ArgumentNullException` checks consistent with surrounding code.
- Guard constructor parameters with the null-coalescing throw inline: `_x = x ?? throw new ArgumentNullException(nameof(x));`, not a separate `if` block.

## API design

- Extensions hang off `ILogger?`, not `ILogger`. A null logger returns a no-op builder rather than throwing.
- Prefer `Action<LogBuilder>` configuration delegates over builder objects for caller-supplied extension. Keep `extraConfig` the sole hook rather than mixing it with property-setting APIs.
- Reuse the repo's naming patterns for delegate parameters and overloads: `extraConfig`, `eventId`, `message`, `args`.
- Builder methods return the builder so chains compose; terminal level helpers do not consume the builder.
- Gate framework-specific APIs with `#if NET6_0_OR_GREATER`, `#if NET7_0_OR_GREATER`, etc. Keep the conditional blocks narrow to only the type or method that requires the newer API.

## Resource management and async

- Prefer explicit ownership and cleanup. A scope handed back to a caller must be safe to dispose exactly once, and safe to `using` when null.
- Prefer `finally` in `Dispose()` when the second cleanup must run even if the first throws.
- In library async code, continue using `ConfigureAwait(false)`.

## Tests

- Mirror the existing xUnit style: clear arrange/act/assert sections, descriptive behavior-oriented names, and separate sync/async coverage instead of hiding cases behind heavy abstraction.
- Assert against `CapturingLogger` records: the rendered line, the `EventId`, the level, and the scope/field bag. Do not assert only that something was logged.
- Cover the null-logger no-op path and the `IsEnabled == false` path when adding an event method.
- Name test methods as `MethodName_does_behavior`, not `MethodName_WhenCondition_ThenOutcome`.
