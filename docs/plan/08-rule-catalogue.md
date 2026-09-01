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

## Arrangement

These are the structural-cleanup findings emitted by `verify` from the same fixed-point pipeline as
`arrange --check`. A finding tells the caller which cleanup rules contributed to the file diff; run
`skala arrange` to apply that diff. It deliberately has no generic `skala fix` edit because several
overlapping arrangement rules may contribute to one document rewrite.

| ID | Rule | Scope |
|---|---|---|
| `SK0201` | Block body versus expression body | Syntax |
| `SK0202` | `var` versus an explicit local type | Semantic |
| `SK0203` | Target-typed versus explicit object creation | Semantic |
| `SK0204` | `default` versus `default(T)` | Semantic |
| `SK0205` | Null-checking pattern | Semantic |
| `SK0206` | Empty literal versus `string.Empty` | Semantic |
| `SK0207` | Instance-member `this.` qualifier | Semantic |
| `SK0208` | Redundant control-statement braces | Syntax |
| `SK0209` | Redundant parentheses (`arrange --aggressive`) | Syntax |
| `SK0210` | Using sorting, placement and removal | Syntax, with semantic removal when loaded |
| `SK0211` | Predefined keyword versus framework type name | Semantic |
| `SK0212` | Redundant accessibility modifier | Syntax |
| `SK0213` | File-scoped versus block-scoped namespace | Syntax |
| `SK0214` | Trailing comma | Syntax |
| `SK0215` | Static-member type qualifier | Semantic |
| `SK0216` | Named versus positional argument | Semantic |
| `SK0217` | Explicit versus implicit discard declaration | Syntax |

⚠ **ID correction before 2.0.** These rules initially used `SK20nn` for `nn` 01 through 17, which is
the correctness range and already contains allocated rules. The mapping is mechanical (`SK20nn` →
`SK02nn`), but an old baseline must not replace those strings blindly: `SK2007`, `SK2013` and
`SK2015` also name live correctness rules. Regenerate arrangement findings with `verify` instead.

### Redundant expressions — the cheap third of the parity gap

⚠ **The prose pass for this block is owed.** These rows are the allocation register doing its one
job — recording that a number is taken — written as a rule lands rather than as a considered section.
They are not arrangement rules: nothing here goes through the `arrange` fixed point, and each is an
ordinary `DiagnosticAnalyzer` in `Rules/Rikarin.Skala.Rules/Cleanup/` under a new `Cleanup` category.
They sit in the `SK02xx` band because doc 17 § "Inspection ids are not concepts" puts ReSharper's
whole *Redundancies in Code* family here, and because `SK0209` — redundant *expression* parentheses,
governed by `resharper_parentheses_redundancy_style` — was already here to be double-counted against.

⚠ **Each row is one concept covering several ReSharper inspections, and none of them covers all of
its family.** The count in the last column is inspections *retired*, not inspections listed on the
issue; what was declined and why is in each rule's `falsePositives` in `rules.json`.

| ID | Rule | Scope | Issue | Inspections retired |
|---|---|---|---|---|
| `SK0230` | The `with` expression or object initializer is empty | Syntax | [#137](https://github.com/Rikarin/SKALA/issues/137) | 3 of 3 |
| `SK0231` | The string call produces the string it was given | Semantic | [#132](https://github.com/Rikarin/SKALA/issues/132) | 5 of 8 |
| `SK0232` | The argument or signature element is redundant | Semantic | [#134](https://github.com/Rikarin/SKALA/issues/134) | 3 of 7 |
| `SK0233` | The syntax is redundant | Syntax | [#133](https://github.com/Rikarin/SKALA/issues/133) | 9 of 13 |
| `SK0234` | The cast or type argument is redundant | Semantic | [#128](https://github.com/Rikarin/SKALA/issues/128) | 4 of 8 |
### Cleanup — `SK0240`–`SK0249`

⚠ **The prose pass on this block is owed.** It is written rule by rule as each one lands, so it
records what shipped and what was left rather than reading as a considered section; the section
above is what it should eventually look like.

These are *not* arrangement rules. Arrangement is a formatter pipeline whose findings carry no
generic `skala fix` edit because several rules contribute to one document rewrite; each of these is
an ordinary `DiagnosticAnalyzer` with its own edit list, lives under
`Rules/Rikarin.Skala.Rules/Cleanup/`, and reports under the category `Cleanup`. They are in the
`SK0001`–`SK0999` band because the band is defined by what the finding *is* — a redundancy a reader
has to step over — and not by which component finds it.

⚠ Each id is one Skala concept covering several ReSharper inspections, per
[`docs/plan/17`](17-inspection-parity.md) § "Inspection ids are not concepts". `resharperId` names
the primary inspection only; `supersedes` names the rest.

| ID | Rule | Scope | Fix |
|---|---|---|---|
| `SK0240` | The control flow does nothing | Syntax | safe |
| `SK0241` | The modifier has no effect | Syntax | safe |
| `SK0242` | The `#nullable` directive changes nothing | Syntax | safe |
| `SK0243` | The qualifier is redundant | Semantic | safe |
| `SK0244` | The declaration adds nothing | Syntax | safe |

`SK0240` covers three shapes of [#131](https://github.com/Rikarin/SKALA/issues/131)'s thirteen: a
`continue;` ending a loop body or a `return;` ending a void body (`RedundantJumpStatement`), a
`default:` section whose only statement is `break;` (`RedundantEmptySwitchSection`), and — the
member the issue calls the valuable one — a `catch` whose body is exactly `throw;`
(`RedundantCatchClause`). Only the *last* `catch` of a `try` is reported: deleting an earlier one
changes which handler an exception reaches, which is a behaviour change wearing a redundancy's
clothes. The eight remaining inspections in that issue are boolean-expression shapes
(`RedundantBoolCompare`, `DoubleNegationOperator` and their siblings) and are **not** covered —
each needs an operand's type before it can be called redundant, which would make the rule semantic
and stop it running on a loose file.

`SK0241` covers five of [#129](https://github.com/Rikarin/SKALA/issues/129)'s eleven: `abstract` on an
interface member, `sealed` on a member of a `sealed` type, `class` after `record`, `: int` on an enum,
and `readonly` on a member of a `readonly struct`. ⚠ `static` withdraws the interface half — a static
interface member is *not* implicitly abstract, and `static abstract` is C# 11's abstract static member,
so deleting the keyword there produces a declaration that no longer compiles. The two high-volume
inspections in that issue, `PartialTypeWithSinglePart` and `PartialMethodWithSinglePart`, are **not**
covered: both need to count a symbol's declarations across the whole compilation, and a source
generator can add a part that a loose load has never seen.

`SK0242` is deliberately **narrower than [#135](https://github.com/Rikarin/SKALA/issues/135)'s title**
and its own title says so: it covers the two directive inspections
(`RedundantNullableDirective`, `UnusedNullableDirective`) and none of the four annotation-on-a-constraint
ones, which need to know what a constraint's type argument is. ADR-012 forbids an id's meaning
widening later, so the annotation half is a separate id when somebody specifies it. The rule models the
file's nullable state as two settings — annotations and warnings — each enabled, disabled or
*inherited*, and reports a directive that moves neither. ⚠ The opening state is inherited rather than
enabled: the first `#nullable enable` is therefore never reported, and a `#nullable restore` before any
other directive always is. Comparing against the project's own `NullableContextOptions` is not done,
because in `--load=loose` that value is the loader's rather than the project's, and a rule whose
findings depend on how a file was loaded is not one finding.

`SK0243` is the only semantic rule in this block and covers two of
[#136](https://github.com/Rikarin/SKALA/issues/136)'s four: a qualified type name whose simple name
binds to the same symbol at the same position, and a `base.` that reaches the same member as no
qualifier. ⚠ The `base.` half is **not** "the containing type does not override it": given
`class A { public virtual void M() { } }`, a `class B : A` calling `base.M()` and a `class C : B` that
overrides `M`, dropping the qualifier in `B` turns a non-virtual call to `A.M` into a virtual one that
reaches `C.M`. The member must be one nothing can override further, or the containing type must be
`sealed`.

⚠ **`SK0210` has a measured gap, and it is a bug report rather than a rule.** `UnusedImportClause` is
`SK0210`'s remit and it ships; `RedundantUsingDirective.Global` is `SK0210`'s remit and it does not
fire. `UsingsRule.Unused` builds its removal set from Roslyn's **CS8019** alone
(`Formatting/Rikarin.Skala.Formatting.CSharp/Arrangement/UsingsRule.cs`), and a file-level `using X;`
duplicated by a `global using X;` is reported by the compiler as **CS8933** — measured on a probe
project, where the using is genuinely redundant and CS8019 is silent. Nothing in `SK0243` claims that
inspection: shipping it here would be a second id for one concept.

`SK0244` covers six of [#130](https://github.com/Rikarin/SKALA/issues/130)'s fourteen: an empty
finalizer, an empty sole constructor, an empty namespace, a `: base()` with no arguments, a member
initialized to the value it already holds, and an `override` whose body is a call to the member it
overrides.

⚠ **`EmptyDestructor` stays here at `warning` rather than moving to the performance range** — the
decision the issue asked for. An empty finalizer costs every instance a second GC generation, and that
is real; but the finding and the edit are identical to the other five, so splitting it out would have
made one concept two ids for the sake of a severity. The cost is carried in the message and the
rationale, where a reader sees it. ⚠ A `static` constructor is the mirror image and is never reported:
declaring one, even empty, clears `beforefieldinit`, so deleting it is a timing change.

⚠ `RedundantTypeDeclarationBody` is **declined rather than outstanding**, and the reason is a conflict
inside this catalogue: it asks for `class Foo { }` to become `class Foo;`, and `SK6023` reports those
same braces as an unfinished declaration. Two shipped rules disagreeing about one span is worse than
neither covering it.

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

**`SK1040`–`SK1044` are the cheap end of the band, and shipping them first was the point.**
[17](17-inspection-parity.md) § "A large share of the 580 is cheap" argues that this catalogue is
weighted towards semantically hard rules and under-represents the mechanical ones — the family that
is syntactic, safely fixable and has near-zero false-positive surface. These five are that argument
executed: every one carries a safe fix, and together they cost a fraction of what any single rule in
the `SK1002`/`SK1004` group will.

⚠ **`SK1041` inverts the concern that was raised against it, and the inversion is worth recording
because it looks like a counter-example and is not.** C# defines `x op= y` as `x = (T)(x op y)` with
an *explicit* conversion the long form lacks — so `byte b; b = b + 1;` does not compile while
`b += 1` does. The shapes where the rewrite would lose a conversion are shapes the compiler has
already rejected, and the rule never sees them. What the fix must never do is the opposite: unwrap a
cast on the right-hand side, because `long l; l = (int)(l + 1);` truncates to 32 bits and `l += 1`
does not.

⚠ **`SK1043` finds nothing on the reference trees, and the reason is not that the trees are clean.**
They contain two `for (; cond;)` loops, both inside `[GeneratedCode]` types, and every Skala analyzer
declares `GeneratedCodeAnalysisFlags.None`. A zero from a file that was never analysed — the
distinction § "What a corpus zero is worth" below now exists to keep.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1040` | `nullable-short-form` | `Nullable<T>` | `T?` |
| `SK1041` | `compound-assignment` | `x = x + 1` | `x += 1` |
| `SK1042` | `mergeable-if` | `if (a) { if (b) { … } }` | `if (a && b) { … }` |
| `SK1043` | `for-loop-is-while` | `for (; cond;)` | `while (cond)` |
| `SK1044` | `null-or-empty-check` | `x == null \|\| x.Length == 0` | `string.IsNullOrEmpty(x)` |

⚠ **`SK1050`–`SK1054` are registered here and the prose pass is owed.** The rows below exist so the
numbers are taken and readable; the paragraphs that say *why* each one is worth a rule, in the voice
the rest of this section is written in, have not been written yet.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1050` | `pattern-matching-over-test-and-cast` | `var b = x as T; if (b != null)` | `x is T b` |
| `SK1051` | `simplified-pattern` | `x is not not P`, `x is not (> 5)` | `x is P`, `x is <= 5` |
| `SK1052` | `merged-conditional-access` | `x != null ? x.Y : null` | `x?.Y` |
| `SK1053` | `discard-over-unread-local` | `var ignored = M(out var unused);` | `_ = M(out _);` |
| `SK1054` | `inline-out-variable` | `int v; if (M(out v))` | `if (M(out int v))` |

⚠ **`SK1070`–`SK1073` are registered here and the prose pass is owed.** The rows below exist so the
numbers are taken and readable; the paragraphs that say *why* each one is worth a rule, in the voice
the rest of this section is written in, have not been written yet.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1070` | `tuple-deconstruction` | `var a = t.Item1; var b = t.Item2;` | `var (a, b) = t;` |
| `SK1071` | `with-expression-copy` | `new R(x.A, x.B, c)` | `x with { C = c }` |
| `SK1072` | `redundant-spread-element` | `[.. new[] { a, b }, c]` | `[a, b, c]` |
| `SK1073` | `cached-empty-instance` | `new EventArgs()`, `new Guid()` | `EventArgs.Empty`, `Guid.Empty` |

⚠ **"Use an object or collection initializer" was measured and closed as hosted, not built.** It was
the fifth concept of this batch, and `IDE0017` and `IDE0028` ship in the .NET SDK, report exactly that
shape, and were confirmed on SDK 10.0.400 to *decline* the two cases that make the rewrite unsound —
an assignment guarded by an `if`, and a read of the half-built object between the construction and the
assignments. ADR-008 hosts rather than rebuilds, so no id was allocated. Its two sibling inspections,
`ConvertConstructorToMemberInitializers` and `WithExpressionInsteadOfInitializer`, are different
concepts and remain uncovered; the second is the *opposite* direction to `SK1071`.

## SK2000 — Correctness

Where the tool replaces the part of SonarQube people actually care about. Selected for *findings per
false positive*, not coverage:

`SK2001` comparison always true/false by nullability or range · `SK2002` result of a pure method
discarded · `SK2003` `==` on floating point · `SK2004` `GetHashCode` inconsistent with `Equals` ·
`SK2005` mutation lost through a readonly struct field · `SK2006` `ref`/`out` parameter never assigned on a
path · `SK2007` collection modified during enumeration (syntactic patterns only) · `SK2008` shadowed
loop variable captured in a closure · `SK2009` `switch` over an enum missing members with no
`default` · `SK2010` `string.Compare`/`ToLower` culture-sensitive by accident · `SK2011` `Equals` on
a value type without an override, boxing · `SK2012` self-assignment, self-comparison ·
`SK2013` exception constructed but not thrown · `SK2014` `catch` that swallows without logging or
rethrow · `SK2015` `throw ex` losing the stack trace · `SK2016` interpolated string in a logger call
that takes a template (the `CA2254` case, which the export sets to `suggestion`) · `SK2017` an
`ArgumentException`-family `paramName` literal naming no parameter in scope.

⚠ `SK2017` is outside the `SK2001`–`SK2016` block only in the sense that it was allocated later; it
is the next free number in the band and not the nearest tidy one. It is **not** `SK2006`, which
§ "Cut, with the reason" disposed of and which ADR-012 keeps taken for ever whether or not it ever
shipped.

**These five are decidable from a small amount of local information** — one invocation, one
accessor body, one declaration — with a tight false-positive surface and no dataflow between them.
That is what made them a batch. ⚠ The gap below `SK2030` is a **reservation**, held while a parallel
batch was in flight so that two agents could not take the same number; it is not a decision about
anything, and every number in it is free. It is deliberately not spelled out as a range here — this
document *is* the register, and writing an id down in it is what makes the id look taken.

⚠ **`SK2034` is the one that argues with its own default.** It fires ten times on Skala's own
production source — `@operator`, `@default`, `@using` in `MetricsAnalyzer`, `MemberMetrics`,
`AsyncVoidAnalyzer`, `CheckCommand` and the options generator — and every one is a true positive,
because identifiers named after the syntax nodes they hold are idiomatic when writing Roslyn-shaped
code. It therefore ships at `suggestion` against the export's `warning`, with the divergence recorded
in its `resharperNote`. **Whether the code or the rule should change is deliberately left open**:
contorting a tree to satisfy a rule nobody has argued for is the same error as calibrating a rule to
the tree, which [16](16-risks-and-open-questions.md) § "The reference trees are a test subject"
already names in the other direction.

- `SK2030` `nan-comparison` — `==` or `!=` against a constant `NaN`.
- `SK2031` `unused-value-parameter` — a setter that does work and never reads `value`.
- `SK2032` `redundant-suppress-finalize` — `GC.SuppressFinalize` in a sealed type with no finalizer.
- `SK2033` `stackalloc-in-loop` — a `stackalloc` a loop re-evaluates.
- `SK2034` `escaped-keyword` — a declaration named after a reserved keyword and escaped with `@`.

## SK3000 — Async, concurrency, lifetime

`SK3001` `async void` outside an event handler · `SK3002` blocking on async (`.Result`, `.Wait()`,
`GetAwaiter().GetResult()`) · `SK3003` missing `ConfigureAwait` where the config asks for it
(`resharper_configure_await_analysis_mode` is in the export) · `SK3004` `CancellationToken` accepted
and never passed on · `SK3005` fire-and-forget `Task` with no continuation · `SK3006` `async` method
with no `await` · `SK3007` `Task` returned from a `using` block that disposes what it awaits ·
`SK3008` lock held across an `await` · `SK3009` `Lazy<T>` without a thread-safety mode in shared
state · `SK3501` `IDisposable` created and not disposed on all paths · `SK3502` field of a disposable
type in a type that is not disposable · `SK3503` `IAsyncDisposable` disposed synchronously.

### `SK3510`–`SK3512` — one ownership question, asked three ways

`SK3501` reports a disposable that is never disposed and `SK3502` a type that owns one without
saying so. These three ask the complementary question — *this variable **is** owned by a `using`;
what else happens to it?* — and share a single analysis of what a `using` statement or declaration
owns:

- `SK3510` `using-variable-disposed-again` — an explicit `Dispose()` on a variable `using` already
  disposes. ⚠ Ownership is read off the **declarator**, never off the name: the corpus holds 24
  name-matching shapes and **none** is a true positive, so a name-matching rule would have deleted
  four disposals that nothing else performs.
- `SK3511` `using-resource-object-initializer` — `using var x = new Foo { Bar = Baz() }` does not
  protect the `new Foo`; if the initializer throws, the constructed resource was never assigned to
  the `using` variable and leaks. ⚠ The rule **withdraws when the initializer contains a comment**,
  because the fix rebuilds assignments from expressions and trivia between members belongs to no
  expression — a fix marked *safe* would otherwise have silently deleted a six-line note in the
  reference trees. That guard came from reading the corpus, not from reasoning about it.
- `SK3512` `using-variable-returned` — the plain-value shape of `SK3007`. **Fixless**: returning a
  disposed object is a design error and no edit repairs it.

⚠ **`SK3512` declines `supersedes` deliberately, and the reason generalises.** `Supersession.Apply`
dedupes on a shared `(rule, path, line, column)` and marks the *superseded* finding suppressed — so
declaring `supersedes: ["SK3007"]` would hide the finding that carries the remedy. The two are made
disjoint by construction instead: `SK3512` tests the returned local's type and stays silent on
`Task`/`ValueTask`, which is exactly where `SK3007` fires. `TheTaskShape_IsReportedByExactlyOneOfTheTwoRules`
double-reports when inverted. ⚠ Known gap, stated rather than hidden: where `SK3007` declines because
it cannot rewrite the whole method, neither rule reports.

### `SK3020`, `SK3021` — two independents in the async band

- `SK3020` `null-returned-from-task-method` — a non-`async` `Task` method returning `null`, where
  `await` at the call site is a `NullReferenceException` naming the caller rather than the method.
- `SK3021` `spin-lock-in-readonly-field` — `SpinLock` is a mutable struct, so a `readonly` field
  hands every caller a copy and the lock excludes nothing. ⚠ Its first draft shipped a fix that does
  not compile: deleting `readonly` from an instance field of a `readonly struct` is **CS8340**, which
  forces the keyword. The rule now withdraws there.

⚠ **Both ship on fixture evidence alone.** The reference trees contain none of either shape — not a
clean zero, an absent one.

### Disposal contracts and async shape — the ids this batch allocated

⚠ **The prose pass is owed for this block.** What follows is the register doing the one job ADR-012
needs it to do — the number is taken, and it is written down where the next milestone will read it.
It is not yet the considered account the sections above carry.

- `SK3530` `disposable-field-not-disposed` — the type implements `IDisposable`, constructs a
  disposable field, and nothing in it ever disposes the field. ⚠ **This is the half of the ownership
  question that looks finished**, where `SK3502`'s is the half that looks unfinished: `SK3502`
  reports an owner that is *not* disposable, this one an owner that **is**. The two are disjoint by
  construction — this rule requires `Implements(owner, IDisposable)` and `SK3502` requires its
  negation, so no `supersedes` is involved and neither can suppress the other. ⚠ Disposal is looked
  for across the **whole type**, not in `Dispose`'s body, because the documented pattern puts the
  work in `Dispose(bool)` and reading only the entry point would report every correct implementation
  of it.
- `SK3531` `dispose-async-without-base-call` — an `override` of `DisposeAsync` or `DisposeAsyncCore`
  that never reaches the implementation it replaced, so the base type's flush, graceful close or
  return to a pool stops happening with nothing to say so. `CA1063` pins the synchronous pattern and
  `CA2215` the synchronous base call; neither has an asynchronous counterpart. **Fixless**: the call
  goes last in an override and first in a wrapper, and where it belongs among the override's own
  cleanup is an ordering decision no edit can read. ⚠ **Every finding is provable and the guards are
  what make that true**: the base must be declared in this compilation and its body must invoke
  something. The stated cost is that a framework base in another assembly is not covered at all.
- `SK3532` `ref-struct-owns-undisposed-resource` — ⚠ **the one ownership shape nothing else in this
  family can reach.** A `ref struct` whose only disposal contract is a public parameterless
  `Dispose()` is disposable through the language's pattern rule and through nothing else, so
  `SK3502` — which asks whether the owner implements the contract the field offers — has no contract
  to ask about and is silent by construction. The owner constructs the resource, holds it, and gives
  its callers no `using` to write. ⚠ The field's type must implement **neither** `IDisposable` nor
  `IAsyncDisposable`, which is the disjointness guard rather than a limitation: C# 13 lets a `ref
  struct` implement an interface, and there the ownership is `SK3502`'s to report. **Fixless**: a
  correct `Dispose()` decides which fields it releases and in what order, and the repair may instead
  be that the type should not own the resource.
- `SK3030` `async-iterator-not-enumerated` — a method returning `IAsyncEnumerable<T>` called as a
  whole statement, so the iterator's body never starts. A plain `foreach` over one does not compile,
  which is why the mistake takes this shape instead: it compiles, warns about nothing, and does
  nothing. ⚠ Only where the repair compiles — an `async` enclosing body, a statement in a block, an
  awaitable position — which is the line `SK3503` draws, drawn here in the same place. ⚠ The finding
  is withheld when `_` is already in scope, because the rewrite names the loop variable and
  shadowing is CS0136. The fix is `fixIsSafe: false` against the issue's proposal: it turns a
  statement that does nothing into one that runs the iterator, which is the repair and is still a
  behaviour change somebody should read.
- `SK3031` `async-only-to-await` — the whole body is `return await X()`, so the state machine exists
  to hand back a task the method already had. `suggestion`, and `fixIsSafe: false` because eliding
  the `await` moves exceptions from the returned task to the call and drops the method from the
  stack traces the task carries.

⚠ **`SK3031` and `SK3007` are disjoint by construction, and the construction is the body shape.**
`SK3007` reports a task returned out of a `using` that disposes what it awaits, which is exactly
what `SK3031`'s fix would create — so `supersedes` is the wrong instrument twice over:
`Supersession.Apply` suppresses the *superseded* finding, and here that would hide the report
carrying the remedy. Instead `SK3031` matches only a body that is a **single** statement or a single
expression: a `using` declaration needs a statement before the return, and a `using` statement makes
the block's one statement a `using` rather than a `return`, so no shape it reports can contain one.
`ElidingTheAwaitInsideAUsing_IsTheBugSk3007Reports` pins it from the other end — it applies the edit
by hand to the `using` shape and asserts `SK3007` then fires on the result.

⚠ **`SK4008` is a different concept and stays where it is.** Doc 08 allocated it for "`async` state
machine for a method that always completes synchronously (→ `ValueTask`)", which is a change of
return type; `SK3031` removes a state machine and keeps the signature. Issue #55 read the two as one
and they are not.
### `SK3040`– — the locking band

⚠ **The prose pass for this block is owed.** These entries are the register doing its minimum job —
recording that the number is spent and on what — and not the written-through account the rest of this
document gives. Whoever writes it should read the analyzers, not this list.

- `SK3040` `lock-over-synchronization-primitive` — a `lock` statement taken over a `SemaphoreSlim`, a
  `ReaderWriterLockSlim`, or anything below `WaitHandle`. Every reference type carries a monitor, so
  it compiles; it gives the author a monitor they did not mean, over an object whose own waiting is
  somewhere else. **Fixless**: the repair is to choose which mechanism the code meant, and no edit
  chooses. ⚠ **Not `SK1023`, and disjoint from it by construction** — `SK1023` modernizes a *correct*
  lock over a `readonly object` field, and this rule never fires on `object`.
  ⚠ **`System.Threading.Lock` is excluded explicitly**: it is a synchronization primitive and it is
  also the type a C# 13 `lock` is meant to be taken over, so reporting it would contradict `SK1023`'s
  own fix.
- `SK3041` `non-atomic-volatile-update` — `++`, `--` or a compound assignment on a `volatile` field.
  `volatile` buys visibility and orders nothing else, so the read-modify-write still loses updates;
  the keyword is what makes it worth a warning, because it is the mark of an author who thought about
  threading and concluded wrongly. Withdraws inside a `lock` and inside a constructor — but **not**
  through a lambda written in one. ⚠ **Fixless, against the proposing issue, which asked for a fix.**
  Four obstacles and any one is enough: `Interlocked.Increment(ref f)` on a `volatile` field is
  **CS0420**; `f++` as an expression yields the value *before* and `Interlocked.Increment` the value
  *after*; `^=`, `*=`, `/=` and `<<=` have no interlocked form at all; and the honest repair — drop
  `volatile`, route every access through `Interlocked`/`Volatile` — changes the field's contract and
  touches accesses in other files. The decision is recorded here rather than in a commit message
  because ADR-012 makes the shipped shape permanent.
- `SK3042` `incorrect-double-checked-locking` — a check-lock-check initialization whose field is not
  `volatile`. The read the idiom skips the lock for is the *outer* one, and nothing orders it against
  the constructor's writes, so a second thread can see the reference before the object. ⚠ It works on
  x86/x64 by accident of those processors' ordering and is already wrong on ARM64. **Fixless**: adding
  `volatile` changes the field's contract for every access and is only one of three defensible
  repairs, the others being `Lazy<T>` (→ `SK3009`) and dropping the outer check.
  ⚠ **Two of the three inspections the proposing issue names are deliberately not covered.**
  `PossibleMultipleWriteAccessInDoubleCheckLocking` and `ReadAccessInDoubleCheckLocking` both turn on
  telling `f = new Foo(); f.Init();` apart from `var t = new Foo(); t.Init(); f = t;`, and separating
  either from a harmless `return f;` in the same branch needs a rule about *which* reads publish.
  Stated rather than approximated, and the residue stays counted uncovered in `docs/plan/17`.
- `SK3043` `inconsistent-lock-order` — one type nests the same two lock fields inside each other in
  both orders. The textbook deadlock, and it survives review because the two halves are almost never
  on the same screen. **Fixless**: choosing the hierarchy is the design decision the type failed to
  make, and swapping the order in whichever method the tool saw second is as likely to be the wrong
  half as the right one. ⚠ **The first `scope: Compilation` rule to ship**, and it is not a
  convenience: issue #56's whole argument is that the two halves live in different files, so the unit
  of analysis has to be the symbol. It is excluded from per-file caching for exactly the reason the
  scope exists — its answer for one file depends on files the cache key does not name.
  ⚠ Only a bare identifier and `this.field` are accepted as lock targets, and **that is recall, not
  soundness** — the draft that shipped first said the opposite. A cycle over two fields reached
  through two *different instances* is a real deadlock, the bank-transfer one, and this rule misses
  it; the obstacle is the message, which names fields and not objects, so "`history` while holding
  `balance`" would not say which account. The fixture recording that is filed as a miss.
  ⚠ Its first draft also carried a containing-type guard against a nested type's locks reaching the
  outer type's accumulator. Deleting the guard left the fixture green: Roslyn already scopes a
  symbol-start syntax action to the symbol's own members. The guard was dead code no sabotage could
  kill and is gone; the fixture stays as the pin on the Roslyn behaviour.
- `SK3044` `inconsistently-synchronized-field` — a private field written without the type's one lock
  and accessed under it at least twice elsewhere. The same "which lock is held here" analysis as
  `SK3043`, run over a whole type. **Fixless**: wrapping the statement is one repair and taking the
  lock at the caller is another, and which is right depends on what else the caller does in between.
  ⚠ **Six gates, and the gates are the rule.** Exactly one lock object in the type; no other
  synchronization anywhere in it; the unguarded access must be a *write*; its member must be callable
  from outside and never called by the type from inside the lock; any access inside a lambda
  withdraws the field; and at least two guarded accesses are required. Every one of those buys
  precision at the cost of recall, knowingly — § R3's most expensive false positive is the one that
  sends somebody to read threading code that was correct. ⚠ **Silence is not a claim**: a field this
  rule says nothing about is one it could not decide, not one it believes is synchronized.

⚠ **`SK3040`–`SK3044` all ship fixless, and that is five more than doc 08's bar contemplates.**
Two of the five issues proposed a fix; neither survived contact. Concurrency findings mostly have no
mechanical repair, because the edit that would fix them is a decision about what the type's
contract is — which lock is first, which field is `volatile`, where the boundary of a critical
section falls. The pattern is now established enough to say so once rather than five times.

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

### Performance a declaration states, rather than a body measures

⚠ **The prose pass is owed for this block.** These are recorded here because the register requires
it; the surrounding sections carry an argument and this one carries a list.

Concepts whose whole decision is visible in one declaration and what its body touches, so none of
them needs a profile to justify the edit.

- `SK4020` a lambda, anonymous method or local function that references nothing outside itself and
  is not `static`. `static` is a compile-time assertion that no environment is allocated, so a fix
  that compiles is a fix that is right. Disjoint from `SK4002` by construction: that rule reports a
  capture and this one reports the absence of every capture.
- `SK4021` a `private` instance method whose body never reaches `this`. `static` drops the hidden
  argument and states the independence the body already had. Restricted to `private` because the
  edit has to check every call site, and a visible member's are not all in view.
- `SK4022` a struct that already satisfies `readonly struct` — all instance fields `readonly`, no
  settable instance property, no member writing `this` — and does not say so. The modifier deletes
  the defensive copies that `SK2005` and `SK4007` report the consequences of.
- `SK4023` a `capacity` argument equal to the framework type's own default, on the six types where
  that default is a fixed and known number. The deletion has no runtime effect at all; the value is
  in what the call site stops claiming.
- `SK4024` `GC.Collect` outside measurement code. ⚠ **The one of the five that ships fixless**: the
  call is a symptom of an allocation, a buffer or a handle, and deleting it without dealing with
  that is a memory change nobody measured.
### `SK4030`–`SK4034` — call shapes decided by the receiver's static type

⚠ **The prose pass for this block is owed.** The entries below are the register doing its job —
naming the numbers so they cannot be handed out twice — and not the considered write-up the rest of
this document carries.

`SK4030` the collection's own `Find`/`Exists`/`TrueForAll`/`Contains` where the LINQ extension was
called · `SK4031` a `foreach` over `dict.Keys` that indexes the dictionary with the key it is already
holding · `SK4032` `Substring` allocated only to feed a search that takes a start index ·
`SK4033` the expensive `ConcurrentDictionary` member where a cheap one answers the same question ·
`SK4034` a `Where` that runs after the `OrderBy` it could have run before.

Five ids, one analysis: take the receiver's static type, look at the operator called on it, and ask
whether the type itself already offers the cheaper member. They are grouped here rather than folded
into `SK4001`–`SK4008` for the reason the note above gives about `SK4010`.

⚠ **Three of the batch's five source issues bundled more than one upstream rule, and the ids shipped
are narrower than the issues asked for.** Named here rather than silently dropped:

- `SK4030` takes `S6602`, `S6603`, `S6605` and `S6617` from issue #204 and leaves three. `S6608`
  (index instead of `First()`/`Last()` on an `IList`) changes `InvalidOperationException` into
  `ArgumentOutOfRangeException` on an empty list. `S6609` (`SortedSet.Min`) returns `default(T)` on
  an empty set where `Enumerable.Min()` throws for a value type. `S6613` (`LinkedList.First`) returns
  a `LinkedListNode<T>` rather than a `T`. All three are behaviour changes rather than substitutions.
- `SK4034` takes `S6607` from issue #205 and leaves three. `S6610` (`StartsWith(char)`) replaces a
  culture-aware comparison with an ordinal one, which is a behaviour change wearing a performance
  fix's clothes. `S6612` is a closure rule about `ConcurrentDictionary` factory lambdas and is not a
  LINQ chain at all. `S6618` (`string.Create` over `FormattableString`) is niche.
- `SK4033` leaves `dict.Keys.Contains(k)` alone, and the reason is worth reading: `Keys` hands back a
  plain `List<TKey>` whose `Contains` uses `EqualityComparer<TKey>.Default`, while `ContainsKey` uses
  the comparer the table was *constructed* with. For an `OrdinalIgnoreCase` table the two disagree,
  and nothing at the call site says which kind of table it is.

⚠ `SK4033` declares `supersedes: ["SK1034"]`, and it is the first rule to supersede another Skala
rule rather than a foreign analyzer id. `SK1034` reads `dict.Keys.Count()` and offers
`dict.Keys.Count`, which is correct and still wrong: on a `ConcurrentDictionary` the cost is `.Keys`
taking every lock in the table and materialising a whole new collection, and the answer is
`dict.Count`. Where both fire on the same span the stronger remedy wins and `SK1034` stays in the
report marked superseded, which is what `Supersession.Apply` is for.

#### What the batch measured

Instrument verified first: the twenty-three positive fixtures were compiled as one project outside
the repository and swept with `skala check --load=workspace`, and all twenty-three fired. The same
command was then run over Skala's own tree, which compiles.

| Id | Fixtures (+/−) | Skala's own tree | The zero, or the findings |
|---|---:|---:|---|
| `SK4030` | 6 / 11 | **12** | all twelve read; all twelve true |
| `SK4031` | 4 / 11 | 0 | shape present 4×, correctly declined 4× |
| `SK4032` | 4 / 11 | 0 | shape absent |
| `SK4033` | 5 / 11 | 0 | shape present 3×, correctly declined 3× |
| `SK4034` | 4 / 10 | 0 | shape absent |

⚠ **`SK4031`'s and `SK4033`'s zeros are the kind that is evidence, and one of them is the rule's own
guard firing.** `IntAlign.cs:541` is `foreach (var offset in insertions.Keys.Order())` with
`insertions[offset]` in the body — the loop deliberately wants the keys in *sorted* order, so
iterating the dictionary would reorder it, and the guard that the source be exactly `X.Keys` refuses
it. The other three `SK4031` candidates put `Concat` or `Union` between `Keys` and the loop and read
the value with `TryGetValue`. `SK4033`'s three are `live.Count == loaded.Count` (two counts, not an
emptiness test), `live.Values.ToList()` (the snapshot is what was wanted) and a `Count` returned as a
number.

⚠ **A corpus number for this batch would have been a third kind of zero — the analysis never ran —
and for a reason narrower than issue #277 records.** `skala.jsonc` excludes `Testing/corpus/**` from
analysis outright, so `skala check` over those paths reports `SK9023: no C# files were found` and
exits before any rule is loaded. That is on top of the dependency-closure problem #277 describes.
Counted syntactically, the corpus holds 8 `foreach`-over-`.Keys`, 2 mentions of
`ConcurrentDictionary`, 47 lambda-taking `FirstOrDefault`/`Any`/`All`, and none of either string or
sort shape — so a corpus run would have had something to say about three of the five and could not
have said it.

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

**These four are decidable from a declaration alone**, which is what separates them from the rest
of the band: no dataflow, no call graph, and for three of them no semantic model beyond the declared
symbol. ⚠ **Three of them ship `hasFix: false`, and they are the first fixless rules in the
catalogue** — a possibility this document has repeatedly declined to rule out and never exercised.
Renaming a type is a solution-wide edit that Skala's one-file text-edit model cannot make, and
deleting one is not mechanical: reflection, DI-by-name and serialised payloads all reach a type that
nothing in the tree references.

⚠ **A fifth rule was refused rather than built.** *"A non-constructor method should not share its
type's name"* is already **CS0542** in C#; every shape was compiled and all six are compile errors,
and in the four shapes that remain legal the stated harm cannot occur. **No id was allocated**, so
the band has no gap where it sits — the proposal is closed as refuted, which this document counts as
a result rather than an omission.

⚠ **`SK6020`'s justification is not the one it was proposed with.** `where T : Enum` does *not*
admit `Nullable<TEnum>` — that is CS0312. What `struct` excludes is `System.Enum` itself, which
satisfies the bare constraint by identity: `default(T)` is then `null`, every use boxes, and none of
`Enum.GetValues<T>`/`TryParse<T>`/`IsDefined<T>`/`GetName<T>` can be called, because the BCL declares
all four `where TEnum : struct, Enum`. The refutation is carried in the rule's own `falsePositives`
so that `skala explain` prints it.

⚠ **`SK6022` deliberately drops the `Record` suffix** the proposal named. `LogRecord`, `AuditRecord`,
`DnsRecord` — the domain noun predates the keyword by decades and no syntactic test separates them.
`PersonRecord` goes unreported; that is the price of not reporting `LogRecord`.

- `SK6020` `enum-constraint-without-struct` — `where T : Enum` with no `struct` beside it.
- `SK6021` `exception-name-without-exception-base` — a type named `…Exception` that does not derive
  from one.
- `SK6022` `type-name-restates-its-kind` — a type name that repeats its own kind keyword
  (`OrderClass`, `PointStruct`).
- `SK6023` `empty-type` — a type with no members, no base and no attributes.

⚠ **The prose pass for `SK6040`–`SK6049` is owed.** What follows is the allocation record only, written
so that no number below is handed out twice; the paragraphs that explain the batch the way the rest of
this section is explained have not been written yet.

⚠ **`SK6040` is one ninth of the concept issue #121 named, and the other eight are deliberately not
shipped.** "The local declaration is never used" groups nine ReSharper inspections. Only
`NotAccessedOutParameterVariable` has a repair that cannot change what the program does — the callee
writes the `out` argument either way, so replacing the declaration with `_` removes a name and nothing
else. Deleting an unread ordinary local cannot promise that: `var response = Send();` is unread and
deleting it stops the request. `UnusedLocalFunction` is already **CS8321**, a compiler warning, and is
not re-implemented. The remaining seven — `NotAccessedVariable`, `UnusedLocalFunctionParameter`,
`UnusedLocalFunctionReturnValue`, `UnusedTypeParameter`, `UnusedTupleComponentInReturnValue`,
`UnusedParameterInPartialMethod`, `PrivateFieldCanBeConvertedToLocalVariable` — each need either every
call site or a judgement about intent, and are left in [17](17-inspection-parity.md)'s residue where
they still count.

- `SK6040` `unused-out-variable` — an `out` argument declares a variable nothing reads; write `out _`.

⚠ **`SK6041` needs no safety guard against the body reassigning the loop variable, because C# has
one.** CS1656 forbids assigning to an iteration variable, so a declared type that is only ever read is
the only shape there is, and narrowing it can never invalidate a write. The guard that *is* needed is on
the conversion: only an implicit **reference** or **boxing** widening is reported. An implicit numeric
one (`foreach (long value in ints)`) is an arithmetic width the body depends on, a nullable one says the
loop deals in absence, a user-defined one is somebody's operator, and an **explicit** one —
`foreach (string text in objects)`, which `foreach` uniquely permits — is a downcast written on purpose
and the opposite of this finding.

- `SK6041` `wider-foreach-variable-type` — a `foreach` variable declared as a base type, an interface
  or `object` when the collection already knows the element type.

⚠ **`SK6042`–`SK6049` are unallocated and free.** Issues #114 (a member more accessible than its use
requires), #115 (storage never written after initialization) and #119 (a class with only static
members) were read in the same pass and **no id was taken for any of them**. #114 and #115 are the
family [17](17-inspection-parity.md) § "Two ways a zero can lie" names: ReSharper splits both into
`.Global` and `.Local`, every `.Global` scored zero in the recorded sweep because solution-wide
analysis was off, and Skala analyses one compilation — so only the `.Local` halves are answerable, and
the `.Local` half of #115 is largely `IDE0044` already. The half of #115 that is genuinely uncovered is
the auto-property one (`{ get; private set; }` assigned only in a constructor), and it was declined for
a reason no syntax shows: **Newtonsoft.Json writes private setters by default, with no attribute to
exempt on**, so the finding has a false-positive story the reference trees cannot be used to measure.
#119 is `CA1052`, which ships in the .NET SDK and is off by default — hosting it is the cheaper answer
than reimplementing it, and either way "nothing instantiates this type" stops at the assembly boundary
for a `public` one. ⚠ `catalogued.json` maps `MemberCanBeInternal.Global` to
`SK6002`, which this document allocates to *"public member exposing a mutable array or `List<T>`"* — a
different concept. **That mapping is wrong and should be re-pointed at whichever id #114 eventually
takes**, or dropped; it is left alone here rather than corrected by an agent that is not shipping #114.
⚠ **The prose pass for `SK6030`–`SK6034` is owed.** What follows is the allocation record and the
one-line reason for each; the paragraphs that explain the batch the way `SK6020`'s and `SK6022`'s are
explained above have not been written. Each rule's `falsePositives` in `rules.json` carries the full
argument in the meantime, and `skala explain SK6030` prints it.

**`SK6030`–`SK6034` are declarations that promise something they do not deliver.** A modifier, a
namespace, a keyword or an accessibility that a reader takes as a guarantee and that provides none.
They are decided from a declaration and its members — no dataflow and no call graph — which is what
puts them beside `SK6020`–`SK6023` rather than in the semantic bands.

- `SK6030` `type-in-global-namespace` — a type with no namespace around it at all.
- `SK6031` `readonly-mutable-field` — a non-private `readonly` field holding an array or a mutable
  collection, where the modifier stops reassignment and nothing else.
- `SK6032` `abstract-type-without-abstraction` — an `abstract` class with nothing to override,
  nothing `protected` and no base.
- `SK6033` `only-private-constructors` — a class nothing can construct and nothing can derive from.
- `SK6034` `public-constant-field` — an externally visible `const`, copied into every caller at
  compile time.

⚠ **Four of the five ship `hasFix: false`, and `SK6033` is the one that names why a fix cannot be
partial.** Its static-holder shape has an obvious two-edit repair and its general shape has none, and
`RuleFixtureTests.EveryFix_ProducesTextThatStillParses` asserts that *every* finding of a rule
declaring `hasFix: true` carries edits — so a rule offers a fix for all of its shapes or for none.
`SK6034` is the exception and carries one: `const` → `static readonly` is a single token, and
`fixIsSafe: false` because the breakage it can cause — an attribute argument, a parameter default, a
`case` label — lands in files the edit does not touch.

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

**`SK7070`–`SK7074` extend the justification family `SK7050` and `SK7051` started.** That family is
now four rules — a suppression, a `SuppressMessage`, an obsolescence and a coverage exclusion — and
all four are report-only for the same reason: no edit can write the sentence a human owes the next
reader. ⚠ **`SK7070` and `SK7074` are, with the `SK60xx` batch, the first rules to ship with no fix
at all.**

⚠ **`SK7072` is the first rule to ship a deliberately partial concept, and the part it omits is
named.** Deciding that a suppressed warning *no longer fires* requires the compilation's diagnostics
without the pragma, and an analyzer cannot obtain them: pragma filtering is applied before
`GetDiagnostics` returns, an analyzer cannot enumerate its peers, and re-analysing from inside one is
re-entrant. Only the host could answer, and that is a separate feature. What ships is the sound half
— a `disable` region with no source token in it — with a safe fix. ⚠ Its `resharperId` is left
**null** rather than claiming `RedundantDisableWarningComment`, so the uncovered remainder stays
visible in [17](17-inspection-parity.md)'s residue instead of being hidden by an over-claim of
exactly the kind that document spent a pass removing.

⚠ **`SK7074` states an exclusion rather than merely detecting.** `goto case` and `goto default` never
fire: they are the only way C# expresses fall-through, cannot leave their `switch`, and leave the flow
visible. The exclusion lives in the syntax kinds registered rather than in a filter, and the reference
trees are the argument for it — across all three there is exactly **one** `goto`, and it is a
`goto case`.

`SK7070` `Obsolete` without a message · `SK7071` `ExcludeFromCodeCoverage` without a
`Justification` · `SK7072` a `#pragma warning disable` region with no code in it ·
`SK7073` an empty `#region` · `SK7074` a `goto` to a label (`goto case` and `goto default` are
not reported).

⚠ **The block below is the register entry for `SK7090`–`SK7093` and the prose pass on it is owed.**
It records what shipped and the position each rule takes, written by the agent that built them; it has
not been through the editorial pass the rest of this section has had, and it should be read as notes
that are accurate rather than as finished catalogue prose.

**`SK7090` is `SK7040`'s requirement on the form that compiles.** `SK7040` asks a `TODO` to name the
issue that owns it; `SK7090` asks the same of a thrown `NotImplementedException`, and the two accept
the same vocabulary — a URL, `#123`, a project key such as `SKALA-123` — deliberately character for
character, because two rules asking for "an issue reference" and disagreeing about what one looks like
is a rule nobody can obey. ⚠ This one sits **on** the premise in [00](00-vision-and-principles.md) rather
than beside it: a model asked for an implementation produces a compiling signature with a
`NotImplementedException` body far more readily than it admits it cannot do the work, and that body
type-checks, binds, formats and passes every analyzer in the build. It fails only when it runs, in a
caller that had no way to know.

⚠ **`SK7090` states an exclusion rather than merely detecting.** `NotSupportedException` and
`UnreachableException` never fire. Both are permanent statements about a contract — an operation this
type will never offer, a branch the author asserts is unreachable — and they are what an author writes
when the answer really is "not here". `NotImplementedException` is the one that means "not yet", and
"not yet" is what needs an owner; reporting the other two would make the rule an opinion about
exception types and get it turned off with the part that is worth having. Only a construction that is
*thrown* is reported, which is narrower than Sonar's `S3717`: constructing one to compare against or
to hand to a test helper is not a member that compiles and does not work. Report-only, and this is the
strongest case in the catalogue for a rule with no fix — every mechanical guess available (delete the
member, return `default`, change the exception type) turns a loud failure into a quiet one.

⚠ **`SK7091` refuses the application/library distinction the proposal was written around, and the
refusal is a measurement.** [#236](https://github.com/Rikarin/SKALA/issues/236) is titled "the process
is terminated from library code", and the obvious implementation reads `Compilation.Options.OutputKind`.
That cannot work here: `LooseLoader` constructs its compilation with
`OutputKind.DynamicallyLinkedLibrary`, so *"this compilation is a library"* and *"no project file was
loaded"* are the same observation — and loose is the mode [00](00-vision-and-principles.md) says Skala
exists for, because a folder of generated `.cs` files has no project. An `OutputKind` rule would report
every console application analysed without its project file, which is precisely the false-positive
engine [16](16-risks-and-open-questions.md) § R3 is about.

So the line `SK7091` draws is a different one and it holds under every load mode: **the entry point may
end the process**, because ending it is the process's own decision and there is nothing above that
frame to unwind into; everywhere else — a library, a service, and an executable's own helper class
alike — `Environment.Exit` destroys `finally` blocks, `IDisposable` cleanup and buffered writes that
somebody else wrote and did not choose to abandon. The entry point is the compilation's own where
there is one and a `static Main` by name otherwise, and that fallback is load-mode insurance rather
than laziness: a loose compilation has no entry point to ask for. ⚠ `Environment.FailFast` is
deliberately never reported — skipping cleanup is the whole point of it, so reporting it would be
reporting a decision rather than an accident.

⚠ **`SK7092` ships the provable half of `S2139` and names the half it omits.** A finding requires the
logging call to be handed *the caught exception itself*. Deciding that an arbitrary call "is logging"
is name-matching, and name-matching a bare `logger.LogError("failed")` beside a `throw;` would report
every method in the tree called `Error`; passing the caught exception to something in the logging
vocabulary is not a guess, because nothing else does that. So a `catch` with no exception variable
never fires, and neither does one that logs a message without the exception — under-reporting in the
direction that keeps the rule usable, which is the same trade `SK7072` made. ⚠ **Wrapping is not
rethrowing and is not reported**: `throw new ImportException(message, error)` translates the failure at
a boundary and produces one record rather than two, and logging the original before translating is how
detail the translation drops survives at all. Only bare `throw;` and `throw error;` count.

⚠ **`SK7093` answers "policy or defect?" by refusing the question and finding a decidable one
underneath it.** `S106` says standard output should not be used to log, which is correct in a library
and wrong in a console application's entry point — and `SK7091`'s measurement applies here unchanged:
`LooseLoader` builds every loose compilation as `OutputKind.DynamicallyLinkedLibrary`, so nothing in
the tree distinguishes "a library" from "no project file was loaded", and an `OutputKind` rule would
report every line of every console application analysed without its project. What `SK7093` reports
instead is the narrower fact [#230](https://github.com/Rikarin/SKALA/issues/230)'s own title names: **a
logger is in scope at this call site and the code wrote to the console anyway.** A member or parameter
typed `ILogger`, `ILogger<T>` or `ILog` is present, so the routing question is already answered for
this code and answered differently two lines away — a contradiction inside one method rather than a
policy judgement about the project's shape. ⚠ The consequences are deliberate in both directions: an
entry point printing usage has no logger and is never reported, which is right *by construction*
rather than by an exemption somebody has to maintain, and a service class that writes to the console
and has no logger at all is under-reported, which is the direction [16](16-risks-and-open-questions.md)
§ R3 says to err in. The interface is matched on the type's own name because the namespace is the part
that differs across `Microsoft.Extensions.Logging`, `Serilog`, `NLog` and `log4net`.

⚠ **All four are `requiresSemantics: true`, so the reference-tree sweep measures nothing about them —
and this batch has the direct proof [#277](https://github.com/Rikarin/SKALA/issues/277) was missing.**
`Testing/corpus/real/*` are source slices with no project files, so the only load mode they support is
loose, and `AnalyzerHost` withdraws every semantic rule there. The SARIF says so in as many words —
each of the four carries `"skipped": "requires a semantic model; --load=loose has no project"` — so the
corpus zero for `SK7090`–`SK7093` is the *third* kind of zero, the analysis never running, and it is
not evidence of anything. What makes it provable rather than merely suspected: the corpus contains
**twelve** `throw new NotImplementedException` sites in `newtonsoft`, none carrying an issue reference,
and wrapping three of those files in a throwaway `.csproj` — which still does not compile, 14 `CS`
errors — makes `SK7090` report **nine** of them under `--load=workspace`. Same files, same analyzer,
same day: 0 loose, 9 workspace. ⚠ Those nine are all members of test doubles implementing an interface
the test never calls, which is the one class measured to fire in volume; the rule does not exempt them
and § "SK7090" in `rules.json` records why.

Measured against Skala's own tree with `check . --load=workspace` (587 findings overall, so the run is
real): all four report **zero**, and the three kinds of zero split cleanly. `SK7090`, `SK7091` and
`SK7093` are *shape absent* — the tree holds no `NotImplementedException`, no `Environment.Exit`, and
no member typed `ILogger` or `ILog` anywhere. `SK7092` is the one that is **shape present and correctly
declined**: `Tools/Rikarin.Skala.Cli/Program.cs` writes `exception.ToString()` to `Console.Error` from
a `catch`, and `AnalysisCommands.cs` writes `exception.Message` from two more — all three then
`return` rather than rethrow, so there is one record and no finding. That is the only one of the four
whose zero is evidence.

`SK7090` a thrown `NotImplementedException` with no issue reference · `SK7091` `Environment.Exit`
outside the entry point · `SK7092` the exception is both logged and rethrown · `SK7093` the console is
written to where a logger was meant.

⚠ **"The block adds nesting and nothing else" ([#225](https://github.com/Rikarin/SKALA/issues/225),
Sonar `S1199`) is refuted, no id was allocated, and the reason is measured rather than argued.** The
proposal's premise was that `SK0208` owns *control-statement* braces and that the free-standing block
is a residue the arrangement option does not reach. It reaches it.
`RedundantBracesRule.Rewriter.VisitBlock` in
`Formatting/Rikarin.Skala.Formatting.CSharp/Arrangement/RedundancyRules.cs` walks the statements of
every block and lifts any statement that is itself a `BlockSyntax` — a free-standing `{ … }` nested in
a method body is exactly that shape, and `skala arrange --check` over one reports `SK0208 redundant
braces` and would remove it. What `SK0208` declines to lift is a block holding a local declaration, a
local function, a label, or a preprocessor directive, because lifting those widens a scope or moves a
directive. ⚠ **That residue is the wrong half to build a rule on**: a block that scopes a declaration
is a block that adds something besides nesting, which is the one case the proposed rule's own title
excludes. A new `SK7xxx` rule here would either duplicate `SK0208` or report precisely the blocks that
are not redundant, so it is not built and the number stays free. ⚠ Note for whoever revisits this:
`resharper_csharp_braces_redundant` is **tier D**, which here does *not* mean unimplemented —
`ArrangementOptions.Ids` collects the key and the rewriter runs. The conformance sweep records it as
`❌ SPURIOUS`, "Skala moved and the oracle did not", so the tier is about fidelity to `jb cleanupcode`
rather than about whether the shape is reached. It is reached, which is all this refutation needs; the
divergence against the oracle is a separate question and is not disturbed here.
⚠ **`SK7100` and `SK7101` are appended here with the prose pass owed.** The rows below are the
register entry ADR-012 requires and no more; the paragraph placing them beside `SK7010`, and
recording why the second ships at `none`, has not been written.

`SK7100` a documentation comment that is word for word the one on the member it overrides or
implements — reported only where the two are *identical*, because a similarity threshold is what
would make the rule dangerous. · `SK7101` a declaration that is not publicly visible and carries no
documentation comment — `SK7010`'s predicates with the accessibility test negated, shipped at
`none` and enabled per path, because it is the highest-firing uncovered inspection in the parity
measurement and that is an argument for caution rather than for volume.
⚠ **The prose pass for `SK7080`–`SK7084` is owed.** What follows is the allocation register entry —
enough that the id is written down and `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister`
can see it — not the worked-through account the rest of this section carries.

**`SK7080`–`SK7084` extend the threshold family `SK7001`–`SK7006` and `SK7030` established**, and
they extend the same machinery: one `MetricThresholds` record read once per file from
`dotnet_code_quality.SK70xx.threshold`, the measured value carried on the diagnostic under
`skala.metric.value`, and no fix, which is what a measurement has instead of a repair.

⚠ **`SK7080` counts only the part of the chain this compilation declares.** Raw inheritance depth
penalises using a framework — `MyControl : Button` is one decision somebody made and the eight
types above `Button` are not — so the walk counts the first non-source base once and stops. The
consequence is that a base class in a referenced project also ends the chain, which under-reports;
that is the safe direction. ⚠ **An error type anywhere on the chain withdraws the measurement
entirely**, because a chain that cannot be walked reads as depth 1 and reporting the smaller number
would be silently wrong. That is why the rule finds nothing on the corpus slices (#277): the shape
is there and the analysis correctly declines.

⚠ **`SK7081` is the metric the other seven do not carry.** A type can be short, shallow and simple
in every one of `SK7001`–`SK7006` and still name forty other types, and each of those forty is a
reason this file has to be reopened when something else changes. Special types are excluded — the
language's own vocabulary would add the same handful to every type and separate nothing — and so
are the type itself, whatever nests it and whatever it nests, because a type that organises itself
with nested helpers is not coupled to somebody else's design. ⚠ Like `SK7080`, an unresolved
reference is skipped rather than counted, so its zero on the corpus is a declined measurement.

`SK7080` a class with more source-declared base classes than the threshold (default 4) ·
⚠ **`SK7082`'s exemption is the rule.** A right-associated ladder — `a ? x : b ? y : z` — costs one
level, not one per rung: that shape is an `else if` chain written as an expression, it reads top to
bottom, and charging it per rung would fire on the one nested form that is idiomatic. Parentheses
around a rung do not change the number. A lambda body restarts the count, as it does for `SK7006`,
and only the outermost conditional of a nest reports.

`SK7081` a type declaration naming more distinct other types than the threshold (default 80) ·
`SK7082` conditional expressions nested deeper than the threshold (default 1, ladders exempt) ·
`SK7083` a string literal written more times in one file than the threshold (default 5), counting
only literals of at least `minimum_length` characters (default 5) that contain a letter.

⚠ **`SK7083` is the first rule in this family to take two options**, and the second is what makes it
usable: a repeat count on its own reports `", "` and `"true"` long before it reports anything worth
extracting. It is also spelled differently on purpose — `dotnet_code_quality.SK7083.minimum_length`
rather than a second `.threshold`, because a rule with two numbers cannot spell both of them the way
the family spells one. A `const` initialiser is not counted, because it is the repair; an attribute
argument is not counted, because a constant is the only extraction available there and the ones that
repeat are display names and obsolescence messages.

⚠ **The magic-number rule (#228) was built, measured and cut, and no id was allocated for it.**
ADR-012 makes an id permanent, so a concept that does not ship must not take one. A prototype with
nine exemptions — `0`, `1`, `2`, attribute arguments, enum member values, parameter defaults, `case`
labels, constant patterns, element-access indices, and the initialiser of anything with a name —
reports **727** findings on Skala's own tree across 132 files, 460 of them outside test code. That
is 1.8 per file, an order of magnitude past `SK7083`, which is the largest of the four that shipped.
Volume alone would be arguable. What settles it is *what* it reports: a splitmix64 mixer in
`CloneDetector` (`0x9E3779B97F4A7C15UL`, and the shifts 30, 27 and 31), and the binary header offsets
in `CloneIndex` (`AsSpan(4)`, `AsSpan(8)`, `AsSpan(12)`, `20 + version.Length`). In both, naming the
numbers makes the code *less* readable — a transcribed algorithm stops being recognisable and a
layout stops being visible — and no syntactic signal separates them from a genuine unexplained
threshold, because the distinction lives in what the author knew. A tenth exemption would not have
found it and neither would a hundredth. This is doc [16](16-risks-and-open-questions.md) § R3's "a
hundred rules that are usually right" with the measurement attached, and `defaultSeverity: none`
does not rescue it: a rule nobody can afford to turn on is a page in this catalogue and an id taken
for ever in exchange for nothing.

## SK8000 — Tests

`SK8001` test method with no assertion · `SK8002` `Assert.True(x == y)` instead of `Assert.Equal` ·
`SK8003` `[Fact]` on a method with parameters · `SK8004` `async void` test ·
`SK8005` `Thread.Sleep` in a test · `SK8006` test that is `[Skip]`ped without a reason ·
`SK8007` non-deterministic input (`DateTime.Now`, `Guid.NewGuid`, `Random`) in an assertion path.

⚠ **`SK8020`–`SK8022` are appended here with the prose pass owed.** The rows below are the register
entry ADR-012 requires and no more; the paragraph that explains what the three add to this range, and
how they relate to the `SK8001`–`SK8004` cuts above, has not been written.

`SK8020` a class with `[TestMethod]` members and no `[TestClass]` — MSTest only, because xUnit has no
class attribute to be missing and NUnit 3 made `[TestFixture]` optional. ·
`SK8021` a `[TestClass]` or `[TestFixture]` that declares no test, in itself or in any base type —
report-only, because choosing between writing the missing test and deleting the class is the whole
of the finding. · `SK8022` an equality assertion called as `(actual, expected)` — reported only where
the `expected` argument is not a constant and the `actual` argument is, because a constant cannot be
produced by the code under test.

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
`SK9030` analyzer threw · `SK9031` analyzer failed to load ·
`SK9095` an arrangement rule threw and was skipped; the rest of the catalogue still ran — ⚠ the
sibling of `SK9030`, and allocated for the same reason: a rule that throws must cost its own rewrite
and nothing else. Warning, and it is a **Skala bug** even when the throw comes out of a dependency —
`SK-FUZZ-0012` is Roslyn's binder throwing out of a legitimate `GetSymbolInfo` ·
`SK9096` arrangement reverted, a touched identifier now resolves to a different symbol ·
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
| Rules this document names | **214** | excluding band edges (`SK1000`–`SK1999` and the like), `SK3499`/`SK3500`, and `SK9xxx` |
| **Shipped** — present in `rules.json` | **180** | **84.5 %** |
| **Cut** — deliberately not built, reason recorded | **12** | § "Cut, with the reason" |
| **Retired** — allocated, superseded, never to be built | **1** | the id stays taken for ever (ADR-012) |
| **Outstanding** — planned, not built, not disposed of | **21** | includes the twelve declared cut with no reason recorded |

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

### ⚠ What a corpus zero is worth, and for most rules it is nothing

**The bar's second clause — zero false positives across two reference trees — is currently vacuous
for every rule with `requiresSemantics: true`, and that was discovered while shipping twenty of
them.** `Testing/corpus/real/{vixen,newtonsoft,serilog}` are vendored **source slices with no project
files and no dependency closure**; a compilation over them is a sea of error types, measured at
roughly 12 951 CS errors for vixen, 2 316 for newtonsoft and 1 484 for serilog. Every semantic guard
in every analyzer withdraws against an error type, so the rule goes quiet and the sweep prints a
clean zero.

⚠ **The proof is a number in this document.** § "Backing storage, precomputed lookups and span call
sites" records `SK3501` finding **1** on `corpus/real`. It finds **0** today, under both
`check --load=loose` and a direct compilation, and the rule has not changed. That figure is stale for
**instrument** reasons rather than code reasons, and correcting it by editing the number would be the
error rather than the fix.

This is [17](17-inspection-parity.md)'s *"a zero from a disabled inspection and a zero from a clean
codebase are the same zero"*, one level up: **a zero from an uncompilable tree and a zero from clean
code are the same zero.** The distinction a report must therefore draw, and which several rules above
now draw explicitly, is between three different zeros — the shape is absent from the trees, the shape
is present and correctly declined, and the analysis never ran. Only the second is evidence.

Where a semantic rule needed real evidence it was measured against **Skala's own tree**, which
compiles; that is how `SK3511`'s one true positive in `Reporting/…/SarifWriter.cs` was found, which
no corpus sweep could have seen. Fixing the instrument is tracked separately and is not a rule
problem.

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
`SK3501` shipped because of that measurement; `SK2016` and `SK3502` now cover the conservative
CA2254 and CA1001 cases as well.

⚠ **`CS1717`/`CS1718` reach the identifier spellings only.** `Prop = Prop`, `other.Prop =
other.Prop` and `other.Prop == other.Prop` produce nothing. `SK2012` now covers non-virtual
auto-properties declared in the same file, without assuming arbitrary accessors are side-effect-free.
`SK2001` covers comparisons decided by integral type endpoints that the compiler leaves silent;
neither rule duplicates the compiler-covered cases. The compiler also leaves an `IEquatable<T>` implemented without
an object equality override silent. `SK2004` now covers that gap for self-typed contracts, while
leaving existing overrides and the compiler-covered missing-hash-code case alone.

### ⚠ Declared cut with no recorded reason — reclassified as outstanding

M7's retrospective says "twenty of the twenty-three were cut" and then gives reasons for eight of
them. The following twelve had no reason recorded anywhere in this document or in the commit that
wrote it, so they were **outstanding**, not cut:

`SK4001` LINQ in a hot path · `SK4002` closure allocation in a hot loop · `SK4003` `params` array at
a call site that could use a span · `SK4004` boxing in a constraint-satisfiable position ·
`SK4006` `ToList()`/`ToArray()` immediately re-enumerated once · `SK4007` large `struct` passed by
value · `SK4008` async state machine for a synchronous method · `SK6001` (retired — see above) ·
`SK6004` interface with one implementation · `SK6008` extension method on `object` ·
`SK8006` `[Skip]` without a reason · `SK8007` non-deterministic input in an assertion path.

Saying "no reason was recorded" is the point. `SK6008`, `SK8006`, `SK4001`, `SK4002`, `SK4003`, `SK4004`,
`SK4006`, `SK4007` and `SK8007` now ship; `SK6001` is retired as a duplicate allocation. The other two remain
outstanding. These shipped rules are report-only: choosing a contract, justification, performance
tradeoff or controlled test input requires author intent.

### Outstanding, with what each is waiting on

The remaining rules that were never declared cut. Where a milestone recorded why one did not ship, the
reason is kept; it is a description of remaining work, not a disposal.

| Group | IDs | Waiting on |
|---|---|---|
| Declaration-shape rewrites | `SK1002`, `SK1008` | The unsafe-fix path and an `--include` story. Each is a good rule and neither is a *safe* fix (M5) |
| ⚠ Hot-path rules | `SK1027`, `SK1032` | Path-scoped configuration. **The `hint` default is suspect — see below** |
| The rest of the modernization set | `SK1004`, `SK1007`, `SK1009`, `SK1021`, `SK1024`, `SK1029`, `SK1036` | Nothing recorded. Not started |
| Security | `SK5003`, `SK5004`, `SK5006`, `SK5008` | The remaining M8 rules; a wrong security rule is worse than a missing one |

### Priority 1 hygiene rules

`SK6008`, `SK7040`, `SK7050`, `SK7051` and `SK8006` now ship. They are deliberately conservative:
the object-extension and suppression-attribute rules bind the framework symbols they name; the TODO
rule accepts URLs, hash-number issues and project keys; a pragma accepts an adjacent meaningful
comment; and the skipped-test rule reports only a constant empty or placeholder xUnit `Skip` value.
All five are report-only because no honest mechanical edit can choose a receiver contract, create an
issue, or write the author's justification.

### Priority 2 correctness and lifetime rules

`SK2009`, `SK2014`, `SK2016`, `SK3005` and `SK3502` now ship. Their boundaries are intentionally
narrow: enum switches exclude flags and catch-all arms; empty catches accept a comment or filter as
an explicit decision; logger interpolation binds only Microsoft.Extensions.Logging's `message`
parameter; fire-and-forget reports only bare Task calls in synchronous bodies, leaving async bodies
to `CS4014`; and disposable-field ownership requires a direct instance-field construction. All five
are report-only because adding enum behavior, exception recovery, logging property names, an async
API boundary or a disposal implementation requires author intent.

### Next correctness batch

`SK2002`, `SK2004`, `SK2008`, `SK2010` and `SK2011` now ship. Pure-result detection recognizes
framework method-level contracts and a closed list of immutable string transformations. Equality
contracts are checked only for `IEquatable<Self>` without an object override. Loop captures require
semantic capture of an incremented for-loop variable by a delegate stored through framework
`List<T>.Add`; arbitrary callbacks and storage are not guessed. Culture checks bind string methods
and accept explicit comparison/culture arguments. Value-type equality reports only calls that bind
to the inherited `ValueType.Equals` implementation. All five are semantic and report-only; their
repair requires an assignment target, identity policy, capture lifetime or culture decision.

### Pattern, span, async-policy and file-length batch

`SK1011`, `SK1014`, `SK1028`, `SK3003` and `SK7030` now ship. The three modernization rules
carry safe fixes for conservative shapes: a stable null-guarded receiver with one member equality,
two integral comparisons with representable constant bounds, and a framework byte span copied only
to feed `Encoding.UTF8.GetString`. Pattern fixes exclude expression trees, captured/ref storage,
overloaded operators and constant-result ranges that could become compiler diagnostics. Span fixes
bind the replacement overload and keep the existing slice operation intact.

`SK3003` is report-only and requires explicit `library` mode from the resolved per-file
`resharper_configure_await_analysis_mode` (or unprefixed alias). It respects existing explicit
`ConfigureAwait` choices. Missing, disabled and UI mode do not report missing configuration;
UI mode's redundant-`ConfigureAwait(true)` inspection is a different concept and is not implemented.

`SK7030` is a syntax-scoped, report-only physical-line metric with a configurable 1000-line default:
`dotnet_code_quality.SK7030.threshold`. Blank/comment/inactive lines count; the terminal empty
line after a final newline does not. Generated code is excluded. The default is a broad review
policy, not a threshold calibrated to Skala's current files. Per-file severities and metric values
use the existing analysis host and reporting pipeline.

Validation: the rule fixtures include exact-count, language-floor, generated-code, ref/capture,
configuration and negative cases. Runtime differential tests compare range results, getter-call
counts, decoded output and slice exception types/parameter names before and after the safe fixes.
The workspace integration test exercises all five rules through `check` and `verify`, applies the
three safe fixes, and compares warm/cold findings after another file changes and after policy changes.
An audit of Skala's own workspace found 11 file-length hints and no findings for the other four;
`ConfigureAwait` analysis remains disabled by this repository's existing configuration. Those zeros
are not a substitute for the positive fixtures. The five analyzers together consumed about 198 ms
of summed analyzer time in that run (98 ms relational patterns, 75 ms file length, 14 ms property
patterns, 11 ms span decoding and under 1 ms for the disabled async policy); these are observations,
not performance guarantees.

### Returning expressions, list patterns, UTF-8 literals and comparison checks

`SK1012`, `SK1013`, `SK1026`, `SK2001` and `SK2012` now ship as semantic rules. The three
modernizations have safe fixes, with deliberately bounded initial coverage:

- `SK1012` (C# 8): returning equality chains over a stable local/parameter, distinct integral,
  enum or string constants, an explicit fallback, and identity-converted results of one type.
- `SK1013` (C# 11): null-guarded arrays/strings with an exact length of 1–8 and distinct,
  constant-index element equalities. A preceding `SK1011` fix combining null and length into a
  property pattern does not hide the list-pattern opportunity. No custom indexers or slices.
- `SK1026` (C# 11): framework `Encoding.UTF8.GetBytes` of constant ASCII text passed to an
  already-selected `ReadOnlySpan<byte>` parameter. Speculative binding must preserve the exact
  constructed consumer method; mutable array/span APIs and non-ASCII text are excluded.

The two correctness rules are report-only. `SK2001` uses exact fixed-width integral/char ranges,
not nullable annotations or flow guesses, and leaves `CS0652` cases to the compiler. `SK2012`
requires a same-file, non-virtual auto-property and an identical stable receiver; arbitrary
accessors, NaN-sensitive comparisons and compiler-covered `CS1717`/`CS1718` cases are excluded.
Deleting either operation could remove side effects or conceal the author's intended operand.

Source constant dependencies, including transitive initializers, must stay in the finding's file;
metadata constants are allowed. Auto-property declarations must also be in the finding's file.
This keeps body-dependent proofs compatible with the existing per-file semantic cache. The fixes
exclude comments/directives and observable caller-argument-expression text; pattern spellings
such as a constant named `_` are not copied into discard patterns.

Validation includes exact fixture counts and fix availability, language floors, all nine integral
type endpoints, cross-file dependencies, compiler-warning exclusions and negative cases. Runtime
differential tests compare selected-branch effects, null/length/element outcomes, and encoded bytes
including NUL and escaped ASCII. Workspace integration exercises `check`, `verify`, safe fixing,
per-file severity and warm/cold cache agreement after another file changes.

An audit of Skala's own workspace found one `SK1026` opportunity in `CacheKeyPathTests`: a
constant ASCII payload allocated as a byte array only to feed `CacheKey.For`'s read-only span.
It found no occurrences of the other four rules; their evidence remains the positive and negative
tests, not those zero counts. The five analyzers together consumed about 727 ms of summed analyzer
time in that run. The audit did not rewrite unrelated existing code.

### Shared Lazy, hot-path review, loop captures, materialization and test input

`SK3009`, `SK4001`, `SK4002`, `SK4006` and `SK8007` now ship. All five are semantic and
report-only; none invents a synchronization policy, removes a snapshot, reuses a capture with a
different lifetime, or chooses test values on the author's behalf.

| ID | Initial scope | Default |
|---|---|---|
| `SK3009` | Direct static framework `Lazy<T>` field construction with `false` or `LazyThreadSafetyMode.None` | warning |
| `SK4001` | Explicit framework `Enumerable` method calls in configured paths, one finding per fluent pipeline | none |
| `SK4002` | Delegate lambdas capturing loop-body locals or a C# 5+ ordinary `foreach` iteration variable | hint |
| `SK4006` | Framework `ToList`/`ToArray` of a stable local/parameter immediately consumed by `foreach` | hint |
| `SK8007` | Live clock, new GUID or unseeded random input directly in an xUnit assertion in a bound test method | suggestion |

`Lazy<T>` is thread-safe by default; an omitted mode is **not** a defect. The rule excludes
default/safe modes, thread-static and instance fields, indirect construction and nonconstant modes.
Static storage does not prove concurrent access, so external synchronization or deliberate
single-threaded use can justify suppression. Constant dependencies are kept in the finding's file
or in metadata, including transitive initializers, to preserve per-file cache correctness.

Hot paths must be chosen explicitly. For example:

```editorconfig
[Rendering/**/*.cs]
dotnet_diagnostic.SK4001.severity = suggestion
dotnet_diagnostic.SK4002.severity = suggestion
dotnet_diagnostic.SK4006.severity = suggestion
```

The performance rules request measurement, not blanket removal of LINQ or closures. Queryable
providers, custom materializers and expression-tree captures are not treated as the framework
patterns. `SK4006` suppresses the hint whenever the loop body references the source collection,
or contains an await/yield, preserving obvious snapshots including the `SK2007` fix. Hidden
mutation through another method and eager-evaluation timing still require human judgment.

The test rule initially supports semantically bound xUnit assertions and Fact/Theory attributes,
including derived attributes. Seeded/unknown random instances, assertion messages, `nameof`,
deferred lambdas, helpers and local functions are excluded. It does not follow earlier values
through variables or guess custom assertion contracts. Intentional real-clock/randomness tests
can be scoped out; there is at most one finding per assertion.

Validation covers positive and negative fixtures, exact counts, absence of fixes, generated code,
the LINQ rule's opt-in default, semantic capture boundaries, legacy foreach behavior and constant
dependencies. Workspace integration checks per-file policy, `check`, `verify`, no-op safe fixing,
warm/cold agreement after an unrelated file changes, and invalidation after severity changes.

The workspace audit produced 54 `SK4002` hints. Inspection confirmed loop-local captures in
assertion predicates, configuration lookups, generated-code helpers and callbacks; it did not
establish that those paths are hot or need rewriting. The other enabled rules produced no
findings, and `SK4001` stayed disabled by policy. Positive fixtures supply the evidence for those
quiet rules. Summed analyzer time for this batch was about 1.0 s in that run, not a performance
guarantee. No existing production or test code was rewritten to remove the audit hints.

### Dedicated locks, floating-point arithmetic, boxing, struct copies and commented code

`SK1023`, `SK2003`, `SK4004`, `SK4007` and `SK7060` now ship with explicitly bounded coverage:

| ID | Initial scope | Fix / default |
|---|---|---|
| `SK1023` | A private readonly object constructed for use exclusively as lock targets | safe / suggestion |
| `SK2003` | Exact equality directly involving built-in float/double arithmetic | none / warning |
| `SK4004` | An interface method call through an explicit boxing cast of an already-constrained value-type parameter | none / hint |
| `SK4007` | A known-large source struct local/parameter passed by value in a loop body | none / hint |
| `SK7060` | Standalone ordinary comments that parse as multiple code-like statements | none / hint |

The lock fix requires C# 13 and the framework `System.Threading.Lock` constructor/scope API.
It edits the field type and initializer together. Every bound reference in the file must be a
direct lock target; Monitor calls, escapes, aliases, reassignment and casts prevent the rewrite.
Partial containing types, directives, field attributes and interior declaration comments are
excluded, so no other source part or inactive branch can hide a reference from the proof.
References through constructed generic types are checked against the original field definition;
lock bodies containing yields are excluded because the new scope cannot cross an iterator yield.

Floating-point review is deliberately narrower than all `==` uses: plain variable comparisons,
property change guards, zero/NaN/infinity constants, constant-folded results, nullable arithmetic,
decimal and custom operators are excluded. It does not invent a tolerance. The boxing rule likewise
does not remove the cast: a constrained call can mutate the original struct instead of a boxed
copy, even where avoiding the allocation is possible.

Struct-copy review uses `dotnet_code_quality.SK4007.threshold` (default 64, strictly exceeded).
The value is a **lower bound** from primitive fields and recursively known same-file structs,
not a guessed ABI size. Unknown fields contribute zero. Partial/generic/other-file types and
explicit/auto layouts are excluded from that calculation; ref fields and fixed buffers are not
counted. Padding, native pointer size and explicit layout-size overrides are not guessed.
Layout attributes must not depend on constants declared in another source file.
`in`/`ref`/`out`, factories and user-defined conversions are not by-value local copies. The
diagnostic records `skala.size.lower_bound`; changing the call signature requires author review.

Comment detection is syntax-only and intentionally a hint. It examines standalone `//` groups and
`/* */` comments, excluding XML documentation, inline comments, labelled/fenced examples, URLs,
prose, incomplete code and single statements. Parsing is bounded to 8192 characters, 100 line
comments and 128 opening delimiters. A complete parse and code-token density are required, but
unlabelled examples can still look like disabled code. There is no automatic deletion.

Validation covers positive/negative fixtures, exact finding counts, fix availability, language/API
guards, generated code, source-constant/layout dependencies, threshold fallback and parser budgets.
A runtime test checks that the lock fix preserves mutual exclusion and nested reentrancy; size
tests compare the reported payload bound with `Unsafe.SizeOf` on the running framework. Workspace
integration checks all five through `check` and `verify`, applies the lock fix, and compares warm
and cold results after file and policy changes.

A cold workspace audit of Skala with all five rules selected and hints included found zero
occurrences of each. Existing dedicated synchronization fields already use `Lock`; collection
monitors and lock examples inside test-source strings do not satisfy the field proof. These zero
counts are not positive corpus coverage: fixtures and workspace integration remain the evidence
for firing behavior. Summed analyzer time for the five rules was about 1.45 s in that profiled
run, not a performance guarantee. No unrelated production or test source was rewritten.

### Backing storage, precomputed lookups and span call sites

`SK1003`, `SK1022`, `SK1025`, `SK2005` and `SK4003` now ship:

| ID | Initial scope | Fix / default |
|---|---|---|
| `SK1003` | An uninitialized private backing field used only by one property's get/set accessors | safe / suggestion |
| `SK1022` | A constant cached character array used exclusively by framework span searches | safe / hint |
| `SK1025` | A constant private static dictionary used exclusively for lookups | safe / hint |
| `SK2005` | A mutating source struct method invoked on a readonly field | none / warning |
| `SK4003` | A temporary params array with an accessible corresponding ReadOnlySpan overload | none / hint |

The three storage rewrites share a file-local private-field reference proof, including constructed
generic types and documentation references. Partial containing types, directives and attributed or
commented field declarations are excluded. `SK1003` requires C# 14, explicit get/set bodies and
matching field/property types, leaves accessor logic intact, and rejects initializers, escaped
storage, other consumers, caller-expression capture, nested functions, layout/serialization
attributes and non-nullable reference storage. Reflection on private backing storage is outside
the proof.

`SK1022` requires a real SearchValues factory and matching span overloads. It supports character
sets of 4 or more distinct constant characters (at most 256), rejects mutation and escaping uses,
and excludes other static initializers/constructors to avoid reentrant initialization differences.
String search calls and byte sets remain outside this initial scope. `SK1025` accepts only default
Dictionary construction with constant collection entries and integral/char/string keys. It keeps
that construction, including duplicate-key validation, before freezing; only indexer reads, Count,
ContainsKey and TryGetValue are allowed. Neither hint promises a speedup: creation costs and the
target workload still matter, and both support ordinary path-scoped severity settings.

`SK2005` resolves the old ambiguous wording "mutable struct with a readonly field" as **mutation
lost through readonly struct storage**, not a prohibition on readonly members inside mutable
structs. Its proof requires a same-file non-readonly void method directly assigning or incrementing
its own instance field. Constructors/init accessors, conditional methods, expression trees,
reference types and bodies in another file are excluded. `SK4003` speculatively binds a C# 12
ReadOnlySpan collection expression and checks the overload signature; it does not assume equal
behavior between overload implementations. These two rules cannot choose an automatic fix.

Validation includes positive/negative fixtures, exact counts, language/API guards, generated code,
cross-file dependency guards, runtime equivalence for property validation, Unicode searches, frozen
lookups and duplicate-key failures, and a runtime demonstration of the lost struct mutation.
Workspace integration covers check, verify, all three safe fixes, severity overrides and warm/cold
cache agreement.

A cold, profiled Skala workspace audit with hints included reported one `SK4003` candidate:
`MetricsAnalyzer.SupportedDiagnostics` passes a fresh array to `ImmutableArray.Create`, for which
an accessible ReadOnlySpan overload was verified. It is a real call-site opportunity, not evidence
of a hot-path bottleneck. The other four rules reported zero occurrences; their positive evidence
remains the fixtures and workspace integration test. No audit finding was auto-fixed. Eligibility
checks run before whole-file field-reference scans; summed time for these five analyzers in the
final audit was about 1.35 s, not a performance guarantee.

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
