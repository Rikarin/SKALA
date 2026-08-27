# 16 — Risks and Open Questions

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

**Mitigation:** the divergence register (`SK-DIV-*`) plus a hard rule — any construct that appears in
the corpus more than 50 times must be at 100 %, and the tail is only allowed in constructs that are
genuinely rare. Plus `resharper_formatter_tags_enabled` as the human escape hatch for the handful of
places where the tools cannot agree. Plus, honestly: if a divergence is small and Skala's answer is
better, change the Rider setting to match Skala rather than the reverse — the settings are the
author's, and they can move.

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

⚠ **And R1 as written cannot be met short of 100 %, which is worth saying plainly rather than
missing it four milestones in a row.** The report attributes every divergent line to the innermost
node that owns it, so "at 100 %" means "no divergent line is attributed to this construct". The
nineteen constructs that fail are `IdentifierName` (92 divergent lines), `ArgumentList` (28),
`StringLiteralExpression` (25), `Block` (21) — and `ForStatement` (2 of 2 lines it owns),
`OmittedTypeArgument` (2), `DefaultLiteralExpression` (1). The first four are where a wrap decision
*lands*; the last three are constructs that own two or three lines of the whole corpus, so one
divergence is 33 % of them. Both halves say the same thing: **every divergent line is attributed to
something that occurs more than fifty times, because everything that occurs at all occurs more than
fifty times.** R1 is therefore equivalent to 100 % line fidelity, and it should be re-stated for M4
as a rule about constructs whose *attributed share* is above a threshold rather than about any
divergence at all.

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
| `corpus/real` (380 files) | **1** finding, `SK6003`, read and correct |
| Vixen (4 681 files, loose + semantic) | **25** findings, all `SK8005`, every one read, **zero false positives** |
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

### Q1 — Does `jb cleanupcode` reproduce Rider's *editor* formatting exactly? — ✅ **narrowed**

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

What remains of Q1 is the smaller question of cleanup-profile parity, which the oracle harness
handles by pinning the profile explicitly ([12](12-conformance-and-testing.md)).

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
file: all **56** path-scoped sections and their reasoning comments survive verbatim, the effective
options still resolve to Vixen's values where Vixen set them and the canonical's where it did not,
and the override report is **7 lines** against a 5 188-line file.

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

### Q5 — Does the ReSharper severity mapping survive? — ✅ **resolved at M5: partly, and off by default**

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
   `arrange_null_checking_pattern` hint, `use_null_propagation` hint), and `SK1034` covers what three
   `replace_with_single_call_to_*` inspections split. So the table is a **recorded choice** — one key
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
   would switch `SK1020` off in the repository Skala was built for, without anyone deciding to. The
   912 values in an export were chosen for ReSharper's inspections; a value nobody has looked at is
   not consent.

**So the mechanism ships opt-in** — `skala check --resharper-severities`, or
`"analysis": { "resharperSeverities": true }` — with the predicted precedence:
`dotnet_diagnostic.SK…` beats the ReSharper key beats the rule's default. `skala explain <id>`
prints the key, its value here, and the note. The headline claim survives in a smaller and truer
form: the export *can* configure Skala's rules, one rule at a time, when someone asks it to.

**Residual risk: low**, and it moved from a design question to a documented per-rule decision.

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
