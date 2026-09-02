# 16 — Risks and Open Questions

## ⚠ The reference trees are a test subject, not a specification

**Vixen does not get a vote on what the rules are.** It is the tree the tool is measured against
because it is the largest body of C# the author owns and the one whose formatting the author cares
about — not because its present habits are the standard. Where Vixen does not follow a rule, **Vixen
changes.** The rule is not removed, softened, demoted, or guarded until the tree goes quiet.

The reason is that the alternative is circular. Skala exists to make eighteen repositories consistent
and modern, and the modernization set exists because an agent trained on a decade of C# writes
2018-era code unless something says no. A rule tuned until the largest existing codebase passes it
cannot say no to that codebase — it certifies whatever already exists, and the tool can never move
anything forward. The same applies to Serilog and Newtonsoft.Json in `corpus/real/`, which are there
to be *unfamiliar* input; a rule adjusted so that vendored third-party code stops firing has been
adjusted by a repository with no stake in this project's standards.

⚠ **This does not touch the false-positive bar, and conflating the two is the failure mode this
section is most worried about.**

| | What it is | What it means for a rule |
|---|---|---|
| **A false positive** | The finding is **wrong**. The code does not do what the rule says it does. | The rule is defective. Fix it or do not ship it. The zero-false-positives bar in [08](08-rule-catalogue.md) is unchanged and unconditional. |
| **A correct finding nobody wants to act on** | The finding is **right**. Somebody has decided not to change the code today. | The rule is fine. This is what `skala baseline` is for: accept the present, gate the future ([09](09-quality-gates-and-reporting.md)). |

So `SK3002`'s seven true findings on Vixen are seven baseline entries and a small piece of Vixen's
backlog. They were never evidence about the rule.

⚠ **What stops being evidence of quality is a low finding count on the reference trees.** "Fires zero
times on Vixen" is a fact about Vixen. It is a reason to be careful about a rule whose correctness is
therefore *untested* on real code — that argument is R3's and it survives intact — and it is never a
reason to cut, demote or disable a rule that is right. The three reasons that still justify cutting a
rule are all about the rule:

1. **It duplicates a diagnostic the user already sees** from the compiler or a framework analyzer.
2. **It costs something measurable for no gain** — a compilation-scoped rule that disables the warm
   incremental path for every run.
3. **It cannot be implemented correctly**, or its fix cannot be made behaviour-preserving.

[08](08-rule-catalogue.md) § "Rule status" records which decisions rest on those three and which rest
on a Vixen count and are therefore **suspect and awaiting revisit**. They are marked rather than
reversed in place, because the record of a decision being reversed is worth more than a document that
reads as though it were always right.

⚠ This belongs in [00](00-vision-and-principles.md) § "Non-negotiables" as well as here, and is not
yet written there.

### ⚠ The opt-in that does not cost what it says

M9 revisited `SK3001` under the instruction above. Half of its recorded justification — "Vixen
contains no `async void` method at all" — is struck, and the other half held up under measurement:
enabling any compilation-scoped rule sends every run with a change down the cold path, which on
Skala's own tree is 8.5 s against a warm 6.9 s and moves the analyzer phase from one tree to all of
them. **The `none` default stands on reason 2 alone, which is where it should always have stood.**

⚠ **What did not survive is the sentence that made the default acceptable.** `rules.json` tells a
repository that has `async void` to set `dotnet_diagnostic.SK3001.severity = warning` and "pay the
cost knowing what it bought". It does not pay it. `IncrementalAnalysis` decides whether the warm path
is available by asking `descriptor.IsEnabledByDefault`, which comes from `rules.json` and which an
`.editorconfig` severity does not change; Roslyn's driver, meanwhile, filters on the *effective*
severity and runs the analyzer. So the opted-in repository gets the rule **and** keeps the warm path
— and `DiagnosticCache.Store` drops uncacheable rule ids from what it writes, so every unchanged file
contributes a cache entry containing no `SK3001` findings at all.

The rule therefore reports correctly on a cold run and **silently under-reports on every warm one**.
Findings vanish rather than going stale, which is the direction that looks clean. Nothing in the
repository fails when this happens, because the only compilation-scoped rule that is on by default is
`SK7020`, which is behind `--duplication`.

⚠ It is worth naming what this is an instance of. **A cost that is documented and not actually
charged is not a conservative default — it is an unmeasured one wearing a measurement's clothes.**
The guard needs to ask the effective severity, and the per-tree scan that answers it needs its own
`--profile` measurement before it goes in, because the warm path exists to stay under five seconds.

## The risks that could sink this

### R1 — ⚠ Rider fidelity is asymptotic, and the last 0.1 % is most of the work

ReSharper's formatter is twenty years of accumulated special cases, many undocumented, several
version-dependent. The differential harness will get to 99 % quickly and then produce a long tail of
one-line divergences in constructs nobody thinks about — a lambda inside a collection initializer
inside an attribute argument, a conditional access chain broken across a ternary.

**Why it matters:** the whole value proposition is "the IDE and the gate agree". At 99 % they
disagree on roughly one line in a hundred, which on a 1.35 M-line tree is 13 000 lines that Rider
will reformat back the moment someone opens the file. Formatting ping-pong between two tools is
worse than either tool alone.

**Mitigation:** the divergence register (`SK-DIV-*`) plus a hard rule about where the residue is
allowed to sit — § "The rule, re-stated" below has it, and § "The rule as originally written" has
the version it replaces and the reason that version could never be met. Plus
`resharper_formatter_tags_enabled` as the human escape hatch for the handful of places where the
tools cannot agree. Plus, honestly: if a divergence is small and Skala's answer is better, change the
Rider setting to match Skala rather than the reverse — the settings are the author's, and they can
move.

⚠ **M3 measured it, and the shape is exactly as predicted.** `fidelity constructs` attributes every
divergent line to the innermost node that owns it and puts that beside how often the construct
occurs: of the 54 constructs occurring more than 50 times in `corpus/real/`, **27 are at 100 %** at
98.86 % overall. The rule is not met. What it is short of is characterised rather than mysterious —
the largest attributed shares are `IdentifierName`, `StringLiteralExpression` and `ArgumentList`,
which are where a wrap decision *lands* rather than constructs mishandled in themselves, and the
eight `SK-DIV-*` entries name the decisions.

⚠ The paragraph above under-states one thing and over-states another. The tail is *not* mostly
exotic constructs: two of the eight entries (SK-DIV-0001, SK-DIV-0004) are about preprocessor
conditionals, which are ordinary, and they are limitations of parsing without a project rather than
of the formatter. And "99 % means 13 000 lines Rider will reformat back" is the wrong test for those
two, because Skala does not *touch* what it cannot see — the disagreement is code Skala left alone,
not code it moved.

⚠ **M3.1 measured it again at 99.70 %, and the rule is still not met: 37 of 56.** The count is
`constructs`' own, now run with the oracle's preprocessor symbols supplied — without them a file
wrapped in a `#if` is disabled text for Skala and every line of it counts against whatever construct
happens to own it, which attributes SK-DIV-0004 to `ClassDeclaration` and says nothing about either.

#### The rule as originally written, and why it could not be met

**The original text, preserved verbatim, because a bar that is quietly replaced is a bar that was
never measured against:**

> any construct that appears in the corpus more than 50 times must be at 100 %, and the tail is only
> allowed in constructs that are genuinely rare

⚠ **It cannot be met short of 100 % line fidelity, which is worth saying plainly rather than missing
it four milestones in a row.** The report attributes every divergent line to the innermost node that
owns it, so "at 100 %" means "no divergent line is attributed to this construct". Two things follow,
and they are the two halves of why the rule is unusable:

1. **The population is almost everything.** Every divergent line is attributed to *something*, and
   almost everything that occurs in a 76 000-line corpus at all occurs more than fifty times. Of the
   twenty-one constructs the residue touches today, twenty occur more than fifty times; the one
   exception, `ComplexElementInitializerExpression`, occurs seventeen. So the "genuinely rare"
   escape hatch the rule promised covers **2 of 185** attributed divergent lines. The rule is
   therefore equivalent to 100 % line fidelity wearing a frequency test.
2. ⚠ **The frequency test and the fidelity test count different things, and that is the deeper
   defect.** "Appears more than 50 times" counts *occurrences of the construct*; "at 100 %" is
   measured over *lines the construct is the innermost owner of*. Those two populations are unrelated
   in size. `ForStatement` occurs 376 times and owns **2** lines, so a single divergence makes it
   0.00 % — a construct that is 376-times common by the gate's own test and statistically empty by
   the test it is then graded on. `Block` occurs 4 899 times and owns 17 923 lines, so five divergent
   lines make it 99.97 %. A rule that selects on one number and grades on the other is measuring
   noise at one end and nothing at the other.

**What R1 is actually for, and what must survive any re-statement.** Rider and Skala must not
reformat each other's work. A divergence class that is *systematic* — a construct Skala handles
differently from the oracle every time it appears — produces ping-pong on every file that contains
the construct, and no amount of overall percentage hides it. A divergence that is a *tail* — one
line here, one there, in shapes nobody writes twice — costs a handful of lines once. The original
rule was reaching for that distinction and picked the wrong instrument for it.

#### The rule, re-stated

**R1 is met when, on `corpus/real/`, both of the following hold, measured by
`ConstructReport` with the oracle's own preprocessor symbols supplied:**

| | Population | Bar |
|---|---|---|
| **(a) the share rule** | every construct that is the innermost owner of **≥ 100 lines** of the oracle's output | its **attributed share** — divergent lines it owns over lines it owns — is **≤ 1 %** |
| **(b) the count rule** | every construct below that floor | it is attributed **≤ 3 divergent lines** |

Three things changed and each is deliberate:

1. **The population is keyed on lines owned, not on occurrences**, because lines owned is what the
   fidelity half measures. This is the defect in point 2 above, and fixing it is most of the value:
   `ForStatement` and `Parameter` no longer masquerade as common constructs.
2. **The bar is a share rather than zero.** 1 % is not an arbitrary round number: it is the rate this
   document's own opening paragraph calls unacceptable — "at 99 % they disagree on roughly one line
   in a hundred … which Rider will reformat back". A construct at or under 1 % is inside the tail; a
   construct above it is a systematic disagreement in a construct with real mass, which is exactly
   what R1 exists to forbid.
3. **Below the floor the rule switches to an absolute count**, because a share computed over eleven
   lines is not a measurement. Three lines is small enough that a systematic defect cannot hide
   behind a rare construct, and large enough that genuine exotica are permitted — which is the
   "genuinely rare" allowance the original rule promised and could not deliver.

⚠ **Measured at `8cbd66d`** — `dotnet run --project Testing/Rikarin.Skala.Testing -c Release -- constructs real`,
which reports 56 constructs occurring more than 50 times and 37 at 100 % under the old rule. Under
the re-stated rule the same table reads:

| Construct | Occurrences | Lines owned | Divergent | Share | Verdict |
|---|---:|---:|---:|---:|---|
| `Block` | 4 899 | 17 923 | 5 | 0.03 % | ✅ (a) |
| `ClassDeclaration` | 481 | 15 536 | 11 | 0.07 % | ✅ (a) |
| `IdentifierName` | 78 013 | 13 566 | 64 | 0.47 % | ✅ (a) |
| `ArgumentList` | 15 779 | 3 642 | 25 | 0.69 % | ✅ (a) |
| `StringLiteralExpression` | 3 387 | 3 064 | 18 | 0.59 % | ✅ (a) |
| `NumericLiteralExpression` | 8 537 | 2 937 | 14 | 0.48 % | ✅ (a) |
| `CompilationUnit` | 380 | 687 | 4 | 0.58 % | ✅ (a) |
| `PredefinedType` | 6 490 | 415 | 2 | 0.48 % | ✅ (a) |
| `FalseLiteralExpression` | 396 | 331 | 2 | 0.60 % | ✅ (a) |
| `TrueLiteralExpression` | 402 | 323 | 1 | 0.31 % | ✅ (a) |
| `NullLiteralExpression` | 656 | 320 | 1 | 0.31 % | ✅ (a) |
| `CollectionExpression` | 596 | 290 | 1 | 0.34 % | ✅ (a) |
| `SingleVariableDesignation` | 725 | 182 | 1 | 0.55 % | ✅ (a) |
| **`ParameterList`** | 3 125 | 309 | 4 | **1.29 %** | ❌ **(a)** |
| **`EqualsValueClause`** | 5 368 | 70 | **11** | 15.71 % | ❌ **(b)** |
| **`Parameter`** | 4 314 | 11 | **8** | 72.73 % | ❌ **(b)** |
| `IfStatement` | 1 429 | 39 | 2 | 5.13 % | ✅ (b) |
| `ForStatement` | 376 | 2 | 2 | 100.00 % | ✅ (b) |
| `ComplexElementInitializerExpression` | 17 | 6 | 2 | 33.33 % | ✅ (b) |
| `DefaultLiteralExpression` | 91 | 60 | 1 | 1.67 % | ✅ (b) |

Every construct not listed owns no divergent line and passes both clauses trivially. `(file)` — the
report's row for lines no syntax node owns, 6 of 5 598 — is not a construct and is not graded.

⚠ **R1 is not met, and it now says something.** Three constructs fail rather than nineteen, and all
three name an entry that is already in the register: `EqualsValueClause` and `Parameter` are
[SK-DIV-0005](../divergences.md)'s `=` break and the argument-list chop of
[SK-DIV-0007](../divergences.md); `ParameterList` at 1.29 % is the same chop seen from the enclosing
list. **That is the property the old rule lacked: its failures are a work queue rather than a
restatement of the overall percentage.** The old rule's nineteen failures included `Block` at
99.97 %, which is not work anyone should do.

⚠ **The harness still prints the old rule's verdict.** `ConstructReport.Render` emits
`R1: constructs occurring more than 50 times: 56; at 100 %: 37` and the columns the re-stated rule
needs are all in the table beneath it, so the rule above is checkable today by reading them — but
the headline line is the superseded one. Changing what that line reports is
`Testing/Rikarin.Skala.Testing/ConstructReport.cs`, and it is owed before M4 rather than after,
because M4's own bar is per-span and will want the same shape.

⚠ **The tool for working it is new and it is not the ranked report.** `locate <set> <kind>` prints
the divergent lines attributed to one construct with file and line, because the ranked report orders
by line count and R1 counts constructs — a construct with two divergent lines is exactly as far from
the rule as one with ninety, and the ranked report never shows it. Nine of the eleven constructs
that moved to 100 % at M3.1 were found that way.

**Residual risk: high.** This is the risk that decides whether the project succeeds. ⚠ The number is
much better than it was — one line in 333 rather than one in a hundred — and the *shape* of the
residue is now known well enough to say that the last of it is not reachable by more of the same
work: see [../divergences.md](../divergences.md) § SK-DIV-0005, where the rule ReSharper applies was
swept over a hundred cells and is not a function of anything this formatter measures.

### R2 — The fitting engine is the only novel code, and novel code is where the bugs are

Everything else in Skala is assembly of well-understood parts. The three-state group model (ADR-002)
does not exist in any published formatter; Prettier-lineage tools have two states and ReSharper's
implementation is closed.

**Mitigation:** the property suite ([12](12-conformance-and-testing.md)) is designed for exactly
this — idempotency, token equivalence, width monotonicity and preservation are all violated by the
plausible bugs in a three-state resolver. Fuzzing runs nightly from M1, not from M7.

⚠ **M3 is where the novel code arrived, and the property suite earned its place immediately.** The
three-state model needed a fourth measure and a fifth: `pointWidth`, which stops at the first
*optional* break, and the trailing context, because a group is not the line it lands on. Two of the
three worst bugs in the milestone were found by properties rather than by fidelity — a
non-idempotency that no corpus file contains and that took a 4 708-file tree to surface, and a
blank-line rule that disagreed with itself between the first pass and the second. Neither moved the
fidelity number at all before it was fixed.

⚠ **M3.1 found the third and it had been there since M3.** Two of the fitter's three measures —
`AfterPoint` and `SegmentOf` — were **zero for every group that spends a continuation level**,
because `MeasureSegments` looked for a group's own break points among its direct children and such a
group opens its indent scope inside itself. Neither property caught it: the output was idempotent,
token-equivalent, deterministic and stable, and simply not what the oracle writes. It cost 0.1 points
of line fidelity and three points of file fidelity, and it was found by reading a diff rather than by
a test. ⚠ The lesson for M4 is that the property suite tests *consistency*, and a measure that is
consistently zero is consistent.

**Residual risk: medium.** Contained by testing, not by cleverness.

### R3 — The false-positive budget is not met, and the analysis half is switched off

A 1 % false-positive rate on a corpus that produces 5 000 findings is 50 wrong findings, which is
survivable. A 5 % rate is 250, which is not. The rules most likely to over-fire are exactly the ones
with the most value: modernization rules that do not understand why the old form was chosen, and
async rules that do not understand the call graph.

**Mitigation:** the shipping bar in [08](08-rule-catalogue.md) — zero false positives on the
reference corpus, a documented false-positive story, and a "should not fire" fixture set at least as
large as the positive one. Plus the nightly rule-count job, which catches drift. Plus shipping
uncertain rules at `hint`, where they are invisible until asked for.

⚠ **M5 shipped the first rules and the bar bit immediately: six analyzers of the thirty-six the
catalogue lists.** That is not the milestone falling short, it is the third clause — a "should not
fire" set at least as large as the positive one — costing more than the rule. The measurements:

| | |
|---|---|
| Fixtures | 21 positive, **36 negative**; every rule's negative set is larger than its positive one, asserted by a test |
| `corpus/real/` (380 files) | 143 findings — 27 `SK1005`, 114 `SK1010`, 2 `SK1020` — every one reviewed, **zero false positives** |
| Vixen (4 688 files, via binlog) | 12 findings, all `SK1010`, all correct |
| Fix verification | 170 fixes applied across 65 corpus files: 16 327 compiler errors before, 16 315 after, **no `(file, id)` pair worse than before** |

⚠ **Two of the six shipped rules fire zero times on both reference trees.** `SK1030` and `SK1035`
have their fixtures and nothing else. A rule with no corpus occurrences has a false-positive rate
that is *measured* at zero and *tested* at nothing, and that distinction is the whole of this risk.

⚠ **`fidelity audit` is the instrument, and it is deliberately more aggressive than the product.**
It runs the semantic rules under a loose compilation, which `skala check` refuses to do, because for
an audit the asymmetry is in the safe direction: every finding it produces is one to check, and the
ones it misses are misses rather than false positives.

⚠ **M6 shipped four more analyzers, seven metrics and duplication, and the bar bit again.** Four of
the twenty-nine ids in `SK2xxx`/`SK3xxx`. The measurements:

| | |
|---|---|
| Fixtures | 13 positive, **30 negative** for the four rules; 14 / 25 for the metrics |
| `corpus/real/` (380 files) | **0** findings from the four new rules |
| Vixen (4 660 files, loose + semantic) | **7** findings, all `SK3002`, every one read, **zero false positives** |
| Fix verification | 38 fixes across 10 Vixen files: 195 253 compiler errors before, 195 241 after, **0 `(file, id)` pairs worse** |
| Duplication | 4.8 % production over Vixen, 514 groups |

⚠ **Three of the four fire zero times on both reference trees**, so the risk this section is about is
*larger* after M6 rather than smaller. `SK2013`, `SK2015` and `SK3001` have their fixtures and
nothing else. The mitigation applied differently to each, and the difference is the useful part:

- `SK2015` is purely syntactic, so its zero cannot be an unresolved symbol in disguise. A grep found
  the only three candidate statements across both trees and all three are correctly not reported.
  Shipped enabled: a cheap rule whose zero is a measurement rather than a silence.
- `SK2013` is semantic but trivially guarded — an object creation that *is* an expression statement,
  on a type deriving from `Exception`. Shipped enabled for the same reason.
- ⚠ `SK3001` ships **disabled**, and not for a noise reason. It is compilation-scoped, so enabling it
  costs every run the incremental cache's warm path, and Vixen contains no `async void` method at
  all. Paying a repository-wide performance cost for a rule with no measured occurrences is not a
  trade to make on somebody's behalf.

⚠ **`SK3002`'s seven findings are the interesting case, because all seven are true and none is
actionable.** One is a public API whose own doc comment argues for blocking; six are the same
child-process-draining pattern in test helpers. That is not a false-positive rate — it is the case
the baseline exists for, and six of the seven sit in `*.Tests` where doc 08's `.editorconfig`
mechanism is the right instrument. The distinction between "wrong" and "true but unwanted" is the
one this section has to keep making, and conflating them is how a correct rule gets deleted.

⚠ **A metric rule cannot have a false positive in this section's sense** — it reports a measurement
against a threshold. What it can be is useless, and the failure mode is identical: a threshold low
enough to fire on ordinary code teaches people to switch the category off. So the thresholds sit
above the corpus p99, six of the seven ship at `hint`, and `SK7010` ships at `none` because turning
it on produces 1 868 findings on `Testing/corpus` alone.

⚠ **M7 shipped three more analyzers out of the twenty-three ids in `SK4xxx`/`SK6xxx`/`SK8xxx`, and
for the first time the binding clause was not the false-positive one.** It was the reference trees
not containing the shape. The measurements:

| | |
|---|---|
| Fixtures | 10 positive, **27 negative** for the three rules |
| `corpus/real` sources (380 of 1 140) | **1** finding, `SK6003`, read and correct |
| Vixen checkout (4 681, loose + semantic) — ⚠ the full external tree, not `corpus/real/vixen`'s 200 | **25** findings, all `SK8005`, every one read, **zero false positives** |
| Fix verification | the ten positive fixtures compiled as one tree: **0 compiler errors before applying every fix, 0 after** |

⚠ **`SK8005`'s twenty-five findings are M6's `SK3002` case again, at four times the scale.** All
twenty-five are true. Three are the shape the rule exists for — a bare sleep with no deadline
followed straight by an assertion. Fourteen are a back-off *inside* a `while (… && elapsed <
patience)` loop, where the sleep is the polling interval rather than the wait. Eight are tests where
advancing a real clock is the subject: `Wall_time_passing_does_not_advance_the_script`, a frame
limiter fed a deliberate 50 ms hitch, a runaway-guard watchdog whose case has to be slow. So the
rule is right 25 times out of 25 and *useful* three times out of 25, which is a different quantity
and the one this section keeps having to name. It ships at `suggestion` rather than at the `warning`
its range defaults to, so it never fails a gate.

⚠ **The loose audit's floor was raised before the numbers were believed, and that mattered.** A first
pass over Vixen reported `SK8005` **zero** times. The reason was not the rule: Vixen builds with
`<ImplicitUsings>enable</ImplicitUsings>`, the generated `GlobalUsings.g.cs` does not exist in a
loose compilation, and so `Thread`, `Task` and `List<T>` are unresolved in every file that never
writes the `using` — 195 724 compiler errors, and every semantic rule quietly answering "no finding"
for the wrong reason. Handing the audit a stand-in global-usings file dropped the tree to 128 833
errors and turned that zero into 25, and `SK3002` from 7 into 44. **A semantic rule's zero under
`--load=loose` is not evidence of anything until the errors around it have been looked at**, which
is the same asymmetry doc 12 § "Testing the rules" describes and a sharper version of it: the misses
are silent and they are not small.

⚠ **`SK4010` is a new entry on the "measured at zero, tested at nothing" pile, and `SK8002` is why
one rule was cut rather than shipped silent.** `SK4010` fires nowhere on either tree; the four
candidate chains in Vixen are three the rule correctly reads as different shapes and one — an
indexed `Where((t, i) => …).Any()` — that a guard refuses, so at least the zero has one live guard
behind it. `SK8002` (`Assert.True(x == y)`) was measured before it was written: 12 396 candidate
calls in Vixen, 90 of them in the shape the rule would fire on, and **all 90** are cases where the
rewrite either does not compile or asserts something else. Doc 08 § "What M7 added" has the
breakdown. A rule that would have been the loudest in the milestone is the one that never existed.

⚠ **M8 shipped five of the nine `SK5xxx` ids, and it is the milestone where this section's
instrument stopped working.** Every rule in the range defaults to `error`, so a false positive is a
build somebody cannot fix by fixing their code — and the reference corpus cannot detect one,
because it contains no vulnerable code to be right or wrong about.

| | |
|---|---|
| Fixtures | 17 positive, **40 negative** |
| `Rules/…/corpus/vulnerable` (7 files) | 23 findings, every rule represented, pinned per rule |
| `Rules/…/corpus/safe` (7 files) | **0**, required, and held after three sensitivity fixes |
| `corpus/real/` (380 files) | **0**, verified symbol-independently as well |
| Vixen (4 717 files, `44b88648`) | **0**, same |
| Cost (`skala check --profile`) | cold 297 ms of 2 249 ms analysis; **warm 26.7 ms of 312 ms** |

⚠ **A zero on the reference trees is not evidence here, and saying so is the point.** M7 found one
way for a measurement to lie — the loose compilation's unresolved-symbol floor. M8 found the
next one along: for a category the trees have *none of the shape* of, a correct rule and a rule that
never runs produce identical output. Both zeros above were therefore re-derived without the
compiler. The taint rules are intra-procedural, so a source and a sink must share a method and
therefore a file; a file-level containment count is a sound upper bound, and it is zero on both
trees. The syntactic rules' trigger tokens (`DES`, `CipherMode`, any certificate callback,
`XmlUrlResolver`) appear in **no file** of either tree. Two Vixen files survived the first grep and
both were artefacts of it — `HttpRequestException` in a `catch` filter matching `HttpRequest`, in
files that never call `Process.Start`.

⚠ **The safe corpus is the mitigation this range actually has, and it earned its cost immediately.**
Each safe file is the same shape as its vulnerable twin with the vulnerability removed the way a
reviewer would remove it, so a rule that were a keyword search would pass the positives and fail
there. It also caught three *misses* in the taint engine that reading the code twice did not: a
property matched only as a field, a fluent `Append` chain whose receiver is the previous call's
result, and the control-flow graph's `foreach` lowering, which dropped taint at the top of every
loop. None of the three is an edge case; all three are the common path.

⚠ **Four rules were cut and none of the reasons is a finding count** — a rule that fires a lot is
work for the repository it fires on, and only a rule that is wrong is a defect. `SK5003`'s sanitizer
is inter-procedural, which fails in the direction that *fires*; `SK5004` duplicates `SYSLIB0011` and
`CA2326`; `SK5006`'s entropy threshold does not exist; `SK5008` needs to judge identifier names.
Doc 08 § "What M8 added" carries the argument for each.

**Residual risk: medium**, and the composition has changed rather than the level. Eighteen analyzers
is more evidence than thirteen, and the "measured at zero on real code" pile grew by five at once —
every rule M8 shipped is in it. What offsets that is that these five are the first rules in the
project whose *negative* case is measured against code written to be safe rather than against code
that merely happens not to be affected, which is a stronger claim than any previous milestone made,
on a smaller population.

⚠ **Two of this section's own conclusions do not survive § "The reference trees are a test subject",
and they are marked rather than deleted.** Both are the same move — a true finding on Vixen read as
evidence about the rule:

- **`SK8005` ships at `suggestion` rather than at its range's `warning`** because 25 of 25 true
  findings on Vixen were judged "true and not what you would change". That is 25 baseline entries and
  a piece of Vixen's backlog, not a fact about the rule. The severity is **suspect and awaiting
  revisit**; [08](08-rule-catalogue.md) § "Rule status" carries the flag.
- **`SK3001` ships disabled** for two reasons stacked together. The first — it is compilation-scoped,
  so enabling it costs every run the warm incremental path — is reason 2 of the three above and
  **survives**. The second, "it buys nothing measurable: Vixen contains no `async void` method at
  all", **does not**, and it should never have been load-bearing. The default stands on the first
  reason alone, which is enough, and the second is struck.

⚠ **`SK7010` at `none`, and the metric thresholds sitting above the corpus p99, are the same shape
and are the harder case.** A threshold has no correct value independent of some population, so
calibrating one is not optional — but calibrating it against the tree it will be run on is how a
metric comes to certify the present. Flagged here rather than decided: the thresholds are
[08](08-rule-catalogue.md)'s and the argument for each is worth making explicitly against a standard
rather than against a p99.

**Residual risk: medium**, and unchanged. Thirteen analyzers is more evidence than ten. M7 drained
one entry from the "measured at zero" pile (`SK6003` has a corpus finding, `SK8005` has 25) and
added one (`SK4010`), and it found a *new* way for a measurement to lie — the loose compilation's
unresolved-symbol floor — which is the kind of thing this section exists to keep writing down.

### R4 — Scope. This is three products

A formatter, a linter with 200 rules, and a quality-gate platform. Any one of them is a serious
project; CSharpier is a formatter alone and has years in it. The failure mode is not abandonment, it
is three half-finished halves — a formatter at 97 %, forty rules, and a gate nobody trusts.

**Mitigation:** the milestone order is strict and each milestone is adoptable on its own. M3 alone —
a formatter that matches Rider — is a complete, useful product, and the plan says so. If the project
stops there it has still replaced the thing that has no alternative.

**Residual risk: medium-high**, and it is the risk that is fully under the author's control.

### R5 — Roslyn moves

C# 15 will add syntax. A formatter that meets unknown syntax must not corrupt it.

**Mitigation:** the document builder has a total-function requirement — every `SyntaxNode` kind maps
to a handler, and the fallback handler emits the node `Verbatim` from its original span rather than
throwing or guessing. A generated exhaustiveness test over `SyntaxKind` fails the build when Roslyn
adds a kind Skala does not name, which turns "silently mangles new syntax" into "fails to compile
after a package bump". Plus token equivalence, which catches it at runtime anyway.

**Residual risk: low.**

### R6 — The binlog dependency is awkward in practice

`skala check` needing a build is a real ergonomic cost, and the staleness cases
([07](07-analysis-host.md)) are a class of confusing failure.

**Mitigation:** three load modes, clear reporting of which one ran, and `loose` mode being genuinely
useful for the agent path (which is the highest-frequency use). Also: CI builds anyway, so the cost
lands where it is already paid.

**Residual risk: low-medium.**

### R7 — Single maintainer

One person plus AI agents, across a game engine, an engine editor, an MMO framework and now a
static-analysis tool.

**Mitigation:** none available. What the plan does instead is minimise the *maintenance* surface:
generated code over hand-written (options, rules, docs), no bundled third-party rules to track, no
server, no telemetry, no plugin ecosystem, and a strict "no unowned features" policy. And the tool is
built to be used by agents, which means agents can also maintain it — the conformance harness is
precisely the artefact that makes agent-written formatter changes reviewable.

**Residual risk: structural.** Acknowledged rather than mitigated.

## Open questions

### Q1 — Does `jb cleanupcode` reproduce Rider's *editor* formatting exactly? — ⚠ **reopened**

The oracle assumption is that CLI cleanup and the IDE's "Reformat Code" produce identical output for
the same `.editorconfig`. Mostly true; ReSharper has settings that exist only in the IDE
(`resharper_use_indent_from_vs`) and cleanup profiles that differ from the format action.

The sharp edge here was indentation autodetection — Rider detecting a file's actual indentation and
formatting against *that* rather than the configured value, which would make the IDE and the oracle
disagree with each other and leave Skala unable to match both.

**Resolved by decision, not by experiment:** the template now sets

```ini
resharper_autodetect_indent_settings = false
resharper_apply_auto_detected_rules  = false
```

so the configured indentation is the only indentation, in the IDE and on the command line. Both keys
are Tier C (accepted, ignored) for Skala — it has no autodetection to switch off — and `skala config
check` reports `SK9006` if either is ever set back to `true`, because that reintroduces a
disagreement Skala cannot resolve.

⚠ **What was called "what remains of Q1" is the whole of Q1, and it has now bitten once.** The
sentence used to read: "What remains of Q1 is the smaller question of cleanup-profile parity, which
the oracle harness handles by pinning the profile explicitly." Pinning the profile explicitly is not
a handling of that question. It is a *choice* of answer, made once, in `OracleProfile.FormatOnly`,
and never re-examined — and the profile that was chosen is `Built-in: Reformat Code`, which is
precisely the built-in profile that switches documentation-comment formatting **off**.

#### The second instance: documentation comments (SK-DIV-0006)

M3 asked the oracle whether it formats documentation comments, got "no" under every shape of the
`resharper_xmldoc_*` family, and recorded it as a property of `jb cleanupcode`. It is a property of
the profile: `CSharpFormatDocComments` is a real cleanup task, `Full Cleanup` enables it, `Reformat
Code` does not, and the pinned profile is the latter. The consequence stood for six milestones — an
entire sub-formatter built behind an opt-in flag, its seventeen keys held at Tier D, and one key
actively *demoted* from Tier A for doing the right thing. See
[../divergences.md](../divergences.md) § SK-DIV-0006 and
[../oracle-cleanup-profile.md](../oracle-cleanup-profile.md).

This is not the same failure as the autodetect one. Autodetect was the IDE having a setting the CLI
does not; this is the CLI and the IDE agreeing perfectly and Skala asking the wrong one of the CLI's
profiles. The first is a tool difference and the second is a configuration difference, and the
second is worse, because nothing about it looks like a difference at all.

#### ⚠ What it costs, and why it matters more later

For any area where the pinned profile and Rider disagree, **ADR-011's oracle is not the
specification and Skala has no differential safety net there.** Both halves are load-bearing:

- *Not the specification.* The differential scores agreement with the profile. Where the profile
  does less than the editor, agreement is the wrong target and the ratchet quietly rewards matching
  it — which is exactly how `resharper_space_after_triple_slash` came to be demoted for being
  correct. A number that goes down when the formatter improves is worse than no number.
- *No safety net.* Every other option in Skala is checked, on every commit, against a machine that
  answers independently. In such an area the only evidence is hand-written fixtures and properties
  Skala checks against itself — and a formatter checking its own answer against its own signature is
  the thing an oracle exists to replace.

⚠ **This is the reason to care now rather than at the point of replacement.** The roadmap ends with
ReSharper removed and the oracle gone. Every fixture is a snapshot taken under one profile, and once
the tool is gone, a profile the fixtures were generated under wrongly cannot be re-asked — the
snapshot *is* the specification, and its gaps become permanent. The remaining lifetime of the oracle
is the only window in which "which profile" is still a question with an answer.

#### What would close it

The instance is closable and closing it is cheap: one element in `OracleProfile.FormatOnly` plus
`./build.sh Oracle`. Q1 itself needs the general form, which nobody has done — **enumerate the
cleanup tasks the built-in profiles enable and diff them against the pinned profile**, rather than
probing task names one at a time as a milestone happens to need them. The task list is recoverable
(`CodeCleanupTask_*` in the resource strings; 113 names on 2025.2.6) and
[../oracle-cleanup-profile.md](../oracle-cleanup-profile.md) already records the method. That sweep
was run against the names the roadmap named, which is why it missed this one: the roadmap did not
name documentation comments, because M3 had already concluded the oracle would not format them.

⚠ Until that sweep exists, treat "the oracle does not do X" as **"the pinned profile does not do
X"** wherever it appears in this repository, and check before building anything on it.

### Q2 — How aggressive should the first run be?

The adoption path ([11](11-cli-and-integrations.md)) assumes one enormous formatting commit. An
alternative is a `--conservative` mode that only fixes what is unambiguously wrong (spaces, blank
lines) and leaves all line breaks alone, so the first diff is a tenth the size and the second diff
comes later.

Undecided. It costs a mode and it may be the difference between adoption and revert on a repository
that is not the author's.

### Q3 — Should arrangement ever run in the pre-commit hook?

It needs a compilation, which needs a build, which a pre-commit hook cannot afford. The syntactic
subset can run, and probably should. But a commit where half the arrangement rules ran and half did
not is a commit with an arbitrary boundary in it.

Leaning: syntactic subset in the hook, full arrangement as a deliberate command and in CI as a
*check* (not a fix).

### Q4 — What is the story for multi-repository consistency? — ✅ **resolved**

Designed and built: [03](03-configuration-model.md) § "Canonical distribution across repositories".
The package survives; the mechanism this document leaned toward does not.

**The lean was wrong on its central claim.** "A package that drops the canonical `.editorconfig` at
restore time" cannot be built. A probe package carrying the file three ways, restored and built by a
consumer, showed that `content/` and `contentFiles/` are never copied into a consuming project
directory under `PackageReference`, and that a package's `build/*.targets` do not run during restore
at all — they arrive through the `obj/*.nuget.g.targets` that restore is in the middle of writing.

Dropping it from a **build** target does run, and is worse. On a probe repository whose canonical
made a block-scoped namespace an `IDE0161` error, the configuration took **three builds** to take
effect: build 1 installed the file after the compiler had already been handed the old configuration,
build 2 was incremental and skipped `CoreCompile` entirely, and only build 3 failed. **A gate whose
first two runs pass is not a gate** — and that is before the parallel writes into the source tree
and the file changing under Rider.

There is also a constraint the original framing missed, and it is the one that decides the shape:
**Rider reads exactly one file per directory and it is called `.editorconfig`.** Every design that
puts the canonical elsewhere — `EditorConfigFiles` from a package, a `.globalconfig`, a second file
beside it — is invisible to the IDE. And the compiler cannot bridge it either: an `.editorconfig`'s
section globs resolve relative to the directory containing the file, so a canonical in the NuGet
cache has a `[*]` that matches only the NuGet cache. That is now a test, asked through Roslyn's own
matcher.

**What replaced it:** the package stops being an installer and becomes a *carrier*.
`Rikarin.Skala.Canonical` ships the payload, its manifest, and a **check-only** MSBuild target
(5 ms per project, measured). `skala config sync` is the only thing that writes — explicit, offline,
one reviewable diff. `skala config diff --canonical` is the gate condition and exits 3 on drift, as
this document proposed.

**Layering.** One file, two blocks, separated by `# skala:canonical begin` / `# skala:local begin`
markers, the local block second — so editorconfig's own later-section-wins rule makes local
overrides beat the canonical, and Skala never has to know what they are. Tested against Vixen's real
file: all **56** path-scoped sections survive **verbatim**, the effective options still resolve to
Vixen's values where Vixen set them and the canonical's where it did not, and the override report is
**7 lines** against the 5 188-line result (the 4 272-line canonical block plus Vixen's 916-line local
one, measured at `8cbd66d`).

⚠ **The reason verbatim preservation is required is not that the local block is good, and the
document used to say it was.** The earlier version of this paragraph cited Vixen's "reasoning
comments" as what the mechanism protects. Vixen's `.editorconfig` was not authored: it was built by
agents as they went, 916 lines and 56 sections, and never reviewed as a whole. Its overrides are not
decisions. **The true reason is stronger and general:** nobody can tell a reasoned override from an
accidental one by looking at it, so a sync that dropped, merged or normalised *either* kind would be
unsafe in every repository rather than only in this one. A mechanism that has to be right about
which overrides matter is a mechanism that will one day be wrong.

⚠ **`SK9013` is the instrument for the other half of the problem, and the two acts are different.**
The tool preserving an override is a safety property. A person *auditing* accumulated overrides is a
review, and `SK9013` — one info-level finding per option the local block sets that the canonical also
sets — is the artefact it is done from. Seven lines is a reviewable list; a repository whose report
runs to two hundred has a question to answer that Skala is not going to answer for it. ⚠ Vixen's
seven are **suspect by default and are not precedent**: they are the unreviewed file's, and each
needs justifying on its merits or removing. Vixen conforms to the canonical; the canonical does not
bend to Vixen.

**Rollout.** Drift (`SK9008`, error) is `sha256(block) ≠ the marker's own sha256` — decidable from
the file alone, offline, at any version. Behindness (`SK9009`, info) is `the marker ≠ the tool's
canonical` and never fails anything. So publishing a new canonical changes nothing anywhere until a
repository chooses to bump, and eighteen repositories take the reformatting commit in whatever order
suits them. That is the same shape as the baseline story in [09](09-quality-gates-and-reporting.md):
accept the present, gate the future.

**ADR-001 survives.** The canonical is the export plus exactly the two additions `skala config fix`
already makes (`root = true`, `max_line_length`); all 4 226 of the export's assignments are carried
through with their values unchanged, asserted by a test. The maintainer loop is still change a
setting in Rider, re-export, `./build.sh Canonical`, publish — and a re-export that skips the
regeneration is a red build, not a silent divergence.

The one thing left open is whether `Rikarin.Skala.Sdk` should reference `Rikarin.Skala.Canonical` or
absorb it. It should reference it: a canonical bump is a repository-wide reformatting commit and a
rule bump is not, and one version across both forces every repository to take the reformat to get a
bug fix.

### Q5 — Does the ReSharper severity mapping survive? — ✅ **resolved twice, and the answer is no**

⚠ **Read this heading before the section.** M5 answered "partly, and off by default" and shipped an
opt-in bridge. That bridge has since been **removed entirely**, along with the `resharperId` field it
depended on. The four findings below stand and are why; the "so the mechanism ships opt-in"
paragraph at the end is superseded and is kept only as the record of what was tried.

[03](03-configuration-model.md) claimed the 853 `resharper_*_highlighting` keys can configure
Skala's rules through a mapping table. That works where a Skala rule corresponds one-to-one with a
ReSharper inspection. Many do not: one ReSharper inspection may cover what Skala splits into three
rules, or vice versa.

**The guess was right about the direction and wrong about the default.** M5 measured it against the
real export; the full argument is in [03](03-configuration-model.md) § "Severities" and the four
findings are:

1. **Many-to-many in both directions.** The null-check neighbourhood alone carries six inspections at
   four severities (`merge_into_pattern` suggestion, `join_null_check_with_usage` suggestion,
   `convert_type_check_to_null_check` warning, `convert_type_check_pattern_to_null_check` warning,
   `arrange_null_checking_pattern` hint, `use_null_propagation` hint), and `SK1034` covered what three
   `replace_with_single_call_to_*` inspections split (⚠ retired, #281; the shape is unchanged). So the table is a **recorded choice** — one key
   per rule in `rules.json`, with a `resharperNote` naming what was passed over — and never a
   derivation. The safe direction is the one the guess named: rule → at most one key, never the
   reverse.
2. **Partial.** `SK1005` maps to nothing: Rider drives file-scoped namespaces from the *Microsoft*
   key `csharp_style_namespace_declarations = file_scoped:suggestion` and reports them under
   `resharper_arrange_namespace_body_highlighting = hint`. One concept, two mechanisms, two
   severities, no inspection id.
3. ⚠ **A plausible key that does not exist is worse than none.** `ConvertToFileScopedNamespace` and
   `ConvertToThrowIfNull` both snake-case into keys JetBrains never emits; a mapping nothing can set
   looks like a feature and behaves like a comment.
   `RuleCatalogTests.EveryDeclaredReSharperKey_ExistsInTheExport` reads the export and fails the
   build for it.
4. ⚠ **The decisive one.** The export sets
   `resharper_use_throw_if_null_method_highlighting = none`, so reading these keys as authoritative
   would have switched `SK1020` off in the repository Skala was built for, without anyone deciding
   to. The 912 values in an export were chosen for ReSharper's inspections; a value nobody has
   looked at is not consent. ⚠ **`SK1020` is retired (#281), so the only rule this was ever
   demonstrated on no longer fires.** The reasoning is generic and stands; re-measuring it against a
   live rule is owed, and until somebody does, this section rests on an example rather than a
   current measurement.

**M5's answer was that the mechanism ships opt-in** — `skala check --resharper-severities`, or
`"analysis": { "resharperSeverities": true }` — with `dotnet_diagnostic.SK…` beating the ReSharper
key beating the rule's default.

⚠ **That answer has been withdrawn and the mechanism deleted.** Finding 1 above is the reason, taken
to its conclusion: a rule declared **one** `resharperId`, and `Testing/parity-analysis/catalogued.json`
maps **295 inspections onto 162 rules, 49 of which cover more than one** — `SK4010` covers eleven. So
`resharper_<x>_highlighting = none` either switched off a rule covering ten other concepts or was
inert for the other ten. "A recorded choice, one key per rule" was a way of writing that down, not a
way of fixing it. Skala exists to **replace** ReSharper rather than to keep speaking its
configuration vocabulary, and nothing consumes Skala yet, so **no migration path was built and none
is wanted** — there is deliberately no translate command.

Gone with it: `--resharper-severities`, `"analysis": { "resharperSeverities": true }`, the
`resharperId` field, the SARIF `rules[].properties.resharperSeverityKey`, and the two tests named in
findings 3 and 4. ⚠ **`resharperNote` stays** — prose about how a concept lines up against
ReSharper's is still worth having — and so does `supersedes`, which is a different field carrying
`CA*`/`IDE*`/Sonar ids and is how hosting is recorded (ADR-008). ⚠ **Reading a Rider export for
formatting *options* is untouched**; only the severity axis went.

⚠ **One thing was lost that nothing replaces**: `resharperId` was the independent second source
cross-checking the hand-written `catalogued.json`, in `verify_ledger.py` and again in C#. See
[17](17-inspection-parity.md) § "Skala's coverage of the universe" — that map is now unchecked on its
keys, deliberately.

**Residual risk: low**, and it is no longer a design question at all.

### Q6 — Duplication across repositories?

Sonar can detect a clone between two projects. Skala's index is per-repository. Cross-repository
clone detection would be genuinely useful in a tree of eighteen related projects — and is a research
project with a storage story attached.

Out of scope, noted because the question will come back.

### Q7 — What happens when Rider changes its formatter?

A ReSharper update changes output; the fixtures become wrong; fidelity appears to regress. This is a
recurring maintenance cost with no clean answer.

**Current plan:** the fixture header records the ReSharper version, regeneration is a reviewed commit
([12](12-conformance-and-testing.md)), and the divergence register absorbs deliberate
non-following. Skala is allowed to *not* follow a ReSharper change — it is a compatibility target,
not a master.
