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
`.editorconfig` marks — `[Core/**/*.cs] dotnet_diagnostic.SK1022.severity = suggestion`.

⚠ **The sentence that used to follow — "Vixen already segments its config by folder exactly this way;
the mechanism exists and costs nothing" — is withdrawn as a justification.** The mechanism is real
and correct and other repositories will use it. But Vixen's `.editorconfig` was not authored: it was
built by agents as they went, 916 lines and 56 path-scoped sections, never reviewed as a whole. That
a rule's default can be justified by how one unreviewed file happens to be laid out is the error
[16](16-risks-and-open-questions.md) § "The reference trees are a test subject" names, and the
`hint` default is listed under § "Decisions that rest on a reference-tree count" for it.

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
for a method that always completes synchronously (→ `ValueTask`) · `SK4010` a `Where` the next
operator could have taken as its predicate.

⚠ `SK4010` is outside the `SK4001`–`SK4008` block on purpose. Those eight ids name eight concepts
this document allocated before any of them existed, and the register above is only a register if a
new concept takes a new number rather than the nearest tidy one. Folding
`xs.Where(p).First()` into `xs.First(p)` is not any of the eight.

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

The metrics from [07](07-analysis-host.md) § "Metrics" — `SK7001` cyclomatic complexity ·
`SK7002` cognitive complexity · `SK7003` method length in statements · `SK7004` type size in
members · `SK7005` parameter count · `SK7006` nesting depth · `SK7010` public-API comment density —
plus:

⚠ Those seven ids used to live only in doc 07's table, and this document is the allocation
register. `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister` now asserts the containment,
because a register that the code can drift away from is a register nobody can trust to answer
"is this number free".

`SK7020` duplicated block ≥ *n* tokens ([09](09-quality-gates-and-reporting.md) § "Duplication") ·
`SK7030` file over *n* lines · `SK7040` TODO/FIXME without an issue reference ·
`SK7050` `#pragma warning disable` without a justification comment · `SK7051` `SuppressMessage`
without a real `Justification` · `SK7060` commented-out code (token-density heuristic, `hint`).

## SK8000 — Tests

`SK8001` test method with no assertion · `SK8002` `Assert.True(x == y)` instead of `Assert.Equal` ·
`SK8003` `[Fact]` on a method with parameters · `SK8004` `async void` test ·
`SK8005` `Thread.Sleep` in a test · `SK8006` test that is `[Skip]`ped without a reason ·
`SK8007` non-deterministic input (`DateTime.Now`, `Guid.NewGuid`, `Random`) in an assertion path.

Scoped to test projects by convention (`*.Tests`) and by `.editorconfig` section. ⚠ This used to read
"matching how Vixen already segments `[**/*.Tests/**/*.cs]`", and that clause is withdrawn for the
reason § "SK1000 — Modernization" gives: `*.Tests` is a .NET-wide convention and stands on its own,
while Vixen's sections are an unreviewed accident and are not a specification.

## SK9000 — Tool diagnostics

Already referenced throughout: `SK9001` unknown config key · `SK9002` config inherited from above the
repository root · `SK9003` style key in `skala.jsonc` · `SK9004` duplicate option alias ·
`SK9005` contradictory options · `SK9006` a setting is on that Skala cannot honour and that makes the
IDE and the oracle disagree (`autodetect_indent_settings`, `use_indent_from_vs`) ·
`SK9007` `skala.jsonc` is not valid JSON ·
`SK9008` canonical block drifted · `SK9009` repository behind the canonical ·
`SK9012` canonical version pinned in `skala.jsonc` · `SK9013` local block overrides a canonical
option · `SK9014` `.editorconfig` carries no canonical block ·
`SK9016` applying the canonical changes a `dotnet_diagnostic` severity — ⚠ **warning** when it moves
a *compiler* diagnostic up, because with `TreatWarningsAsErrors` that is a build failure from an
`.editorconfig` commit touching no code; info for a lowered one or an analyzer's ·
`SK9017` an option Skala owns was set to a value outside its domain — ⚠ **warning**, and the only
configuration diagnostic that fails `config check` without `--strict`. `SK9001` is info because the
export carries ~2 000 keys Skala will never implement and the user wrote nothing wrong; here the key
*is* in the registry, the configured value was discarded, and the code is formatted against a value
nobody chose. The message names the key, the value, the domain and **what is in force instead** ·
`SK9010` file did not parse · `SK9011` unbalanced preprocessor
structure, not formatted · `SK9007` `skala.jsonc` is not valid JSON · `SK9020` binlog stale for a file · `SK9021` binlog missing a file · `SK9022` no binary log found · `SK9023` no C# files under the requested paths · `SK9024` no solution or project to load · `SK9025` load mode produced no compilation, fell back · `SK9015` a file could not be read or written ·
`SK9030` analyzer threw · `SK9031` analyzer failed to load · `SK9096` arrangement reverted, a touched identifier now resolves to a different symbol ·
`SK9097` the format-and-arrange pipeline did not reach a fixed point · `SK9098` arrangement reverted, new
diagnostics · `SK9099` **formatter output was not token-equivalent** — the one that means "stop and
file a bug".

⚠ **This list is the allocation register, and ADR-012 makes every entry permanent.** The canonical
distribution work first claimed `SK9010` and `SK9011` — both already live in the formatter as "file
did not parse" and "unbalanced preprocessor structure" — and was renumbered to `SK9013` and `SK9014`
before it merged. Two meanings behind one id is precisely what a baseline cannot survive: a
fingerprint carries the rule id, so the collision silently un-suppresses one finding and wrongly
suppresses the other. **Check this list before allocating**, and prefer the next free number over the
next tidy one.

⚠ **`SK9012` currently has two meanings, and the guard did not catch it.** Measured at `8cbd66d`:

| Site | Meaning |
|---|---|
| `ConfigDiagnosticIds.CanonicalVersionInToolConfig` | a canonical version was pinned in `skala.jsonc` |
| `Formatting/Rikarin.Skala.Formatting.CSharp/FormatCommand.cs` | an `IOException` was thrown while formatting a file |

This is exactly the collision the paragraph above says was caught before it merged, and it is live.
`ToolDiagnosticIdTests.ToolDiagnosticIds_AreDeclaredOnce` misses it because it matches
`public const string … = "SK9012";` and the formatter passes the id as a **bare string literal** to
the `SkalaDiagnostic` constructor. `SK9007` was missing from this register for the same reason and
has just been added to it. **The guard reads declarations, not uses**, and until it reads uses the
register is enforced only against the half of the code that declares a constant. Resolving the
collision is a renumber of the formatter's use to the next free id — `SK9015` — and it is owed
before anyone holds a baseline containing either.

## Rule status

⚠ **This count is generated, and the reason it is generated is that the hand-kept one went stale
inside a single merge.** The table below recorded "21 shipped, 19.8 %", measured at `8cbd66d`; M8's
five `SK5xxx` landed after it was typed and nothing noticed. A catalogue that misreports its own
coverage is the same failure as a document describing behaviour the tool does not have, which is
what the truth pass was for. The numbers now come from intersecting the ids this document names with
`rules.json`, and `RuleCatalogTests.TheCoverageBlock_MatchesTheRegistry` fails when the block and the
registry disagree. Regenerate with `skala rules docs`.

<!-- BEGIN GENERATED COVERAGE -->
<!-- Regenerate with `skala rules docs`. Do not edit by hand: the numbers
     are computed from this file and rules.json, and a hand-kept count went
     stale inside one merge. -->

| | | |
|---|---:|---|
| Rules this document names | **109** | excluding band edges (`SK1000`–`SK1999` and the like), `SK3499`/`SK3500`, and `SK9xxx` |
| **Shipped** — present in `rules.json` | **39** | **36.1 %** |
| **Cut** — deliberately not built, reason recorded | **12** | § "Cut, with the reason" |
| **Retired** — allocated, superseded, never to be built | **1** | the id stays taken for ever (ADR-012) |
| **Outstanding** — planned, not built, not disposed of | **57** | includes the twelve declared cut with no reason recorded |

<!-- END GENERATED COVERAGE -->

⚠ **Three states, and the third is what makes the number honest.** A rule counted as outstanding
when it was actually cut on purpose makes the roadmap look as though it is failing at something it
decided; a rule counted as cut when nobody recorded a reason is a decision nobody can review. The
twelve M7 declared cut without recording why are **outstanding**, and § "Declared cut with no
recorded reason" is where they are named.

⚠ **26 % is the shipping bar working, not the project falling behind.** Four milestones each shipped
far fewer rules than they planned — 4 of 20, 6 of 36, 3 of ~15, 5 of 9 — because a rule ships only
with a fix, zero false positives across two reference trees, and a negative fixture set at least as
large as the positive one. Twenty-nine rules that are always right is the goal.
[16](16-risks-and-open-questions.md) § R3 describes the alternative — a hundred that are usually
right — as the failure mode, not the target.

⚠ Three artefacts agree with each other and are test-enforced — `rules.json`, `allocated-ids.txt`
and `docs/rules/`. `SK7003`, `SK7004` and `SK7005` ship and were named nowhere in this document until
the reconciliation below.

### ⚠ The three metrics this catalogue did not name

`SK7003` (member over the statement-count threshold), `SK7004` (type over the member-count
threshold) and `SK7005` (member over the parameter-count threshold) ship, have `rules.json` entries,
`docs/rules/` pages and fixtures, and fire on both reference trees. § "SK7000 — Maintainability"
delegates to [07](07-analysis-host.md) § "Metrics" rather than naming them, so the catalogue never
carried them. They are named here now:

| ID | Rule | Scope | Default |
|---|---|---|---|
| `SK7001` | Cyclomatic complexity over the threshold | Semantic | hint |
| `SK7002` | Cognitive complexity over the threshold | Syntax | suggestion |
| `SK7003` | Member over the statement-count threshold | Syntax | hint |
| `SK7004` | Type over the member-count threshold | Syntax | hint |
| `SK7005` | Member takes more parameters than the threshold | Syntax | hint |
| `SK7006` | Member nests deeper than the threshold | Syntax | hint |
| `SK7010` | Public API without a documentation comment | Syntax | none |

⚠ **`SK6001` and `SK7010` are two ids for one rule.** § "SK6000 — API and design" allocates `SK6001`
as "public API without doc comments (opt-in, per path)"; `SK7010` is that rule, and it is the one
that shipped. ADR-012 makes both ids permanent, so the fix is not renumbering: `SK6001` is **retired
before it was ever built**, `SK7010` is the live id, and this note is what stops somebody
implementing `SK6001` in three years.

### Reasons that justify a cut

⚠ Three, and none of them is about a reference tree —
[16](16-risks-and-open-questions.md) § "The reference trees are a test subject, not a specification":

1. **It duplicates a diagnostic the user already sees** from the compiler or a framework analyzer.
2. **It costs something measurable for no gain** — most concretely, a compilation-scoped rule that
   disables the warm incremental path on every run.
3. **It cannot be implemented correctly**, or its fix cannot be made behaviour-preserving.

⚠ **"It fires zero times on Vixen" is not on that list and never was a reason.** It is a fact about
Vixen. It is a reason to say that a rule's correctness is *untested on real code*, which is
[16](16-risks-and-open-questions.md) § R3's "measured at zero, tested at nothing" and stands
unchanged — and it is never a reason to cut, demote or disable a rule that is right. **Where Vixen
does not follow a rule, Vixen changes.**

### Cut, with the reason

| ID | Reason | Kind | Recorded at |
|---|---|---|---|
| `SK3006` `async` with no `await` | The compiler's `CS1998` says it, on by default in every project | 1 | M6 |
| `SK2006` `ref`/`out` parameter never assigned on a path | An `out` parameter left unassigned is **`CS0177`, a compiler error** — the code does not build, so there is nothing to warn about. A `ref` parameter is never required to be assigned, so the other half of the id names no defect at all | 1 | M9 |
| `SK3008` lock held across an `await` | **`CS1996`, a compiler error**: `await` inside a `lock` body does not compile, so the shape this id names cannot be written down. ⚠ see below | 1 | M9 |
| `SK8003` `[Fact]` with parameters | `xUnit1001`, on by default wherever `xunit.analyzers` is referenced | 1 | M7 |
| `SK8004` `async void` test | `xUnit1049`, same | 1 | M7 |
| `SK8002` `Assert.True(x == y)` | ⚠ see below | 3 | M7 |
| `SK4005` `string +=` in a loop | The fix introduces a `StringBuilder` before the loop and reads it after — a dataflow proof, not an edit | 3 | M7 |
| `SK6006` `enum` without an explicit zero | The fix inserts a member into a public API | 3 | M7 |
| `SK6007` `struct` without `IEquatable<T>` | The fix generates an implementation | 3 | M7 |
| `SK6002` public member exposing a mutable array/`List<T>` | No mechanical fix, large false-positive surface | 3 | M7 |
| `SK6005` optional parameter in a public virtual method | Same | 3 | M7 |
| `SK8001` test method with no assertion | Same, and the worst of the three: an assertion inside a helper is indistinguishable from no assertion without following the call | 3 | M7 |

⚠ **`SK8002`'s recorded reason splits in two, and only one half survives.** The half that survives is
about the rewrite: `Assert.Equal` has no overload taking a custom failure message, so rewriting any
of the 3 401 two-argument calls *deletes* something the author wrote on purpose;
`Assert.NotEqual(0, flags & Member)` over a `[Flags]` enum does not compile, because the `0` was an
implicit constant conversion and the rewrite drops it, so `T` cannot be inferred; and `Assert.Equal`
calls `Equals`, which is a **different predicate** from `operator ==` and is precisely what
`ConventionTests` exists to assert. Those are facts about C#, xUnit and the rule, they hold in any
repository, and they are reason 3. The half that does not survive is the conclusion drawn from them:
*"the honest form of the rule fires zero times on a tree with twelve thousand candidates"*. **That is
a Vixen count and it is struck.** The cut stands on the first half alone; what is *not* disposed of
by it is a narrower or fixless `SK8002` that reports only the shapes the rewrite is valid for, and
this note is the record that nobody has ruled that out.

⚠ **What `SK3008`'s cut does not dispose of.** `lock` is a keyword and `CS1996` makes its bad
spelling impossible, so the id as written names nothing. Holding a lock across a suspension is still
a real bug — it is just spelled `await semaphore.WaitAsync(); … await Work();` or
`Monitor.Enter(gate)`, neither of which is a `lock` statement. That is a **different concept**, and
ADR-012 says a different concept takes a different number rather than the nearest tidy one, so it is
not `SK3008` and nobody has allocated it. The obstacle M6 recorded — the dataflow that decides
whether a semaphore is still held at the `await` — is unchanged and is still most of the rule.

### ⚠ The compiler already says it, measured rather than assumed

M9's cuts rest on a claim that was worth checking rather than remembering: which of these shapes does
`csc` already report, on by default, in an ordinary SDK project? Compiled at `net10.0` with
`EnableNETAnalyzers` on and nothing suppressed:

| Shape | What the compiler says | Disposes of |
|---|---|---|
| `value = value;`, `_field = _field;` | `CS1717` warning | most of `SK2012` |
| `a == a`, `a < a` | `CS1718` warning | most of `SK2012` |
| `Equals(object)` overridden, no `GetHashCode` | `CS0659` warning | most of `SK2004` |
| `operator ==` without `Equals`/`GetHashCode` | `CS0660`, `CS0661` warnings | most of `SK2004` |
| `out` parameter unassigned on a path | **`CS0177` error** | all of `SK2006` |
| `await` inside a `lock` body | **`CS1996` error** | all of `SK3008` |
| unawaited task **inside an `async` method** | `CS4014` warning | part of `SK3005` |
| comparison to a constant outside the type's range | `CS0652` warning | part of `SK2001` |

⚠ **And which it does not**, which is the half that mattered more, because three rules were nearly
cut on a guess. `CA2016` (forward the `CancellationToken`), `CA2254` (logger template),
`CA2000` (dispose objects before losing scope) and `CA1001` (own disposable fields) **did not fire**
in that project. They are shipped in the SDK and they are not in the default analysis set, so a rule
that duplicates one of them does not duplicate a diagnostic the user already sees. `SK3004` and
`SK3501` ship because of that measurement.

⚠ **`CS1717`/`CS1718` reach the identifier spellings only.** `Prop = Prop`, `other.Prop =
other.Prop` and `other.Prop == other.Prop` produce nothing, so `SK2012` is not fully disposed of and
stays outstanding rather than cut. The same is true of `SK2004`: an `IEquatable<T>` implemented
without overriding `Equals(object)` or `GetHashCode` is silent, and it is the shape that puts a type
in a `HashSet` and gets reference equality.

### ⚠ Declared cut with no recorded reason — reclassified as outstanding

M7's retrospective says "twenty of the twenty-three were cut" and then gives reasons for eight of
them. These twelve have no reason recorded anywhere in this document or in the commit that wrote it,
so they are **outstanding**, not cut:

`SK4001` LINQ in a hot path · `SK4002` closure allocation in a hot loop · `SK4003` `params` array at
a call site that could use a span · `SK4004` boxing in a constraint-satisfiable position ·
`SK4006` `ToList()`/`ToArray()` immediately re-enumerated once · `SK4007` large `struct` passed by
value · `SK4008` async state machine for a synchronous method · `SK6001` (retired — see above) ·
`SK6004` interface with one implementation · `SK6008` extension method on `object` ·
`SK8006` `[Skip]` without a reason · `SK8007` non-deterministic input in an assertion path.

Saying "no reason was recorded" is the point. Two of them look cheap and obviously right
(`SK8006`, `SK6008`) and their absence is a gap rather than a decision.

### Outstanding, with what each is waiting on

The sixty-three that were never declared cut. Where a milestone recorded why it did not ship, the
reason is kept; it is a description of remaining work, not a disposal.

| Group | IDs | Waiting on |
|---|---|---|
| Declaration-shape rewrites | `SK1001`, `SK1002`, `SK1008` | The unsafe-fix path and an `--include` story. Each is a good rule and none is a *safe* fix (M5) |
| Evaluation-changing rewrites | `SK1006`, `SK1012`, `SK1015` | The guard that makes each provably behaviour-preserving is most of the rule (M5) |
| ⚠ Hot-path rules | `SK1022`, `SK1025`, `SK1027`, `SK1032` | Path-scoped configuration. **The `hint` default is suspect — see below** |
| The rest of the modernization set | `SK1003`, `SK1004`, `SK1007`, `SK1009`, `SK1011`, `SK1013`, `SK1014`, `SK1021`, `SK1023`, `SK1024`, `SK1026`, `SK1028`, `SK1029`, `SK1031`, `SK1033`, `SK1036` | Nothing recorded. Not started |
| Correctness | `SK2003`, `SK2005`, `SK2008`–`SK2011`, `SK2014`, `SK2016` | Nothing recorded beyond the shipping bar. Not started |
| ⚠ Correctness, partly disposed of by a compiler warning | `SK2001`, `SK2004`, `SK2012` | § "The compiler already says it" measures which spellings `csc` reports. What is left of each is the shape it does *not*: an always-true comparison that is not to an integral constant; an `IEquatable<T>` with no `GetHashCode`; `Prop = Prop` and `a.P == a.P`, where a property accessor may have a side effect and the fix is therefore not mechanical |
| Async and lifetime | `SK3003`, `SK3009`, `SK3502` | Nothing recorded. Not started |
| ⚠ Async, partly disposed of by a compiler warning | `SK3005` | `CS4014` covers the fire-and-forget inside an `async` method. What is left is the same call in a *synchronous* method, where the compiler says nothing — and where the repair is to make the caller `async`, which changes its signature. A fixless form is not disposed of |
| Security | `SK5001`–`SK5009` | **M8**, and deliberately last: a wrong security rule is worse than a missing one |
| Maintainability, non-metric | `SK7030`, `SK7040`, `SK7050`, `SK7051`, `SK7060` | Nothing recorded. Not started |

### ⚠ Decisions that rest on a reference-tree count, and are awaiting revisit

Marked rather than reversed, because the record of a decision being reversed is worth more than a
document that reads as though it were always right.
[16](16-risks-and-open-questions.md) § "The reference trees are a test subject" is the instruction.

| Decision | What it rested on | Status |
|---|---|---|
| **`SK8005` ships at `suggestion`**, not at the `warning` its range defaults to | 25 true findings on Vixen judged "true and not what you would change" — 14 polling back-offs, 8 tests whose subject is a real clock | ⚠ **Suspect.** Twenty-five true findings are twenty-five baseline entries and a piece of Vixen's backlog. The severity needs re-deciding against the standard, not against the tree |
| **`SK3001` ships disabled** (`none`) | Two reasons stacked: (a) compilation-scoped, so it costs every run the warm incremental path; (b) "Vixen contains no `async void` method at all" | ⚠ **Half struck, and (a) is now measured.** (b) is a Vixen count and is struck. (a) held up: enabling any compilation-scoped rule turns every run with a change into a cold one, which on Skala's own tree is **8.5 s against 6.9 s**, and moves the analyzer phase from one tree to all of them — 3.3 s of work the warm path did not do. The default stands. ⚠ **What does not stand is the note saying an opting-in repository "pays the cost knowing what it bought"** — see [16](16-risks-and-open-questions.md) § "The opt-in that does not cost what it says" |
| **Hot-path rules ship at `hint` and are promoted per path** | The mechanism, plus "Vixen already segments its config by folder exactly this way" | ⚠ **The mechanism is fine and other repositories will legitimately use it. The citation is not.** A default justified by how one repository's `.editorconfig` happens to be laid out is a default derived from that repository |
| **Metric thresholds sit above the corpus p99**, and `SK7010` ships at `none` | `SK7010` at `warning` produces 1 868 findings on `Testing/corpus` alone | ⚠ **Suspect, and the hardest case.** A threshold has no correct value independent of *some* population, so calibration is not optional — but calibrating against the tree the rule will be run on is how a metric comes to certify the present. Each threshold needs an argument against a standard rather than against a p99 |
| **`SK3002`'s seven Vixen findings**, triaged as "true, but none is something anyone would change" | — | ✅ **Not a decision about the rule, and never was.** Seven correct findings, seven baseline entries, and Vixen's work. The rule is unaffected and this row is here so that nobody re-reads the triage as a defect report |

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

### ⚠ What M9 added: five rules out of twenty-two, and the one false positive a tree found

M9 is the `SK2xxx`/`SK3xxx` milestone. Twenty-two ids were outstanding in the two bands; **five**
ship, two are cut with a reason, and the rest stay outstanding. Every one that ships is `Semantic`,
so **none of them costs the incremental cache anything** — the warm path is available on every run
that does not enable a compilation-scoped rule, and after M9 that is still only `SK3001` and
`SK7020`.

| Id | Scope | Fix | Fixtures (+/−) | `corpus/real` (380) | Vixen (4 680) | Cost |
|---|---|---|---:|---:|---:|---:|
| `SK2007` collection modified during enumeration | Semantic | `.ToList()` | 3 / 8 | 0 | **0** | 54.5 ms |
| `SK3004` `CancellationToken` accepted, not passed on | Semantic | a named or appended argument | 3 / 9 | 0 | **0** | 68.2 ms |
| `SK3007` a task built from a `using` resource is returned | Semantic | `async` + `await` | 3 / 9 | 0 | **0** | 43.9 ms |
| `SK3501` a disposable local is never disposed | Semantic | `using var` | 3 / 12 | 1 | **157** | 39.8 ms |
| `SK3503` an `IAsyncDisposable` is disposed synchronously | Semantic | `await using` | 3 / 7 | 0 | **36** | 6.9 ms |

Cost is `skala check --profile` over Skala's own binlog, summed across compilations. The five
together are 213 ms of a 3 269 ms analyzer budget — 6.5 %, all of it behind `MetricsAnalyzer`'s
79 %. ⚠ Two of them were four times that before the order of their checks was measured: `SK3007`
cost 400.7 ms asking a semantic question before a syntactic one that excludes almost everything, and
`SK3004` 330.9 ms resolving every parameter of every enclosing method on every invocation in the
tree to ask whether its type was `CancellationToken`, which the name in the source answers for free.
That is what doc 13's "every Skala rule's cost is reviewed against `--profile` before release" is
for, and it is the first milestone where it caught something.

⚠ **The false positive, and it is the reason this section exists.** `SK3501` reported
`var backend = new NullAudioBackend();` in `Samples/10-VoiceChat/Program.cs`. The backend is never
returned, never assigned, never passed and never captured — every guard the rule had said it kept
ownership. What it does is `device = backend.OpenDevice(options)`, and the device, which is
`IDisposable`, goes into a field and is used for the rest of the program's life. A `using` on the
backend closes it. **Ownership travels outward through what an object hands back, not only inward
through what is handed to it**, and no amount of reading the rule produced that; a reference tree
did. The guard is now "a read whose result is itself disposable withdraws the finding", it removed
six findings on Vixen, and it is the shape a fifth negative fixture documents.

⚠ **Three zeros, each with a denominator.** M7 established that a semantic rule's zero under a loose
compilation can be an artefact of unresolved symbols. Two things were done before these were
believed. First, **containment**: a probe file carrying one positive fixture per rule was audited
*alongside* each tree, and all five rules fired on it — so the analyzers are alive in that exact
compilation and the zeros are about the trees. Second, a **syntactic candidate count**, which is
what turns a zero into a ratio:

| | Vixen | What the candidates are |
|---|---:|---|
| `SK2007` — `foreach (… in X)` whose body names `X.Add`/`X.Remove`/… | 7 → **0** | All seven are `foreach (var x in items) this.items.Add(x)`: a parameter enumerated, a field mutated. One symbol, two collections. This is exactly what the receiver-text check exists to separate, and it is the only thing it caught |
| `SK3004` — method bodies declaring a `CancellationToken` | 348 | 244 of the awaits inside them pass the token; **13 do not**, and every one of the 13 calls a method that declares no token parameter at all — `Task.WhenAll`, `HttpListener.GetContextAsync`, `WaitForEarlierAsync(IReadOnlyList<AssetId>)`, `WebInterop.ImportAsync(string)`. There is nothing to forward |
| `SK3007` — a `return …Async(` inside a `using` | **0** | The shape does not occur. Vixen awaits inside the block |

⚠ **`SK3501`'s 157 and `SK3503`'s 36 were read, and all 193 are true.** They are not all *useful*,
which is a different quantity and the one doc 16 § R3 keeps having to name:

- **`SK3501`, 157 findings.** 11 hold a real resource — `ForwardLightingRenderFeature` disposing GPU
  scene and record buffers (×4), a `VolumetricFogRenderer`, a `ThumbnailCache`, an `MmoRealm` (×2), a
  `TcpListener`, a `MemoryStream`. The other 146 are ECS systems and one decoder, whose `Dispose` is
  inherited from `SystemBase` and is `GC.SuppressFinalize(this)` and nothing else. Those findings are
  correct — the type is `IDisposable` and the local is not disposed — and their repair changes
  nothing at runtime. ⚠ That `ISystem : IDisposable` where most systems have nothing to dispose is a
  fact about Vixen's ECS, not a defect in the rule, and 157 baseline entries is what the baseline is
  for. Vixen's own tests already write `using var world = new World();` on the line above.
- **`SK3503`, 36 findings.** 15 are on types whose `DisposeAsync` does real work — file streams, two
  `NamedPipeServerStream`s, two `CancellationTokenRegistration`s (whose `Dispose` blocks on a running
  callback and whose `DisposeAsync` does not, which is the case the rule exists for). 21 are
  `MemoryStream`, where `Stream.DisposeAsync` calls `Dispose` and the asynchronous path buys a state
  machine and nothing else. Correct, and a baseline entry.

⚠ **Every fix was applied and re-bound, on both trees and in the harness.** Over Vixen, 236 fixes
across 74 files: **128 833 compiler errors before, 128 821 after, 0 `(file, id)` pairs worse than
before**. Over `corpus/real`, 172 fixes across 66 files, 15 743 → 15 731, same zero. And the harness
now asks the question the audit cannot: `EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic` applies a
fixture's own fix, re-binds it, and fails if the rule still fires or if a diagnostic appeared that
was not there. ⚠ It found one in a rule that had shipped a milestone earlier — `SK2015`'s
`throw ex;` → `throw;` left the catch variable unused, which is `CS0168` from a rule carrying
`fixIsSafe: true`, and a broken build wherever `TreatWarningsAsErrors` is on.

⚠ **The Vixen numbers need a global-usings stand-in and it is still not committed.** Vixen builds
with `<ImplicitUsings>enable</…>` and a loose compilation has none, so without one the tree has
195 253 errors and every semantic rule answers "no finding" for the wrong reason. With the SDK's
default implicit-using set as a stand-in the tree falls to **128 833** — reproducing M7's figure
exactly rather than M8's re-derived 128 490, so the two stand-ins differed and neither is in the
repository. Every M9 count above was measured with one, and the next milestone will build a third.

### ⚠ What M8 added: five rules out of nine, and the corpus that is the only real evidence

M8 is the `SK5xxx` milestone and the last one, because a wrong security rule is worse than an absent
one. Nine ids, **five** ship, all at `error` — the range's default, unchanged, because a security
rule's severity comes from what it means rather than from how much it fires.

| Id | Scope | Fix | Fixtures (+/−) | corpus/vulnerable | corpus/safe | `corpus/real` (380) | Vixen (4 717) |
|---|---|---|---:|---:|---:|---:|---:|
| `SK5001` request data concatenated into SQL | Semantic | ⚠ none | 4 / 10 | 6 | **0** | 0 | 0 |
| `SK5002` request data reaches a process start | Semantic | ⚠ none | 3 / 7 | 4 | **0** | 0 | 0 |
| `SK5005` broken cipher, or ECB | Semantic | ⚠ none | 4 / 9 | 6 | **0** | 0 | 0 |
| `SK5007` certificate callback that accepts everything | Semantic | ⚠ none | 3 / 7 | 5 | **0** | 0 | 0 |
| `SK5009` XML reader that parses a DTD and resolves it | Semantic | ⚠ none | 3 / 7 | 2 | **0** | 0 | 0 |

⚠ **The reference trees measure nothing here, and that is the whole reason doc 08 asked for a
separate corpus.** Both zeros above are real and both are uninformative: `Testing/corpus/real` is a
logging library, a JSON serialiser and a sample of a game engine, and Vixen is a game engine. None
of them contains SQL reaching a request, a disabled certificate callback, a broken cipher or an XXE.
The zeros were also verified *symbol-independently*, because the audit runs under a loose
compilation with 195 724 errors and a zero from unresolved symbols looks exactly like a zero from a
correct rule. Since the taint rules are intra-procedural, a source and a sink must occur in the same
method and therefore the same file — so a file-level containment count is a sound upper bound:

| | Vixen | corpus/real |
|---|---:|---:|
| files with a SQL sink token | 1 | 0 |
| …of those, also naming a declared source type | **0** | 0 |
| files with a process sink token | 16 | 2 |
| …of those, also naming a declared source type | **0*** | 0 |
| files naming `DES`/`TripleDES`/`RC2`/`CipherMode` | **0** | **0** |
| files naming any certificate callback or `SslPolicyErrors` | **0** | **0** |
| files naming `DtdProcessing` | 1 | 0 |
| …of those, also naming `XmlResolver` | **0** | 0 |

\* Two Vixen files matched the source grep and both are artefacts of it: the token was
`HttpRequestException` inside a `catch` filter, not `HttpRequest`, and neither file calls
`Process.Start` at all — both mentions are in doc comments. Read by hand; the upper bound is zero.

⚠ **`corpus/safe` is the number that decided whether these rules ship, and it is a hard 100 %.**
Fourteen files live in `Rules/Rikarin.Skala.Rules.Tests/corpus/`, kept away from
`Testing/corpus` so hand-written inputs can never move the fidelity measurement. Every file in the
safe half is the *same shape* as its twin in the vulnerable half — the same request read, the same
`StringBuilder`, the same loop, the same callback, the same `XmlReaderSettings` — with the
vulnerability removed the way a reviewer would remove it: a bound parameter, an `ArgumentList`, a
parsed integer, an allow-list `switch`, a pinned thumbprint, a null resolver. A rule that were really
a keyword search would pass the vulnerable half and fail here.

⚠ **The vulnerable half found three misses that two readings of the code did not**, and all three
were in the common path rather than at an edge:

- `HttpClientHandler.DangerousAcceptAnyServerCertificateValidator` is a **property**, and `SK5007`
  matched only `IFieldReferenceOperation` — so the single most explicit way of writing the finding
  was invisible.
- `builder.Append(a).Append(b)`: the second call's receiver is the *result* of the first, so taint
  arriving through `b` had nowhere to land. Chaining is how most `StringBuilder` code is written.
- Roslyn's control-flow graph lowers `foreach (var x in xs)` to `e = xs.GetEnumerator()` typed
  `IEnumerator` with `Current` typed `object` — so every request read inside a loop lost its taint at
  the top of the loop.

⚠ **Four rules were cut, and none of the reasons is a finding count.** A rule that fires often is
work for the repository it fires on; only a rule that is *wrong* is a defect.

- **`SK5003`** path built from user input without containment. Its sanitizer is almost always a
  helper (`EnsureInside(root, path)`), and recognising a sanitizer that lives in another method is
  precisely the inter-procedural analysis this document puts out of scope. The asymmetry is fatal in
  the wrong direction: a taint rule that cannot see the *source* stays silent, but one that cannot
  see the *sanitizer* fires. `SK5001` and `SK5002` are safe from this because their sanitizers are
  inline by nature — parameterisation, `int.Parse`, an allow-list `switch`.
- **`SK5004`** deserialization with a polymorphic serializer. `BinaryFormatter` is `SYSLIB0011`, an
  error on modern SDKs, and the type throws at runtime on .NET 9+ — the rule would be a second copy
  of a compiler diagnostic, the cut M6 made for `SK3006`/`CS1998` and M7 for `SK8003`/xUnit1001. The
  `TypeNameHandling` half needs taint to be a vulnerability at all and is `CA2326` otherwise.
- **`SK5006`** hardcoded credential "by shape and entropy". Entropy does not separate a credential
  from a GUID, a base64 asset, a test vector or a hash constant: a threshold that admits real secrets
  admits all of those, and one that excludes them excludes short real passwords. There is no
  threshold to choose, so the rule as specified cannot be implemented correctly.
- **`SK5008`** `Random` used for a token or key. "Is this identifier a token" is an identifier-name
  judgement, and `key` is also every dictionary, sort, cache and frame key ever written. ⚠ The
  narrower question *can* be decided — `Random` output reaching a cipher key or IV is unambiguous and
  the engine M8 built would answer it — but that is a different concept from the one this id names,
  and ADR-012 says a narrower concept takes a new number rather than reusing an allocated one. It is
  the obvious next allocation.

⚠ **`SK5005` was allocated narrower than its own sentence in this document**, for the same kind of
reason. The catalogue wrote "weak hash/cipher (`MD5`, `SHA1`, `DES`, ECB)" and only the cipher half
shipped, as `weak-cipher-algorithm`. `MD5` and `SHA-1` have a large population in which they are not
security controls at all — cache keys, ETags, content addresses, and every wire protocol that froze
its digest a decade ago and specifies it normatively. An RFC 6455 WebSocket handshake is *defined* as
a SHA-1 of the client key and a fixed GUID. Reporting it would assert a vulnerability in code that
cannot have one. Separating that population from a password digest needs to know what the digest is
compared against, which is a question about the value's use that this rule does not ask. The hash
half needs its own id, and an argument about how it will decide the question, before it is built.

⚠ **`SK5009` needs two facts where this document named one, and the platform is why.** On .NET
Framework `DtdProcessing = DtdProcessing.Parse` was enough, because the default `XmlResolver` was an
`XmlUrlResolver`. On .NET Core and later the default resolver is `null`, so parsing a DTD resolves
nothing external and is not XXE — a rule firing on `DtdProcessing.Parse` alone would report, at
`error`, every program that legitimately reads documents with entity declarations. So the rule fires
only where the resolver is put back *and* the DTD is parsed.

⚠ **All five ship with `hasFix: false`, which is this range's "rarely" column rather than a gap.**
Parameterising a query means changing the text, adding a binding and choosing the parameter's type.
Splitting an `Arguments` string into an `ArgumentList` means parsing a command line — the exact
parsing the rule exists to avoid guessing at. Swapping `DES` for `Aes` compiles and makes every
value already encrypted unreadable: that is a data migration, not an edit. Each message instead
carries a concrete "do this instead", and for the taint rules that text lives in `taint.json` beside
the sink it belongs to, so a new sink arrives with its own advice.

⚠ **Cost, from `skala check --profile` — which doc 13 promised and nothing had implemented.**
`logAnalyzerExecutionTime: true` had been set on every run since M5 and nothing read it. Skala
checking itself through its own binlog: cold, 2 249 ms of analyzer time in a 9 783 ms run, of which
the five security rules are 297 ms (13.2 % of analysis, 3.0 % of wall); warm, after one file
changed, 312 ms of analyzer time in a 7 684 ms run, of which the five are **26.7 ms** (8.6 % of
analysis, 0.35 % of wall). `MetricsAnalyzer` was 77 % of analysis before M8 and still is. The warm
path is unaffected because the per-file cache means the taint rules see only the files that changed.

### ⚠ What M7 added: three rules out of twenty-three, and one of them has no fix

M7 is the `SK4xxx`/`SK6xxx`/`SK8xxx` milestone. Those three ranges list twenty-three ids and **three**
ship. The bar did the same job it did in M5 and M6, and this time it was not the false-positive
clause that bit — it was the reference trees having almost nothing of the shape.

| Id | Scope | Default | Fix | Fixtures (+/−) | `corpus/real` (380 files) | Vixen (4 681 files) |
|---|---|---|---|---:|---:|---:|
| `SK4010` a `Where` the next operator could have taken | Semantic | suggestion | safe | 4 / 10 | 0 | 0 |
| `SK6003` abstract type with a public constructor | Syntax | suggestion | safe | 3 / 9 | **1** | 0 |
| `SK8005` `Thread.Sleep` in a test | Semantic | suggestion | ⚠ none | 3 / 8 | 0 | **25** |

⚠ **`SK8005` is the one with corpus mass, and all twenty-five findings were read.** None is false —
every one is a `Thread.Sleep` lexically inside a method carrying `[Fact]`. Sorted by what a reader
would do about them: fourteen are a back-off inside a `while (… && elapsed < patience)` loop, where
the sleep is the polling interval rather than the wait and the deadline is already generous; eight
are tests where advancing a real clock *is* the subject — `Wall_time_passing_does_not_advance_the_script`,
a frame limiter fed a 50 ms hitch, a runaway-guard watchdog whose case has to be slow, a profiler
that has to have a duration to record; and three are the shape the rule exists for, a bare sleep
with no deadline followed straight by an assertion. A rule whose true findings are 88 % "true and
not what you would change" is exactly doc 16 § R3's distinction, and it is why the rule ships at
`suggestion` rather than at the `warning` this range's row in the table above defaults to: it never
fails a gate, and a repository that wants it to bite promotes it in the `[**/*.Tests/**/*.cs]`
section its `.editorconfig` already has.

⚠ **`SK8005` ships with `hasFix: false`, and that is not a gap.** The replacement for a sleep is a
change to what the test synchronises on — a handle, a task, a polled predicate with a generous
timeout — and every one of those is a different program. The range's row promises "sometimes" rather
than always for a reason, and a fix that guessed here would be the tool breaking tests on its own
advice.

⚠ **`SK4010` fires zero times on both trees and its zero is worth reading rather than hiding.** Four
`Where(…).<terminal>()` chains exist in Vixen. Two have a `Distinct()` or a `Select()` between the
two calls, one is a `Count()` *inside* the predicate rather than after it — and the fourth,
`typeArguments.Where((t, i) => …).Any()`, is the indexed `Func<T, int, bool>` overload the rule
explicitly refuses because no consumer has a counterpart for it. So the zero is not the machinery
missing: it is three shapes the rule correctly reads as different and one guard firing on the only
candidate there was.

⚠ **The fixes were verified against a tree that compiles rather than against the corpus.** The audit's
usual instrument compares compiler-error counts before and after over `corpus/real`, where the
baseline is 13 221 errors and a fix that broke something could hide in the noise. All ten positive
fixtures were instead compiled together as one clean tree: **0 compiler errors before applying every
`SK4010` and `SK6003` fix, 0 after.**

⚠ **Twenty of the twenty-three were cut, and the reasons fall into three kinds.**

- **A framework analyzer already says it.** `SK8003` (`[Fact]` with parameters) is xUnit1001 and
  `SK8004` (`async void` test) is xUnit1049, both on by default in any project that references
  `xunit.analyzers`. This is the same cut M6 made for `SK3006` against `CS1998`: a rule whose whole
  content is a second copy of a warning the user already sees is noise with a rule id. Neither
  occurs anywhere in either tree in any case — Vixen contains no `async void` at all.
- **The fix is not safe, or there is no fix and no measurement either.** `SK4005` (`string +=` in a
  loop) needs a `StringBuilder` introduced before the loop and read after it, which is a dataflow
  proof, not an edit. `SK6006` (`enum` without a zero) inserts a member into a public API.
  `SK6007` (`struct` without `IEquatable<T>`) generates an implementation. `SK6002`, `SK6005` and
  `SK8001` have no mechanical fix and a large false-positive surface each — `SK8001`'s worst, since
  an assertion inside a helper is indistinguishable from no assertion without following the call.
- ⚠ **`SK8002` was cut by its own measurement, and this is the useful one.** `Assert.True(x == y)`
  looks like the easiest rule in the range, so it was measured before it was written. Vixen has
  **12 396** `Assert.True`/`False`/`IsTrue`/`IsFalse` calls. **3 401** of them pass a second argument
  — a custom failure message, which xUnit's `Assert.Equal` has no overload for at all — so rewriting
  any of those *deletes* the thing the author added on purpose, and the rule has to require the
  single-argument form. That leaves **90** whose single argument's top-level operator is `==` or
  `!=`, and every one of the ninety is a case the rewrite must not touch:
  **83** are `(flags & Member) != 0` over an `[Flags]` enum, where `Assert.NotEqual(0, flags & Member)`
  does not compile — the `0` was an implicit constant conversion to the enum and the rewrite drops
  it, so `T` cannot be inferred; **three** are `a == b || c == d`, whose top-level operator is `||`;
  **three** are `Vixen.Core.Mathematics.Tests/ConventionTests`, which asserts the behaviour of
  `operator ==` on floats and vectors, and `Assert.Equal` calls `Equals` — a *different predicate*,
  which is the whole subject of those three tests; and **one** compares a struct with a user-defined
  `==`. `corpus/real` agrees at its own scale: 620 calls, 419 single-argument, 2 with a top-level
  equality, and both of those are flag masks. So the honest form of the rule fires **zero** times on
  a tree with twelve thousand candidates, and every version of it that fires is a version that
  breaks the build or changes what a test asserts.

### ⚠ What M6 added: four rules, seven metrics and duplication

M6 is the `SK2xxx`/`SK3xxx` milestone, and it ships **four** analyzers out of the twenty-nine those
two ranges list. The same bar did the same job it did in M5, and the reasoning is below the table.

| Id | Scope | Default | Fixtures (+/−) | `corpus/real` | Vixen (4 660 files) |
|---|---|---|---:|---:|---:|
| `SK2013` exception constructed, not thrown | Semantic | warning | 3 / 6 | 0 | 0 |
| `SK2015` `throw ex;` resets the stack trace | Syntax | warning | 3 / 7 | 0 | 0 |
| `SK3001` `async void` outside an event handler | Compilation | ⚠ **none** | 3 / 8 | 0 | 0 |
| `SK3002` blocking on an async call | Semantic | warning | 4 / 9 | 0 | **7** |
| `SK7001`–`SK7006`, `SK7010` metrics | Syntax/Semantic | hint … none | 14 / 25 | — | 85 / 768 / 5 / 13 / 197 / 2 / — |
| `SK7020` duplicated block | Compilation | warning | — | — | **514 groups, 4.8 %** |

⚠ **Only `SK3002` has corpus evidence, and this is the honest part.** Its seven Vixen findings were
each read: one is a deliberately synchronous public API whose own doc comment argues for blocking,
and six are the same shape — a test helper draining a child process's `stdout`/`stderr` with
`ReadToEndAsync()` and then `GetAwaiter().GetResult()` after `WaitForExit()`, which is a documented
deadlock-avoidance pattern. All seven are *true*; none is something anyone would change, which is
what a baseline is for. Six of the seven are in `*.Tests` projects, where the `.editorconfig`
mechanism this document already describes is the right tool rather than a guard inside the rule.

⚠ **`SK2013` and `SK2015` fire zero times on both reference trees, and the zero is real rather than
absent machinery.** `SK2015` is purely syntactic, so it cannot be silenced by an unresolved symbol:
a grep finds exactly two `throw <identifier>;` statements in Vixen and one in `corpus/real`, and all
three are throwing a captured local from outside any `catch` clause — which the rule correctly does
not report. They are cheap rules with no cache cost, so shipping them enabled costs nothing; their
evidence is their fixtures, and doc 16 § R3's distinction applies to them exactly as it does to
`SK1030` and `SK1035`.

⚠ **`SK3001` ships disabled, and the reason is the incremental cache rather than noise.** Deciding
whether an `async void` method is an event handler needs the whole compilation — the `+=` may be
anywhere — which makes the rule `Compilation`-scoped and therefore never servable from the per-file
cache. Enabling it costs every run the warm path that doc 07 § "The incremental cache" promises,
and it buys nothing measurable: Vixen contains **no `async void` method at all**. So it is correct,
it is tested, and a repository that has `async void` turns it on knowing what it costs. M6 also fixed
the guard that made this coherent — `IncrementalAnalysis` now asks whether a compilation-scoped rule
is *enabled*, not merely supported, so a rule nobody turned on can no longer disable the cache.

⚠ **Two of the twenty-nine were cut outright rather than deferred.** `SK3006` (`async` with no
`await`) duplicates the compiler's own `CS1998`, which is on by default in every C# project, and a
rule whose whole content is a second copy of a warning the user already sees is noise with a rule id.
The rest of `SK2xxx`/`SK3xxx` need either a fix that is a refactor (`SK3004` threading a
`CancellationToken` through call sites) or a guard that is most of the rule (`SK3501`'s disposal
paths, `SK3008`'s lock-across-await, which in C# is a compile error in the syntactic case and a
`SemaphoreSlim` dataflow problem in the real one).

⚠ **The metrics are a different kind of rule and the bar reads differently for them.** A metric has
no false positives in doc 16 § R3's sense: it reports a measurement against a threshold, and the
measurement is either right or a bug. What it can be is *useless* — a threshold low enough to fire on
ordinary code teaches people to switch the category off, which is the same outcome by another route.
So the defaults sit well above the corpus's p99 rather than at the textbook number, all but `SK7002`
ship at `hint`, and `SK7010` ships at `none` because enabling it on a repository that has never
documented anything produces 1 868 findings on `Testing/corpus` alone. Cognitive complexity is pinned
to SonarSource's published worked examples so that a number here is comparable to SonarQube's on the
same code.

### ⚠ What M5 actually shipped, and why it is nine ids rather than forty

Seventeen ids are allocated at the end of M5; **six are analyzers**, three are the formatter's own
findings, and eight are tool diagnostics. (M6 takes the total to twenty-nine — see the section above.)

| Id | Scope | Loose mode | Floor | Fixtures (+/−) | corpus/real | Vixen |
|---|---|---|---:|---:|---:|---:|
| `SK0001` the file is not formatted | Syntax | ✅ | — | — | 301 files | not measured¹ |
| `SK0002` unbreakable long line | Syntax | ✅ | — | — | hint | hint |
| `SK0003` malformed xmldoc | Syntax | ✅ | — | — | hint | hint |
| `SK1005` file-scoped namespace | Syntax | ✅ | 10 | 3 / 7 | 27 | 0 |
| `SK1010` `is null` / `is not null` | Semantic | — | 9 | 5 / 7 | 114 | 12 |
| `SK1020` `ArgumentNullException.ThrowIfNull` | Semantic | — | — | 3 / 6 | 2 | 0 |
| `SK1030` `??=` | Syntax | ✅ | 8 | 4 / 6 | 0 | 0 |
| `SK1034` `Count` over `Count()`/`Any()` | Semantic | — | — | 4 / 6 | 0 | 0 |
| `SK1035` `Enum.GetValues<T>()` | Semantic | — | — | 2 / 4 | 0 | 0 |

¹ `SK0001` over Vixen is the M3 formatting diff, which doc 15 § M3 records as deliberately deferred.
The 301 on `corpus/real/` is not a defect count: those files are *inputs*, vendored unformatted on
purpose, and the rule is reporting exactly that.

The catalogue above lists thirty-six `SK1xxx` ids. Nine shipping is the shipping bar doing its job
rather than the milestone falling short: **a rule ships when it has a fix, zero false positives on
the reference corpus, and a "should not fire" fixture set at least as large as the positive one**, and
most of the thirty-six do not survive the third clause without more work than M5 had. The ones that
were cut and why is the useful part:

- ⚠ **`SK1001` (collection expressions), `SK1002` (primary constructors), `SK1008` (records)** rewrite
  a declaration's shape. Every one of them is a *good* rule and none of them is a *safe* fix, so each
  needs the unsafe-fix path and an `--include` story that M5's `fix --safe` deliberately does not
  give it.
- ⚠ **`SK1015` (`is T t`), `SK1006` (`using` declaration), `SK1012` (switch expression)** change how
  many times something is evaluated or where a `Dispose` happens. The guard that makes each of them
  provably behaviour-preserving is most of the rule.
- ⚠ **`SK1022`, `SK1025`, `SK1027`, `SK1032`** are the hot-path rules the catalogue already marks
  `hint`. They need the path-scoped configuration to be worth anything, and shipping them silent is
  shipping nothing.

Two of the six that did ship — `SK1030` and `SK1035` — fire **zero** times on both reference trees.
Their evidence is their fixtures and nothing else, which is worth stating rather than hiding behind a
count: a rule with no corpus occurrences has a false-positive rate that is measured at zero and
tested at nothing.

## Documentation

`docs/rules/SK1002.md` is generated from `rules.json` and contains the summary, the rationale, the
bad/good examples, the known false positives, the configuration keys, and the related rules in other
tools. `skala explain SK1002` prints it. The website, if there ever is one, renders the same files.
One source, three surfaces — the same rule the option registry follows.
