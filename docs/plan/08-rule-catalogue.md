# 08 — Rule Catalogue

## The ranges

`SK` + four digits. A range is allocated once and never re-purposed.

| Range | Category | Default severity | Fix? |
|---|---|---|---|
| `SK0001`–`SK0999` | Formatting and arrangement violations (what `skala format`/`arrange` would change) | suggestion | always |
| `SK1000`–`SK1999` | **Modernization** — the code is fine, the language has moved | suggestion | almost always |
| `SK2000`–`SK2999` | Correctness and bug risk | warning | usually |
| `SK3000`–`SK3499` | Async and concurrency | warning | sometimes |
| `SK3500`–`SK3999` | Disposal and lifetime | warning | sometimes |
| `SK4000`–`SK4999` | Performance and allocation | suggestion | usually |
| `SK5000`–`SK5999` | Security | error | rarely |
| `SK6000`–`SK6999` | API and design | suggestion | sometimes |
| `SK7000`–`SK7999` | Maintainability, metrics, duplication | warning | no |
| `SK8000`–`SK8999` | Tests | warning | sometimes |
| `SK9000`–`SK9999` | Tool, configuration and infrastructure diagnostics | varies | n/a |

## Rule metadata

`Rules/Rikarin.Skala.Rules.Metadata/rules.json`, one entry per rule, the single source for the
analyzer's `DiagnosticDescriptor`, the docs page, the `explain` text, the SARIF `rules[]` block, and
the ReSharper severity mapping:

```jsonc
{
  "id": "SK1002",
  "title": "Use a primary constructor",
  "category": "Modernization",
  "defaultSeverity": "suggestion",
  "scope": "Semantic",                 // Syntax | Semantic | Compilation — drives cache + loose mode
  "requiresSemantics": true,
  "hasFix": true,
  "fixIsSafe": true,                   // safe fixes may be applied by `--fix` without review
  "resharperId": "ConvertToPrimaryConstructor",
  "supersedes": ["IDE0290"],           // Roslyn/third-party rules this replaces; both firing is deduped
  "since": "0.3",
  "languageVersion": "12.0",           // never fires below this LangVersion
  "summary": "…one sentence…",
  "rationale": "…the paragraph `skala explain` prints…",
  "examples": { "bad": "…", "good": "…" },
  "falsePositives": "…the known ones, stated…"
}
```

⚠ `languageVersion` is load-bearing for a mixed ecosystem. A rule that suggests C# 14 syntax in a
project pinned to C# 10 is a rule that produces uncompilable fixes. Every modernization rule declares
its floor and is silent below it — checked against the compilation's *effective* `LangVersion`, not
the SDK's.

⚠ `supersedes` is how the tool avoids double-reporting when a third-party analyzer is hosted. If
`IDE0290` and `SK1002` both fire on the same span, one is dropped, and which one is a documented,
deterministic choice (`supersedes` wins; the superseded one is recorded in the SARIF as suppressed
with reason `superseded`).

## SK1000 — Modernization

The reason the tool exists in an AI-heavy workflow. These are not bugs; they are code written in an
older dialect than the one the repository speaks. An agent trained on a decade of C# writes
`new List<string>()` and `x == null` unless something tells it otherwise, and telling it once in a
prompt does not survive to the next session. A rule does.

**Language shape**

| ID | Rule | Floor |
|---|---|---|
| `SK1001` | Collection expression `[…]` for array/list creation and `.ToArray()`/`.ToList()` chains | 12 |
| `SK1002` | Primary constructor where the ctor only assigns fields | 12 |
| `SK1003` | `field` keyword instead of a hand-written backing field | 14 |
| `SK1004` | `extension` block instead of a static class of extension methods | 14 |
| `SK1005` | File-scoped namespace | 10 |
| `SK1006` | `using` declaration instead of a `using` statement whose block is the rest of the method | 8 |
| `SK1007` | Top-level statements in an entry point that is only a `Main` | 9 |
| `SK1008` | `record` for a type that is only data with value equality | 9 |
| `SK1009` | Required members instead of a constructor whose only job is enforcement | 11 |

**Pattern matching**

| ID | Rule |
|---|---|
| `SK1010` | `is not null` / `is null` instead of `!=`/`==` (respects user-defined `==`) |
| `SK1011` | Property pattern instead of chained member-access comparisons |
| `SK1012` | `switch` expression instead of an `if`/`else if` chain that only returns |
| `SK1013` | List pattern instead of length checks plus indexing |
| `SK1014` | Relational/logical patterns instead of `&&`-chained comparisons on one operand |
| `SK1015` | `is T t` instead of `is T` followed by a cast |

**Newer BCL over older idiom** — the highest-value group, because these are correctness and
performance wins that read as style:

| ID | Instead of | Use |
|---|---|---|
| `SK1020` | `if (x is null) throw new ArgumentNullException(…)` | `ArgumentNullException.ThrowIfNull(x)` (and the `ArgumentOutOfRangeException.Throw*` family) |
| `SK1021` | `new Regex(literal)` | `[GeneratedRegex]` partial method |
| `SK1022` | `IndexOfAny(char[])` / repeated `Contains` on a constant set | `SearchValues<T>` |
| `SK1023` | `lock (new object())` | `System.Threading.Lock` |
| `SK1024` | `DateTime.Now` / `DateTimeOffset.UtcNow` in injectable code | `TimeProvider` |
| `SK1025` | `static readonly Dictionary` used only for lookup | `FrozenDictionary` |
| `SK1026` | ASCII `string` constants passed to byte APIs | `"…"u8` |
| `SK1027` | `string.Format` / concatenation in a hot path | interpolation, or `DefaultInterpolatedStringHandler` |
| `SK1028` | `Encoding.UTF8.GetString(bytes)` on a slice | span overloads |
| `SK1029` | `Task.Run` wrapping synchronous-only work in an async method | direct call, or `ValueTask` |
| `SK1030` | `x = x ?? y`, `if (x == null) x = y` | `??=` |
| `SK1031` | `if (x is not null) x.P = v` | `x?.P = v` (C# 14) |
| `SK1032` | `params T[]` | `params ReadOnlySpan<T>` where callers permit |
| `SK1033` | `Dictionary.ContainsKey` then indexer | `TryGetValue` / `TryAdd` / `CollectionsMarshal` |
| `SK1034` | `.Count() > 0`, `.Any()` on `ICollection` | `.Count > 0` |
| `SK1035` | `Enum.GetValues(typeof(T))` | `Enum.GetValues<T>()` |
| `SK1036` | manual `IAsyncEnumerable` consumption via `MoveNextAsync` loops | `await foreach` |

⚠ Several of these (`SK1022`, `SK1025`, `SK1027`, `SK1032`) are only wins in hot paths and are noise
everywhere else. They ship at `hint`, not `suggestion`, and become `suggestion` inside paths the
`.editorconfig` marks — `[Core/**/*.cs] dotnet_diagnostic.SK1022.severity = suggestion`. Vixen
already segments its config by folder exactly this way; the mechanism exists and costs nothing.

## SK2000 — Correctness

Where the tool replaces the part of SonarQube people actually care about. Selected for *findings per
false positive*, not coverage:

`SK2001` comparison always true/false by nullability or range · `SK2002` result of a pure method
discarded · `SK2003` `==` on floating point · `SK2004` `GetHashCode` inconsistent with `Equals` ·
`SK2005` mutable struct with a readonly field · `SK2006` `ref`/`out` parameter never assigned on a
path · `SK2007` collection modified during enumeration (syntactic patterns only) · `SK2008` shadowed
loop variable captured in a closure · `SK2009` `switch` over an enum missing members with no
`default` · `SK2010` `string.Compare`/`ToLower` culture-sensitive by accident · `SK2011` `Equals` on
a value type without an override, boxing · `SK2012` self-assignment, self-comparison ·
`SK2013` exception constructed but not thrown · `SK2014` `catch` that swallows without logging or
rethrow · `SK2015` `throw ex` losing the stack trace · `SK2016` interpolated string in a logger call
that takes a template (the `CA2254` case, which the export sets to `suggestion`).

## SK3000 — Async, concurrency, lifetime

`SK3001` `async void` outside an event handler · `SK3002` blocking on async (`.Result`, `.Wait()`,
`GetAwaiter().GetResult()`) · `SK3003` missing `ConfigureAwait` where the config asks for it
(`resharper_configure_await_analysis_mode` is in the export) · `SK3004` `CancellationToken` accepted
and never passed on · `SK3005` fire-and-forget `Task` with no continuation · `SK3006` `async` method
with no `await` · `SK3007` `Task` returned from a `using` block that disposes what it awaits ·
`SK3008` lock held across an `await` · `SK3009` `Lazy<T>` without a thread-safety mode in shared
state · `SK3501` `IDisposable` created and not disposed on all paths · `SK3502` field of a disposable
type in a type that is not disposable · `SK3503` `IAsyncDisposable` disposed synchronously.

## SK4000 — Performance

`SK4001` LINQ in a per-frame or per-request path (path-scoped, off by default) · `SK4002` closure
allocation in a hot loop · `SK4003` `params` array allocated at a call site that could use a span ·
`SK4004` boxing in a generic constraint-satisfiable position · `SK4005` `string +=` in a loop ·
`SK4006` `ToList()`/`ToArray()` materialising something immediately re-enumerated once ·
`SK4007` `struct` larger than 64 bytes passed by value repeatedly · `SK4008` `async` state machine
for a method that always completes synchronously (→ `ValueTask`).

## SK5000 — Security

Deliberately narrow, deliberately loud. Rules here are `error` by default, so they must be right.

`SK5001` SQL built by concatenation reaching a command · `SK5002` process start with unsanitised
input · `SK5003` path built from user input without `Path.GetFullPath` containment ·
`SK5004` deserialization of untrusted input with a polymorphic serializer · `SK5005` weak
hash/cipher (`MD5`, `SHA1`, `DES`, ECB) · `SK5006` hardcoded credential or key material by shape and
entropy · `SK5007` certificate validation disabled · `SK5008` `Random` used for a token or key ·
`SK5009` XML reader with DTD processing enabled.

Taint-tracked rules (`SK5001`–`SK5004`) are built on Roslyn's `ControlFlowGraph` +
`DataFlowAnalysis` with intra-procedural propagation and a declared source/sink/sanitizer table in
`taint.json`. ⚠ Inter-procedural taint is explicitly out of scope for v1: it is where the false
positives live and where Sonar's advantage is real. Where Skala cannot prove a flow, it says nothing
rather than guessing — [00](00-vision-and-principles.md)'s false-positive bar applies hardest here.

## SK6000 — API and design

`SK6001` public API without doc comments (opt-in, per path) · `SK6002` public member exposing a
mutable array or `List<T>` · `SK6003` `abstract` type with a public constructor ·
`SK6004` interface with one implementation and no test double (hint) · `SK6005` optional parameter
in a public virtual method · `SK6006` `enum` without an explicit zero value · `SK6007` `struct`
without `IEquatable<T>` · `SK6008` extension method on `object`.

## SK7000 — Maintainability

The metrics from [07](07-analysis-host.md) § "Metrics", plus:

`SK7020` duplicated block ≥ *n* tokens ([09](09-quality-gates-and-reporting.md) § "Duplication") ·
`SK7030` file over *n* lines · `SK7040` TODO/FIXME without an issue reference ·
`SK7050` `#pragma warning disable` without a justification comment · `SK7051` `SuppressMessage`
without a real `Justification` · `SK7060` commented-out code (token-density heuristic, `hint`).

## SK8000 — Tests

`SK8001` test method with no assertion · `SK8002` `Assert.True(x == y)` instead of `Assert.Equal` ·
`SK8003` `[Fact]` on a method with parameters · `SK8004` `async void` test ·
`SK8005` `Thread.Sleep` in a test · `SK8006` test that is `[Skip]`ped without a reason ·
`SK8007` non-deterministic input (`DateTime.Now`, `Guid.NewGuid`, `Random`) in an assertion path.

Scoped to test projects by convention (`*.Tests`) and by `.editorconfig` section, matching how Vixen
already segments `[**/*.Tests/**/*.cs]`.

## SK9000 — Tool diagnostics

Already referenced throughout: `SK9001` unknown config key · `SK9002` config inherited from above the
repository root · `SK9003` style key in `skala.jsonc` · `SK9004` duplicate option alias ·
`SK9005` contradictory options · `SK9006` a setting is on that Skala cannot honour and that makes the
IDE and the oracle disagree (`autodetect_indent_settings`, `use_indent_from_vs`) ·
`SK9010` file did not parse · `SK9011` unbalanced preprocessor
structure, not formatted · `SK9020` binlog stale for a file · `SK9021` binlog missing a file ·
`SK9030` analyzer threw · `SK9031` analyzer failed to load · `SK9098` arrangement reverted, new
diagnostics · `SK9099` **formatter output was not token-equivalent** — the one that means "stop and
file a bug".

## What gets built, in what order

Not all of the above at once. The selection rule for v1 is: **a rule ships when it has (a) a fix,
(b) zero false positives on the reference corpus, and (c) a documented false-positive story.** The
corpus is 1.35 M lines of real code; a rule that fires 400 times there and is right 390 times is not
ready.

Milestone order in [15](15-roadmap.md), but the shape is: `SK9xxx` first (they are the tool talking
about itself and are needed to develop everything else) → `SK0xxx`/`SK1xxx` (the formatter's own
findings and the modernization set, which is the differentiator) → `SK2xxx`/`SK3xxx` (the SonarQube
replacement's core) → `SK7xxx` (metrics and duplication, needed for the gate) → `SK4xxx`/`SK6xxx` →
`SK5xxx` last, because security rules that are wrong are worse than absent.

## Documentation

`docs/rules/SK1002.md` is generated from `rules.json` and contains the summary, the rationale, the
bad/good examples, the known false positives, the configuration keys, and the related rules in other
tools. `skala explain SK1002` prints it. The website, if there ever is one, renders the same files.
One source, three surfaces — the same rule the option registry follows.
