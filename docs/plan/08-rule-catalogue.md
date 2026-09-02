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
| `SK0232` | The argument or signature element is redundant | Semantic | [#134](https://github.com/Rikarin/SKALA/issues/134) | 4 of 7 |
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

`SK0240` covers five shapes of [#131](https://github.com/Rikarin/SKALA/issues/131)'s thirteen: a
`continue;` ending a loop body or a `return;` ending a void body (`RedundantJumpStatement`), a
`default:` section whose only statement is `break;` (`RedundantEmptySwitchSection`), a `case` label
sharing its section with `default:` (`RedundantCaseLabel`, `RedundantEnumCaseLabelForDefaultSection`),
an empty `finally` (`RedundantEmptyFinallyBlock`), and — the member the issue calls the valuable one —
a `catch` whose body is exactly `throw;` (`RedundantCatchClause`). Only the *last* `catch` of a `try`
is reported: deleting an earlier one changes which handler an exception reaches, which is a behaviour
change wearing a redundancy's clothes. ⚠ **A `try` matching both the rethrowing-catch and the
empty-`finally` shape produces one finding carrying one composite edit**, because two deletions
compose into `try { … }` — CS1524 — and one finding per pass leaves a finding standing on the fix's
own output. The remaining inspections are the five boolean-expression shapes (`RedundantBoolCompare`,
`DoubleNegationOperator` and their siblings), which each need an operand's type before they can be
called redundant, plus `RedundantIfElseBlock` and `RedundantSwitchExpressionArms`; none is covered,
because each would make the rule semantic and stop it running on a loose file.

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

⚠ **`SK0210` was believed to have a measured gap here, and the belief is refuted — see
"Finishing the redundancy cleanups" below.** `UnusedImportClause` is `SK0210`'s remit and it ships;
`RedundantUsingDirective.Global` is `SK0210`'s remit and **it fires too**. `UsingsRule.Unused` does
build its removal set from Roslyn's **CS8019** alone
(`Formatting/Rikarin.Skala.Formatting.CSharp/Arrangement/UsingsRule.cs`), and a file-level `using X;`
duplicated by a `global using X;` does report **CS8933** — but it reports **CS8019 as well**, in every
shape that was measured, so the name reaches the removal set anyway. The earlier probe that found
"CS8019 is silent" does not reproduce. Nothing in `SK0243` claims that inspection, which remains
right: it would have been a second id for one concept.
⚠ **`SK0210`'s "measured gap" was refuted, and the paragraph that stood here was wrong.** It said
`RedundantUsingDirective.Global` does not fire because `UsingsRule.Unused` builds its removal set from
**CS8019** alone while the compiler reports a file-level `using X;` duplicated by a `global using X;`
as **CS8933**, "measured on a probe project, where the using is genuinely redundant and CS8019 is
silent". Re-measured directly against Roslyn, over four arrangements of the shape — the namespace used
in the same file, used only in another file, two `global using`s in different files, and both
directives in one file — the compiler reports **CS8019 alongside** CS8933 (or `CS0105`) in every one:

```
A file-level using duplicated by a global using, namespace used
  Use.cs CS8933 [Hidden] line 0: The using directive for 'System.Text' appeared previously as global using
  Use.cs CS8019 [Hidden] line 0: Unnecessary using directive.
```

CS8933 is *additional*, not *instead of*. ⚠ The likeliest way the original measurement reached the
opposite conclusion is that both diagnostics are **Hidden**, so neither appears in `dotnet build`
output at any verbosity — a build that prints no CS8019 is not a compilation that produced none. So
`UsingsRule.Unused` does see the shape, and `RedundantUsingDirective.Global` is `SK0210`'s remit and
appears to be within it; what is still owed is an end-to-end `arrange` check that the directive is
actually removed, which is a smaller question than the one this paragraph asked. Nothing in `SK0243`
claims that inspection either way: shipping it here would be a second id for one concept.

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

### Finishing the redundancy cleanups: three refutations and no new id

⚠ **This section is owed prose about work that ended in refutations rather than rules, and the owed
half is the part that has to be written down.** A batch was scoped to close out the remainder of
[#131](https://github.com/Rikarin/SKALA/issues/131),
[#129](https://github.com/Rikarin/SKALA/issues/129),
[#130](https://github.com/Rikarin/SKALA/issues/130),
[#135](https://github.com/Rikarin/SKALA/issues/135) and
[#178](https://github.com/Rikarin/SKALA/issues/178) with new ids. It shipped **no new id**: two
shapes belonged inside `SK0240`, which now holds them, and everything else was measured and found
not to be a rule. What is still owed is the corpus evidence for the shapes nobody has looked at
(`RedundantIfElseBlock`, `RedundantSwitchExpressionArms`) and a decision on whether `#130`'s
remainder justifies a *semantic* sibling to `SK0244`, which is the one live option this section did
not close.

⚠ **[#292](https://github.com/Rikarin/SKALA/issues/292) is refuted: `SK0210` already sees the
global-duplication shape, and the measurement that said otherwise was wrong.** The issue reports
that a file-level `using X;` duplicated by a `global using X;` is CS8933 and that "CS8019 is
silent", so `UsingsRule.Unused` — which reads CS8019 alone — cannot see it. Measured across ten
shapes (SDK implicit usings, a user-written `global using` in another file, both directives in the
same file, `using static`, a global alias, a using inside a namespace, and a multi-directive
ordering case): **CS8019 fires alongside CS8933 every time**, the name is already in the removal
set, and the arranger already deletes the directive. The decisive comparison is the set built from
CS8019 against the set built from CS8019 ∪ CS8933 — identical in all ten. **Adding CS8933 would be a
strict no-op**, so it was not added; the note against `SK0210` above, and the same claim in
`SK0243`'s `falsePositives`, are both wrong on this point. The one shape that starts with neither
diagnostic is a using *inside* a namespace declaration, where it is genuinely load-bearing until the
arranger hoists it out — and the pipeline's own re-bind pass removes it in the same run, which is
SK-FUZZ-0018's fix working rather than a gap.

⚠ **[#135](https://github.com/Rikarin/SKALA/issues/135)'s remainder is refuted with the compiler as
the witness, and the reason is stronger than the loader-dependence already recorded.**
`RedundantNotNullConstraint` describes a shape that cannot exist: `notnull` may not be combined with
`class`, `struct` or `unmanaged` in either order — every combination is CS0449 — so no legal program
has a `notnull` made redundant by a sibling constraint, and it is clean under both an enabled and a
disabled annotation context. The three `RedundantNullableAnnotationOn*Constraint` inspections are
the other half: `where T : class?` and `where T : IComparable?` are **clean and meaningful** where
annotations are enabled, and where they are disabled **the compiler already reports CS8632**. Either
the annotation says something or the compiler has already said it does not; there is nothing left
for a Skala rule to add.

⚠ **[#178](https://github.com/Rikarin/SKALA/issues/178) is refuted on the shipping bar rather than
on the shape.** An empty method body is a shape, not a defect: a virtual no-op hook, an interface
implementation with nothing to do and an empty `Dispose()` are all correct, `SK2014` owns the empty
`catch`, `SK6023` owns the type whose body is empty, and `SK7090` owns the not-implemented stub.
What survives those exclusions is a non-virtual, uncommented, private method with an empty body —
and **there is no fix**, for the reason `SK6023` gives about deleting a type: something may name it,
including code the compilation cannot see. A rule with no fix does not meet this document's bar, so
no id was allocated (ADR-012: an id is permanent, and one is not allocated for a concept that will
not ship).

⚠ **The corpus slice does not compile, and the missing implicit-usings file is 22 % of why.** Over
the 380 source files of `Testing/corpus/real/`, a loose compilation reports **11 590 CS errors**
without a `GlobalUsings.g.cs` tree and **9 029 with one** — 2 561 errors, 5 743 → 4 370 `CS0246` and
4 474 → 3 496 `CS0103`, attributable to a file the slice omits. `SK0240` is `Syntax`-scope and runs
anyway, which was verified rather than assumed: six planted shapes were dropped into a copy of the
corpus outside the repository and **all six fired** under `--load=loose`, with no `SK9030` in the
SARIF's `toolExecutionNotifications`. Against the unmodified corpus `SK0240` reports **2** findings,
one of them the new `case`-label shape, in `Vixen.Audio/Effects/DistortionEffect.cs` where
`case DistortionCurve.SoftClip:` shares a section with `default:`. The empty `finally` reports **0**,
and that zero is **shape
absent**: all twelve files containing a `finally` have a body in it. ⚠ This is also why a *semantic*
rule cannot be measured on this slice at all — a compilation with 9 000 errors answers a symbol
question with whatever it managed to bind.

⚠ **`RuleFixtures.Compile` does not pass `allowUnsafe`, so no fixture for an `unsafe` shape can
compile.** Found while considering the nested-`unsafe` half of `RedundantUnsafeContext` for
`SK0241`: `unsafe class C { unsafe void M() { … } }` is CS0227 in the fixture harness. The
nested-context shapes were dropped rather than tested against a compilation that rejects them — the
trap `SK0240`'s deleted iterator guard was committed into once already.
### Cleanup — `SK0250`

⚠ **The prose pass on this block is owed**, like the two above it: it is written as one rule lands and
records what was measured rather than reading as a considered section.

| ID | Rule | Scope | Floor | Fix |
|---|---|---|---|---|
| `SK0250` | The discard designation is redundant | Syntax | 9 | safe |

`SK0250` covers `RedundantDiscardDesignation`, the fourth of
[#133](https://github.com/Rikarin/SKALA/issues/133)'s thirteen and the one that issue refused. It
reports a *designation* of `_` on a declaration or recursive pattern — `o is string _`,
`o is Point { X: 0 } _`, `case int _:` — where the pattern means the same with nothing after it.

⚠ **The refusal recorded on #133 was right and its stated reason was wrong, and the real reason is
worse.** #133 declined the inspection because "`out var _` becomes `out _` only where nothing named
`_` is in scope, and answering that needs a symbol lookup that would make the whole rule semantic".
That reads the inspection as the `var _` ⇔ `_` style choice — and **that choice is
`resharper_csharp_prefer_explicit_discard_declaration`, a tier-A option Skala already performs**
through `SK0217`'s `DiscardDeclarationRule` (`ArgumentStyleRule.cs`), in both directions, against the
oracle. Shipping it would not have cost `SK0233` its syntactic scope; it would have been one edit
owned by two ids, which is the double-count doc 17 § "Inspection ids are not concepts" exists to
prevent and the same trap #133 already records for `SK0209`. The option registry answers this before
the code does, and it was not asked.

The designation reading has neither problem. A designation position **declares**; it can never refer
to something already in scope, so there is no lookup and the rule is syntactic — it runs and is
measurable under `--load=loose`.

⚠ **It is a separate id from `SK0233` because its language floor is not `SK0233`'s.** `o is string` is
C# 1 — that is the `is` operator — but `case string:`, `string => …` and `Point(int, int)` are bare
*type patterns*, which the compiler refuses below C# 9 with `CS8400: Feature 'type pattern' is not
available in C# 8.0`. `languageVersion` is per rule, so folding this into `SK0233` would either put a
9.0 floor on nine shapes that have none — an empty attribute argument list is C# 1 — or declare a
floor the registry does not hold. The floor is the concept boundary here, not the taste.

**What the other four issues in this batch have left.** ⚠ Two are now empty and the entries are
refutations, not deferrals:

- [#132](https://github.com/Rikarin/SKALA/issues/132) — **nothing shippable remains.**
  `RedundantToStringCallForValueType` and `RedundantStringInterpolation`'s single-hole form were
  refused with reasons that still stand. `RedundantVerbatimPrefix`'s identifier form is `SK2034`'s
  concept. ⚠ `RedundantStringType` is **not a C# inspection at all** — JetBrains' own page for it is
  about a **resource entry in a `.resx` file**, where naming the string type restates the default
  entry type. There is no C# code shape to write a rule against. The export gives no hint of that:
  its entire description is "Redundant string type", which is why it sat on the queue looking like a
  language rule for as long as nobody opened the `WikiUrl` beside it.
- [#136](https://github.com/Rikarin/SKALA/issues/136) — **nothing shippable remains, and the reason
  changed.** Its two uncovered inspections are both `SK0210`'s, and the CS8933 story that put one of
  them on the queue is refuted above.
- [#133](https://github.com/Rikarin/SKALA/issues/133) — after `SK0250`, ten of thirteen.
  `RedundantFixedPointerDeclaration` is unsafe-code-only. ⚠ `RedundantPropertyParentheses`
  ("Parameterless property parentheses are redundant") and `RedundantArrayLowerBoundSpecification`
  ("Redundant array lower bound specification") are **Visual Basic**, not C#: neither
  `Property Foo()` nor `Dim a(0 To 5)` has a C# spelling, and the export mixes every language
  ReSharper inspects into one file — it also contains `ConvertToVbAutoProperty` and a thousand
  `CppClangTidy*` entries. #133 recorded them as "could not be identified from the export with enough
  confidence", which was the right caution and the wrong conclusion.
- [#134](https://github.com/Rikarin/SKALA/issues/134) — four of seven after
  `UnusedAnonymousMethodSignature` joined `SK0232`. `RedundantExplicitParamsArrayCreation` and
  `RedundantCallerArgumentExpressionDefaultValue` stay refused. `RedundantImmediateDelegateInvocation`
  is left: its fix inlines a lambda body into an expression position, which is a rewrite and not a
  deletion, and it does not belong under a title that says "argument or signature element".
- [#128](https://github.com/Rikarin/SKALA/issues/128) — still four of eight, and the crash under it
  was the finding. `RedundantTypeArgumentsInsideNameof` is real but **the fix needs C# 14**:
  `nameof(List<int>)` cannot become `nameof(List)` (`CS0305`), only `nameof(List<>)`, and unbound
  generics in `nameof` are `CS9202` below 14.0 — measured. Like `SK0250`'s floor, that is a separate
  id's problem rather than a branch of `SK0234`, and nobody has specified it yet, so no id is taken
  for it.

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

⚠ **`SK1060`–`SK1064` are registered here and the prose pass is owed.** The rows below take the
numbers and say what each rule rewrites; the paragraphs explaining why each is worth a rule, in the
voice the rest of this section is written in, have not been written yet.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1060` | `index-from-end` | `items[items.Count - 1]` | `items[^1]` |
| `SK1061` | `nameof-expression` | `typeof(Widget).Name`, `"count"` as `paramName` | `nameof(Widget)`, `nameof(count)` |
| `SK1062` | `escape-free-string-literal` | `"{\"id\":1}"`, `"\x41"` | `"""{"id":1}"""`, `"A"` |
| `SK1063` | `interpolated-string-form` | `string.Format("{0}", x)`, `$"{x.ToString()}"` | `$"{x}"` |
| `SK1064` | `unsigned-right-shift` | `(int)((uint)x >> n)` | `x >>> n` |
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

⚠ **`SK1090`–`SK1094` are registered here and the prose pass is owed.** The rows below exist so the
numbers are taken and readable; the paragraphs that say *why* each one is worth a rule, in the voice
the rest of this section is written in, have not been written yet.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1090` | `computed-property` | `public string S { get; } = "https";` | `public string S => "https";` |
| `SK1091` | `private-auto-property` | `private int Total { get; set; }` | `private int Total;` |
| `SK1092` | `tuple-literal` | `var p = new Tuple<int, string>(1, "a");` | `var p = (1, "a");` |
| `SK1093` | `cast-in-declaration` | `var w = (TextWriter)new StringWriter();` | `TextWriter w = new StringWriter();` |
| `SK1094` | `nullable-annotation-syntax` | `[CanBeNull] string Name` | `string? Name` |

⚠ **What makes `SK1090` shippable is one fact rather than a tighter guard.** The concept has failed
here before, and the wall was always that an auto-property is part of the type's *layout*: a
serializer or a source generator writes it by reflection with nothing in the source announcing that
it does, and Newtonsoft.Json writes a private setter by default with no attribute needed. The rule
reports only a **get-only** auto-property, whose backing field is emitted `initonly` — and .NET
Core's `FieldInfo.SetValue` refuses an init-only field, so no reflection path writes it and its
disappearance is not observable from outside the type. The initializer must additionally be a
compile-time constant by Roslyn's own folding, which is what makes evaluating it per read the same
program rather than an allocation per caller.

⚠ **`SK1091` is not hosted, and that was measured rather than assumed.** `IDE0032` and `IDE0044` were
the reason to expect it would be. On a plain `net10.0` project with `EnforceCodeStyleInBuild=true`
and no `AnalysisMode` raised, neither surfaces at its default severity; forced to `warning` both fire
— on **fields**, and `IDE0032` fires in the *opposite* direction, folding a field plus a trivial
property *into* an auto-property. Nothing in the SDK turns a private auto-property back into a field.
Reporting the zero without forcing the severities first would have been the disabled-check zero doc
16 warns about.

⚠ **`SK1093` and `SK0202` were checked against `VarRule`'s source, not against `rules.json`'s
summary — and the summary is wrong.** It says the arrangement rule "would use `var` **or** an
explicit local type under the configured preference"; `TypeInferenceRules.VarRule` converts explicit
type → `var` only, and returns on its first line for a declaration already written `var`. Skala has
no `var` → explicit arrangement rule at all. That settles the overlap in both directions: `SK1093`
reports only declarations already written `var`, which `VarRule` never looks at, and after the fix
the declared type is deliberately *not* the initializer's own type, which is the identity `VarRule`
requires before it converts.
⚠ **`SK1080`–`SK1084` are registered here and the prose pass is owed.** The rows below take the
numbers and say what each rule rewrites; the paragraphs explaining why each is worth a rule, in the
voice the rest of this section is written in, have not been written yet. Every one of them has its
full false-positive story in `rules.json` in the meantime, which is where the reasoning currently
lives.

| ID | Concept | Instead of | Use |
|---|---|---|---|
| `SK1080` | `of-type-over-filter-and-cast` | `xs.Where(x => x is T).Cast<T>()` | `xs.OfType<T>()` |
| `SK1081` | `redundant-sequence-call` | `seq.Cast<T>()` on an `IEnumerable<T>`, `xs.ToList().ToArray()` | `seq`, `xs.ToArray()` |
| `SK1082` | `indexer-over-element-at` | `list.ElementAt(i)` | `list[i]` |
| `SK1083` | `foreach-over-indexed-for` | `for (var i = 0; i < xs.Count; i++) … xs[i]` | `foreach (var x in xs)` |
| `SK1084` | `loop-filter-as-query` | `foreach (var x in xs) { if (p(x)) { … } }` | `foreach (var x in xs.Where(p)) { … }` |

⚠ **Three concepts in this batch were measured and closed as hosted rather than built, and two of
them were inside rules that shipped anyway.** `Count() > 0` → `Any()` — the `UseMethodAny` family,
five of the thirty-eight inspections issue #100 collects — is reported by **`CA1827`**, on
`Count()` and `LongCount()` alike, confirmed by compiling the shape on SDK 10.0.400 with
`AnalysisMode=All` and reading the warning off the build. `if (!set.Contains(x)) set.Add(x)` is
reported by **`CA1868`** in the same measurement. ADR-008 hosts rather than rebuilds, so neither is
in `SK1080` or `SK1081`, and the concepts named in those two issues ship one branch narrower than
the issues describe. The third, `RedundantDictionaryContainsKeyBeforeAdding`, was already `SK1033`'s.

⚠ **`MultipleOrderBy` is not a redundancy and was moved out of `SK1081` for that reason.**
`xs.OrderBy(a).OrderBy(b)` does not discard the first sort: `OrderBy` is stable, so the result is
ordered by `b` with ties broken by `a` — which is `xs.OrderBy(b).ThenBy(a)`, the arguments read
backwards. The rewrite is exact and it belongs to a correctness id, because the finding is that the
code says something other than what its author meant rather than that a call does nothing. No id is
allocated for it here; it is left in the queue as a correctness proposal.

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

⚠ **The prose pass for the attribute-contradiction batch is owed.** What follows is the allocation
register entry — enough that the ids are written down and
`RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister` can see them — not the worked-through
account the rest of this section carries.

**Four rules, one question asked of one pair of things: read an attribute, read the thing it is on,
and report where the two disagree.**

- `SK2100` `ineffective-thread-static` — `[ThreadStatic]` on a field that is not `static`, where it
  does nothing whatsoever, or on a `static` field with an initializer, where it does something
  worse. ⚠ **The initializer case is the one people actually hit.** The initializer runs in the
  static constructor, once, on whichever thread reaches the type first; that thread sees the value
  and every other thread sees `default`. Nothing throws and nothing warns, and the field reads
  correctly in the debugger on the thread that happens to be attached. `= 0`, `= false`, `= null`
  and `= default` are excluded because they assign exactly what the other threads already see, and
  a `const` is excluded because it must carry an initializer and is implicitly static.
- `SK2101` `pure-attribute-on-void` — `[Pure]` on a method that returns nothing. ⚠ **There are at
  least three different `PureAttribute`s and they do not mean the same thing.** The rule resolves
  by namespace-qualified name and accepts exactly two: the BCL's
  `System.Diagnostics.Contracts.PureAttribute`, which promises no visible state change and is
  therefore vacuous on a method with no result, and `JetBrains.Annotations.PureAttribute`, which
  means the return value must be used and is inapplicable where there is none. ⚠ The comparison is
  a *string* rather than `GetTypeByMetadataName`, which returns null when two assemblies declare
  the name — and JetBrains' annotations are routinely present twice, once from the package and
  once from a source-embedded `Annotations.cs`. A method with an `out` or `ref` parameter is
  declined: its results leave through the parameters. The annotation is load-bearing here
  specifically, because `SK2002` reads it to decide whether a discarded result matters.
- `SK2102` `debugger-display-missing-member` — a `{…}` hole in `[DebuggerDisplay]` naming an
  identifier that is not a member of the annotated type. ⚠ **The text inside the braces is a
  limited expression language, not a member name**, so the accepted grammar is one identifier, an
  optional `this.` in front and an optional all-letters format specifier behind — and anything else
  withdraws **the whole attribute**, not the one hole. The dotted path is the exclusion that
  matters most: `{Owner.Name}` needs the member's type to answer and `{DateTime.Now}` has a root
  that is not a member of anything, so reporting either would be wrong for a different reason each
  time. ⚠ **A sabotage that failed to fail moved this rule's member lookup.** It carried two guards
  — a walk of the implemented interfaces, and a match on the mangled name an explicit interface
  implementation is stored under — and breaking either one left every fixture green, because each
  masked the other: an explicit implementation requires the interface to be in `AllInterfaces`,
  where the member appears under its plain name. The mangled match was removed as having no case
  of its own, and a **default interface member** fixture was added, which is the one case only the
  interface walk saves. It ships without a fix: only the author knows which member was meant.
- `SK2103` `duplicated-attribute` — one declaration carrying the same `AllowMultiple` attribute
  twice with provably identical arguments.

⚠ **`SK2103` is where this batch's internal boundary is drawn, and it is drawn by construction
rather than by a filter.** The other three ask *does this attribute contradict the declaration it is
on?* and read the declaration to answer. `SK2103` never reads the declaration at all; it asks
whether two applications of one attribute say the same thing. Nothing it looks at is something they
look at, so no shape can reach both and no exclusion list is needed to keep them apart. Two fixtures
assert it rather than describing it: a type carrying two `[DebuggerDisplay]` strings where only one
names a missing member produces exactly one finding — `SK2102`'s, because differing arguments are
not a repetition — and a `void` method carrying both vendors' `[Pure]` produces exactly one finding
— `SK2101`'s, because two different attribute classes are not a repetition either.

⚠ **The general form of issue #269 is not what shipped, and the three sub-concepts left out were
left out because the compiler already has them.** An attribute on a target it does not declare is
`CS0592`; a repeat where `AllowMultiple` is false is `CS0579`; and an attribute naming a member that
does not exist is `CS8776` for the nullable-contract attributes — `[MemberNotNull("Nope")]` is a
**compile error**, which was the assumption this batch expected to be able to build on and measured
instead. Repetition with identical arguments is the one remaining case the compiler is silent
about, because for an `AllowMultiple` attribute it is legal. The fourth sub-concept — one attribute
contradicting another — is **not built and no id is allocated for it**: no decidable table of
contradicting pairs was found, and ADR-012 makes an id permanent, so a concept nobody has specified
must not take one.

⚠ **The `[Flags]` proposal (#214) was closed as hosted, and that is the outcome rather than a
shortfall.** All three directions ship in the SDK's own analyzers, measured on a probe project with
`AnalysisMode=All` rather than assumed: `CA2217` fires on a `[Flags]` enum whose members are neither
powers of two nor combinations of declared ones, `CA1027` fires on a non-`[Flags]` enum whose
members are powers of two, and `CA1008` fires both when a `[Flags]` enum has no zero member and when
its zero member is not named `None`. The same probe confirmed the legitimate exceptions are already
handled — an enum declaring `None = 0` and `ReadWrite = Read | Write` produced **no** finding from
any of the three. ADR-008 is host, never rebuild, so no `SK` id was allocated.
### `SK2040`–`SK2044` — equality and hashing

⚠ **The prose pass for `SK2040`–`SK2044` is owed.** What follows is the allocation register entry —
enough that the ids are written down and `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister`
can see them — not the worked-through account the rest of this section carries.

**They share one core and stay disjoint by construction rather than by filtering.** `EqualityMembers`
resolves a type's equality surface once — which equality members it declares, and which of its own
members each body reads — and `HashCodeContract` splits the members `GetHashCode` reads into two
halves. A hashed member is either compared by the type's equality, and then the only open question is
whether it can change, or it is not compared, and then the finding is about the contract itself.
Every hashed member is in exactly one half, both rules read the same split, and neither looks at the
other's output.

- `SK2040` `unintended-reference-comparison` — `==` on a source class that defines value equality and
  declares no `operator ==`. ⚠ The test is on the **bound operator**, never on a list of type names,
  which is what keeps records, `string`, `Uri` and every type with its own operator out with no
  exclusion list to maintain. The type must be declared in the compilation: `encoding ==
  Encoding.UTF8` reads as identity against a cached singleton, and calling that wrong needs
  knowledge of what the library guarantees.
- `SK2041` `base-equality-call-is-identity` — `base.Equals` or `base.GetHashCode` inside an equality
  member, where the call binds to `System.Object`. A base that overrides equality is doing real work
  and is never reported; a struct's `base.GetHashCode()` binds to `ValueType.GetHashCode`, which does
  hash the fields, and is not reported either.
- `SK2042` `hash-code-over-uncompared-member` — `GetHashCode` reads a member no `Equals` compares, so
  two instances the type calls equal hash differently.
- `SK2043` `mutable-hash-code-member` — `GetHashCode` on a class reads an equality-relevant member
  that can still be assigned after construction.
- `SK2044` `inconsistent-equality-members` — `Equals(Self)` with no `IEquatable<Self>`, or `==` and
  `IComparable` with no relational operators. One finding per type, in that order.

⚠ **`SK2042` reports the opposite direction from the one its issue named, and the issue's direction
was refuted.** Issue #6 asked for "the hash code does not include the members equality compares".
That direction is not a contract violation: a hash over a *subset* of the compared members still
gives every equal pair the same hash, which is the whole of what the contract asks, so it is a
collision-quality observation. The direction that does break the contract is the reverse — a hash
over a member `Equals` ignores — and that is what shipped.

⚠ **`SK2044` is in the correctness band and its issue proposed the design band.** Issue #212 named
`SK6000`–`SK6999`; the id came from a range reserved for this batch, and the three inconsistencies it
reports each make two spellings of one question give different answers, which is a wrong answer
rather than a taste. The disagreement is recorded here rather than resolved by moving the id, because
ADR-012 makes it permanent either way.

⚠ **Three sub-concepts were declined rather than shipped, and each refusal is worth as much as one of
the rules.** `CompareNonConstrainedGenericWithNull` — `t == null` on an unconstrained `T` — because
the repair is a constraint or an `EqualityComparer<T>.Default` call rather than an edit to the
comparison, and because the export itself ships it at `none`. `CheckForReferenceEqualityInstead`,
which is the mirror direction and asks what the author meant rather than what the code does.
And `S4035`, "classes implementing `IEquatable<T>` should be sealed": unsealed is not the defect, the
defect is an `Equals` that admits a derived instance through an `is T` test so that a base and a
derived instance compare equal in one direction only — a different analysis, with a different
false-positive story, that would need its own number and its own measurement.

⚠ **Two halves were built and then withdrawn, because a probe compiled against a real project
showed they were already reported.** `ReferenceEquals` on a value type is `CA2013`, on by default
with the same advice, so `ReferenceEqualsWithValueType` moved to the hosted map in `classify.py`
rather than into `catalogued.json`. `operator ==` with no `Equals(object)` override is `CS0660` and
`CS0661` — *compiler* warnings, always on, needing no analyzer package and no configuration — which
is the whole of what `SK2044`'s first sub-case reported. ⚠ **Both would have shipped had the probe
not been compiled**, because neither is in the hosted map and nothing in the fixture harness or in
the parity pipeline looks at what `csc` already says. `CA1036` was checked the same way for the
ordering half and does **not** fire at the SDK's recommended analysis level, which is the only
reason that one stands; a repository at `AnalysisMode=All` would host it too.

⚠ **A type whose base list did not bind is withdrawn from all five, and that guard came from the
measurement rather than from reading the code.** On the reference trees `SK2044` reported
`Vixen.Raven`'s `BufferTypeSymbol` for not implementing `IEquatable<BufferTypeSymbol>` — which its
base list declares on the same line. Without the SDK's implicit global usings the name binds to an
*error* type, so `AllInterfaces` holds `IEquatable<>`, the comparison against `System.IEquatable`1`
fails, and the rule states the opposite of the truth. Skala loads a compilation three ways and two
of them can be incomplete, so this is a live shape rather than a laboratory one. It cannot be a
fixture, because a fixture has to compile and the whole point is a name that did not resolve; it is
pinned as a unit test instead.

⚠ **Four of the five ship fixless**, which is four more than doc 08's bar contemplates and the same
decision `SK3040`–`SK3044` took. In each the repair is a choice between two edits that mean opposite
things — drop the member from the hash or add it to `Equals`; freeze the state or stop using the type
as a key — and no signal in the code says which was intended. `SK2040` carries the one fix in the
batch, rewriting the comparison to `Equals(a, b)`, and it is `fixIsSafe: false` because it changes
the answer.
⚠ **The prose pass for `SK2060`–`SK2064` is owed.** What follows is the allocation register entry —
enough that the ids are written down and `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister`
can see them — not the worked-through account the rest of this section carries.

**`SK2060`–`SK2064` are the family of expressions that read as something they are not**, and every
one of them has a legitimate form that is textually identical to the defect. That is what makes the
batch a batch, and it is why four of the five ship without a fix: in each case the repair is a
decision about which of two plausible programs the author meant, and a tool that guesses is worse
than one that reports.

`SK2060` `assignment-in-condition` — a simple `=` that is the *entire* condition of an `if`,
`while`, `do`, `for` or `?:`. ⚠ The discriminator is "entire": `while ((line = reader.ReadLine())
!= null)` assigns inside a condition and is correct, and `if ((ok = TryLoad()))` uses the
forty-year-old double-parenthesis convention to say the assignment was meant. Both are declined. ·
`SK2061` `identical-operands` — the same side-effect-free path on both sides of `&&`, `||`, `&`,
`|`, `^`, `-`, `/` or `%`. ⚠ The six comparison operators were dropped after measurement: `CS1718`
covers every comparison shape this rule could report, and § "The compiler already says it" now
records why the sentence that said otherwise was wrong. Floating-point operands are excluded
outright, because `x - x` is a NaN-preserving zero; properties are excluded because a getter is a
call and because `SK2012` owns the automatic-property case. · `SK2062` `repeated-condition` —
a later `else if` condition structurally equal to an earlier one in the same chain. ⚠ Sequential
`if`s are **not** compared: the first body usually changed the answer, so a repeat there is not a
defect. · `SK2063` `misleading-operator-sequence` — `x =- 1`, where an `=` is hard against a unary
`-`, `+` or `!` that is then spaced away from its operand. ⚠ Whitespace is the entire signal, which
is the one place in this catalogue where trivia, not structure, decides a correctness finding. ·
`SK2064` `non-short-circuit-boolean` — `&` or `|` between two non-nullable `bool` operands whose
right side has no side effect. ⚠ The only rule of the five that carries a fix, and the only one
whose worst failure would be catastrophic rather than noisy: `flags & Mask` on an integer or a
`[Flags]` enum must never be reported, so the rule reads the operand types and is `Semantic` for
that reason alone.

⚠ **Four fixless rules in one batch is more than doc 08's bar contemplates, and it is deliberate.**
For `SK2060` the two repairs — `==`, or parentheses around the assignment — are different programs.
For `SK2061` and `SK2062` the repair is *what the other side should have said*, which is the whole
content of the bug. For `SK2063` both `x -= 1` and `x = -1` are plausible readings. A fix in any of
these would be the tool choosing which bug it found.

⚠ **The batch was measured, and three of the five could be.** `SK2060`, `SK2062` and `SK2063` are
syntactic, so they run under `--load=loose` and the 4 459-file corpus is available to them; all
five ran over Skala's own tree under `--load=workspace`, which produced **0 CS diagnostics** here
and 590 findings across the catalogue. Every rule reports **zero** on both. ⚠ **Three of those
zeros are shape-absent and one is not.** Widening each rule to its bare shape and re-running finds
nothing at all for `SK2060`, `SK2061`, `SK2062` and `SK2064` on either tree — and **17**
occurrences for `SK2063`, every one of them in `Testing/corpus/unformatted/collapse/`, where
whitespace has been stripped on purpose and `GoalIndex = -1;` is stored as `GoalIndex=-1;`. The
shipped rule declines all 17 because the operand is not spaced away from the sign. ⚠ **A rule that
reads whitespace for meaning meets machine-mangled whitespace sooner than most**, and that guard is
the whole of the difference between a clean corpus and seventeen false positives on it.

⚠ **`SK2061` and `SK2062` compare expressions structurally with `SyntaxFactory.AreEquivalent`, not
textually.** Roslyn's comparison already ignores trivia and compares tokens and structure, so no
hand-written comparer was needed; a text comparison would call `if (a && b)` and `if (a  &&  b)`
different and would call two conditions sharing a sub-expression the same.
### The collection batch

⚠ **The prose pass on this block is owed.** These rules are entered here because a shipped id that
this document does not name fails `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister`; the
paragraph that places them against the rest of the range, and against `SK4030`–`SK4033`, has not
been written yet.

- `SK2080` `duplicate-initializer-key` — a set or dictionary initializer that writes the same
  constant key twice. ⚠ **The comparer is not resolved: the rule declines whenever the constructor
  is given any argument at all.** Key equality belongs to the collection's comparer, so
  `new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 1, ["A"] = 2 }` throws
  where the two keys are distinct ordinally. Declining on *any* argument is broader than declining
  on a comparer argument on purpose — it costs a capacity-only initializer a true finding, and the
  rule never has to decide which parameter a comparer arrived through. Constant keys only, a closed
  receiver table, and a key type whose default equality is decidable: `string`, `bool`, `char`, the
  integral types and any `enum`. ⚠ **Both spellings**: `new HashSet<T> { "a", "a" }` is a collection
  initializer and `HashSet<T> s = ["a", "a"]` is a collection expression, a different node kind, and
  a rule registered only on the first says nothing about the second — which is what a probe file
  dropped into a real project found after the fixtures were green. A spread anywhere in a collection
  expression withdraws the finding.

⚠ **`CA2244` overlaps `SK2080`'s indexer form, and only the indexer form — and it is not in the
default .NET analysis set.** Measured the way § "The compiler already says it" measures: an ordinary
`net10.0` SDK project with nothing configured reports `CA2200` on a control shape and says nothing
about any of the three duplicate-key forms. It fires in *this* repository only because
`Directory.Build.props` raises `AnalysisMode`. So a repository that has not raised it sees no
duplicate finding, which is the standard that let `SK3004` and `SK3501` ship past `CA2016` and
`CA2000`. `CA2244` also says nothing at all about the `Add` form — the one that throws
`ArgumentException` at construction — or about a set that silently drops an element.
- `SK2081` `collection-passed-to-itself` — a set, list or array given to one of its own members as
  the *other* collection: `set.UnionWith(set)` does nothing, `set.ExceptWith(set)` is `Clear()`
  written so nobody reads it as `Clear()`, `set.SetEquals(set)` is `true` with a hash walk in front
  of it, `list.AddRange(list)` doubles the list. ⚠ **The two sides must be the same *storage*, which
  is a symbol walk and not a text match** — `a.items` and `b.items` are one field symbol through two
  receivers — and every symbol on the path must be a local, a parameter or a field, because a
  property is an accessor call. ⚠ `a.Equals(a)`, `Concat`, `Zip` and `Array.Copy` are deliberately
  outside the table: reflexive equality is what an equality test asserts, `items.Concat(items)` is a
  legitimate "twice", and `Array.Copy(buffer, 1, buffer, 0, n)` is how a shift is written.
- `SK2082` `overwritten-collection-element` — one entry assigned twice inside a contiguous run of
  element writes to one collection, so the first value is computed and discarded. ⚠ **Roslyn's
  `AnalyzeDataFlow` answers questions about variables, not about indexed elements**, so there is no
  dataflow under this rule and it does not pretend to have any: the moment a statement that is not
  such a write appears between the two, or an invocation, `await`, lambda, nested assignment, `++`
  or `ref` argument appears inside one, the run ends. That is a fraction of `S4143` and it is the
  fraction that can be proved without a lattice. ⚠ "The keys are different" is a stronger claim than
  "the keys are not the same" and only two constants answer it, so an undecidable pair ends the run
  rather than being stepped over. ⚠ `ConcurrentDictionary` is out of the receiver table on purpose:
  another thread may read between the writes, so "nothing read it" is not a claim to make there.
- `SK2083` `provably-empty-collection` — a `foreach` over a local created empty that nothing in the
  member ever touches again. ⚠ **Proved by exhaustion, not by dataflow**: `AnalyzeDataFlow` answers
  questions about variables and not about a collection's contents, so the rule asks a question it
  can answer — *every* reference to the local inside its declaring member must be the subject of a
  `foreach`. A collection can only be filled through a member call, an assignment, or by being
  handed somewhere, and each of those is a reference of another kind, so one such reference
  withdraws the finding and the analyzer never has to decide what it does. The scan covers the whole
  member rather than running forward from the declaration, so the answer does not depend on
  ordering. Locals only: a field can be filled by anything holding the instance.

⚠ **The linear-search-in-a-set rule (#36) was refuted by measurement, and no id was allocated for
it.** The issue's premise — that `Enumerable.Contains` on a `HashSet<T>` reached through
`IEnumerable<T>` "binds to the O(n) extension rather than the O(1) member, so the data structure
chosen for lookup speed is used at list speed" — is **false on .NET**.
`Enumerable.Contains<T>(IEnumerable<T>, T)` opens with an `is ICollection<T>` test and delegates to
the collection's own `Contains`. Measured on this machine, 200 lookups of the last element of a
two-million-element collection reached through `IEnumerable<T>` take **0 ms** for every one of
`HashSet`, `SortedSet`, `ImmutableHashSet`, `ImmutableSortedSet`, `FrozenSet` and `Dictionary`
(the last through `Dictionary.Contains(kvp)`, which binds to the extension because
`ICollection<KeyValuePair<K, V>>.Contains` is implemented explicitly). The delegation is observable
as well as fast: a `HashSet<string>(StringComparer.OrdinalIgnoreCase)` reached through
`IEnumerable<string>` answers `Contains("ALPHA")` with `true`, which only the set's own comparer can
do. ⚠ **The one shape that really is linear is the three-argument overload** —
`Contains(value, comparer)` cannot delegate and scans: 20 calls over the same two million elements
take 300 ms. But passing a comparer is an explicit statement that *this* comparison is wanted rather
than the set's, there is no `HashSet<T>.Contains(value, comparer)` to redirect it to, and a rule
that reports something nobody can write differently is the failure mode `SK2034`'s note already
names. There is no rule here to build.
### Format strings, log templates and invisible characters

⚠ **The prose pass on this block is owed.** The rows below are the allocation register doing its one
job — recording that a number is taken — written as each rule landed rather than as a considered
section.

- `SK2070` `log-template-argument-count` — a Serilog template with a different number of holes than
  the call supplies values. ([#20](https://github.com/Rikarin/SKALA/issues/20))
- `SK2071` `log-template-duplicate-property` — a structured log template naming one property twice,
  for Serilog *and* for `Microsoft.Extensions.Logging`. ([#20](https://github.com/Rikarin/SKALA/issues/20))
- `SK2072` `invisible-character-in-literal` — a zero-width, bidirectional or control character
  written as itself inside a literal that could have escaped it.
  ([#183](https://github.com/Rikarin/SKALA/issues/183))
- `SK2073` `caught-exception-not-logged` — an error-level log inside a `catch` that never gives the
  logger the exception it caught. ([#238](https://github.com/Rikarin/SKALA/issues/238))

⚠ **`SK2073` is `SK7xxx` on its issue and `SK2xxx` here.** An entry that cannot be diagnosed from is
a defect in what the program observably does rather than a maintenance cost, and the argument that
puts `SK2014` — a `catch` that swallows without logging or rethrow — in the correctness band puts
this next to it.

⚠ **Two of this batch's concepts were closed as hosted rather than shipped, and the measurement is
the finding.** A probe project built at *default* analysis level — no `AnalysisMode`, no
`AnalysisLevel`, nothing but `dotnet build` — answers which `CA*` rules a repository actually gets:

| Concept | Host | On at default? |
|---|---|---|
| `string.Format` holes versus arguments ([#19](https://github.com/Rikarin/SKALA/issues/19)) | `CA2241` | **no** — needs `AnalysisMode` |
| `ILogger` template holes versus arguments | `CA2017` | **yes** |
| A logger placeholder that is only digits | `CA2253` | no |
| A logger template that is not constant | `CA2254` | no |

⚠ **`CA2017` was not in this repository's hosted map and is the strongest host in it.** It covers
`LoggerExtensions`, `ILogger.BeginScope` *and* `LoggerMessage.Define`, handles `{{` escapes and
`{X,10:N2}` alignment, and correctly declines a `params` array the call did not synthesise — all
verified against a probe rather than read from documentation. It is why `SK2070` is Serilog-only:
ADR-008 hosts `CA*` rather than rebuilding them, and the half of [#20](https://github.com/Rikarin/SKALA/issues/20)
worth an id is the half `CA2017` has never heard of.

⚠ **`CA2017` counts holes, not names.** `logger.LogInformation("{X} then {X}", a, b)` is silent under
every `CA` rule measured, in every analysis mode — which is what leaves the duplicate-property
concept unowned for `Microsoft.Extensions.Logging` as well as for Serilog.

### Culture, comparison policy and query shape

⚠ **The prose pass on this block is owed.** The rows below are the allocation register doing its one
job — recording that a number is taken — written as each rule landed rather than as a considered
section.

- `SK2150` `implicit-string-search-culture` — a `string` `IndexOf`, `LastIndexOf`, `StartsWith` or
  `EndsWith` taking a string and no `StringComparison`.
  ([#51](https://github.com/Rikarin/SKALA/issues/51))
- `SK2151` `invariant-culture-comparison` — an equality-shaped string operation given
  `StringComparison.InvariantCulture`, which is culture-*stable* and not culture-*free*.
  ([#252](https://github.com/Rikarin/SKALA/issues/252))
- `SK2152` `platform-dependent-path-comparison` — a provably path-valued string compared with a
  hard-coded case-insensitive policy. ([#260](https://github.com/Rikarin/SKALA/issues/260))
- `SK2153` `queryable-degraded-to-enumerable` — a deferred LINQ operator bound to `Enumerable` on an
  `IQueryable` receiver. ([#37](https://github.com/Rikarin/SKALA/issues/37))
- `SK2154` `sort-without-ordering` — a sort falling back to a `Comparer<T>.Default` that throws.
  ([#251](https://github.com/Rikarin/SKALA/issues/251))

⚠ **`SK2150` and `SK2151` were expected to close as hosted and did not, and the measurement is the
finding.** A probe project built at *default* analysis level — no `AnalysisMode`, no `AnalysisLevel`,
nothing but `dotnet build` on `net10.0` under SDK 10.0.400 — produces **zero** `CA` diagnostics over
a file containing every shape in this block. The same file at `AnalysisMode=All` produces 60. So the
`CA` rules exist, they are correct, and no consumer has them on:

| Concept | Host | Default | Recommended | All |
|---|---|---|---|---|
| Search with no `StringComparison` ([#51](https://github.com/Rikarin/SKALA/issues/51)) | `CA1310` | **no** | yes | yes |
| Equality with no `StringComparison` | `CA1307` | **no** | **no** | yes |
| `InvariantCulture` where ordinal was meant ([#252](https://github.com/Rikarin/SKALA/issues/252)) | `CA1309` | **no** | yes | yes |
| Missing `CultureInfo` | `CA1304` | **no** | yes | yes |
| Missing `IFormatProvider` | `CA1305` | **no** | yes | yes |
| Parameterless `ToLower`/`ToUpper` | `CA1311` | **no** | yes | yes |

⚠ **`CA1307` is off even at `Recommended`**, which is the row that would have been missed by asking
whether the rule exists rather than what it is set to. ADR-008's corollary — "Skala must be *worth
using with nothing hosted*" — is what these six rows decide, and they decide it the other way from
`CA2017`, which really is on by default and really did retire half of [#20](https://github.com/Rikarin/SKALA/issues/20).

⚠ **`SK2150` is the search half and `SK2010` is the comparison half, and the difference is not the
method list.** A comparison returns a truth value at the site that asked; a search returns an
*offset*, which a `Substring` then slices on, so the same culture-dependence arrives at the reader as
a truncated identifier several frames away. `CultureAndQueryShapeBatchTests` asserts the two rules
are disjoint in both directions rather than describing it.

⚠ **`SK2150`'s method table excludes most of `System.String` on purpose.** `Contains(string)` and
every `char` overload of the four names are *already ordinal* on .NET. Reporting them would be
advising the author to write down the behaviour they already have — and it is what a rule built from
the inspection titles rather than from the framework's documented behaviour would do.

⚠ **The issue for #51 says the fix is "the same fix `SK2010` already emits". `SK2010` emits no
fix at all** — it is `hasFix: false`, because for a comparison there is no way to choose between
ordinal, invariant and user-culture semantics. Search is different only because an offset into a
string is an ordinal question by construction, so `SK2150` does carry a fix; it is `fixIsSafe: false`
against the issue's proposal, because appending `StringComparison.Ordinal` changes what the program
computes and that is the whole point of the finding.

⚠ **`SK2152` is written from Skala's own position rather than from the upstream idea.**
`SarifWriter.PathComparison` is already
`OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase`, and
`CacheKeyPathTests` asserts the correct answer on all three platforms rather than skipping on two of
them. So the guidance is not "stop ignoring case" — it is "compare the path the way this file system
compares it", and the negative fixture for the rule is Skala's own idiom. An operand must be
*provably* a path — out of `System.IO.Path`, or off a `FileSystemInfo` — because name-based path
detection is how this kind of rule acquires its false positives.

⚠ **`SK2154` answers its issue's open question with a no: the sort is decidable only for a sealed
type.** `Comparer<T>.Default` for an unsealed `T` builds an `ObjectComparer` that casts each
*element* to `IComparable` at run time, so a `List<Animal>` holding a comparable `Dog` sorts
correctly even though `Animal` implements nothing. A type parameter is substituted at every call
site and is further out of reach still. And `Nullable<T>` implements neither interface itself while
`List<int?>.Sort()` works — through a dedicated `NullableComparer` — which is the false positive the
rule would otherwise have shipped with.

⚠ **Two of this batch shipped `hasFix: false`, and for `SK2153` the absence is the design.** The
repair for a degraded query is to change a delegate's declared type to `Expression<…>` at another
declaration, or to accept the materialisation by writing `.AsEnumerable()`. Inserting
`.AsEnumerable()` would silence the rule while keeping the defect, which is the one fix `skala fix`
must never have.

⚠ **`SK2153`'s first version reported `.AsEnumerable()` itself, and the rule's own prose had already
claimed that could not happen.** The claim was that the deliberate form is excluded "by construction,
not by a filter" — true of everything chained *after* the call, whose receiver is then an
`IEnumerable<T>`, and false of the call, which is an `Enumerable` extension returning
`IEnumerable<T>` on an `IQueryable` receiver and therefore the rule's exact shape. A rule that
reports its own escape hatch reports its own fix. The negative fixture caught it; the claim in
`rules.json` is corrected rather than deleted.

⚠ **`SK2154`'s LINQ arm shipped inverted and green, and no positive fixture could have seen it.** It
counted parameters against the *reduced* extension method, where `OrderBy(key)` presents one and
`OrderBy(key, comparer)` presents two — so a count of two selected exactly the overload that
*supplies* the ordering and missed every overload that does not. Every positive fixture stayed green
because `List<T>.Sort()` covered them: a positive fixture passes on one finding and never asks which
arm produced it. `CultureAndQueryShapeBatchTests` asserts counts for that reason, and carries the
`AD0001` check the fixture harness still omits ([#279](https://github.com/Rikarin/SKALA/issues/279)).

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

### `SK3060`– — entering, leaving, and escaping

⚠ **The prose pass for this block is owed.** The entries below are the register doing its minimum
job — recording that a number is spent and on what — and not the written-through account the rest of
this document gives. Whoever writes it should read the analyzers, not this list.

⚠ **This block was cut from five proposed issues and spends three numbers, because two of the five
are refuted rather than deferred.** The refutations are written out below the rules; they are the
part of this section worth reading, and ADR-012 is why neither of them is holding an id.

- `SK3060` `unreleased-lock` — a `Monitor.Enter`, `Monitor.TryEnter` or `ReaderWriterLockSlim`
  enter call whose *matching* release is not inside a `finally`. The `lock` keyword compiles to
  exactly that pair, which is the whole reason it exists, so this rule is only about the two calls
  written by hand: put the release on the happy path and the first exception inside the critical
  section holds the lock for the life of the process, deadlocking a thread somewhere else with
  nothing connecting the two. ⚠ **The mismatched-release bug falls out of the same mechanism rather
  than needing a branch** — `EnterWriteLock` released by an `ExitReadLock` has no matching
  `ExitWriteLock` in the `finally`, so it is reported by the rule already written. **Fixless**: the
  edit is to decide where the critical section ends, and wrapping the rest of the method in a `try`
  changes what the method does with its own exceptions. ⚠ **Stated scope limit**: `SemaphoreSlim`
  and the other primitives are out. A semaphore acquired in one method and released in another is
  how a semaphore is *meant* to be used, so the same shape means something different there, and the
  negative fixture set records that rather than hiding it. ⚠ A type with a deliberate
  `Acquire()`/`Release()` protocol withdraws entirely — that is recall spent on precision, and it
  is spent knowingly.
- `SK3061` `ineffective-lock-target` — a `lock` whose monitor is not the monitor another thread
  takes: a target created in the same invocation, so every call locks a different object and the
  critical section excludes nobody; or a private field the type assigns outside a constructor, so a
  thread that entered before the assignment and one that enters after are both inside the body.
  **Fixless**: introducing a shared lock object changes the type's shape and is a design decision.
  ⚠ **The rule is two-thirds of the issue that proposed it, and the missing third is deliberate.**
  See the `CA2002` measurement below.
- `SK3062` `constructor-publishes-this` — an instance constructor that stores `this` in another
  type's static state, hands it to an object reached through a static, subscribes it to an event
  that outlives it, or captures it in a delegate it starts on another thread. The object is not
  there yet: derived constructors have not run, and on a weak memory model the second thread can
  legally observe a field before its initializer — the failure that never reproduces on an x64
  desk. **Fixless**: the repair is a static factory that constructs first and publishes second,
  which is a change to how callers make the type. ⚠ **What it stays silent about is the rule.**
  `button.Click += OnClick;` in a constructor is the overwhelmingly common *legitimate* form and a
  rule that reports it fires on nearly every UI type ever written, so only publication whose second
  reader is outside the constructor's control is reported — static state, or a thread the
  constructor itself started. ⚠ **`current = this;` on the type's *own* static field is `SK2134`
  and not this rule**, excluded deliberately so that one line does not carry two findings.

⚠ **Issue #196's weak-identity third is hosted by `CA2002`, and that was measured rather than
assumed — including one thing `CA2002`'s own documentation does not say.** On a pristine `net10.0`
class library with empty `Directory.Build.props`/`.targets` above it, one shape per file:

| shape | default build | `CA2002` raised |
|---|---|---|
| `lock (this)` | silent | **fires** |
| `lock (typeof(T))` | silent | **fires** |
| `lock ("literal")` | silent | **fires** |
| `lock (stringField)` | silent | **fires** |
| `lock (Thread.CurrentThread)` | silent | **fires** |
| `lock (freshLocal)` | silent | silent |
| `lock (readonly object field)` | silent | silent |
| `lock (mutable object field)` | silent | silent |

⚠ The first two rows are the ones to know about. `CA2002`'s own shipped description says only that
"an object is said to have a weak identity when it can be directly accessed across application
domain boundaries", which reads as a rule about a handful of framework *types* and gives no reason
to expect `lock (this)` or `lock (typeof(T))` to be reported — they are, and that was found by
measuring rather than by reading. ⚠ **Its default state is
"off", not "hidden"**: the shipped descriptor reads `IsEnabledByDefault=False`,
`DefaultSeverity=Warning`, tagged `EnabledRuleInAggressiveMode`, and the SDK lists it only in
`analysislevel_10_all.globalconfig`. So it is invisible in an ordinary build **and visible in
Skala's own repository, which raises `AnalysisMode`** — which is precisely why re-implementing it
would double-report here first. The zeros in that table are instrument-checked: the same file
produced five `CA2002` findings once the severity was raised, so "silent" is the analyzer declining
and not the analyzer absent.

⚠ **Two false positives were found on the reference tree and in a probe, and both fixes are
subtle enough that somebody will otherwise re-add the finding.**

- **`SK3062` reported Vixen's `VideoPlayer`, and that finding was wrong.** The type is `sealed` and
  `thread.Start()` is the last statement of the constructor. `Thread.Start`, `Task.Run` and
  `ThreadPool.QueueUserWorkItem` all publish a **memory barrier**, so everything the constructor
  wrote before them is visible to the new thread — and a sealed type has no derived constructor
  left to run. Nothing changes after the publication, so nothing races. Shape D now requires
  something that still changes afterwards: a statement following the start at any enclosing level,
  or a type that is not sealed. ⚠ **The asymmetry matters as much as the gate.** Shapes A, B and C
  deliberately do *not* share it: storing a reference in static state is not a barrier and buys no
  ordering, and the defect there is that the object is **reachable at all** before the caller has
  the reference to undo it — not that it is unfinished. A gate copied across to them would delete
  the rule's best findings.
- **`SK3060` reported a `partial` type** whose `Acquire()`/`Release()` protocol is split across
  parts. The walk that looks for the release starts from the *syntactic* type declaration holding
  the enter, so it sees one part and not the others — and the parts are usually in different files.
  A partial type is now declined outright where it would otherwise report. Walking every
  `DeclaringSyntaxReference` instead would make the answer for one file depend on files the cache
  key does not name, which is what `scope: Compilation` costs and what this rule declines to pay.

⚠ **How that second one was nearly missed is worth more than the fix, and it is a new member of a
family this document already warns about.** The shape was predicted, written as a negative fixture,
and the suite was run — and the failures were read through `grep … | sort -u | head`. The `head`
cut the third line, which was the one naming the fixture, so the run looked like two unrelated
docs failures and the prediction looked refuted. It was nearly written up as "tested, does not
reproduce"; an independent probe printing the analyzer's actual output is what caught it. ⚠ The
standing rule is *never pipe a gate through `head` or `tail`, because the exit code becomes the
pager's*. **This is the weaker form and it bites the same way: truncated output silently answers a
different question than the one asked.** The rule is therefore not only about exit codes — do not
put a pager between yourself and a gate's output either, when reading it or when running it.

⚠ **Instrument check, because a zero from a rule that never ran and a zero from a rule that ran and
declined are the same zero.** A file carrying one of each shape was planted in the reference tree,
the tree was rebuilt, and it was re-swept with the shipped binary under `--load=binlog`: all four
fired, at the right lines, with the right messages. Removed again. ⚠ **And the planted sweep is not
a whole-tree measurement** — the rebuild behind it was *incremental*, so its binlog carries only the
projects that recompiled, and a finding elsewhere in the tree correctly did not appear in it. The
tree numbers come from a cold full build; the planted run proves only that the rules speak.

⚠ **"Zero CS diagnostics" was true of the build and not of the analysis, and the two are different
claims.** The cold Vixen build reports 0 CS errors and 0 CS warnings; `skala check --load=binlog`
over the same tree reported **546** `CS0103`/`CS0234`/`CS0246`, all confined to **three** files of
generated parser code that the binlog replay does not reproduce. Bounded, and no rule in this block
fires there — but a report that says "0 CS errors" without saying which of the two it measured is
saying nothing.

⚠ **Issue #57's remainder is refuted, and no id is spent on it.** `SK3044` already ships the
provable part of "the field is guarded on some paths and not others"; what #57 was left open for is
the *unguarded read*, and `SK3044`'s third gate declines it because `public int Count => count;` — a
deliberate best-effort snapshot — and a racing read are the same shape. Three ways to separate them
were considered and none survives. **(1) Counting the reads** — two unguarded reads of the same
guarded field cannot be one snapshot — separates `if (count > 0) return count;` from `=> count`, but
the thing it detects is that the code observed twice, not that the code was wrong to; an author who
accepted staleness accepted it for both reads. **(2) Reading the use** — whether the value is used
in a way that assumes it is still true — is the right question and is intent, which is not in the
tree. **(3) Reading the field's type** — a `decimal`, a `Guid` or a multi-field struct cannot be
read atomically, so a bare read can observe a value that was never written rather than one that is
merely stale — is the only candidate that needs no intent at all, and it answers a *different*
question: it fires whether or not the field is guarded anywhere, so it is not this concept, and it
says nothing about the `int count` that the issue is actually about. ⚠ **A guess here is worse than
a silence**: a wrong concurrency finding sends somebody to read threading code that was correct,
and §16 R3 prices that as the most expensive reading there is. `SK3044`'s recorded limits stand,
and "silence is not a claim" is still what they mean.

⚠ **Issue #250 is refuted, and no id is spent on it either.** "The constructor does more than
construct" is a judgement rather than a fact, and its decidable members are already placed. The
sharpest — a virtual or abstract call out of a constructor — is `CA2214`, measured on the same
pristine probe: it fires on a virtual declared on the same non-sealed class and on an abstract
call, correctly declines a sealed class, and is `IsEnabledByDefault=False` like `CA2002`. ⚠ It also
**misses a virtual inherited from a base and not overridden, called from a non-sealed derived
constructor**, which is a real gap and is recorded here rather than turned into a rule, because a
rule whose whole content is one shape another analyzer nearly covers is not worth a permanent
number. The next sharpest member — publishing to a thread the constructor starts — is `SK3062`.
What is left after those two is "a constructor that performs I/O", which fires on every
configuration loader and every type that wraps a file, has no mechanical fix, and says only "use a
factory". That is a style opinion, it belongs at `none` or nowhere, and doc 08 has cut `SK6xxx`
rules for less.

### `SK3050`–`SK3052` — async void wearing three disguises

⚠ **The prose pass on this block is owed.** The rows below are the allocation register doing its one
job — recording that a number is taken — written as the three rules landed rather than as a
considered section.

- `SK3050` `async-void-throw` — a `throw` inside an `async void` method or local function that
  nothing between it and the body's edge can catch. **Fixless.**
  ([#54](https://github.com/Rikarin/SKALA/issues/54))
- `SK3051` `async-method-without-cancellation` — an `async` method that accepts no
  `CancellationToken` and calls something that would have taken one. Fix: append the parameter,
  `fixIsSafe: false`. ([#256](https://github.com/Rikarin/SKALA/issues/256))
- `SK3052` `async-void-lambda` — an `async` lambda or anonymous method whose target delegate returns
  `void`, which makes it `async void` with no signature saying so. **Fixless.**
  ([#272](https://github.com/Rikarin/SKALA/issues/272))

⚠ **`SK3001` reports the signature; these report the three ways the harm arrives anyway.** `SK3001`
matches a `MethodDeclarationSyntax` whose return type is written `void`, and excludes the event
handler because its remedy — return `Task` — is not available to a method whose signature an event
declares. That leaves two holes and `SK3050` and `SK3052` are them: the handler `SK3001` correctly
declines still ends the process when it rethrows, and a lambda has no written return type at all, so
`Register(async () => await X())` against a `Register(Action)` is `async void` that no search for the
keyword finds.

⚠ **`catalogued.json` credited `AsyncVoidLambda` to `SK3001`, and `SK3001` cannot see a lambda.** It
registers on `MethodDeclaration` alone. The parity map is hand-written judgement, and doc 17 § "The
soft edge" says a *missing* entry inflates the residue — this is the other direction and it is worse:
a wrong entry marks a concept covered and takes it off the queue, so the gap it hides is one nothing
will ever count again. The entry now points at `SK3052`, and `AsyncVoidThrowException` — absent
entirely — points at `SK3050`.

⚠ **The three are disjoint by construction and the construction is the owner, not a `supersedes`.**
`SK3050` requires the nearest `async` owner to be a *declaration* that writes `void` in its own
source; `SK3052` requires a lambda or anonymous method, which writes no return type at all; `SK3005`
requires a *synchronous* body, which an `async` lambda is not. `AsyncVoidShapeBatchTests` pins each
pair on a fixture that satisfies **both** rules' shapes at once and asserts exactly one fires —
because two fixtures that differ in shape prove only that the shapes differ, which is true whether or
not either rule looks.

⚠ **`SK3051` and `SK3004` are the same argument at two points in the call graph, separated by a
count.** `SK3004` reports a call with *exactly one* `CancellationToken` in scope; a call is evidence
for `SK3051` only where there are *none*. Applying `SK3051`'s fix is what moves a body from the
second set into the first — the pair is a chain rather than an overlap, and the test asserts the
handover on one file rather than on two.

⚠ **The disjointness claim is per call and not per body, and two sabotages are why it is written
that way.** `SK3051`'s draft carried a second, method-level "declares no `CancellationToken`" check,
which would have made the stronger per-body claim true. Sabotaging it turned nothing red: the scopes
enclosing a call inside a body are a superset of the ones enclosing its declaration, so a method with
a token parameter cannot contain a call with none, and the check was dead code no sabotage could
kill. It is deleted, on `SK3043`'s precedent. ⚠ **Underneath it was a worse one.** The negative
fixture meant to pin the count declared its parameter as `cancellationToken`, so the CS0100 guard
silenced the rule before the count was ever consulted — the fixture was green and proved nothing.
Renaming the parameter to `token` made the count load-bearing, and only then did sabotaging it turn
the fixture red. Two guards, one hidden underneath the other, and only removing the first made the
second reachable.

⚠ **`SK3051` is the batch's expensive decision and the fix is what costs it.** Appending a parameter
— even an optional one — is CS0123 at any method group conversion, because optional parameters do not
participate in delegate conversion. Whether a method is used that way is not visible in the file that
declares it, so the rule takes `SK3001`'s compilation-wide identifier scan and its `scope:
Compilation` with it. The alternative was a fixless rule, and a rule that says "this cannot be
cancelled" without saying what to write is advice rather than a finding.

⚠ **Two of this batch's five issues were closed as hosted, and the measurement is the finding.**
A probe project at *default* analysis level — no `AnalysisMode`, no `AnalysisLevel`, nothing but
`dotnet build` on `net10.0` — answers what a repository actually gets:

| Concept | Host | On at default? |
|---|---|---|
| A `ValueTask` consumed more than once ([#199](https://github.com/Rikarin/SKALA/issues/199)) | `CA2012` | **yes**, at `info` |
| A synchronous call with an awaitable overload ([#200](https://github.com/Rikarin/SKALA/issues/200)) | `CA1849` | no — shipped and off |
| A `throw` out of an `async void` body | none | — |
| An `async` method that accepts no `CancellationToken` | none | — |
| An `async` lambda converted to a `void` delegate | none | — |

⚠ **`CA2012` is on by default and invisible, which is not the same as absent.** It is `info`, and
`dotnet build` prints no info-severity diagnostic and `-warnaserror` does not promote one — the only
readout that shows it is `-p:ErrorLog=…`. It fires on a `ValueTask` awaited twice, on `.Result` and
`.GetAwaiter().GetResult()` off both a call and a stored local, on two `AsTask()` calls, and on a
`ValueTask` returned and never consumed. ⚠ Its one measured gap is a `ValueTask` **parameter** awaited
twice, where there is no creation site to reason about; that is left unowned deliberately, because a
parameter's single-consumption contract is the caller's and not visible here.

⚠ **`CA1849` is off by default, and ADR-008's answer to that is to enable it rather than rebuild it**
— the same answer § "Format strings" reached for `CA2241`. Measured, it fires on `Stream.Read` and
`Stream.Write`, on `File.ReadAllText`, on `Thread.Sleep`, on a **user-defined** `Save()` beside a
`SaveAsync()`, inside an `async` lambda and inside a non-`async` `Task`-returning method. ⚠ **Its one
gap is `task.GetAwaiter().GetResult()`, which `SK3002` already reports** — so between the two the
concept is covered and there was no id left to allocate.

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
`SK5009` XML reader with DTD processing enabled · `SK5010` a pattern that can backtrack, run with
no timeout.

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

⚠ **The prose pass for `SK6050`–`SK6053` is owed.** What follows is the allocation record and the
one-line reason for each; the paragraphs that explain the batch the way `SK6020`'s and `SK6022`'s are
explained above have not been written. Each rule's `falsePositives` in `rules.json` carries the full
argument in the meantime, and `skala explain SK6050` prints it.

**`SK6050`–`SK6053` are members and signatures whose shape contradicts what they say.** A body that
does not do what the parameters promise, a base type that asks what it is, a sequence contract that
returns nothing at all, and a name that does not say whether the caller must await.

- `SK6050` `method-returns-a-constant` — a `private` method that takes arguments, reads none of them,
  and returns a compile-time constant.
- `SK6051` `is-check-against-this` — a class that asks whether `this` is one of its own subclasses.
- `SK6052` `null-returned-instead-of-empty` — a method whose return type is a sequence returning
  `null`.
- `SK6053` `async-suffix-convention` — a method returning an awaitable and not named `…Async`, or the
  reverse. Ships `defaultSeverity: none`; the count that decided that is in its `falsePositives`.

⚠ **A fifth concept in this batch — #211, "the field is exposed rather than a property" — took no id,
because `CA1051` already does it.** ADR-008 hosts rather than rebuilds, and the hosting was measured
rather than assumed: a probe carrying ten field shapes, built at `AnalysisLevel=latest-recommended`
(what `Directory.Build.props` sets), reports the `public` and the `protected` instance field and
correctly exempts a `[StructLayout]` interop struct, a `readonly`-adjacent `static readonly`, a `const`
and a `public` field on an `internal` type — including the interop exemption that is the load-bearing
one. The one thing `CA1051` does not reach is `S2357`'s `internal`-instance-field half, and that half
is declined rather than built: an `internal` field never leaves the assembly, so "it should have been
a property" is a style opinion about code no consumer can see. Recorded in
`Testing/parity-analysis/ledger-sonar.json` as `S1104` and `S2357` resolved `hosted`. **No id was
allocated** — ADR-012 makes one permanent, and a number handed out for a concept that ships nothing is
a number that can never be reused.

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

### Logging declarations — `SK7110`–`SK7119`

⚠ **The prose pass on this block is owed.** The row below is the allocation register doing its one
job, written as the rule landed rather than as a considered section.

- `SK7110` `logger-declared-for-another-type` — a type declaring an `ILogger<T>` field, property or
  constructor parameter whose `T` is neither itself nor one of its base types, so every message it
  writes is filed under another class's category.
  ([#237](https://github.com/Rikarin/SKALA/issues/237))

⚠ **`SK7110` is one of the four ids on its issue, and the other three are declined rather than
outstanding.** `S6669` — what the logger *field* is named — and `S1312` — whether it is
`private static readonly` — are naming and declaration conventions, and § "Reasons that justify a
cut" already holds that a preference with no consequence to show is not a finding. Serilog's
`Log.ForContext<T>()` is out for a different reason: naming a context other than the enclosing type
is what that method is *for*, so the same shape that is a defect in a declared `ILogger<T>` is
ordinary use there.

⚠ **It ships at `suggestion`, and the measurement that would have argued for `none` does not
exist.** Skala's own tree contains no `ILogger<T>` declaration at all — the shape is absent, verified
by grep and by a probe file that made the rule fire the moment it was inserted — so no Skala count can
show the rule is noisy in either direction. What settles the severity is that the finding is not a
preference: `SK7010` and `SK7101` are at `none` because a reader may reasonably disagree with them,
and nobody reasonably wants their messages filed under another class's name.
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
| Rules this document names | **325** | excluding band edges (`SK1000`–`SK1999` and the like), `SK3499`/`SK3500`, and `SK9xxx` |
| **Shipped** — present in `rules.json` | **290** | **89.5 %** |
| **Cut** — deliberately not built, reason recorded | **12** | § "Cut, with the reason" |
| **Retired** — allocated, superseded, never to be built | **1** | the id stays taken for ever (ADR-012) |
| **Outstanding** — planned, not built, not disposed of | **22** | includes the twelve declared cut with no reason recorded |

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

⚠ **`CS1717`/`CS1718` reach every storage path, and this sentence used to say "the identifier
spellings only", which is wrong.** The example it was built from is a *property*: `Prop = Prop`,
`other.Prop = other.Prop` and `other.Prop == other.Prop` do produce nothing, because a property
access is an accessor call. A **field** reached through a member access is a different matter and
is covered — `this.g == this.g`, `b.v == b.v`, `Box.Which == Box.Which`, `a == a` on a `string`
and `b == b` on a reference are all `CS1718`, measured at `net10.0` by
`ExpressionMisreadingBatchTests.TheCompilerCoversEveryComparisonSK2061CouldHaveMade`. ⚠ **That
cost `SK2061` its comparison half.** The rule was drafted over all six comparison operators on
the strength of the old sentence; since it reports storage paths and never properties, the
compiler covered every comparison it could have made and nothing was left over. It ships over
`&&`, `||`, `&`, `|`, `^`, `-`, `/` and `%`, where the compiler says nothing at all. `SK2012` now covers non-virtual
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

### Arithmetic, shift widths and constant-valued comparisons

⚠ **The prose pass over this section is owed.** It was written alongside the five analyzers and
records what they do; it has not been read back against the rest of this document, and the
surrounding sections have not been reconciled with it.

`SK2050`, `SK2051`, `SK2052`, `SK2053` and `SK2054` now ship:

| ID | Initial scope | Fix / default |
|---|---|---|
| `SK2050` | Integral division whose result reaches `float`, `double` or `decimal` | cast the dividend / warning, **not safe** |
| `SK2051` | Built-in integral arithmetic a constant operand makes an identity or a constant | remove the operation / warning, safe |
| `SK2052` | A constant shift count the operand's promoted width masks to a different count | none / warning |
| `SK2053` | A count or a length compared against a bound its non-negativity already decides | none / warning |
| `SK2054` | A signed `%` compared with `==`/`!=` against a non-zero constant | none / warning |

**What each does that `SK2001` does not.** `SK2001` folds a *relational comparison* whose answer the
operand *type's* range fixes, and it is the nearest neighbour to all five. `SK2050`, `SK2051` and
`SK2052` are about arithmetic rather than comparison and share none of its decision procedure.
`SK2054` is an *equality* comparison, which `SK2001` does not consider at all, and turns on the sign
of `%` rather than on any range. ⚠ `SK2053` is the one that had to be checked rather than assumed:
it is the same relational shape, and it is a different rule because a count is an `int`, whose range
decides nothing about zero. The extra fact is the framework contract that a count is never negative,
which lowers the bound from `int.MinValue` to `0`. `SK2053` computes `SK2001`'s answer first and
stands down when the type range already decides, so the two can never report one expression twice;
`ArithmeticAndRangeBatchTests.SK2001_AndSK2053_NeverBothFire` asserts both directions.

⚠ **`checked` does not change any of these answers, and that is why they can ship without reading
`CheckForOverflowUnderflow`.** `SK2050` reports a fraction discarded inside the division operator,
where no overflow arises. `SK2051` reports identities and constant results, neither of which can
overflow. The overflow half of the ReSharper concept behind `SK2050` — `IntVariableOverflow` and its
checked/unchecked variants — is **not** shipped and is discussed under "Refuted or declined" below.

⚠ **`SK2052` cannot be a syntactic rule.** C# masks a shift count rather than clamping it: `x << 32`
on an `int` shifts by `32 & 31`, which is zero, so the expression is its own left operand and not the
zero it reads as; the same text on a `long` masks with 63 and is a real 32-bit shift. A `byte` left
operand promotes to `int` and masks with 31. `nint`/`nuint` are excluded because their mask is 31 or
63 depending on the process, so no single answer about the same source is right.

⚠ **`SK2054`'s exclusion is the rule.** `%` takes the sign of its dividend, so `-5 % 2` is `-1` and
`value % 2 == 1` is false for every negative odd value — while `value % 2 == 0` is correct for both
signs and must never be reported. The dividend is taken as non-negative when its own type is unsigned
(so `someByte % 2 == 1` is left alone even though the operation promotes to `int`), when it is a
non-negative constant, when it is a count or a length, or when it is `Math.Abs`.

**Refuted or declined, with the reason.**

- ⚠ **The overflow and divide-by-zero halves of the `SK2050` concept are not shipped.** A constant
  zero divisor is already `CS0020` and a constant expression that overflows is already `CS0220`, so
  the compiler covers exactly the part that is decidable without flow analysis. What is left —
  a divisor that is zero on some path, a variable product that cannot fit — needs a range lattice
  with flow, which `SK2001`'s does not have. Shipping it over the constant cases alone would be a
  rule that is usually right, which is the failure mode doc 16 § R3 exists to prevent. `SK2050`
  therefore covers `PossibleLossOfFraction` and nothing else from that group, and the parity map
  credits it that narrowly.
- ⚠ **`MathClampMinGreaterThanMax` is declined for now and no id is allocated for it.** It is a live
  bug and it has no possible fix, and `FixRoundTripTests` requires *every* positive fixture of a
  rule with `hasFix: true` to carry edits — so it cannot live inside `SK2051`, which ships a safe
  fix. It needs either a fixless rule of its own or `SK2051` losing its fix, and ADR-012 says not to
  allocate a number for a concept nobody has specified yet.
- `MathAbsMethodIsRedundant` and `SuspiciousMathSignMethod` are not implemented: both reduce to
  constant folding the compiler already performs, and the non-constant form of `MathAbsMethodIsRedundant`
  needs the range lattice that is not there.
- ⚠ **`UselessComparisonToIntegralConstant` is `SK2001`'s, not `SK2051`'s**, and the parity map does
  not currently credit it to anything. That is a mis-credit in `catalogued.json` rather than a gap in
  the product; it is left for whoever owns the parity map.
- ⚠ **Roslyn does not report a cast's target as the operand's converted type.** `SK2050` was written
  on the assumption that it does and its `(double)(a / b)` fixture failed, which is how the
  assumption was caught. The enclosing cast is now read separately. A rule that had checked only the
  conversion would have missed the commonest wrong repair for this defect while looking complete.

Validation is 28 positive and 60 "should not fire" fixtures, exact per-fixture counts, an explicit
`AD0001` sweep over every fixture in the batch — an analyzer that throws is swallowed and then
produces nothing, so its negatives all pass and it reads as half-working rather than dead — plus
runtime demonstrations that `-5 % 2` is `-1`, that `1 << 32` differs between `int` and `long`, and
that `-0.0 + 0.0` is `+0.0`, which is why `SK2051` is integral-only.

#### The measurement, and what each zero means

⚠ **`--load=workspace` was unusable and the number it would have produced was a lie.** A workspace
load over this repository reported **4409 `CS` errors** — 2024 `CS0103` and 1835 `CS0246`, i.e. the
references never resolved — and every rule in this batch is `requiresSemantics`. A run against error
types under-reports silently, so the whole measurement was moved to a Release binlog
(`dotnet build Skala.slnx -c Release -bl:artifacts/skala.binlog --no-incremental`), which reports
**zero `CS` diagnostics**. That is the instrument check: the same command that gave the zero also
proves the compilation bound.

⚠ **The instrument was verified before the zero was believed.** A probe carrying all five shapes was
dropped into `Analysis/Rikarin.Skala.Analysis`, the binlog rebuilt, and all five rules fired on it,
one finding each. The probe was then deleted and the binlog rebuilt.

Against Skala's own solution, with the probe gone, all five report **zero**. Classified:

| ID | The zero | Counted how |
|---|---|---|
| `SK2050` | **Shape present, correctly declined.** 8 integral-looking ratio computations exist — `DuplicationModel`, `PreferenceSweep`, `Fidelity`, `ConstructReport`, `ArrangementDifferential` — and every one already casts or promotes before dividing (`(double)Agreed / Spans`, `100.0 * Plain / …`). The shape the rule reports is exactly the shape this tree already writes correctly | regex census over 458 own-source files, then each hit read |
| `SK2051` | **Shape absent.** The two textual hits are a documentation table and a string inside a test | same census |
| `SK2052` | **Shape present, correctly declined.** 3 real shift-by-32-or-more sites — `BreakPlan.Key`'s `(long)node.SpanStart << 32`, `BreakPlan`'s `key >> 32` on a `long`, `FuzzRandom`'s `1UL << 53` — and all three are 64-bit operands, where those counts are inside the width. ⚠ This is the width trap firing in the tree's favour: the identical text on an `int` is a defect | same census |
| `SK2053` | **Shape absent.** The two textual hits are a comment and a string inside a test | same census |
| `SK2054` | **Shape absent.** The five textual hits are documentation comments and strings inside tests | same census |

⚠ **The reference trees cannot measure any of these five, and a zero from them would not be a
zero.** `Testing/corpus/real` has no project files, so `--load=loose` skips every
`requiresSemantics` rule, and `skala.jsonc` excludes the path outright (#277). What is available is
the same textual census over its 1140 files, which is a reading of the source and not a run of the
rule:

- `SK2053` and `SK2054`: **no candidate lines at all** in Vixen, Serilog or Newtonsoft.
- `SK2052`: 6 candidate lines, every one a `long`/`ulong` operand — `(long)…Order << 32`,
  `value >> 63` — which the rule would decline.
- `SK2050`: 12 candidate lines, all already `float`-typed or explicitly cast.
- ⚠ **`SK2051`: 8 distinct sites in Vixen that the rule would report**, all of the shape
  `coordinates[(b * 2) + 0]` in `Distortion.cs`, `EnvironmentTexture.cs` and `MorphTargetData.cs`,
  where the `+ 0` is written for column alignment against a neighbouring `+ 1`. Every one is a
  **true** finding — the `+ 0` computes nothing — and every one is alignment somebody meant.

⚠ **That count is reported and the rule is not changed for it.** § "Never calibrate a rule to what
Vixen already does" applies in both directions: eight findings on an idiom is not evidence the rule
is wrong any more than zero findings would have been evidence it is right, and issue #8 proposes
`warning` on its own reasoning. The number belongs in front of whoever revisits the severity; it is
not a reason to carve an exception for index arithmetic.

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

### ⚠ `SK5010`, and the four security proposals measured against the SDK and refuted

⚠ **Owed prose: this section records one rule shipped and four `rule-proposal` issues refuted, and
every refutation below is a measurement rather than a judgement about taste.** The batch was five
SonarQube-derived proposals — issues #152, #146, #148, #140, #153 — chosen because each *looked*
decidable from a call site without taint analysis. That was a hypothesis, and it survived for one of
them.

⚠ **The first measurement was not of Skala but of the SDK, and it is the part worth keeping.** This
repository raises `AnalysisMode` in `Directory.Build.props`, so measuring here answers a different
question than the one a user's repository asks. The probe was a `dotnet new classlib -f net10.0` and
a `dotnet new web -f net10.0` on SDK 10.0.400, with an empty `Directory.Build.props` and
`Directory.Build.targets` above them to stop MSBuild's upward walk, built twice: once untouched, and
once with `-p:AnalysisMode=All`.

⚠ **The default state of every `CA*` in this area is `none`, and that is not the same as absent.**
The plain build emitted **0 warnings and 0 errors** across every shape below. The `All` build of the
same sources emitted 78 `CA1822`, 14 `CA5394`, 4 `CA5351`, 4 `CA5350`, 2 `CA2000`, 2 `CA1874` and
2 `CA1305` — which is what proves the plain zero is "off by default" and not "the analyzers never
ran". ⚠ `analysislevel_10_default.globalconfig` in the SDK contains exactly **one** `CA` entry
(`CA1516 = none`) and `analysislevelsecurity_10_default.globalconfig` is **empty**, so the default
severity of a security `CA` is whatever its own descriptor says, and for this family that is off.

| `CA*` | What it covers | Default on a plain `net10.0` project | Bearing on this batch |
|---|---|---|---|
| `CA5394` | Do not use insecure randomness | **off** (fires under `All`) | ⚠ Fires on **every** `System.Random`, hosts #140's shape |
| `CA5350` | Weak cryptographic algorithms (`DES`, `TripleDES`) | **off** (fires under `All`) | Already `SK5005`'s territory |
| `CA5351` | Broken cryptographic algorithms (`MD5`, `SHA1`) | **off** (fires under `All`) | Already read in this doc's `SK5005` note |
| `CA3012` | Review code for regex **injection** | **off**; declined even under `All` | ⚠ Not a timeout rule — different problem |
| `CA3006` | Review code for process **command injection** | **off**; declined even under `All` | Targets arguments, not the executable name |
| `CA3075` | Insecure DTD processing in XML | **off**; declined even under `All` | `SK5009` already ships the two-fact form |
| `CA1874` | Use `Regex.IsMatch` | **off** (fires under `All`) | A performance refactor, not security |
| `CA1875` | Use `Regex.Count` | **off** | A performance refactor, not security |
| `CA5359`/`CA5360`/`CA5361`/`CA5362` | Certificate validation, deserialization callbacks, SChannel, reference cycles | **off** | `SK5007` covers the validation half |
| `CA1842`/`CA1843` | ⚠ **Not regex rules at all** — `WhenAll`/`WaitAll` with a single task | **off** | Named in the brief for #152 in error |

⚠ **The three zeros that survived `AnalysisMode=All` were classified rather than reported.**
`CA3012`, `CA3006` and `CA3075` are all set to `warning` by `analysislevel_10_all.globalconfig` and
still produced nothing, which is the shape of an analysis that never ran. It ran: a `Canary.cs`
added to the same web project produced `CA5351`, `CA5394` and `CA1515` in the same build, so the
zeros are **shape present and correctly declined** — the two `CA3xxx` are taint rules and a minimal
API's query parameter is not among their recognised sources. ⚠ Either way neither is a host, because
neither detects the concept: `CA3012` is about an attacker-controlled *pattern*, and #152 is about
attacker-controlled *input* against a backtracking pattern the author wrote.

**`SK5010` ships. The other four do not.**

⚠ **#152 — the regular expression runs without a timeout — is the one the hypothesis survived.**
There is no `CA` for it: the SDK's complete catalogue is 281 rules, and searching every title for
regex, timeout, process, random, reflection and debug returns only the ten rows above. The finding
is decidable at the call site with no taint and no inter-procedural step, because the timeout is an
argument at the construction or at the static call, and both spellings of "I thought about this" —
a `TimeSpan` and `RegexOptions.NonBacktracking` — are visible in the same expression.

| Id | Scope | Default | Fix | Fixtures (+/−) | `corpus/real` (380 files) | `corpus/vulnerable` |
|---|---|---|---|---:|---:|---:|
| `SK5010` a pattern that can backtrack, unbounded | Semantic | **warning** | ⚠ none | 8 / 22 | **0** | 2 |

⚠ **`SK5010` does not report "a regex with no timeout", and the reference trees are the argument.**
Sonar's `S6444` reports every timeout-less regex; there are sixteen on `corpus/real` and **not one is
a vulnerability** — twelve are `Assert.Matches(new Regex(@"MaxLevels\s*=\s*4"))` in Vixen's own tests,
matching a fixed pattern against source the tool had just produced. So the rule reports only a
pattern it can read and prove dangerous: a compile-time-constant pattern, an **unbounded** outer
quantifier, and a group body that is exactly one quantified atom. ⚠ Serilog decided the second
condition: `(\.(?<argument>[A-Za-z0-9]*)){0,1}` in `KeyValuePairSettings` is a quantified group whose
body carries a quantifier — the shape a naive detector matches — and `{0,1}` cannot blow up. ⚠ The
third is narrower than "the body contains a quantifier" because `(abc*)+` matches the wider test and
is safe. The cost is coverage, stated rather than hidden: `^(\w+\s?)*$` is a real ReDoS the rule is
silent on.

⚠ **It ships at `warning` where the rest of the range ships at `error`, and not because it fires
more.** The other four are wrong unconditionally — a callback that returns `true`, a DES key, a
resolved DTD. A catastrophic pattern is a vulnerability only if something an attacker influences
reaches it, and the rule does not establish that; failing a build over it would assert a fact the
rule did not check.

⚠ **Zero on `corpus/real`, from a run proved to be live.** The 380 files were staged outside the
repository (`SK9023` puts the corpus out of `skala check`'s reach), built to a binlog — **13 036
`CS` errors, which is the corpus, not the rule** — and analysed with `--load=binlog`, because
`requiresSemantics: true` means `--load=loose` would have skipped the rule and produced a zero that
meant nothing. A planted `ZzCanary.cs` in the same compilation produced exactly two `SK5010`
findings and the 380 real files produced none, so the zero is **shape present and correctly
declined**: the trees hold 33 regex call sites and no pattern in them nests an unbounded quantifier.

⚠ **Four of ten sabotages against the analyzer turned nothing red, and three of them were the tests
rather than the rule.** The clauses were correct; nothing depended on them. The fixtures used
`\(a+\)+` and `[(*+]+`, where a scanner that ignored escapes or character classes still fails closed
inside `MatchingParen` and answers "no finding" for the right reason by accident. The discriminating
shapes are the ones where the misread *builds* a group rather than destroying one — `\(a+)+`,
`[(]+)+` and `[](a+)+]` — and each is now the only case that fails when its clause is removed. The
fourth was the sabotage harness's own filter, which did not run `RuleFixtureTests`. ⚠ Worth keeping:
a sabotage that survives is as likely to be a hole in the tests as a dead clause in the rule, and
telling those apart needs the shape that makes the clause load-bearing.

⚠ **#146 — the process is started by an unqualified name — is refuted, and Skala's own tree is the
evidence rather than the excuse.** `Process.Start("git")` resolving through `PATH` is only a
vulnerability if `PATH` is attacker-controlled, and that is a property of the *environment*, not of
the call site — so the premise that made this batch's shortlist is false for this one. The
consequence is measurable: this repository starts an unqualified process in six non-test places —
`Reporting/ChangedLines.cs:249`, `Analysis/SuppressionAuditor.cs:344`, `Analysis/CheckCommand.cs:591`,
`Formatting.CSharp/FormatCommand.cs:495`, `Testing/CliRunner.cs:49` and
`build/Rikarin.Skala.Release/SkalaTool.cs:47` — and **every one of them is correct**, because
hard-coding a path to `git` or `dotnet` is what would actually be wrong across three operating
systems. A rule at `error` firing on all six is doc 16 § R3's failure mode. ⚠ And the dangerous half
already ships: `SK5002` reports request data reaching a process start, which is the case where the
environment is not the attacker's lever.

⚠ **#148 — a debugging feature is enabled unconditionally — is refuted because most of it is not in
C#.** `<DebugType>`, `<Optimize>` and the launch profile are MSBuild and JSON, which an analyzer
over a compilation cannot see at all. What *is* reachable is `CompilationOptions.OptimizationLevel`,
and reporting that would fire on every `Debug` build ever made. That leaves one API,
`UseDeveloperExceptionPage`, and the probe confirms nothing in the SDK reports it unconditionally
called — 0 warnings under both `Default` and `All`. It is still refused, because the guard is
routinely one frame away: `if (app.Environment.IsDevelopment()) ConfigureDevelopment(app);` puts the
call inside a method that is unconditional *in its own body*, and separating that from a real finding
is the inter-procedural analysis this document puts out of scope. The asymmetry is `SK5003`'s
exactly: a rule that cannot see the guard fires.

⚠ **#140 — a predictable generator produces security-sensitive values — is refuted twice over, and
the second reason is the one that matters.** First, `CA5394` hosts the shape: it exists, it covers
`System.Random`, and ADR-008 says the answer to a `CA` that is off is to enable it. Second, and the
reason a targeted Skala rule is not the way out: `CA5394` fired **7 times** on a 7-use probe,
including on `Random.Shared.Next()` and on a `NextDouble()` returning a statistical sample, with no
security-context test whatsoever. So it is untargeted — but narrowing it needs to know whether the
bytes become a token, and this document already settled that question when it cut `SK5008`: "is this
identifier a token" is an identifier-name judgement. ⚠ The narrow concept named there — `Random`
output reaching a cipher key or IV — remains the obvious next allocation and is **not** `SK5010`'s
neighbour by accident; it needs the taint engine, which is why it is not in this batch.

⚠ **#153 — reflection is used to reach a non-public member — is refuted by a false-positive rate of
100 %.** `BindingFlags.NonPublic` appears in 26 files across the reference trees and in **zero**
files of Skala's own source. Every one of the 26 is in a population the proposal itself names as
legitimate: `newtonsoft/Newtonsoft.Json/Utilities` (a serializer reaching private setters, which is
what a serializer is for), `Newtonsoft.Json.Tests/Serialization` and `vixen/Core/Vixen.Ui.Tests`
(test code), and `vixen/Core/Vixen.Engine/Diagnostics/Overlays` (a diagnostics overlay whose whole
purpose is introspection). There is no call-site fact separating those from a real finding — the
discriminator is what the code *is for* — so the rule would ship at a measured zero true positives
and 26 false ones. That is the range's stated bar failing closed, and it is the right outcome.

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

## `SK2090`–`SK2093` — what a handler does with the exception it was handed

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section nine concurrent branches were editing.** What follows
is the register doing the one job ADR-012 needs it to do — the numbers are taken and written down
where the next milestone will read them. It is not yet the considered account the sections above
carry, and it belongs beside `SK2013`–`SK2017`.

Four rules about the same seam: the point where a program decides what to do with a failure it did
not want. Each one reports a place where that decision destroys the evidence.

- `SK2090` a `throw` that can escape a finalizer. The process ends, from the finalizer thread, with a
  stack naming the finalizer queue rather than the code that filled it. ⚠ **The recall decision is
  the rule.** `~T()` is a one-line `Dispose(false)` in the documented pattern, so reading only the
  destructor's block would be silent on nearly every real occurrence; the rule follows **one** call
  hop, into a method declared on the same type whose body is in this compilation, and stops. A
  `Dispose(bool)` inherited from another assembly, and a throw two calls down, are not reached — that
  is the price and it is stated rather than hidden. ⚠ **Without the `disposing` guard the rule would
  fire on every correct implementation of the pattern**: `if (disposing) { … }` is where the managed
  cleanup lives, the finalizer passes `false`, and a `throw` in that branch is unreachable from
  `~T()`. Report-only.
- `SK2091` a `throw` written inside a `finally`. It replaces the exception already in flight, so the
  failure that explains everything is destroyed and the log holds only the cleanup's complaint about
  the state that failure created. ⚠ **Only the explicit keyword.** A `finally` that calls something
  which *might* throw is every `finally` ever written — `Dispose`, `Close` and `Flush` all can — and
  a rule asking that question would fire across the tree and be switched off in a day. Syntactic, so
  it runs under `--load=loose`. Report-only.
- `SK2092` a `catch` clause naming `NullReferenceException`. ⚠ **`catch (Exception)` catches it too
  and is deliberately a different question**; folding the two together would make the finding
  unanswerable, so the rule fires only where the clause names the type itself. Syntactic, matching
  the three spellings a compiling program can use, which is what lets it run under `--load=loose`;
  the residual is a user-defined type of the same simple name, which exists nowhere on either
  reference tree or in `Testing/corpus`. Report-only.
- `SK2093` the handler names the exception, throws a different one, and never passes the first to the
  second. ⚠ **This is the third member of the `SK2014`/`SK2015` family and the most common of the
  three, because it looks like diligence.** It is not `SK2014`, which requires the block to be
  *empty*; and it is **disjoint from `SK7092` by construction rather than by filter** — `SK7092`
  requires the clause to propagate what it caught, this rule requires that it does not, so the two
  conditions are negations and no clause can produce both. The one rule of the four that ships a fix:
  the caught variable is appended as the trailing argument, `fixIsSafe: false` because it changes what
  a caller sees.

⚠ **A fifth concept was measured and closed as hosted rather than shipped, and no id was allocated
for it.** Issue #176 — "a general or reserved exception type is thrown", SonarSource's `S112` — is
covered by the SDK's own `CA2201`. Measured, not assumed: a probe throwing ten exception types builds
with `AnalysisMode=All` and `CA2201` reports nine of them, naming `Exception`, `SystemException` and
`ApplicationException` as "not sufficiently specific" and `NullReferenceException`,
`IndexOutOfRangeException`, `StackOverflowException`, `OutOfMemoryException`, `AccessViolationException`
and `ExternalException` as "reserved by the runtime" — a superset of the list the issue proposed. It
is off under `AnalysisMode=Default`, and ADR-008's answer to that is to enable it, not to rebuild it.
The one gap found is that `CA2201` matches the exact type, so a `Win32Exception` — which derives from
the `ExternalException` it does report — passes; that is not what the issue is about and does not
justify an id.

## `SK2120`–`SK2121` — switches, enums and conditions the compiler already decided

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section nine concurrent branches were editing.** What follows
is the register doing the one job ADR-012 needs it to do — the numbers are taken and written down
where the next milestone will read them. It belongs beside `SK2001` and `SK2009`.

Two rules shipped out of five issues, and ⚠ **the three that did not ship are the more useful half of
the result.** The batch was opened on issues #16, #28, #29, #1 and #169; three of them turned out to
describe something the compiler or the SDK already reports, and each refutation is a test in
`EnumAndTypeCheckBatchTests` rather than a sentence here, so the day a claim stops being true the
file goes red.

- `SK2120` a `|`, `&`, `^` or `~` applied to an enum **whose members carry no explicit values**.
  `enum Color { Red, Green, Blue }` numbers them `0, 1, 2`, so `Green | Blue` is `3` and no declared
  member at all; the value then matches no `case`, equals no member and prints as a number, and
  nothing in the toolchain says a word because the operation is legal C#. ⚠ **The trigger is implicit
  numbering, not the missing `[Flags]`,** and that is what keeps the rule honest: an enum whose author
  wrote `Read = 1, Write = 2` may be a bit set missing its attribute, and one explicit value anywhere
  declines the whole declaration. ⚠ **The rule is disjoint from `CA1027` by arithmetic rather than by
  a filter.** `CA1027` was probed against the SDK at `AnalysisMode=All`, not assumed: it needs at
  least three distinct non-zero values that are all powers of two, and it is silent on `{ A, B, C }`
  and on `{ A, B, C, D }`. A consecutively numbered enum reaches a third non-zero value only once it
  contains `3`, which is not a power of two — so no declaration this rule accepts can satisfy
  `CA1027`, and the two can never report one mistake twice. ⚠ **An enum from a referenced assembly is
  never reported**, because the evidence is the declaration's syntax and metadata has none; that is a
  stated hole with a fixture, not an oversight. Report-only: adding `[Flags]` is a public API change
  and is usually the wrong answer, because the defect is normally the combination.
- `SK2121` an `as` whose conversion always succeeds — the operand already converts to the tested
  type implicitly, so the operator returns the operand and the `null` it appears to guard against is
  the operand's own. ⚠ **This is the only part of issue #1 the compiler does not already own, and
  probing settled that rather than argument.** `d is Unrelated` and `s is int` are `CS0184`, `v is
  int` on an `int` is `CS0183`, `d as Unrelated` is **`CS0039`** and an unreachable type pattern in a
  `switch` is **`CS8121`** — the last two errors, so that code never reaches a linter at all. ⚠ **The
  always-*true* `is` check is still nobody's, and deliberately not this rule's**: `d is D` is `false`
  when `d` is null, so calling it redundant means treating a nullable annotation as a runtime
  guarantee, which `SK2001`'s rationale already refuses to do. `as` needs no such assumption. The one
  rule of the two with a fix, and `fixIsSafe: true` — but ⚠ **the fix is the matching cast, not the
  bare operand**: `var b = d as Base;` declares a `Base`, and rewriting it to `d` would declare a
  `Derived` and change what every member access below it resolves to. Only an identity conversion,
  where the type does not move, is replaced by the operand itself.

⚠ **Three concepts were measured and closed against existing diagnostics, and no id was allocated for
any of them.**

- **Issue #29** — "the switch arm is unreachable given the value's range" — is already a compiler
  **error**: `CS8510` for an arm, `CS8120` for a case, with `CS0031` where the constant does not even
  fit the operand type. Code that would reach such a rule does not build. What the compiler does not
  do is reason from a range the *flow* proved rather than the type — `if (x is >= 0 and <= 10)` and
  then `case 20:` draws nothing — and that residue needs the value lattice issue #169 asks for.
- **Issue #28** — "the enum `switch` has no default section" — is **`CS8524`** in its switch-expression
  form, and `CS8524` names exactly the concern the issue raises: a value of the input type that no
  arm handles and no member declares. ⚠ **The switch-*statement* form is what remains, and it is
  precisely the shape that gave `SK2009` its six false positives on Skala's own source (#280)** — a
  `switch` used as a non-exhaustive filter, where falling through means "do nothing" and is correct.
  Shipping it would have reproduced that defect with a second id.
- **Issue #169** — "the condition's value is already determined" — has its null half hosted by
  **`CA1508`**, verified on a probe that reports `'s is null' is always 'false'` and `'s != null' is
  always 'true'` after a preceding guard. `CA1508` is off under `AnalysisMode=Default`, and ADR-008's
  answer to that is to enable it. The measured residue is real but is not a rule: `if (flag) { if
  (flag) … }`, a constant local re-tested, and a collection reassigned before its count is compared
  all pass `CA1508`. Each needs a value lattice over flow, which is one build shared with the
  nullability rule rather than a rule per shape, and `SK2062` already covers the `else if` case.

⚠ **`SK2009` (#280) should adopt `SK2120`'s discriminator rather than a member-count threshold.** Of
the three boundaries #280 proposes, the measurement here argues against two. A member count is a
magic number that will be wrong for the enum just under it. "Declared in this compilation" does not
separate the six false positives from the three genuine findings, because `OptionValueKind` and
`SyntaxKind` are on the same side of it for a consumer of Roslyn. What does separate them is the
third: `JsonValueKind` is switched *as a value*, and `SyntaxKind` is switched *as a filter* — a
`switch` **statement** with no `default`, every arm returning the same value, and no `return` after
it that depends on the arm. ⚠ **And the compiler is already the boundary for half of it**: `CS8524`
and `CS8509` cover every switch **expression**, so `SK2009` could stand down on expressions entirely
and lose nothing — which alone removes the shape from a third of the false-positive sites and is the
same "stand down where another diagnostic has the answer" mechanism `SK2053` uses against `SK2001`.
Whichever is chosen, #280 is right that it needs a negative fixture built from a real `SyntaxKind`
filter.

**The measurement.** Both rules were swept over Skala's own source through a fresh Release binlog
(`--load=binlog`, **10 CS diagnostics in the load**, 1 286 results in total). Both report **zero**,
and ⚠ **neither zero is the absence of the shape and neither is the analysis failing to run.**

- The instrument was verified before the zero was believed. A probe file planting one `ProbeColor
  left | right` and one `derived as ProbeBase` into `Rikarin.Skala.Core` made both rules fire through
  the same binlog pipeline, at the right lines, with the right messages — which is the only check
  that sees a real reference set rather than the fixture harness's (#297). The probe was then deleted
  and the binlog rebuilt.
- `SK2120`'s shape is **present 37 times and declined 37 times**. Relaxing both guards and re-sweeping
  found 37 bitwise-operations-on-an-enum in Skala's source; the shipped rule reports none of them.
  Spot-checked rather than assumed: `RegexOptions` and `StringSplitOptions` are `[Flags]` enums from
  metadata, and `BraceOwners` is a `[Flags]` enum with explicit values whose flagged site is its own
  `All = Types | Methods | …` composite — declined by both guards independently.
- `SK2121`'s shape is **present 199 times and declined 199 times**. Every `as` in Skala's source is a
  narrowing — `ISymbol` to `IFieldSymbol`, `SyntaxNode` to a specific node type — which is what the
  operator is for.

⚠ **There is no corpus evidence for either rule, and that is a property of the rules rather than an
omission.** Both declare `requiresSemantics: true`, so `AnalyzerHost.SkippedFor` drops them under
`--load=loose` (#277), and `Testing/corpus` neither compiles nor is reachable through `skala check`
(`SK9023`). The self-sweep is the measurement, and #280 argues it is the stronger one anyway, because
Skala's tree actually compiles.
## Nullability — `SK2110`–`SK2113`

⚠ **The prose pass on this block is owed.** The rows below are the allocation register doing its one
job — the numbers are taken and written down where the next milestone will read them — and they were
written as each rule landed rather than as the considered account the sections above carry. They
belong beside § "SK2000 — Correctness" and are appended here only to keep out of a section several
concurrent branches were editing.

⚠ **Every one of these carries the same trap and it is worth stating once.** `GetTypeInfo` on a
written `TypeSyntax` answers `NullableAnnotation.None` for `string` and for `string?` alike — the
annotation of a *type reference* is not the annotation of the symbol it names — so a rule that
compares annotations there is silent on every input and looks exactly like a rule with nothing to
find. The question is `Nullability.FlowState`. And the third flow state, `None`, is what every
expression has in a nullable-oblivious compilation: it is neither `MaybeNull` nor `NotNull`, so
`!= MaybeNull` silently treats the unmigrated world as proven non-null and `== NotNull` withdraws
from it. Which is right differs per rule here, which is why `NullabilityFacts` exposes the context
rather than a verdict.

- `SK2110` `tostring-can-return-null` — an override of `object.ToString()` returning a null constant,
  reported only where the compiler is silent. ⚠ **The boundary against `CS8603` is the rule, and it
  was probed rather than assumed**: the .NET 10.0.400 SDK reports `CS8603` on
  `override string ToString() { return null; }` under NRT and is silent on
  `override string? ToString() => null;` — legal because `object.ToString()` is itself annotated
  `string?` — and on the same return under `#nullable disable`. ⚠ It reads `GetConstantValue`, never
  a flow state, which is why it works in the nullable-oblivious files that are half its reason to
  exist. ([#160](https://github.com/Rikarin/SKALA/issues/160))
- `SK2111` `inert-null-suppression` — a `!` standing where no nullable warning could have been
  issued: warnings off at that position, or a non-nullable value-type operand.
  ([#192](https://github.com/Rikarin/SKALA/issues/192))
- `SK2112` `nullable-local-never-null` — a local declared `T?` on a reference type, assigned once
  with a value the flow analysis proves non-null and never assigned again.
  ([#118](https://github.com/Rikarin/SKALA/issues/118))
- `SK2113` `null-forgiven-service-resolution` — `provider.GetService<T>()!` where
  `GetRequiredService<T>()` moves the failure to the line that asked for the service.
  ([#275](https://github.com/Rikarin/SKALA/issues/275))

⚠ **`SK2111` ships half of its issue and the other half is cut for a defect rather than for noise.**
`S8969` — a `!` on an expression the compiler already proves non-null — was specified and dropped,
because two things it cannot see make it wrong. A `!` may be suppressing a *nested* nullability
warning (`List<string?> a = b!;` suppresses `CS8619`) that the operand's own flow state says nothing
about; and removing one `!` can make another necessary, since in `x!.A(); x!.B();` the second
operand is non-null precisely *because of* the first suppression — so a rule reporting both hands
`skala fix` a pair of edits that together reintroduce the warning. What ships needs no flow analysis
at all and so cannot have either problem. `Testing/.../SK2111/negative/proven_not_null_is_out_of_scope.cs`
is the record of the cut.

⚠ **`IDE0080` does not host `SK2111`, and the check was an instrument first.**
`CSharpRemoveUnnecessaryNullableWarningSuppressionsDiagnosticAnalyzer` ships in the .NET 10.0.400
SDK, and a probe carrying all three shapes reported nothing from it under
`EnforceCodeStyleInBuild=true`, `AnalysisMode=All` and
`dotnet_diagnostic.IDE0080.severity = warning`. That silence means something only because the same
build reported `IDE0090` and `IDE0059` from the same analyzer set under the same `.editorconfig`,
and because `dotnet format analyzers --diagnostics IDE0080` reported nothing on the same files while
`--diagnostics IDE0090` reported the control.

⚠ **`SK2112` ships one of its issue's two inspections and the other is cut because an analyzer
cannot answer it.** `ReturnTypeCanBeNotNullable` narrows a *method's* return annotation, which
propagates to every call site through `var`: `var x = M();` infers `string?` today and `string`
afterwards, so a later `x = null` becomes a new warning in a file the analyzer never saw. A
`DiagnosticAnalyzer` is handed one syntax tree and cannot enumerate callers; ReSharper answers it
from a solution-wide index and Skala has none at analysis time. `VariableCanBeNotNullable` is what
ships, and the same cascade *inside* one method is guarded directly by declining any local that
feeds a `var` declaration.

⚠ **A fifth concept was measured and refuted, and no id was allocated for it.** Issue #47 — "the
expression is null on a path that dereferences it", ReSharper's `PossibleNullReferenceException`,
`ExpressionIsAlwaysNull`, `ConstantConditionalAccessQualifier` and
`PossibleInvalidOperationException` — is hosted by the compiler wherever nullable reference types
are enabled. Measured, not assumed: a probe carrying one shape per inspection, built with the .NET
10.0.400 SDK, produced `CS8602` for the dereference of a `string?`, `CS8600` for its assignment to a
`string`, `CS8604` for passing it to a non-nullable parameter and `CS8629` for `.Value` on an `int?`
— all four inspections' ground, from the compiler, at `warning`. What is left is the `#nullable
disable` case, and that is not a gap this catalogue can fill: with the context off, every
expression's `NullableFlowState` is `None`, so the compiler's own flow analysis has not been run and
there is nothing to read. Reporting there would require a null-value dataflow engine that Skala does
not have and that no rule in this document assumes. ⚠ **The one thing that does survive there was
also measured**: `CA1062` fired on the public methods of the same probe's `#nullable disable` file,
so the argument-validation half of the concept is already covered by an analyzer the SDK ships, and
ADR-008's answer to a check being off is to turn it on rather than to rebuild it. The residual — a
local definitely assigned `null` and then dereferenced, in a nullable-oblivious file — drew nothing
from any analyzer in the probe and is the honest size of the gap.

⚠ **The batch's measurement is four zeros and all four are "shape absent", which is weaker evidence
than a zero usually is.** The sweep is `dotnet build Skala.slnx -c Release --no-incremental` with a
binary log, then `skala check --load=binlog --require-fresh-binlog` over the four ids. **The load
carried 0 `CS` errors and 10 `CS` diagnostics in total** — all `CS9335`, "the pattern is redundant",
at hint — so the semantic model the rules read was a real one, not the ~4 400-error load that
`--load=workspace` produces on this tree (#284). The result was zero findings, exit 0.

⚠ **The instrument was verified before the zero was believed.** A probe carrying one occurrence of
each shape was compiled into `Tools/Rikarin.Skala.Cli`, and the same sweep reported all five: `SK2110`
on `public override string? ToString() => null;`, `SK2111` twice — once for an `int` operand and once
inside `#nullable disable warnings` — `SK2112` on a `string?` local, and `SK2113` on
`GetService<IFormatProvider>()!` **against the real `Microsoft.Extensions.DependencyInjection`
reference this project already carries**, which is the reference set the fixture harness cannot give
(#297). The probe was then deleted and the sweep re-run to get the zero.

What each zero means, counted rather than assumed:

- `SK2110` — **shape absent.** `Directory.Build.props` sets `Nullable=enable` for the whole
  repository and there is not one `#nullable disable` directive in its production code; there are
  eight `override string ToString()` and no `override string? ToString()`. Both of the rule's two
  contexts are missing.
- `SK2111` — **shape absent, in both branches.** The warnings-disabled branch cannot fire in a tree
  with no nullable-oblivious position; the value-type branch found no `!` on a non-nullable value
  type. The ~90 lines that do carry a suppression are on reference types, which is the `S8969`
  ground this rule declines by design rather than by accident.
- `SK2112` — **shape absent, and this is the one worth stating precisely.** Skala's own tree does
  contain nullable locals; **every one of them is initialised with `null` or `default`**, so the
  rule's precondition — an initialiser the flow analysis proves non-null — holds nowhere.
- `SK2113` — **shape absent.** No `GetService<T>()` or `GetKeyedService<T>()` call exists in the
  production code at all; the only occurrences in the repository are this batch's own fixtures, which
  `skala.jsonc` excludes.

⚠ **The vendored reference trees could not be measured and reporting their zero would have been
worse than reporting nothing.** `Testing/corpus/real/` holds Serilog, Newtonsoft.Json and Vixen as
loose sources with no project file, so the only load available is `--load=loose` — which skips every
rule declaring `requiresSemantics`, and all four of these do (#277). A loose run would have printed
four zeros that mean "the analysis never ran". The false-positive evidence for this batch is
therefore its negative fixture set (9, 8, 10 and 9 files) and the probe, and not a corpus count.
## `SK2140`–`SK2143` — what a parameter promises and what the call site does
## `SK2130`–`SK2134` — members, backing fields, and the order things are initialized in

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section several concurrent branches were editing.** What
follows is the register doing the one job ADR-012 needs it to do — the numbers are taken and written
down where the next milestone will read them. It is not yet the considered account the sections above
carry, and it belongs beside `SK2013`–`SK2017`.

Four rules about one seam: the gap between what a parameter list declares and what the compiler
actually does with it at the call site. The declaration is the thing everybody reads; the call site
is the thing that runs.

- `SK2140` an override or an implicit interface implementation declaring a parameter default the
  call site will not use. A default is baked into the caller from the **static** type of the
  receiver, so a base-typed reference gets the base's value and a derived-typed one gets the
  override's, and the same member behaves as two methods depending on which reference the caller
  happens to hold. ⚠ **The compiler is silent on exactly the cases that diverge and loud on the one
  that cannot.** `CS1066` reports a default on an *explicit* interface implementation — a member that
  can never be called with optional arguments at all — and says nothing about an override or an
  implicit implementation. Measured on a probe build, not assumed; the explicit case is declined and
  hosted. ⚠ **`params` was measured too and the issue proposing this rule had it backwards.** An
  override cannot change `params`: dropped, the call still expands through the derived type; added
  where the base has none, it expands through neither — both were compiled and both answers read off
  the build, and Roslyn agrees at the symbol level by propagating the base's `IsParams` onto the
  override's parameter even where no keyword is written. So the `params` half of this rule reports
  interface implementations only, where nothing is inherited and the divergence is real. ⚠ **One
  finding per declaration carrying every edit**, because optional parameters must form a suffix and
  repairing one of two on its own is `CS1737` — `skala fix` applies one finding at a time, so
  per-parameter findings would break the build between the first edit and the second. Fix,
  `fixIsSafe: false`.
- `SK2141` an argument that suppresses the caller-info substitution it sits on top of. Supplying a
  value for `[CallerMemberName]` or `[CallerLineNumber]` makes the compiler step back without a word,
  so a fabricated source location ends up in every log line and exception built from it. ⚠ **The
  general shape does not ship and the narrowing is the whole rule.**
  `OnPropertyChanged(nameof(Other))` is ordinary correct code in every view model ever written, so a
  name or expression argument is a finding only when it restates *exactly* what would have been
  substituted; a location argument has no equivalent deliberate use and is reported for any constant.
  Forwarding needs no guard of its own — a relay passes an identifier and an identifier is not a
  constant. ⚠ **Disjoint from `SK0232` by construction rather than by filter**: `SK0232` excludes
  caller-info parameters outright, because passing `null` to one is the opposite of redundant, and
  nothing here reports `null`. A trailing run goes in one edit, for the reason `SK0232` gives about
  its own trailing defaults. Fix, `fixIsSafe: false`.
- `SK2142` a parameter every path assigns before anything reads it, so the caller's value was
  computed, passed and discarded. The verdict is Roslyn's data flow rather than a syntactic scan,
  which is what makes `if (f) { x = 1; }`, `x += 1`, `x++` and `x ??= y` silent without a guard for
  each. ⚠ **The `ref`/`out`/`in` exclusion is load-bearing rather than cautious**: an `out` parameter
  has no incoming value *by contract*, so data flow reports every correct one with the defect's exact
  signature — measured before the rule was written. ⚠ **A captured parameter is where the analysis
  stops and it says so**, because data flow over a body holding a lambda or a local function is not
  ordered the way its reader is. Parameters only; the caught exceptions and `foreach` variables the
  originating issue also named are separate shapes with their own legitimate uses. Report-only, for
  the reason `SK2090`–`SK2092` are: the repair is a choice between "the caller's value was meant to
  be used" and "this wanted a local", and the rule cannot know which.
- `SK2143` two adjacent arguments handed crosswise to the two parameters they are named after. ⚠
  **`Copy(source, destination)` called as `Copy(destination, source)` is undetectable in general and
  this rule does not try.** The only sound signal is the crosswise *name* match — adjacent
  parameters, identical types, plain identifiers, names of at least three characters — and everything
  looser reports ordinary code. `Max(y, x)` is where reversal is deliberate and a one-letter name is
  no evidence either way. ⚠ **A call to a method of the enclosing member's own name is declined**,
  which is the guard for the deliberate reversal that is genuinely correct: a descending comparer
  written as `Compare(right, left)` delegating to an ascending one is this shape exactly, and
  swapping it back is the one edit that would break it. Fix, `fixIsSafe: false` — the finding is a
  question about intent and the rule is wrong about it in precisely the cases where the author meant
  the reversal.

⚠ **A fifth concept was measured and closed as hosted by the compiler, and no id was allocated for
it.** Issue #31 — "the parameter name differs between partial declarations", ReSharper's
`PartialMethodParameterNameMismatch` — is reported by Roslyn itself at default settings. Measured,
not assumed: a probe declaring mismatched names across partial declarations builds with `CS8826`
("Partial method declarations … have signature differences") for both classic and extended partial
methods, including a difference of case alone, and with `CS9256` ("Partial member declarations …")
for partial indexers and partial constructors. The brief that carried the issue expected `CS8826` to
cover only extended partial methods and expected a residue; there is none. ⚠ **The same probe also
refuted the claim that the implementing declaration's names are the ones callers see.** They are the
defining declaration's: `Take(defining: 1)` compiles and `Take(implementing: 1)` is `CS1739`, which
is what the issue itself said and the opposite of what the brief asserted.
## `SK2160`–`SK2164` — time, clocks and the assertion that changes the program

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section several concurrent branches were editing.** What follows
is the register doing the one job ADR-012 needs it to do — the numbers are taken and written down where
the next milestone will read them — together with the measurements that decided two of the five. It is
not yet the considered account the sections above carry, and it belongs beside `SK2010`, which is the
rule this batch most often argues from.

⚠ **All five are `Correctness`, and four of the five issues proposed `SK1000`–`SK1999` instead.** The
band decides the category, and on reflection the band is right: none of these reports code that is
merely written in an older dialect. Each one reports a program that computes a **wrong answer** — a
different day, a different instant, a negative duration, a value that exists in one build and not the
other. Time is a correctness subject here, not a modernization one.

- `SK2160` **the clock is read from a static** ([#242](https://github.com/Rikarin/SKALA/issues/242),
  `S6354`). Ships **`defaultSeverity: none`**; the justification is below, and it is a measurement
  rather than a preference. The whole analyzer withdraws where `System.TimeProvider` does not resolve,
  because below .NET 8 the repair it names does not exist. Test code is excluded outright, which is
  what makes it disjoint from `SK8007` by construction rather than by filter. Report-only: the repair
  introduces a constructor dependency and changes every call site, which is a workspace refactor and
  not the text edit ADR-005 defines — `SK6053`'s argument, unchanged.
- `SK2161` **the `DateTime` has no time zone and is converted as if it had one**
  ([#243](https://github.com/Rikarin/SKALA/issues/243), `S6562`/`S6563`/`S6566`). ⚠ **Reporting every
  `DateTime` would be absurd, so the rule reports the escape and never the value.** A value whose zone
  nobody stated is not wrong while it is only compared with others of the same unstated zone; it goes
  wrong at the point something turns it into a fixed moment, because that conversion supplies an offset
  it was never given. Three sinks: `ToUniversalTime()`, `ToLocalTime()`, and the
  `DateTime`-to-`DateTimeOffset` conversion in both spellings. ⚠ **The implicit conversion needs the
  operation tree rather than the syntax** — `DateTimeOffset when = built;` contains no `new` and no
  cast — and it is the commonest spelling, so a syntax-only rule would have missed most of the concept.
  ⚠ **The sharpest fact about this defect is that the two conversions disagree**: `ToUniversalTime()`
  reads an `Unspecified` value as local, `ToLocalTime()` reads the same value as UTC. Report-only,
  because which zone the author meant is the entire content of the finding.
- `SK2162` **the date or time `TryParse` has an implicit culture**
  ([#244](https://github.com/Rikarin/SKALA/issues/244), `S6580`). ⚠ **Only the quarter of the issue
  that has no host, and the boundary was measured on a pristine `net10.0` project rather than on this
  repository, which raises `AnalysisMode`.** See § "the `CA1305` boundary" below. Report-only, and
  `SK2010` is the precedent rather than an inconsistency: the repair is to name a culture, and which
  one — invariant for a machine-readable stamp, current for something a person typed — is the finding.
- `SK2163` **elapsed time is measured with the wall clock**
  ([#245](https://github.com/Rikarin/SKALA/issues/245), `S6561`). The one rule in the batch that ships
  a fix: the start local becomes `System.Diagnostics.Stopwatch.StartNew()`, its declared type becomes
  `var`, and the subtraction becomes `.Elapsed`. ⚠ **Both ends must be the process's own clock reads,
  and that requirement is the whole rule** — `DateTime.UtcNow - order.PlacedAt` is "how old is this
  order", a legitimate question about civil time that a `Stopwatch` cannot answer. ⚠ **The fix's
  preconditions are the rule's preconditions, deliberately**: `hasFix: true` is a promise about every
  finding, so a start time carried in a field is a **stated gap** rather than a finding with no repair.
- `SK2164` **the assertion's expression has side effects**
  ([#166](https://github.com/Rikarin/SKALA/issues/166), `S3346`). ⚠ **Keyed on `[Conditional]` rather
  than on a list of assertion methods**, which costs nothing and covers a repository's own
  `[Conditional("TRACE")]` helper. It is also what puts the shape that would otherwise be this rule's
  worst false positive — xUnit's idiomatic `Assert.True(map.TryGetValue(key, out var found))` — out of
  scope **by construction**: no test framework marks its assertions `[Conditional]`, so the call is
  never deleted and there is nothing to report. ⚠ **What counts as a side effect is enumerated, never
  inferred**, and the collection namespaces are listed exactly because a prefix match on
  `System.Collections` would make every `ImmutableList<T>.Add` a false positive. Report-only.

### ⚠ `SK2164` lost a fifth kind of evidence to the compiler, and a fixture is what refuted it

An `out var` that the code below the assertion reads was built as evidence and then removed. With the
call deleted the variable is never assigned, so the read is **`CS0165`, *use of unassigned local
variable***, in any build without the symbol defined. This was not argued — **the positive fixture
written for it could not be made to compile**, and the harness's rule that a fixture which does not
compile proves nothing is what surfaced it. § "the compiler already says it" then decides the question.
Both directions are now silence: an `out var` nobody reads is harmless, and one that is read is the
compiler's finding rather than Skala's.

### The `CA1305` boundary, measured on a plain project

`CA1305` — *Specify IFormatProvider* — ships in the SDK **`IsEnabledByDefault: true` with
`DefaultSeverity: Hidden`**, so it never appears in a build until a repository raises it or opts into
`AnalysisMode=Recommended`. ⚠ **Being silent by default is not a reason to rebuild it**; ADR-008's
answer is to enable it, exactly as recorded for `CA2201` above. Raised to `warning` on a pristine
`net10.0` console project, on a probe whose repaired forms were confirmed silent in the same run, it
reports:

| shape | `CA1305` |
|---|---|
| `DateTime.Parse`, `DateTimeOffset.Parse`, `DateOnly.Parse`, `TimeOnly.Parse`, `TimeSpan.Parse` | reports |
| `DateTime.ToString()`, `DateTime.ToString(string)`, `DateOnly.ToString()`, `TimeOnly.ToString()` | reports |
| `string.Format`, `int.Parse`, `int.ToString()` | reports |
| **every `TryParse` form, on all five types** | **silent** |

So `S6585` — the formatting half of #244 — is **hosted in full and no id was allocated for it**, and
`S6580` is hosted for `Parse` and unowned for `TryParse`. `SK2162` is that gap and nothing else. ⚠ **The
gap is larger in practice than in the rule list**: `TryParse` is the form written wherever input might
be malformed, which is wherever input comes from outside the process — and that is exactly where a date
arrives written in somebody else's culture.

⚠ **Nothing in the SDK covers the other three concepts, at any analysis mode.** A search of all 317
shipped C# `CA*` descriptors, and of the NetAnalyzers assemblies' string tables, returns nothing for
`TimeProvider`, `DateTimeKind`, `Stopwatch`, `DateTime.Now` or `UtcNow`; a probe built at
`AnalysisMode=All` reports nothing on any of the three shapes. `SK2160`, `SK2161` and `SK2163` have no
host to defer to.

### ⚠ Why `SK2160` ships disabled, and what the number that decided it is *not*

Run from a fresh Release binlog of Skala's own solution — a load carrying **10 CS diagnostics, every
one of them `CS9335` (*the pattern is redundant*, a warning), no CS errors, no `AD0001` and no
`SK9030`** — with the severity temporarily raised, `SK2160` reports **6** findings across the whole
first-party tree. **Six cannot calibrate anything, and that is the honest statement rather than a
justification derived from it.** It is not evidence that the rule is quiet; it is evidence that Skala
barely asks what time it is.

The number with content came from Vixen, measured the same way from its own fresh binlog — a load of
**159 CS diagnostics across 8 codes, no `AD0001`, no `SK9030`** — where `SK2160` reports **38**. ⚠
**Every one is a true positive, and that is the problem rather than the reassurance.** **22 of the 38
sit in `*.Tests` projects**, in helper methods such as `static void Settle(TerrainStreamer, Func<bool>)`
that poll a deadline — code the rule's own test exclusion cannot reach, because xUnit has no
class-level attribute and a helper carries none of its own.

⚠ **Vixen is a test subject and never a specification, so 38 does not set a severity either.** What the
two numbers establish between them is the shape of the risk: the population is large on a repository
that has not adopted `TimeProvider`, most of it is test scaffolding that is untestable by design, and
adoption is a decision a repository makes once rather than a defect it repairs line by line. Nothing
available measures the case that would actually decide it — a repository that *has* adopted
`TimeProvider` and still reads the clock from a static on a production path. So the rule ships **`none`**
and is turned on per path, exactly as `SK7010`, `SK7101` and `SK6053` are, and Skala does not assert an
architectural policy on evidence it does not have.

### ⚠ Four zeros, each classified, and one instrument caught lying

`SK2161`, `SK2162`, `SK2163` and `SK2164` report **0** on Skala's own tree and **0** on Vixen. A zero
from a disabled check and a zero from clean code are the same zero, so each was classified by reading
the sites rather than by trusting the count:

- **Skala's own tree — shape absent, in all four cases.** Every syntactic hit for `ToUniversalTime`,
  `ToLocalTime`, `new DateTimeOffset`, a date `TryParse`, a clock subtraction, `Debug.Assert` and
  `[Conditional]` turned out to be inside these analyzers' own documentation comments. Skala contains
  no occurrence of any of the four shapes, and uses `Stopwatch` throughout.
- **Vixen — shape present and correctly declined, in all four cases**, verified site by site. Its six
  `SK2161` candidates are all values whose `Kind` cannot be proved (a property, a pattern variable, two
  parameters) or constructions that were handed a real offset. Its three `SK2162` candidates all pass
  `CultureInfo.InvariantCulture`. Its seven `SK2164` candidates are all pure comparisons such as
  `Debug.Assert(index >= 0)`. ⚠ Its **one** real `SK2163` candidate is
  `static string Ago(DateTime when) { var elapsed = DateTime.UtcNow - when; … }` — a parameter, and
  literally a method for rendering "two days ago". That is the exact false positive the "both ends must
  be clock reads" requirement exists to prevent, and it prevented it.
- **The vendored corpus is not a sound instrument for this, and is reported as such rather than
  quoted.** Compiled from loose sources it carries 7 257 (Serilog), 11 949 (Newtonsoft) and 48 559
  (Vixen snapshot) CS errors, with types declared three times over because the fidelity corpus keeps
  `.cs`, `.expected.cs` and `.arranged.expected.cs` side by side. `SK2160` reports 6 on Newtonsoft
  there; Serilog's zero is **not soundly classifiable** and is left recorded as unexplained rather than
  given an invented reason. Reading Newtonsoft's sources directly is what the `SK2161` and `SK2162`
  conclusions rest on: all 12 of its date-parsing call sites are `TryParseExact` passing
  `CultureInfo.InvariantCulture`, and all 39 of its zone conversions operate on parameters, on a stated
  `DateTimeKind.Utc`, or on constructors given a real offset.

⚠ **One hypothesis in this batch was written down, measured, and refuted, and the refutation is worth
more than the change it prompted.** Serilog ships `namespace System; abstract class TimeProvider` under
`#if !NET8_0_OR_GREATER`. It was assumed that this makes the metadata name ambiguous, that
`GetTypeByMetadataName` returns null for it, and that `SK2160` therefore withdrew from Serilog
entirely — a zero meaning "the analysis never ran". **Measured, that is false**: on a compilation
containing the shim the singular lookup returns a symbol, the *source* one, and the plural returns two,
`[serilog, System.Private.CoreLib]`. Nothing withdrew. The real cause of the count that prompted the
investigation was a **measurement harness that built its own compilation options and omitted the opt-in
that enables a `none`-severity rule** — the disabled-check zero, met in person, in the very
investigation that exists to catch it. The analyzer now uses `GetTypesByMetadataName` regardless, for
the exclusion rather than for the guard: where both a shim and the framework type exist, a body
deriving from either is the designated place to read the real clock.

### Sabotage

Each of the five guards was removed in turn and the fixture suite re-run. All five turned red, each on
exactly the negative fixture that documents the removed guard: `SK2160` on
`a-time-provider-implementation`, `SK2161` on `the-kind-is-stated`, `SK2162` on `the-provider-is-passed`
and `try-parse-exact-has-a-provider`, `SK2163` on `the-start-is-not-a-clock-read`, `SK2164` on
`an-immutable-collection`. ⚠ **The first attempt at `SK2161`'s sabotage was invalid and looked identical
to a valid one**: replacing a condition with `if (false)` fails the build under `TreatWarningsAsErrors`
(`CS0162`), so the run went red with *zero* failing tests. A non-zero exit code is not evidence that a
sabotage worked, and the count of failures is what has to be read.
down where the next milestone will read them — plus, for three of the five, the measurement that
decided what the rule is *for*, because in this batch that measurement is most of the content. It is
not yet the considered account the sections above carry, and it belongs beside `SK2030`–`SK2034`.

**Five rules about storage: where a value comes from, when it arrives, and who else can see it.**

⚠ **Three of the five were narrowed by a probe rather than by argument, and the probe is what makes
them worth an id at all.** A single file compiled at `AnalysisMode=All` was read for what the
platform already says about each proposed shape. The answers moved two rules and refuted a third's
larger half:

| Shape | What the platform says |
|---|---|
| `private` / `internal` / `private readonly` field, never assigned | `CS0649` |
| `public` field, never assigned | nothing — and it is not decidable, since any consumer may write it |
| get-only auto-property, non-nullable reference, nullable warnings on | `CS8618` |
| get-only auto-property, value type or nullable reference, or warnings off | **nothing** |
| extended `partial` method with no implementation | `CS8795` — a compile **error** |
| classic `partial void` with no implementation, called | **nothing** |
| instance code writing a `private static` field | nothing; `CA2211` fires on a **public** static field's *declaration* |
| static field initializer reading a field below it | **nothing** |
| *instance* field initializer reading another instance field | `CS0236` — a compile **error** |

- `SK2130` `forward-static-initializer` — a static field initializer that reads a static field of the
  same type declared below it, which reads `default` because initializers run in declaration order.
  ⚠ **Being exact about the construct is the whole rule, because three neighbours look identical and
  are all correct.** A static *property* runs when it is called; a static *method* is ordered against
  nothing; and a **static constructor runs after every field initializer**, so a read from there sees
  a fully initialized type. ⚠ **The referenced field must carry an initializer of its own**, and that
  is about the message being true rather than about the count — a field without one reads `default`
  from above it as well, so the declaration order is not what makes it wrong and a finding blaming
  the order would be pointing at the wrong thing. ⚠ **The instance version of this concept does not
  exist**: `CS0236` forbids an instance field initializer from referencing any instance member at
  all, which is why the rule is `static` only and needs no exclusion to say so. Cross-file pairs in a
  `partial` type are declined, because the order between parts follows the order the files reach the
  compiler. Report-only: the repair moves a declaration, and which of the two moves is right depends
  on what else in the type depends on the order.
- `SK2131` `unassigned-get-only-property` — a `{ get; }` with no initializer that no constructor
  assigns. ⚠ **Issue #24 proposed six ReSharper inspections and five of them dissolve, which is worth
  as much as the one that shipped.** The three field inspections are `CS0649`; the public-field case
  is not decidable in a compilation at all; and the non-nullable-reference property is `CS8618`,
  hosted under ADR-008 rather than duplicated. What survives is the half the compiler is silent about
  — a value type, a nullable reference, or anything in a nullable-oblivious file — and ⚠ **it is
  decidable for a reason worth writing down: a `{ get; }` with no initializer can be assigned from
  nowhere but a constructor of its own declaring type**, and every part of that type is in this
  compilation, because a type cannot be split across assemblies and a generator's part is compiled
  source like any other. That makes it *not* the usage-based rule the concept looked like. The
  residue is reflection against the compiler-generated backing field, which nothing compilation-local
  can see. Report-only: the property is permanently `default`, and saying what it should hold instead
  is the one thing the declaration does not contain.
- `SK2132` `mismatched-backing-field` — an accessor reaching for a field that backs a different
  property. The only rule in the batch with a fix. ⚠ **The name convention is a convention, not a
  fact, and correct code breaks it constantly** — `Count` over `_items`, `Value` over `_inner` — so
  two conditions must hold **together, about two properties rather than one**: the examined property
  must itself have a conventionally named field of exactly its own type, and the field the accessor
  touches must be the conventionally named field of a *different* property of the same type. The
  first condition is what declines `Count`/`_items`; the second is what declines `Value`/`_inner`
  unless an `Inner` property also exists, at which point the names have been crossed rather than
  chosen. ⚠ **The accessor must be nothing but the field access**, which declines
  `get => _items.Count;` and a getter that logs first by construction: an accessor that does anything
  besides reach for storage is stating a decision. The fix is `fixIsSafe: false` because it changes
  what the program computes, which is exactly the point of the finding.
- `SK2133` `unimplemented-partial-method` — a `partial void` with no implementing declaration that
  something calls. ⚠ **The declaration alone is never the finding, and that is the difference between
  this rule and `S3251` as stated.** An unimplemented `partial` method is legal and erasing it is the
  feature; the defect is that the *call* is erased with it, argument evaluation included, so work
  written into an argument silently does not happen. ⚠ **Requiring a call is also what makes the rule
  decidable rather than merely narrow**: a classic `partial void` carries no accessibility modifier
  and is therefore implicitly `private`, so every caller it can ever have is inside the declaring
  type, which is in this compilation in full. ⚠ **The other half of #186 is `CS8795` and was verified
  as a compile error**, so it can never reach a compiling analysis; `ReturnsVoid` keeps it out by
  construction rather than by hope, and that half is refuted rather than shipped. Report-only,
  because writing the implementation and deleting the hook are opposite repairs and the source does
  not say which.
- `SK2134` `instance-write-to-static` — an instance member assigning a static field of its own type.
  ⚠ **`CA2211` was verified to be a different question rather than assumed to be**: it fires on a
  *public* static field's declaration, is silent on a private one, and says nothing about who writes
  it. This rule never reads visibility and never reports a declaration. ⚠ **Lazy initialization is
  the look-alike and is declined by recognising the guard, not the name** — `??=`, an `x ?? y`
  right-hand side, and an assignment under a condition mentioning that same field together with
  `null` or `default`, which is also what covers double-checked locking. ⚠ **A counter incremented
  from a constructor is reported on purpose**, and a fixture pins it positive so that nobody later
  mistakes it for a false positive and excludes it: it is shared mutable state, `++` is not atomic,
  and `Interlocked.Increment` — which is declined by construction, being an argument rather than an
  assignment — is the repair. Only the enclosing type's own static fields, so a process-wide setting
  on somebody else's type is out. Report-only: the two repairs change the type in opposite
  directions.

⚠ **`SK2131` and `SK2132` are the pair that could collide, and they cannot — by construction rather
than by a filter.** Both read a property. `SK2131` requires an *auto*-property, which has no accessor
body at all; `SK2132` requires an accessor body that is a field reference. No property can be both,
and a batch test asserts that on the one file where it would show rather than trusting the argument.
`SK2130` and `SK2134` are disjoint for the same kind of reason: a static field initializer is neither
instance code nor an assignment expression.

⚠ **Four of the five ship report-only, and the reason is the same one each time rather than four
different ones.** In every case two repairs exist that move the code in opposite directions — hoist
the declaration or sink the reader, write the implementation or delete the hook, make the state
per-instance or make the member static — and the finding is precisely the evidence that the author
knows which and the analyzer does not. `SK2132` is the exception because there the two candidate
repairs are not symmetric: one of them is a rename of a property that other code already calls.

## `SK2180`–`SK2184` — type identity, conversion and which member the call actually reaches

⚠ **The prose pass is owed for this block, and it is appended here rather than merged into §
"SK2000 — Correctness" only to keep it out of a section several concurrent branches were editing.**
What follows is the allocation register doing the one job ADR-012 needs of it: the numbers are taken
and written down where the next milestone will read them. The block belongs beside `SK2121`, which
is the rule every one of these is measured against.

**Five issues, five rules — and the shape of the result is that four of the eight upstream
inspections behind them turned out to be compiler diagnostics.** The batch was opened on issues #2,
#23, #264, #35 and #49. Two of those issues were expected to dissolve entirely; instead each kept a
narrow slice the compiler leaves alone, and the refutations are pinned as `[Theory]` cases in
`TypeIdentityBatchTests.TheCompilerAlreadyOwnsThisShape` rather than as sentences here, so the day a
claim stops being true the file goes red.

- `SK2180` `foreach-element-downcast` — the loop variable's type is narrower than what the sequence
  yields, so C# writes an explicit conversion into the loop that the source does not show and the
  loop throws on the first element that is not of that type. ⚠ **This is what is left of issue #2
  once the compiler has taken its share, and the share was measured rather than guessed.** At
  `AnalysisMode=All`, `(Sealed)derived`, `(IUnrelated)sealedValue` and `(Sealed)unrelatedInterface`
  are all **`CS0030`, errors** — that source never reaches an analyzer at all. What the compiler is
  silent about is a *possible* downcast, and deciding whether a plain `(Derived)b` or a covariant
  array store can succeed needs to know which values reach the site, which is the value lattice that
  refuted issue #169 in the neighbouring batch. The `foreach` form needs none of it. ⚠ **An `object`
  element type is never reported, and that exclusion carries most of the rule's safety**: a
  non-generic `IEnumerable`, an `ArrayList` or a `List<object>` offers no other spelling, so the cast
  there is the API's doing. Only reference conversions and unboxings — a narrowing numeric
  conversion cannot throw and is a different concept. Report-only: `OfType<T>()` *skips* the
  mismatched elements and an explicit cast in the body still *throws*, and the source says which
  behaviour it has rather than which was wanted.
- `SK2181` `get-type-on-a-type` — `GetType()` on a receiver that is already a `System.Type`, which
  returns `System.RuntimeType` for every input. ⚠ **The mistake is invisible to every test that
  checks the obvious things**: the result is non-null, it is a `Type`, and two calls agree — so a
  registry keyed on it has one key and nothing throws. Probed at `AnalysisMode=All` with
  `EnforceCodeStyleInBuild`: no compiler diagnostic and no `CA*` diagnostic of any kind. ⚠ **`System.Type`
  declares its own parameterless `GetType()`**, hiding `object`'s, so a call on a `Type` receiver does
  not bind to `object.GetType()` — testing the containing type for `System_Object`, which is the
  obvious spelling, silences the rule on every fixture it exists for, and that is how it was found.
  The reflection-emit idiom — comparing the result against a `typeof` whose operand itself derives
  from `System.Type` — is declined, as is the documented escape hatch `((object)t).GetType()`, which
  the rule does not look through because it reads the receiver's *static* type. Fix offered,
  `fixIsSafe: false`: one deletion, and it changes what the expression evaluates to, which is the
  finding.
- `SK2182` `type-compared-by-name` — `x.GetType().Name == "Order"` where the literal names a type
  this compilation can already see. ⚠ **That single resolution test is the whole specification, and
  it is what separates the defect from the idiom**: comparing a name is the only option for a type
  loaded reflectively, for a plugin whose assembly is deliberately not referenced, and across a
  boundary this project does not compile against — and in all of those the name does not resolve
  here, so the rule is silent. `Name` resolves against this compilation's own declarations and
  `FullName` through metadata as well, because a fully-qualified name that resolves is a type the
  file could have written. ⚠ **`AssemblyQualifiedName` is never reported**: it carries a version, a
  culture and a public key token, so a comparison against it is a statement about which *build* of a
  type this is, which `typeof(T)` cannot make. ⚠ **The fix is `GetType() == typeof(T)` and never
  `is T`** — a name comparison is exact and `is` matches subclasses, so that rewrite would change
  behaviour the string comparison never had.
- `SK2183` `static-member-via-derived-type` — `Leaf.Count` where `Count` is declared on `Root`. ⚠
  **Nothing hosts it, and both candidates named in the brief were measured rather than assumed.** In
  a probe built outside this repository with empty `Directory.Build.props`/`.targets` above it, at
  `AnalysisMode=All` with `EnforceCodeStyleInBuild`, `Leaf.Count`, `Leaf.Read()`, `Leaf.Limit` and
  `Leaf.Total` produced nothing; `CA1000` and `IDE0002` were each raised to `warning` and stayed
  silent on all four, with `CA1000`'s own shape planted alongside to prove the instrument was live —
  it fired four times. ⚠ **`IDE0002` could not be made to fire on any shape at all**, including its
  own documented one, so its state is reported as *unverifiable* rather than as *off*: a zero from an
  instrument that never moved is not evidence. `suggestion`, because the two spellings resolve to the
  same member — what is wrong is what the line says, not what the program does — and that is also
  why the fix is the only `fixIsSafe: true` in the batch.
- `SK2184` `hidden-base-interface-overload` — a call that binds to the derived interface's overload
  while a base interface's better-matching overload sits unreachable behind it. ⚠ **The premise was
  executed, not reasoned about.** With `IParent.M(string)`, `IChild : IParent` declaring `M(object)`,
  and one implementation of both, `c.M("literal")` runs `IChild.M(object)` and `p.M("literal")` on
  the same instance runs `IParent.M(string)` — same argument, same object, two different methods, and
  no diagnostic from the compiler or from any `CA*` rule. ⚠ **The genuinely ambiguous member is a
  compiler error and is deliberately not this rule**: `IBoth : ILeft, IRight` with both declaring
  `Value` and `Run()` gives **`CS0229`** and **`CS0121`**, both errors, so that source does not
  build. ⚠ **Applicability and betterness must both hold, and together they are what keep
  `IDictionary` out** — `IDictionary<K,V>.Add(K,V)` hides `ICollection<KeyValuePair<K,V>>.Add(KVP)`
  by exactly this mechanism, but the hidden overload takes one argument and the call passes two.
  Betterness is decided by conversion and not by heuristic: every parameter of the hidden overload
  must convert implicitly to the corresponding parameter of the bound one, with at least one
  conversion not an identity. Report-only: the two repairs are a cast at this one call site and a
  change to the interface, and which is right depends on what the interface is for.

⚠ **What none of these does that `SK2121` does.** `SK2121` folds a conversion the type hierarchy has
already decided *succeeds*, and it is the only rule in either batch that removes an operator. These
five never touch a conversion that is decided: `SK2180` reports one that is undecided and unshown,
`SK2181` and `SK2182` report a *value* that is not the one the author meant, `SK2183` reports a
spelling, and `SK2184` reports which overload a call reached. `SK2121` is also the boundary in the
other direction — its own remarks record that the always-*false* half of issue #1 is `CS0184`,
`CS0183`, `CS0039` and `CS8121`, and this batch found the same pattern one concept over: **four more
of the eight inspections behind these five issues belong to the compiler**, three of them as errors.

⚠ **A claim in this batch was refuted by its own fixture and the prose was corrected rather than the
fixture deleted.** `SK2183` was written with an accessibility guard against a `public` type deriving
from a less visible base, whose declaring type the fix could then not name. That shape cannot be
built: `CS0060` makes a base less accessible than its derived type a compile **error**, and a base in
an unreferenced assembly is `CS0012`. The guard is kept — it is the one call in the batch that can
throw, since `Compilation.IsSymbolAccessibleWithin` raises for a `within` argument that is neither a
type nor an assembly — but it is documented as defensive and has no fixture, and the fixture written
for it now says the opposite of what it was written to say.

**The measurement.** All five rules were swept over Skala's own source through a fresh Release binlog
(`dotnet build -c Release --no-incremental -bl:`, then
`--load=binlog --require-fresh-binlog`; **`SK9021` coverage 594 of 596 files, 100 %**, **11 CS
diagnostics in the load** — `CS9335` ×10 and `CS8933` ×1 — 1 451 results in total). ⚠ **Both
flags matter and only together.** `--no-incremental` is what makes the binlog cover the whole
tree rather than whatever MSBuild happened to rebuild, and `--require-fresh-binlog` is what turns
an incomplete one into an error instead of a warning above a plausible number. The two files not
covered are `build/Build.cs` and `build/Configuration.cs`, which are not in `Skala.slnx` at all —
the same gap that let a compile error live under `build/` through a green CI. All five report **zero**, and ⚠ **none of the five zeros is
the analysis failing to run**, which was established before any of them was believed.

- **The instrument was verified first.** A probe file planting one shape per rule into
  `Rikarin.Skala.Core` — a narrowing `foreach`, a `GetType()` on a `Type`, a
  `GetType().Name == "ProbeOrder"`, a `ProbeLeaf.Count`, and a call through an interface hiding a
  better overload — made **all five** fire through the same binlog pipeline, at the right lines and
  with the right messages. That is the only check that sees a real reference set rather than the
  fixture harness's (#297). The probe was deleted and the binlog rebuilt.
- `SK2180`'s shape is **present 9 times and declined 9 times.** Relaxing the `object` exclusion and
  the reference/unboxing restriction and re-sweeping found nine narrowing `foreach` statements in
  Skala's source, and ⚠ **every one of them is the shape the exclusion was written for**:
  `foreach (Match m in Regex.Matches(…))`, where the loop binds the non-generic enumerator and the
  sequence therefore yields `object`. The shipped rule reports none of them.
- `SK2184`'s shape is **present once and declined once.** Relaxing the betterness test found
  `IEnumerable.GetEnumerator()` sitting behind `IEnumerable<T>.GetEnumerator()` in `BreakPlan.cs`.
  It is declined by construction rather than by a filter: the call takes no arguments, so no
  parameter conversion can be non-identity and no overload can be "better".
- `SK2181`, `SK2182` and `SK2183` are **shape absent.** Each was relaxed in the direction that would
  count the raw shape — every `GetType()` on a `Type` receiver, every `GetType().Name` compared to
  any literal whether it resolves or not, and every static member reached through a type qualifier
  that is not its declaring type — and each still reported **zero** across the whole tree.

⚠ **There is no corpus evidence for any of the five, and that is a property of the rules rather than
an omission.** All five declare `requiresSemantics: true`, so `AnalyzerHost.SkippedFor` drops them
under `--load=loose` (#277), and `Testing/corpus` neither compiles nor is reachable through
`skala check` (`SK9023`). The self-sweep is the measurement.

⚠ **The sweep also found a crash that is not this batch's.**
`Rikarin.Skala.Rules.Cleanup.RedundantArgumentAnalyzer` throws `IndexOutOfRangeException` **17
times** on Skala's own tree and is disabled for the rest of the run each time, so its rule is
measuring nothing over most of the source. That is issue **#298**, already filed and already
diagnosed — a loop that bounds its counter by the parameter count and then indexes by the argument
position — and this sweep is the first count of how often it actually fires: 17. It reaches the
SARIF only as an `SK9030` `toolExecutionNotification` and does not fail the gate (#295), which is
how it survived. None of the
five analyzers in this batch appears in that list.
## `SK6060`–`SK6062` — the shape of a declaration, and three questions that stop at the assembly edge

⚠ **The prose pass is owed for this block.** What follows is the register doing the job ADR-012 needs
it to do — the numbers are taken and written down where the next milestone will read them — together
with the measurements that decided the batch. It is not yet the considered account the sections above
carry, and it belongs beside `SK6040`/`SK6041`, which are the rules it most often argues from.

⚠ **Five concepts were briefed and three ship. The two that do not are the more useful result**, and
both were closed by measurement rather than by argument.

- `SK6060` `invariant-type-parameter` ([#117](https://github.com/Rikarin/SKALA/issues/117),
  `TypeParameterCanBeVariant`). An interface type parameter that occurs in one direction only and is
  not declared `out` or `in`. ⚠ **This is the compiler's own variance-safety rule run in reverse, and
  the composition is the entire content of it.** Each occurrence is classified by the position it sits
  in — a return type is covariant, a by-value or `in` parameter contravariant, a `ref`/`out` parameter
  and a get-set property invariant, an event contravariant, a generic method's constraint
  contravariant, a base interface covariant — and then composed through the declared variance of every
  generic type enclosing it, flipping on a contravariant parameter and collapsing on an invariant one.
  That composition is what separates `IEnumerable<T> Get()` from `List<T> Get()` and `Action<T> Get()`,
  which are indistinguishable at the level of "the parameter is in a return type" and of which the
  first can be `out`, the second nothing, and the third `in`.
- `SK6061` `caller-info-parameter-not-last` ([#207](https://github.com/Rikarin/SKALA/issues/207),
  `S3343`). ⚠ **The shape the concept is usually described with does not compile, and establishing that
  is what made the rule narrow.** A caller-info attribute on a parameter without a default value is
  `CS4022`, so "a required parameter after a caller-info one" is not a program anyone can write.
  Everything after an optional parameter is optional or `params`, and *that* is the defect: two
  optional parameters where the first is filled by the compiler and the second is what the caller
  wanted. A trailing *run* of caller-info parameters is the correct shape and is never reported, which
  is also why the fix rotates the whole run to the end rather than swapping a pair. `params` is
  declined outright — it must be last, so the run has nowhere to go and the only rewrite satisfying
  both constraints is the one already written. Report-with-fix, `fixIsSafe: false`, because reordering
  optional parameters silently re-points every positional argument at the call sites.
- `SK6062` `write-only-local-collection` ([#123](https://github.com/Rikarin/SKALA/issues/123),
  `CollectionNeverQueried.Local`). A local collection created, written to, and never read. ⚠ **Locals
  only, and that restriction is not caution — it is the whole reason the question is answerable.** A
  field or a property can be read by anything holding the instance, which is the assembly boundary that
  closed [#114](https://github.com/Rikarin/SKALA/issues/114) and
  [#119](https://github.com/Rikarin/SKALA/issues/119). Proved by exhaustion rather than dataflow, the
  argument `SK2083` uses for the mirror-image defect: every reference must be the receiver of a
  discarded mutating call or the target of an indexer assignment, and anything else withdraws the
  finding without the analyzer deciding what it does. ⚠ **A discarded return value is required rather
  than tolerated** — `if (set.Remove(x))` reads the collection through the `bool`. Report-only: the
  usual repair is to add the reader that was lost, not to delete the writes, and the source does not
  say which.

⚠ **`SK2083` and `SK6062` are disjoint by construction rather than by filter.** `SK2083` reports a
`foreach` over a collection nothing fills — it requires a read and forbids every write. `SK6062`
requires a write and forbids every read. No source satisfies both.

### ⚠ `SK6060`'s algorithm was written after the compiler, not before it, and three shapes refuted the first draft

Twelve interface shapes were compiled with the modifier already applied before a line of the analyzer
existed, and the answers are the specification. Three of them broke a draft that had looked complete:

- **A nested enum, class or struct inside an interface is `CS8427` outright** — "cannot be declared in
  an interface that has an `in` or `out` type parameter" — so the modifier is illegal there no matter
  what the signatures say. Nothing in the signature-walking model would ever have found this.
- **A nested delegate or interface is legal and is variance-checked through its own members**, which
  are not the enclosing interface's members and were not being walked. One guard covers both: an
  interface declaring any nested type is declined.
- **A `ref` return is an invariant position, not a covariant one.** It is the one shape in this rule
  that reads as an ordinary return type and is not.

Two further shapes were checked and left the algorithm alone, which is worth as much: `where U : T` on
a *sibling* type parameter of the interface is not variance-checked, and neither is a non-abstract
`static` member's signature. The rule classifies the static member anyway. That is deliberately more
conservative than the compiler — it loses a finding and cannot invent one.

⚠ **Two negative fixtures were refuted by the rule and confirmed wrong by the compiler**, which is the
instrument working in the direction that matters. `Action<T>` in a return type was written as a
negative on the assumption that a flipped position is no position; `in T` is legal there and the file
is now a positive. `event EventHandler<T>` was written as "an event of an invariant delegate";
`EventHandler<TEventArgs>` is declared `in` on modern .NET, so `out T` is legal and that assumption
about the BCL was simply false. A locally-declared invariant delegate replaced it.

### ⚠ `SK6061`'s guard fixtures all passed for the wrong reason first, and the fix is worth writing down

An `override`, an interface implementation and a `partial` implementation are declined because their
parameter order is fixed by something else in the program. The first version of each negative fixture
carried the defect on the *base* declaration as well — which is where the rule correctly reports it —
so every file fired and the guard was never exercised at all. ⚠ **A derived declaration may add
caller-info attributes its base does not carry**, verified against the compiler, which leaves the
derived member as the only candidate in the file and makes the guard the only thing standing between
the fixture and a finding. The same probe turned up `CS4026` on the explicit-implementation and
partial-implementation shapes — the attribute "will have no effect because it applies to a member that
is used in contexts that do not allow optional arguments" — which is the compiler making the guard's
argument independently.

### ⚠ Two concepts closed without an id, and both were closed by a probe

⚠ **No id was allocated for either.** ADR-012 makes an id permanent and doc 08 § "What this does not
recommend" is explicit that ids must not be allocated against a concept that turned out not to exist.

- **[#113](https://github.com/Rikarin/SKALA/issues/113) — the namespace does not match the file's
  location — is hosted by `IDE0130`** (ADR-008). ⚠ **The issue's stated reason for rebuilding it is
  false.** It says `IDE0130` "is off by default and does not know about `<RootNamespace>` overrides
  the way a project-loading tool can". Measured on a probe built outside this repository with empty
  `Directory.Build.props`/`.targets` above it: `IDE0130` is `isEnabledByDefault: true` with
  `defaultLevel: note`, and its effective severity comes from its style option's default notification,
  which is silent — so it is **enabled and effectively `Hidden`**, not off. And it reports
  `Namespace "Probe.Wrong.Place" does not match folder structure, expected "Probe.Sub.Folder"` on a
  file under `src/Sub/Folder` in a project whose `RootNamespace` is `Probe`. It knows exactly what the
  issue says it cannot know. ⚠ Two further measurements are worth keeping: setting
  `dotnet_style_namespace_match_folder = true` alone does **not** make it fire, and setting
  `dotnet_diagnostic.IDE0130.severity` alone does — the severity is the knob, not the option; and like
  every `IDE*` it needs `EnforceCodeStyleInBuild=true` to appear at build time at all.
- **[#116](https://github.com/Rikarin/SKALA/issues/116) — the parameter type is narrower than the code
  needs — is refuted**, and the refutation has two halves that meet in the middle. The **public** half
  is the assembly boundary again, but sharper than in #114: a public member's parameter type is not a
  guess about usage, it is a *contract*, and narrowing or widening it is a breaking change no
  single-compilation analysis is entitled to propose. That leaves only the **non-public** half — and
  ⚠ **`CA1859` ships on by default at `note` and argues the opposite direction on exactly that
  slice.** Measured: `CA1859` is `isEnabledByDefault: true`, `defaultLevel: note`, and on a private
  method returning `IReadOnlyList<int>` it says *"Change return type … from
  `System.Collections.Generic.IReadOnlyList<int>` to `System.Collections.Generic.List<int>`"*. A rule
  telling an author to widen a non-public signature would contradict a shipped, on-by-default SDK
  analyzer telling them to narrow it. `CA1002` was probed too and is a different concept — off by
  default (`isEnabledByDefault: false`, `defaultLevel: warning`), fires only on the *public* surface,
  and its remedy is `Collection<T>` rather than `IEnumerable<T>`; doc 08 allocates that ground to
  `SK6002`.

⚠ **The `.Global` and public halves of [#123](https://github.com/Rikarin/SKALA/issues/123) hit the same
wall as #114 and #119 and are not shipped**, and the internal-type slice of it is hosted: **`CA1812`**
reports *"'NeverInstantiated' is an internal class that is apparently never instantiated"* and is
**off by default** (`isEnabledByDefault: false`, `defaultLevel: warning`), so the work there is
enabling and mapping rather than writing an analyzer. What `SK6062` ships is the one part of that
issue no assembly boundary touches: a *local*.

### ⚠ Three zeros on the reference trees, each classified, and one of them says nothing at all

Swept over all three vendored trees — 380 source files, `.expected.cs` excluded — staged outside the
repository because the corpus is unreachable through `skala check` in place (`SK9023`), built into
real projects with empty `Directory.Build.props`/`.targets` above them, and read through
`--load=binlog`. ⚠ **`--load=loose` would have skipped all three rules** (issue #277) and
`--load=workspace` ignores its path argument (issue #284), so binlog is the only mode in which this
measurement means anything.

⚠ **The first set of numbers was discarded and re-taken, and the reason is `BinlogLoader`'s own
measurement.** An *incremental* build's binlog is not stale — its mtime is seconds old — it is
**partial**, containing only the projects MSBuild rebuilt, and on Vixen a complete build's binlog
covers 98 % of the tree where an incremental one covers 1 %. The builds here already used
`--no-incremental`, but the first sweep omitted `--require-fresh-binlog`, which is the flag that turns
coverage below 90 % into an error rather than a number nobody checks. Re-taken with both:

| Tree | Files covered | Coverage | `SK9021` | CS diagnostics | `SK` findings | Distinct `SK` rules |
|---|---:|---:|---:|---:|---:|---:|
| `newtonsoft` | 110 / 110 | 100 % | 0 | 2 531 | 280 | 38 |
| `serilog` | 70 / 70 | 100 % | 0 | 944 | 141 | 21 |
| `vixen` | 200 / 200 | 100 % | 0 | 10 982 | 211 | 24 |
| **total** | **380 / 380** | **100 %** | **0** | **14 457** | **632** | — |

No `AD0001` and no `SK9030` anywhere. The finding totals are the evidence the analysis ran at all, and
are why the three zeros below are readable. ⚠ **One thing here is unexplained and is recorded rather
than smoothed over**: the `vixen` invocation reports `partial: true`, whose only producer in
`AnalyzerHost` is a cancelled run that returns *no* findings — and this run returned 211. The zero was
therefore not trusted on that tree until the planted probe was run against it directly, below.

⚠ **`<ImplicitUsings>` was set to `enable` and the difference is not cosmetic.** The slice omits the
generated file, and its absence lies in both directions. CS-error lines in the MSBuild log, disabled
then enabled: serilog **1 694 → 972**, vixen **9 534 → 8 218**, newtonsoft **1 808 → 1 806**. A sweep
run without it is measuring a different program on two of the three trees.

**The instrument was verified before any zero was reported**, in the pipeline rather than in the
harness, and on **two** trees rather than one. A file carrying all three shapes was planted into the
serilog project and then into vixen, each rebuilt with `--no-incremental` and swept with
`--require-fresh-binlog`. All three fired both times — `SK6060` on `IPlantedFactory<T>`, `SK6061` on a
`[CallerMemberName]` parameter followed by an `int level = 0`, `SK6062` on a `List<string>` filled in a
loop — and deleting the file returned each sweep to zero. ⚠ **The vixen plant is the one that
matters**, because it is the tree whose invocation reported `partial: true`: the probe fired there with
the flag still set, which is what makes vixen's zero a statement about vixen's code rather than about a
truncated run.

| Rule | Findings | Classification |
|---|---:|---|
| `SK6060` | 0 | **Shape present, correctly declined** — 2 candidates, 2 different reasons |
| `SK6061` | 0 | ⚠ **Shape absent** — the corpus contains no caller-info attribute at all |
| `SK6062` | 0 | **Shape present, correctly declined** — 68 candidates |

- **`SK6060`.** Exactly two generic interfaces with an unannotated type parameter exist across the
  three trees, and each is declined for a different one of the rule's reasons. `ISyncCodec<T>`
  (`vixen/Core/Vixen.Net.Engine/SyncField.cs`) has `void Write(ref BitWriter, in T)` and
  `bool Read(ref BitReader, out T)` — the `in` is contravariant and the `out` parameter is
  **invariant**, so neither modifier is legal and the compiler agrees. `IInlineBuffer<T>`
  (`vixen/Core/Vixen.Core.Collections/InlineBuffer.cs`) declares one member,
  `static abstract int Capacity { get; }`, which does not mention `T` at all — the unused-type-parameter
  guard, which is a different finding.
- **`SK6061`.** ⚠ **This zero is worth nothing and saying so is the point.** A search of all 380 files
  finds no `[CallerMemberName]`, `[CallerFilePath]`, `[CallerLineNumber]` or
  `[CallerArgumentExpression]` anywhere. The rule was never given the chance to be wrong here, and its
  false-positive evidence is the fixture set and the planted probe, not this sweep.
- **`SK6062`.** 68 locals initialised to a `new` collection of a type the rule classifies, and every one
  of them is read somewhere in its member. That is the zero that carries weight in this batch.

⚠ **`SK6062` was sweepable and the brief assumed it would not be**, which is worth recording because
the reasoning generalises. A usage-based rule cannot be measured on a source slice when the usage it
asks about lives outside the slice — most of the callers are missing, so "nothing uses this" is a
statement about the vendoring rather than about the code. `SK6062` asks only about a **local**, and
every reference to a local is inside the member that declares it and therefore inside the slice. The
same property that makes the rule decidable across the assembly boundary makes it measurable on an
incomplete tree. `SK6060` and `SK6061` are declaration-shape questions and were never usage-based.
## `SK2190`–`SK2194` — structs, spans and value semantics

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section several concurrent branches were editing.** What
follows is the register doing the one job ADR-012 needs it to do — the numbers are taken and written
down where the next milestone will read them. It is not yet the considered account the sections above
carry, and it belongs beside `SK2005` and `SK2011`.

Five rules about what a value type does when it is copied, hashed or captured — the three things
that happen to a struct without anybody writing them down.

⚠ **Three of the five originating issues described shapes that were asserted not to compile, and
compiling them said otherwise.** That is the measurement this block rests on and it was taken on a
probe built outside this repository, with empty `Directory.Build.props`/`.targets` above it so that
this repository's raised `AnalysisMode` could not answer for a consumer's.

- `SK2190` `struct-key-without-equality` — a `Dictionary`, `HashSet` or `ConcurrentDictionary`
  constructed over a source struct key that declares no equality and is given no comparer, so
  `ValueType.Equals` and `ValueType.GetHashCode` run by reflection on every insert and every lookup.
  ⚠ **Issue #4's premise about `SK2011` is refuted, and it was refuted by reading the analyzer rather
  than the issue.** The issue says `SK2011` reports at the *declaration*; `InheritedValueTypeEquals`
  registers on `InvocationExpression` and fires at the `.Equals` call site. So the three inspections
  in that issue about a comparison — `UsageOfDefaultStructEquality` and both
  `DefaultStructEqualityIsUsed` scopes — are already covered where a comparison is written, and what
  was left is the use site with no comparison in it at all. ⚠ **`CA1815` was measured and does not
  host this.** At the SDK's default analysis mode it reports nothing — not at `Hidden` either;
  nothing appears in the SARIF error log, which is where a hidden diagnostic would show. At
  `AnalysisMode=All` it reports on the struct's declaration, only for a publicly visible struct, and
  whether or not anything ever hashes it. Report-only: generating equality members needs to know
  which fields define identity, and that is the one thing a struct which declared none has not said.
- `SK2191` `readonly-receiver-mutation` — a struct method that writes its own fields, called through
  an `in` or `ref readonly` parameter, a `ref readonly` local, or a `foreach` variable. The compiler
  makes a defensive copy, the method mutates the copy, and the write is discarded in silence. ⚠
  **Disjoint from `SK2005` by receiver kind, not by a filter.** `SK2005` reports the `readonly`
  *field* receiver and nothing else — its own `negative/parameter.cs` asserts silence on an `in`
  parameter, and that fixture is now covered by this rule instead. No receiver is both kinds, so
  neither rule can be configured into a duplicate of the other, and the shared evidence bar lives in
  one file so they cannot drift apart. ⚠ **The bar for "mutating" is a write the analysis has read at
  the top level of the method's own body, never the absence of a `readonly` modifier.** Almost
  nothing in real code marks struct members `readonly`; the looser test would report most of a
  repository for a copy that is real and almost always harmless. ⚠ **Nothing in the .NET analyzers
  covers any of it**, measured at `AnalysisMode=All` and `AnalysisLevel=latest-all`, and the compiler
  is silent too. This is the rule in the batch with the most value for AI-generated code, which
  writes non-`readonly` structs and `in` parameters by default and produces the shape without meaning
  to.
- `SK2192` `span-reference-comparison` — `==` binding to `Span<T>`'s or `ReadOnlySpan<T>`'s own
  operator, which is true only when both point at the same memory. ⚠ **It compiles, and the brief
  that carried the rule said it does not.** `ReadOnlySpan<char> == ReadOnlySpan<char>`,
  `Span<char> == Span<char>`, `ReadOnlySpan<byte> == ReadOnlySpan<byte>` and
  `ReadOnlySpan<char> == string` all build clean at `net10.0`, no compiler warning, nothing at
  `AnalysisMode=All`. ⚠ **What does *not* compile is `span.Equals(span)`** — `CS1503`, because the
  only `Equals` in reach takes `object` and a span cannot be boxed — so the "`.Equals` where
  `SequenceEqual` was meant" half of the issue is not a shape that exists and the operator is the
  whole rule. The fix is bound before it is offered rather than merely composed: `SequenceEqual` is
  an extension on `System.MemoryExtensions`, and where speculative binding at the comparison's own
  position does not resolve it there is no finding rather than a fix that breaks the build. ⚠ **The
  issue proposed `fixIsSafe: true` and it ships `false`**, for the reason `SK2040` gives about the
  same kind of edit: the fix changes the answer, and changing the answer is the entire point of the
  finding.
- `SK2193` `immutable-array-collection-initializer` — `new ImmutableArray<T> { … }`, which calls
  `Add` on the default value of the struct and throws `NullReferenceException` on the first element.
  One of the three uncovered inspections the export sets to `error`. ⚠ **An empty initializer is
  excluded and is a different, weaker defect**: it calls `Add` zero times, does not throw, and yields
  a `default` array that fails later and somewhere else — reporting it under a message that says the
  code throws would be saying something untrue about it. ⚠ **The collection-expression spelling
  `[…]` is deliberately not the fix**, although C# 12 makes it correct on this type: it needs a
  target type and `var ids = new ImmutableArray<int> { 1 };` has none, so that fix would be right in
  most places and `CS9176` in the rest. The replacement is `ImmutableArray.Create<T>(…)` built from
  the type's own spelling, so it binds in exactly the files the original bound in, with the type
  argument written out because inference from the elements is not the same answer.
- `SK2194` `mutable-captured-primary-parameter` — a member body assigning a primary constructor
  parameter, which makes the hidden capture field mutable instance state with no declaration and no
  modifier. ⚠ **Capture itself is the feature and is never reported**; the second inspection on the
  issue, `PrimaryConstructorParameterCaptureDisallowed`, asks a project-policy question the export
  itself ships at `none`. ⚠ **`CS9107` was probed and it is not `CS9124`.** The compiler warns,
  always on, when a captured parameter's value is *also passed to the base constructor* — and says
  nothing about one that is merely assigned. That overlap is excluded here rather than reported
  twice. ⚠ **Records are excluded structurally rather than by a test somebody can forget**: a record
  is a different syntax node, so the analyzer never sees one. In a positional record the parameter is
  also where the property is written down, both symbols point at the same `ParameterSyntax`, and a
  name in a member body resolves to the property — a different analysis with a different answer, and
  the exact shape that shipped a rule dead earlier in this milestone.

⚠ **Three of the five ship report-only and it is the same reason each time.** Equality members,
`in`/`readonly` receivers and captured parameters all have two repairs that move the code in opposite
directions — declare the identity or supply a comparer, drop the modifier or return the value,
declare the field or delete the write — and the finding is the evidence that the author knows which
and the analyzer does not.

**What the batch was measured against.** A complete `dotnet build Vixen.slnx -c Release
--no-incremental` binlog, loaded with `check --load=binlog --require-fresh-binlog`: 13 450 findings
over 3 666 files, **20 CS diagnostics**, and **no `SK9021` at all** — every selected source file was
in a compilation, so the run covered 100 % of what it selected and the flag accepted it. ⚠ **The
`--no-incremental` half is not optional and neither is the flag**: an incremental build's binlog
holds only the projects MSBuild rebuilt, it is not stale, and `BinlogLoader`'s own recorded
measurement is that such a binlog covers 1 % of Vixen against a complete one's 98 %. **Zero findings
from all five rules**, and the zero is classified rather than reported:

| Rule | Candidate sites in Vixen | What the zero is |
|---|---|---|
| `SK2190` | 5 119 `new Dictionary`/`HashSet`/`ConcurrentDictionary` | present in bulk, correctly declined |
| `SK2191` | 11 018 `in` parameters, 847 `ref readonly` locals | present in bulk, correctly declined |
| `SK2192` | 0 span `==` comparisons | shape absent |
| `SK2193` | 0 `new ImmutableArray<T> { … }` | shape absent |
| `SK2194` | 6 160 primary constructor declarations | present in bulk, correctly declined |

The candidate counts are text scans and are upper bounds, not semantic matches; what they establish
is that the first, second and fifth rules had thousands of chances and took none of them.

⚠ **The instrument was verified before the zero was believed.** One file carrying all five shapes
was planted into the corpus slice, rebuilt and re-checked through the same
`check --load=binlog` path: all five fired, one finding each. Deleting it returned the count to
zero. Without that step a zero from these rules and a zero from an analysis that never ran would
have been the same zero.

⚠ **The corpus slice is a weak second instrument and says so.** `Testing/corpus/real/` copied
outside the repository as three projects builds with **≈5 500 unique CS errors** because it is a
slice — most of what it references is not in it. Enabling `<ImplicitUsings>enable</ImplicitUsings>`
removes about **1 020** of them (13 036 → 10 996 raw log lines, each error printed twice), which is
the direction doc 17 records; it does not make the slice a compiling tree. Zero findings there too,
but the finding that matters is Vixen's.
## `SK3540`–`SK3542` — resource and handle lifetime

⚠ **The prose pass is owed for this block, and it is appended here rather than into §
"`SK3000` — Async, concurrency, lifetime" only to keep it out of a section several concurrent
branches were editing.** What follows is the register doing the one job ADR-012 needs it to do — the
numbers are taken and written down where the next milestone will read them. It is not yet the
considered account the sections above carry, and it belongs beside `SK3530`–`SK3532`.

Three rules about the same seam from three sides: what a type *declares* about a resource's lifetime
versus what actually happens to it.

- `SK3540` `dispose-method-without-interface` — a public parameterless `Dispose()` that releases
  something, on a type that does not implement `IDisposable`. ⚠ **This is the half of the ownership
  question `SK3502` does not ask, and the two read different declarations.** `SK3502` reads a
  *field* — the type constructs a disposable and offers no matching contract — and says nothing about
  how the type cleans up. This reads a *method*: the cleanup is written, public, spelled exactly the
  way the framework spells it, and the base list is silent, so `using` does not bind, `is IDisposable`
  is false, and every container teardown walks past it. ⚠ **The two are deliberately *not* made
  disjoint**, unlike `SK3502`/`SK3530` and `SK3502`/`SK3532`: one type can be wrong in both ways at
  once and each rule is then saying its own true thing about a different declaration, so suppressing
  either would delete a finding rather than a duplicate. `supersedes` would be the wrong instrument
  in any case — `Supersession.Apply` works on a shared span and these two report different spans — and
  a batch test pins the choice from both ends. ⚠ **`ref struct`s are excluded and that is what keeps
  it from contradicting `SK3532`**: a `ref struct`'s `Dispose()` *is* the disposal contract, bound by
  the language's pattern rule with no interface anywhere, so reporting it as undeclared would report
  the correct spelling of the thing as a defect — on exactly the declaration `SK3532` exists to say is
  missing. ⚠ **The body must release something** — a `Dispose`, `DisposeAsync`, `Close` or
  `GC.SuppressFinalize` call — which is what keeps a pooled object's reset out. Not hosted:
  `CA1063` and `CA1816` both take a type that *already* implements `IDisposable` as their subject.
  **Fix, `fixIsSafe: false`**: the edit is certain and its consequence is not, because a type that
  becomes `IDisposable` is one every caller may wrap in a `using` and every container will now tear
  down.
- `SK3541` `short-lived-http-client` — a `using` owns a directly constructed `HttpClient`, so the
  connection pool underneath it closes once per call and the sockets sit in `TIME_WAIT`. ⚠ **The
  whole family this joins says "this is not disposed" and this one says "this is disposed".**
  `SK3501`, `SK3502`, `SK3530` and `SK3532` each report a release that is missing; here the release is
  present, is what the shape of every other disposable asks for, and is the defect — which is why it
  cannot be an exception inside one of theirs. ⚠ **`new HttpClient(sharedHandler, disposeHandler:
  false)` is excluded and it is the important exclusion**: that is the documented mitigation, the
  sockets belong to the handler, and without the test the rule would report the fix. An entry point is
  excluded too, because a client disposed once for the process is not one disposed per call.
  **Fixless**: the repair is a `static readonly` client, an injected one, or `IHttpClientFactory` — a
  decision about where the type gets its dependencies. Deleting the `using` in place would turn a
  bounded leak into an unbounded one.
- `SK3542` `dangerous-handle-without-ref-count` — `SafeHandle.DangerousGetHandle` where the declaring
  type never touches the reference count. ⚠ **The finding is the missing *pair* and not the call**,
  because the call is named dangerous and is sometimes correct. What is asked is the only question
  answerable from one file with certainty — does the type contain a `DangerousAddRef` or a
  `DangerousRelease` anywhere at all — and either half, anywhere in the type, withdraws it. Whether a
  pair that exists brackets *this* call is a flow question with no safe wrong answer. The residual
  cost is stated rather than guarded: a type that ref-counts through a helper in another file is
  reported and should not be. **Fixless**: the repair either wraps the use in an `AddRef`/`Release`
  pair with a `finally`, or — more often right — stops taking the raw value and hands the `SafeHandle`
  itself to the interop call, and which one applies depends on where the value goes.

⚠ **Two of the five concepts this batch was given were refuted, and neither was allocated an id.**
Both were hosted by something already on, measured rather than assumed:

- **A reference taken to storage that cannot be referenced safely** (issue #62,
  `ByRefArgumentIsVolatileField` and `AddressOfMarshalByRefObject`) is **the C# compiler's own
  work, on by default in both halves**: `CS0420` for a `volatile` field passed by `ref`, `CS0197` for
  a field of a `MarshalByRefObject`. ⚠ **The compiler's version is also the more precise one** —
  Roslyn deliberately exempts the `Interlocked` family from `CS0420`, which is the one place the
  pattern is correct, and a Skala rule written from the inspection's description would have reported
  it.
- **`BeginInvoke` with no matching `EndInvoke`** (issue #174) asks the wrong question on every
  framework Skala supports. Delegate `BeginInvoke` compiles clean on `net10.0` and throws
  `PlatformNotSupportedException` on the first call, paired or not — measured by running it. A rule
  distinguishing the paired case from the unpaired one would be sorting two shapes that both fail
  identically. ⚠ **If anything is worth reporting here it is the call itself and not the missing
  pair**, which is a different concept and needs its own issue before it gets a number.
## `SK4040`–`SK4041` — collections copied on every read, and buffers nobody reads

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK4000 —
Performance" only to keep it out of a section several concurrent branches were editing.** What
follows is the register doing the one job ADR-012 needs it to do — the numbers are taken and written
down where the next milestone will read them. It belongs beside `SK4030`–`SK4034`.

**Two rules shipped out of five issues, and ⚠ the three that did not ship are the more useful half of
the result.** The batch was opened on issues #203, #185, #69, #72 and #267. Three of them turned out
to be covered by an analyzer that already ships in the .NET SDK, and each disposition rests on a
measured default state rather than on a rule's documentation: a probe was built **outside this
repository**, with empty `Directory.Build.props`/`.targets` beside it so that Skala's own raised
`AnalysisMode` could not colour the answer, and the descriptors were then read directly out of
`Microsoft.CodeAnalysis.NetAnalyzers.dll` shipped with SDK 10.0.400.

- `SK4040` a **property** whose getter is one call that allocates a fresh copy of a collection the
  property's own type already accepts. Property syntax reads as a field read — that is the whole of
  the convention, and it is why a caller writes `Items.Count` and then `Items[0]` and pays for two
  copies without seeing either. ⚠ **The reported shape is narrower than "a property that copies", and
  deliberately so: the copy has to be one the property could simply have skipped.** Where the source
  already converts to the property's type by identity or by reference, deleting the materializing
  call is an edit that keeps the signature; where the call also converts — `int[] Items =>
  list.ToArray();` — there is no edit that keeps the declared type, and a finding with no available
  answer is one the reader argues with instead of acting on. ⚠ **A deliberate defensive copy has
  exactly this shape and nothing tells the two apart.** That is not a hole to close later — whether a
  caller should see later mutations is a decision about the API, held nowhere in the source — and it
  is why the rule ships at `suggestion` rather than at the `warning` the proposal asked for, and why
  the fix is `fixIsSafe: false`. The right answer to a deliberate copy is often to keep it and move it
  behind a method, whose parentheses admit the work, and only a person can choose that.
- `SK4041` a local `StringBuilder` that is constructed, appended to, and never read. ⚠ **It is almost
  never a performance mistake by intent; it is a missing line** — usually the `return
  builder.ToString();` that was never written. ⚠ **A builder handed to anything else escapes, and the
  rule stands down at the first sign of it rather than reasoning about it**: an argument, an
  assignment, a `return`, a second local aliasing it, or any reference inside a lambda or a local
  function ends the analysis for that local. The default is "this is a read" and only nine mutating
  members count as writes, so a member added to `StringBuilder` in a later framework silences the rule
  instead of making it wrong. ⚠ **No fix, and the reason is the finding.** The edit that repairs it is
  the read the author did not write; deleting the builder is the other candidate and is wrong whenever
  an append's argument has side effects, so the rule reports and stops, as `SK4024` does.

⚠ **Three concepts were measured and closed against analyzers that already ship, and no id was
allocated for any of them.**

- **Issue #69** — "the class is never inherited and is not `sealed`" — is **`CA1852`**, measured
  `enabledByDefault: true, defaultSeverity: Hidden`. That is the middle of the three states rather
  than "off": the analyzer runs in every build and its findings are invisible until a repository
  raises the severity, which is what ADR-008 already says to do. A probe confirms the behaviour as
  well as the state — `CA1852` reports the `internal` class that nothing derives from, is silent on
  the `public` one, and is silent on the `internal` one that has a derived type. ⚠ **The silence on
  `public` is not a gap a Skala rule could fill**, and it is the same assembly-boundary problem that
  closed #114 and #119: "never inherited" is not answerable from one compilation for a type another
  assembly can see.
- **Issue #72** — "the parameter expects a constant and is given one at runtime" — is **`CA1857`**,
  measured `enabledByDefault: true, defaultSeverity: Warning`, and it is the one probed rule that
  fires in an ordinary `dotnet build` with no `AnalysisMode` raised at all. It covers both halves the
  issue named: it reports a `[ConstantExpected]` parameter given a variable, and it reports a constant
  outside a declared `Min`/`Max` range. There is no residue.
- **Issue #267** — "the sequence is enumerated more than once" — is **`CA1851`**, measured
  `enabledByDefault: false, defaultSeverity: Warning`. Off, so ADR-008's answer is to enable it — the
  same disposition #169's null half took against `CA1508`. ⚠ **What settled it was not the existence
  of the rule but its measured coverage against a thirteen-shape probe**, because the specification
  this batch would have shipped was "report where the receiver's static type is `IEnumerable<T>`", and
  `CA1851` is flow-sensitive rather than type-sensitive and therefore strictly better: it reports the
  two-operator chain, the `foreach` followed by a LINQ call, two `foreach` loops, a `Where` result
  walked twice and an enumeration inside a loop; it declines a `List<T>` **assigned to an
  `IEnumerable<T>` local**, which a static-type rule reports and is wrong about, and it declines the
  branch where only one walk happens at run time. Its own residue is real and small — it says nothing
  about a field — and a rule for that alone would be a narrower duplicate of a better analysis.

⚠ **`SK4006` is not this concept, and the map said it was.** `catalogued.json` credited ReSharper's
`PossibleMultipleEnumeration` to `SK4006`, and `SK4006` is *Review a materialization used only by
`foreach`* — a `ToArray()` that should be **removed**. Multiple enumeration is a `ToArray()` that
should be **added**. ⚠ **And "mirror images" understates it, which is what building the fixture rather
than arguing the point turned up: the two are not merely different, they contradict each other on code
that satisfies both.** A sequence walked once through a materialization and once more afterwards —
`foreach (var v in source.ToArray()) …` followed by `source.Count()` — is a multiple enumeration *and*
an `SK4006` finding, verified by running `SK4006` over exactly that file; taking `SK4006`'s advice
there deletes the only thing keeping the second walk off the source and makes the multiple enumeration
worse. So a map that treats one as coverage of the other does not merely overstate the catalogue, it
records the opposite of what the tool says. The mapping was already deleted from `catalogued.json` and
`ledger-resharper.json` records why; what was missing was anything asserting it. Four shapes now pin
it in `CollectionCopyAndBufferBatchTests` — one satisfying neither, one each satisfying one, and the
contradictory one — so the day any of it stops being true the file goes red.

⚠ **Neither new rule takes a `catalogued.json` key, and that was checked rather than assumed.**
`types-2026.xml` — ReSharper's own issue-type catalogue — has no inspection for a property that copies
a collection and none for an unread `StringBuilder`; the nearest names, `CollectionNeverQueried` and
`RedundantCollectionCopyCall`, are different concepts and the latter is already mapped to `SK1081`.

⚠ **`SK4040` overlaps `CA1819` on exactly one shape and disagrees with it about everything else.**
`CA1819` was measured `enabledByDefault: false, defaultSeverity: Warning` and asks about the
property's *type*: it reports `int[] P => field;`, which copies nothing, and it is silent on
`IReadOnlyList<T> P => xs.ToList();`, which is the whole of this concept. The one shape both see is
`T[] P => array.ToArray();`, and there they say two different true things — one about exposure, one
about cost.

⚠ **Nothing in the SDK reports `SK4041`'s shape.** The probe at `AnalysisMode=All` produced only
`CA1834` on the same code, which is about `Append(char)` versus `Append(string)` and fires whether or
not the buffer is ever read.

⚠ **`SK4041` sits in the performance band and issue #185 proposed the correctness one, and the id is
what it is.** The concept is dead work — an allocation and an `O(n)` copy whose result is discarded —
which is a defensible reading of `SK4000`–`SK4999`, and SonarSource classifies the same rule as a code
smell rather than a bug. The number was allocated from this batch's reserved range and ADR-012 makes
it permanent either way; recording the discrepancy here is what stops it being rediscovered as a
mistake.

**The measurement.** Both rules were swept over Skala's own source through a fresh Release binlog —
`dotnet build Skala.slnx -c Release --no-incremental -bl:`, then `check --load=binlog
--require-fresh-binlog`. ⚠ **The `--no-incremental` half is not optional and the flags are what make
the number readable**: an incremental build's binlog is not stale, it is *partial*, and
`--require-fresh-binlog` is what turns that into an error rather than a plausible answer from a
fraction of the tree. `SK9021` reports **590 of 592 selected files, 100 % coverage** — the two
uncovered files are in no compilation — with **11 CS diagnostics in the load** (`CS9335` × 10,
`CS8933` × 1) and **zero CS errors**, over 1 457 findings in total.

The instrument was verified before either zero was believed. A probe file planting one
`IReadOnlyList<string> Items => entries.ToList();` and one filled-and-dropped `StringBuilder` into
`Rikarin.Skala.Core` made both rules fire through the same binlog pipeline, at the right lines and
with the right messages, which is the only check that sees a real reference set rather than the
fixture harness's. ⚠ **The probe had to be edited before it would build, and the reason is the one the
CA-probing rule is about**: `CA1822` rejected the method as an *error* under this repository's raised
`AnalysisMode`. That is why the `CA*` probes for this batch were built outside the repository with
empty `Directory.Build.props`/`.targets` beside them. The probe was then deleted and the binlog
rebuilt.

- **`SK4040` reports zero, and the shape is absent.** Relaxing *both* discriminating guards — the
  plain-name-path test and the conversion test — and re-sweeping still finds nothing: Skala's source
  contains no property whose whole getter is a materializing call. A grep for the arrow form
  corroborates it, returning 21 hits of which every one is a rule fixture, a batch-test string literal
  or `rules.json` prose. ⚠ **This is the weakest of the three kinds of zero and it is reported as
  such**: it is evidence the rule does not over-fire and no evidence at all about how often the shape
  occurs in the wild.
- **`SK4041`'s shape is present 147 times and declined 147 times.** Relaxing the rule to report every
  `StringBuilder` local with at least one append — the whole population — finds 147 in Skala's source,
  and the shipped rule reports none of them. Spot-checked rather than assumed:
  `DiagnosticCache.CompilationFingerprint` returns `builder.ToString()` and is declined by the read;
  `BaselineCommand` hands its builder to `Describe(builder, …)` and is declined by the escape. ⚠ **147
  declined and zero reported is the number that says the rule is worth having**, because it is the
  count of times the analysis had to be right.

⚠ **The sweep also turned up something that is not this batch's**: `SK9030` records that
`RedundantArgumentAnalyzer` (`SK0232`) throws `AD0001` and is disabled for the rest of the run,
seventeen times. That is issue #298 still live, and it is invisible in the terminal report because
`skala check` writes `SK9030` only into the SARIF's `toolExecutionNotifications` and does not fail the
gate (#295). Neither of this batch's analyzers throws, asserted in
`CollectionCopyAndBufferBatchTests.NoAnalyzerThrows` rather than left to the same silence.

**The reference trees.** `Testing/corpus/real` — the vendored Vixen, Serilog and Newtonsoft.Json — is
unreachable through `skala check` in place (`SK9023`), so it was copied outside the repository, given
a `net10.0` project with empty `Directory.Build.props`/`.targets` beside it, and swept the same way.
⚠ **`<ImplicitUsings>` moves the number a long way and in one direction: 53 658 CS errors with it
disabled, 47 280 with it enabled**, so the slice really is missing the generated usings file and a
sweep taken without it is measuring a differently-broken tree. Both rules report **zero** across
75 514 results, of which 71 757 are CS diagnostics — the corpus does not compile and the analyzers run
in it anyway. `SK4041`'s shape is present there: relaxing the rule finds **36 sites**, including
`Newtonsoft.Json`'s `MemoryTraceWriter`, and the shipped rule declines every one of them because each
ends in `builder.ToString()`.

⚠ **The 36 were briefly recorded as false positives, and the cause is a trap worth writing down:
`skala check` runs the analyzers compiled into the CLI binary, and `git checkout` on a rule's source
does not rebuild it.** The relaxed build used for the census was still in
`Tools/Rikarin.Skala.Cli/bin/Release` when the corpus was first swept, so the relaxed rule's findings
were reported under the shipped rule's id — indistinguishable in the output from a rule that
over-fires. It was caught only because the first "false positive" inspected had its `ToString()` four
lines below the declaration, which no version of the rule should ever have reported. **A sweep taken
after any experiment on a rule's source needs the tool rebuilt first, and the cheap guard is
`--no-cache` plus a rebuild, not one or the other.**
## `SK2200`–`SK2202` — events, delegates and effects that do not happen

⚠ **The prose pass is owed for this block, and it is appended here rather than into § "SK2000 —
Correctness" only to keep it out of a section several concurrent branches were editing.** What follows
is the register doing the one job ADR-012 needs it to do — the numbers are taken and written down
where the next milestone will read them — together with the measurements that disposed of two of the
five issues the batch was given. It is not yet the considered account the sections above carry, and it
belongs beside `SK2013` and `SK2031`.

Three rules from five issues, and the split is the interesting part. ⚠ **Two of the issue texts are
wider than the inspections they cite, and in both cases the inspection is the decidable half.** The
issues were written from an inspection id joined to a category, and the widening happened in the
sentence explaining why Skala should have the rule — which is exactly the place a specification is
least likely to be re-read against the source.

- `SK2200` **the field initializer is overwritten by every constructor** ([#12](https://github.com/Rikarin/SKALA/issues/12), `MemberInitializerValueIgnored`). A
  private instance field is given a value at its declaration, and every constructor that runs field
  initializers assigns it again before anything reads it. Two values are written down for one field
  and only one of them is ever true. ⚠ **The load-bearing guard is the `override` one and it is not
  visible from the shape.** Field initializers run *before* the base constructor call, so a base
  constructor that calls a virtual member this type overrides reads the initialized value — and in
  that program the initializer is not dead at all. The rule declines any field named inside an
  `override` member, loosely, because every shape that test recognises produces silence and none of
  them can produce a finding. ⚠ **Only constructors that actually run field initializers count**: a
  `this(…)` chain does not run them, so a delegating constructor is evidence of nothing and is
  skipped rather than treated as a counterexample. Records, primary constructors and any implicitly
  declared constructor stop the walk for the whole type. The write must be the constructor's *first*
  contact with the field, which means every preceding statement must neither mention it nor contain
  an invocation, an object creation, `this` or `base` — each of which could read it without spelling
  it. ⚠ **The initializer must be side-effect free, because the fix deletes it**; `= new List<int>()`
  is declined rather than reported, since an allocation whose constructor registers something is
  precisely what `SK2013` and `CA1806` argue about. Fix, `fixIsSafe: true`.
- `SK2201` **the unsubscription passes an anonymous function** ([#18](https://github.com/Rikarin/SKALA/issues/18), `EventUnsubscriptionViaAnonymousDelegate`).
  ⚠ **The issue reads as "the `+=` can never be undone", and that question needs a lifetime proof
  nobody can produce.** A subscription that lives exactly as long as its subscriber is the
  overwhelmingly common case and it is correct; separating it from the leak needs to know that the
  publisher outlives the subscriber, which is not decidable from the subscribing line. **The
  inspection the issue cites is about the `-=`, and that one needs no proof at all.** Delegate
  removal compares invocation-list entries by target and method, and an anonymous function written
  at one syntax site is a different instance from one written at any other — so `changed -= (s, e)
  => Redraw();` removes nothing, in every program, whatever was subscribed. The `+=` half of the
  concept is not shipped and the fixtures say so in a file of their own. A method group is correct
  and is never reported. Report-only: the repair is to name the delegate and store it where both
  sides can see it, which is a change to the subscribing method and usually to the type's fields —
  three edits away, in places the diagnostic cannot see.
- `SK2202` **the modification sits inside a conditional invocation** ([#42](https://github.com/Rikarin/SKALA/issues/42),
  `PossiblyUnintendedSideEffectsInsideConditionalInvocation`). ⚠ **The issue reads as `?.`, `??` and
  `&&` together, and three of those four have no sound rule in them.** Short-circuiting is what
  `&&`, `||`, `??` and `?:` are *for*: `x != null && x.Consume()` is the idiom rather than the
  defect, and no condition separates the deliberate case from the accidental one. **ReSharper's own
  description is narrower than the issue and is decidable**: "Possibly unintended *modification*
  inside *conditional invocation*" — an assignment, a `++` or a `--` inside the part of a `?.` or
  `?[` that runs only when the receiver is not null. `logger?.Log(sequence++)` stops advancing the
  counter the moment `logger` is null, and nothing on the line says the increment was conditional;
  the null test is about the *receiver*, and the arguments fall inside its reach through precedence
  rather than because anybody asked. Only a modification counts — an invocation does not, because
  `logger?.Log(Format(value))` is ordinary code. Report-only: hoisting the modification out turns an
  intermittent effect into an unconditional one, which is a behaviour change and not a cleanup.

⚠ **`SK2202` and `SK2064` take opposite sides of the same fact and cannot meet.** `SK2064` reports
`&` written where `&&` was meant and *declines* any right operand with a side effect, because a side
effect there is the documented reason to reach for the non-short-circuiting operator. `SK2202` reports
a side effect that is skipped. If the two ever overlapped one of them would be wrong; they do not,
because neither `&` nor `&&` appears anywhere in `SK2202`'s shape and `?.` appears nowhere in
`SK2064`'s. A batch test asserts that on a file carrying both rather than trusting the argument.

### ⚠ Two issues closed as hosted, and `CA1806` is on by default rather than off

⚠ **[#50](https://github.com/Rikarin/SKALA/issues/50) — "the constructed object is discarded" — is `CA1806`, and no id was allocated for it.**
Measured on a probe built outside this repository with empty `Directory.Build.props`/`.targets` above
it, on SDK 10.0.400: at **stock settings, with no `AnalysisMode` and no `.editorconfig`**, `CA1806`
reports every `new Foo();` in statement position — `isEnabledByDefault: true`, `defaultLevel: note`.
It fires on the plain case, on the case with constructor arguments, on `new
InvalidOperationException(…)`, **and on `new Timer(…)`** — the "constructor with side effects"
exemption the issue asks for does not exist in `CA1806` either. `_ = new Widget();` is correctly
silent. `IDE0058` reports the same four lines once code style is enforced. There is no residue: the
concept is the whole of `CA1806`'s object-creation branch, and `SK2013` already covers the exception
subset with the fix `CA1806` does not have. ⚠ **ReSharper ships `RemoveConstructorInvocation` at
`DO_NOT_SHOW`**, which is the same verdict from the other direction.

⚠ **[#12](https://github.com/Rikarin/SKALA/issues/12)'s main shape — "the assigned value is never read" — is `IDE0059`, and only its fourth
inspection survived as `SK2200`.** `IDE0059` reports every local-assignment shape the issue names, at
the same lines, with a fix. Its measured state is the middle one and worth writing down: the
descriptor says `isEnabledByDefault: true, defaultLevel: note`, tagged
`EnforceOnBuild_HighlyRecommended` — but on the probe, `EnforceCodeStyleInBuild=true` **alone produced
no `IDE0059` at all**, and it appeared only once `dotnet_diagnostic.IDE0059.severity` was raised in an
`.editorconfig`. Enabled, and silent in a build until somebody asks for it. ⚠ **What no `CA*` or
`IDE*` reports is the field-initializer shape**, `MemberInitializerValueIgnored`: at
`AnalysisMode=All` with every style rule at `warning`, the probe's overwritten initializers drew
nothing. That residue is `SK2200`.

### ⚠ `S3172` is refuted rather than narrowed, and `SK2201` is the only decidable part of it

[#165](https://github.com/Rikarin/SKALA/issues/165) proposes Sonar's `S3172`, "delegates should not be subtracted": `d -= h` on a multicast
delegate removes the last matching contiguous run rather than every occurrence, and removes nothing
at all when the run was combined differently. ⚠ **The general form reports correct code and there is
no narrowing that saves it.** `subscribers -= handler` is what every `Remove` method and every
`Dispose` in event-driven C# is made of; whether `handler` is itself multicast is a fact about a value
that arrived as a parameter, and it cannot be decided from the subtraction. The one shape that *is*
decidable — subtracting a delegate combined literally on the line, `d -= (Action)(a + b)` — occurs
nowhere in either reference tree and is not a shape anybody writes. What remains of the concept after
that is the anonymous-function case, which is `SK2201`, so the rule is not lost: it is the same defect
approached from the operand rather than from the operator. No id is allocated for the general form.

### The measurement, and every zero classified

Two reference trees and the three vendored corpus slices, all on SDK 10.0.400.

| Tree | Load | Binlog coverage (`SK9021`) | CS diagnostics | `SK2200` | `SK2201` | `SK2202` |
|---|---|---|---|---|---|---|
| Skala | `--load=binlog --require-fresh-binlog` | 591 of 593 (100 %) | 11 across 2 codes (`CS9335` ×10, `CS8933` ×1) | 0 | 0 | 0 |
| Vixen | `--load=binlog --require-fresh-binlog` | 4 651 of 4 726 (98 %) | 161 across 8 codes | 0 | 0 | **1** |
| corpus `newtonsoft` | `--load=binlog` | n/a (own project) | 2 619 | 0 | 0 | 0 |
| corpus `serilog` | `--load=binlog` | n/a (own project) | 1 125 | 0 | 0 | 0 |
| corpus `vixen` | `--load=binlog` | n/a (own project) | 11 113 | 0 | 0 | 0 |

⚠ **Both binlogs were built with `--no-incremental` and read with `--require-fresh-binlog`, and the
first Vixen pair was discarded because the build was incremental.** An incremental build's binlog is
not stale — its mtime is seconds old — it is *partial*, and a sweep over a third of a tree that comes
back green is worse than no sweep because it is believed. The re-taken Vixen run reproduced the same
coverage (98 %) and the same finding, so nothing changed by it; the discipline is the point.

⚠ **The corpus is three copies of every file.** `Testing/corpus/real/` carries `X.cs`,
`X.expected.cs` and `X.arranged.expected.cs` side by side — 380 sources in 1 140 files — and
compiling all of them together produced 11 260 `CS0111` and 2 112 `CS0101` before the duplicates were
excluded. It also needs one project per slice: the three vendored libraries collide with each other.
**`<ImplicitUsings>` moves the number and does not fix it: 13 036 CS errors disabled → 10 996
enabled**, 2 040 fewer, still dominated by `CS0246` for packages the slice does not carry. The corpus
is a formatter fixture set, not a compilable tree, and a semantic rule's zero on it is worth much less
than the same zero on Vixen.

**The instrument was verified before any zero was believed.** A file carrying all three shapes was
planted in `Core/Rikarin.Skala.Options/`, the solution rebuilt, and `skala check --load=binlog` run
over it: `SK2200`, `SK2201` and `SK2202` each reported, on the right lines, through the real pipeline
rather than through the fixture harness. The file was then deleted.

Classifying the zeros:

- `SK2201` — **shape absent.** Zero occurrences of `-=` with an anonymous function anywhere in Vixen
  or in Skala's compiled sources. The one occurrence in the whole repository is
  `Testing/corpus/constructs/syntax/event-accessors.cs`, a formatter construct fixture that is not a
  `Compile` item and is in no compilation.
- `SK2200` — **shape absent, and measured rather than assumed.** The obvious explanation for the zero
  was the side-effect-free-initializer guard, since `= new()` and `= []` are how Vixen writes most
  initializers. ⚠ **That explanation is wrong**: with the guard temporarily removed and the CLI
  rebuilt — the relaxation confirmed live by `SK2200/−/an-allocating-initializer` going red — the
  Vixen sweep still reports **0**. What the rule declines is not what makes the tree quiet.
- `SK2202` — **one true finding and nine correct declines**, all in the same sweep. See the
  initializer-member note above.
- ⚠ **The `d -= (a + b)` shape `S3172` needs is absent from both trees**, which is the measurement
  behind refusing to allocate an id for the general form.

⚠ **Two analyzers crash on both reference trees and neither is in this batch.**
`RedundantArgumentAnalyzer` (`SK0232`) threw `AD0001` 17 times on Skala's own tree and 92 times on
Vixen — "Index was outside the bounds of the array" — and `RedundantCastAnalyzer` threw twice on
Vixen. `skala check` records this as `SK9030` in the SARIF's `toolExecutionNotifications` and **does
not fail the gate** (issue #295), so both have been silently reporting nothing for the rest of every
run they crash in. That is a pre-existing defect on `master` and is written down here rather than
fixed in this batch. None of `SK2200`–`SK2202` appears in any `SK9030`.
## `SK2170`–`SK2174` — statements and literals that read as something else

⚠ **The prose pass for `SK2170`–`SK2174` is owed.** What follows is the allocation register entry —
enough that the ids are written down and `RuleCatalogTests.EveryCatalogueRule_IsNamedInTheRegister`
can see them — not the worked-through account the rest of this section carries.

**`SK2170`–`SK2174` extend the `SK2060`–`SK2064` family**: an expression or a statement whose shape
on the page says one thing and whose grammar says another. What separates the two batches is where
the misreading lives. `SK2060`–`SK2064` read *operators* — an `=` that should have been `==`, two
operands that are the same expression, an `=` written hard against a `-`. These five read *layout,
spelling and pattern shape*: how far a line is indented, how many digits an escape took, which side
of an `is` a `!` sits on, and a pattern that is a null check written inside out.

⚠ **Three of the five concepts the issues describe turned out to belong to the compiler, and the
probe that established it reached further than doc 17 supposed.** Nine shapes were compiled on
SDK 10.0.400 and the warnings read off the build:

- **`CS0642`, "possible mistaken empty statement", covers the whole of `MisleadingBodyLikeStatement`
  (#38's first inspection).** It fires for `if`, `else`, `lock`, `do`, `using` and `fixed` outright,
  and for `while`, `for` and `foreach` *exactly when a block follows the `;`*. That last clause is
  the point: `while (Step()) ;` standing alone is the idiomatic spin loop and is silent, and the
  same line followed by `{ … }` warns. The compiler is quiet precisely where the shape is harmless
  and loud precisely where it misleads, so there was nothing for a rule to add. `SK2170` ships the
  *indentation* half of #38 instead, which the issue did not describe and which no compiler sees.
- **`CS0078` covers `LongLiteralEndingLowerL` (#39's first inspection)** — "the 'l' suffix is easily
  confused with the digit '1'", on by default, firing on `1l` and on `1lu` and silent on `1ul`.
- **`CS8518` makes `is not { }` uncompilable on a non-nullable value type** — "an expression of type
  'int' can never match the provided pattern", and likewise for a `T` constrained to `struct`. That
  is not a rule the compiler took away; it is a *guarantee handed to `SK2173`*. There is no
  compiling program in which `is not { }` stands on something `is null` would reject, so the rewrite
  is total and the rule needs no semantic model to know it.

Both hosted inspections are now recorded in `classify.py`'s `HOSTED` map against the compiler
warning that owns them, beside `PartialMethodParameterNameMismatch`; each had been falling through
to the Uncovered residue and inflating the gap by one.

`SK2170` `misleading-body-indentation` — an `if`, `else`, `while`, `for`, `foreach`, `lock`, `using`
or `fixed` with a single unbraced statement as its body, followed by a statement indented to exactly
the body's column. ⚠ **The second place in this catalogue where trivia rather than structure decides
a correctness finding, and `SK2063` is the first.** Indentation is not structure anywhere in C#, so
nothing but a formatter is looking at it. ⚠ **Prefix comparison, never a column count** — one tab
and one space are the same column and different indentation, so a line that mixes them fails the
prefix test and is declined rather than guessed at. Report-only: bracing the body and outdenting the
statement are different programs. · `SK2171` `variable-length-hex-escape` — a `\x` escape with one,
two or three hex digits, whose length the character after it decides. ⚠ **`\x` is the only escape in
C# without a fixed length**, so appending a letter to a string ending in `\x41` silently changes its
last character; `\u` cannot do this, which is why the fix is a spelling change and nothing else. ·
`SK2172` `forgiven-is-operand` — `x! is T`, where the `!` reads as an inverted `is` and suppresses
nothing. ⚠ **`is` never issues a nullability warning about its own operand**, measured by compiling
`if (s! is object) { }` followed by `s.Length` with and without the `!`: both report `CS8602`, at
the same position. That measurement *refuted* the reason this rule was first going to ship a fix —
the suppression was assumed to carry into the flow state, and does not — and it is report-only for
the other reason instead, that `x is not T` and `x is T` are both plausible readings of `x! is T`
and are opposite programs. · `SK2173` `negated-empty-pattern` — `not { }` with no type, no
positional clause, no subpattern and no designation, which matches exactly the null values. ·
`SK2174` `unparenthesised-precedence-mix` — an operand of a shift or bitwise operator that is itself
an unparenthesised binary expression of a different precedence family.

⚠ **`SK2174` could not ship until the `SK0209` boundary was settled, and it is settled by
construction rather than by agreement.** `skala arrange` removes redundant parentheses;
`ParenthesesRedundancy.MayRemove` refuses unconditionally when the parent is a shift or a bitwise
operator, because `resharper_parentheses_non_obvious_operations` names exactly those. Every pair of
parentheses `SK2174` adds has such a parent, so the arranger will never take one back and
`skala fix` and `skala arrange --aggressive` cannot fight. ⚠ **The other direction was checked
too**: `CodeCleanupTask_AddMissingParentheses` exists in the oracle and **no committed profile
enables it**, so the formatter is not already doing this.

⚠ **Three of `SK2174`'s drafted rows are gone, each for a different reason.** A *comparison* operand
under `&` or `|` only compiles when every operand is `bool`, which is `SK2064`'s subject — it
reports it and offers `&&`, so reporting it here as well would be two rules on one token. Since
arithmetic and shift operands are never `bool`, the two rules end up disjoint *by construction*:
`SK2064` fires only on `bool` operands and `SK2174` only on integral ones, and no expression can
satisfy both. A **`?:` operand** describes no program at all — the conditional operator binds looser
than every binary operator, so it can never *be* an unparenthesised binary operand, and the only
reachable nesting is `a ? b : c ? d : e`, the chained-ternary idiom every reader parses correctly. A
**shift under a bitwise operator** is bit packing — `key << 8 | digest[i]` — and the corpus is what
cut it: the rule without that exclusion reports the shape on Skala's own `CorpusSample.KeyOf` and in
`pathological/operators-crammed-together.cs`, and both are the idiom rather than the hazard.

⚠ **`SK2172` is `Semantic` for one reason only: to be disjoint from `SK2111` by construction.**
`SK2111` owns the `!` that is inert because nullable warnings are off at that position or because
the operand is a non-nullable value type; `SK2172` declines both, so no `!` in the catalogue can be
reported twice. Two fixtures — `value_type_operand_is_sk2111.cs` and
`warnings_disabled_is_sk2111.cs` — satisfy *both* rules' shapes, which is the only kind of fixture
that tests disjointness at all: a fixture proving the two shapes merely differ proves nothing,
because they differ whether or not either rule looks.

### ⚠ Two guards the corpus bought, and one instrument caught lying

**The measurement.** `--load=loose` over the 4 459-file corpus, copied outside the repository
because `SK9023` makes it unreachable in place; and `--load=binlog --require-fresh-binlog` over
Skala's own tree, from a `--no-incremental` Release build that produced **0 CS diagnostics** and a
binlog covering **592 of 594** selected files (100 %; the two are `build/Build.cs` and
`build/Configuration.cs`, which `Skala.slnx` does not contain). Shipped, all five report **zero** on
Skala's tree; on the corpus four report zero and `SK2174` reports **6**.

⚠ **One of those six is a genuine defect in a reference tree, and it is the best thing in the
batch.** `real/newtonsoft/Newtonsoft.Json/Utilities/DateTimeUtils.cs:812` writes
`int m = n >> 5 + 1;` two lines below a comment that says `n >> 5` is the conservative estimate for
the month. `+` binds tighter than `>>`, so the code shifts by six. Three of the six findings are
that one line in its `real/`, `collapse/` and `scramble/` copies; the other three are
`pathological/operators-crammed-together.cs`, a file whose whole purpose is crammed operators. ⚠
**The fix parenthesises what the code does now**, not what the comment says it meant, which is the
only thing a `fixIsSafe` edit may do — it makes the sentence unambiguous and leaves the decision to
a person.

⚠ **Every zero classified, and one of them is not a zero.** `SK2171` and `SK2172` are **shape
absent**: widened to report *every* `\x` escape including the four-digit ones, `SK2171` finds
nothing in 4 459 corpus files or Skala's 592, and a `!` immediately left of an `is` occurs nowhere in
either tree. `SK2170` and `SK2173` are **shape present and correctly declined**, in quantity. ⚠
**But `SK2172`'s corpus zero is neither — it is the analysis never running.** Under `--load=loose`
**no `Semantic`-scope rule fires at all**: 1 109 of that run's 1 115 findings are `Syntax` and the
remaining 6 are tool diagnostics, and *zero* come from any of the catalogue's 100-odd semantic
rules. A planted `x! is string`, with and without a `#nullable enable` directive above it, produces
nothing there. `SK2172`'s only real measurement is the binlog one, where 589 findings from 22
distinct semantic rules prove the semantic half ran.

⚠ **`SK2170` shipped with a guard the first draft did not have, and the corpus bought it.** Asking
for the following statement to be indented *at least as deep* as the body reports **4** times on
`unformatted/scramble/`, a slice whose whitespace is randomised on purpose; there the following
statement lands 2, 4 or 6 columns *past* the body, which reads as mangled or as a continuation
rather than as a sibling. Asking instead for the column a reader would actually see — exact
alignment — declines all four and costs nothing real. **A rule that reads whitespace for meaning
meets machine-mangled whitespace sooner than most**, which is the sentence `SK2063` earned first.

⚠ **`SK2173`'s designation guard is the largest single thing standing between this batch and a
catastrophe, and the number is not close.** Widened by dropping *only* the "no designation"
requirement, `SK2173` reports **203** findings on Skala's own tree and **39** on the corpus, of
which **13 distinct occurrences are in `real/vixen`**. Every one is `x is not { } bound`: the
idiomatic C# null-check-and-bind, which is not a null check at all because it binds. The shipped
rule declines all of them and reports zero. ⚠ **Skala's own analyzers are among the 203** — the file
implementing `SK2170` contains four of them.

⚠ **The `ImplicitUsings` exercise moved almost nothing here, and the reason is worth writing down.**
Compiled as a project over `real/newtonsoft` with the `.expected.cs` duplicates excluded, the slice
reports **1 808** CS errors with `ImplicitUsings` disabled and **1 806** with it enabled — Newtonsoft
targets old frameworks and writes every `using` out. The slice does not compile either way, so no
semantic rule can be measured on it; that is why `SK2172` is measured on the binlog load instead,
and why the other four are `Syntax` and bind nothing.

### Sabotage

Each guard was removed in turn and the fixture suite re-run. ⚠ **Two of the eleven turned nothing
red, and both were defects rather than passes.**

| Guard removed | What went red |
|---|---|
| `SK2170`'s exact-alignment test | `SK2170/−/deeper_than_the_body` |
| `SK2170`'s empty-body/block exclusion | `SK2170/−/empty_body_then_block_is_cs0642` |
| `SK2170`'s directive check | `SK2170/−/directive_between` |
| `SK2171`'s four-digit ceiling | `SK2171/−/four_digits` |
| `SK2171`'s `\\` consumption | `SK2171/−/escaped_backslash` |
| `SK2172`'s nullable-context check | `SK2172/−/warnings_disabled_is_sk2111` |
| `SK2172`'s value-type check | `SK2172/−/value_type_operand_is_sk2111` |
| `SK2173`'s designation check | `SK2173/−/not_empty_in_the_four_ways` |
| `SK2173`'s comment check | `SK2173/−/comment_inside_the_pattern` |
| `SK2174`'s bit-packing exclusion | `SK2174/−/bit_packing` |
| `SK2174`'s different-family test | `SK2174/−/one_family_throughout` |

⚠ **`SK2170`'s empty-body exemption declined every empty statement, and removing it turned nothing
red.** The reason is that `while (x) ;` puts the `;` on the header's own line, which the line test
already declines — so the exemption was unreachable through the fixtures. Widening the sabotage
showed it was also *wrong*: `while (x)`, then `;` on its own line, then a statement aligned with the
`;` is a true finding that `CS0642` does not report, because the compiler reports an empty loop body
only when a block follows. The guard now declines exactly that overlap, and two new fixtures reach
it from both sides. This is the same shape of defect `SK2063`'s removed third condition was.

⚠ **`SK2170`'s directive fixture put the reported statement *inside* the `#if`.** With the symbol
undefined that statement is disabled text, so the block held one statement, no pair was compared,
and removing the directive check changed nothing. The region has to sit *between* two live
statements for the guard to be reachable at all. Repaired; the sabotage now turns it red. ⚠ **A
fixture whose interesting half is inside an inactive `#if` tests the preprocessor, not the rule.**

⚠ **`EmptyStatement` — #38's second inspection, a stray `;` standing alone between two statements —
is deliberately left uncovered and is not in the parity map.** `CS0642` does *not* reach it (the
probe confirms a lone `;` in a block warns about nothing), and nothing in Skala covers it either. It
is redundancy rather than correctness: a `;` on its own line misleads no one. Leaving it out of
`catalogued.json` inflates the measured gap by one, which is the safe direction for a hand-written
map to be wrong in. `ConfusingCharAsIntegerInConstructor` — #39's third inspection, a `char`
argument widening to an `int` parameter — is uncovered for a different reason: it is a question
about overload resolution rather than about how a literal reads, and it is a different rule from
`SK2171`.
