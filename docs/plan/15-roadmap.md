# 15 — Roadmap

Sequenced by dependency and by risk, not by appeal. Each milestone has a **definition of done** that
is measurable, and each produces something usable — the tool is adopted incrementally on the
author's own repositories, and every milestone that cannot be adopted is a milestone whose feedback
arrives too late.

Sizes are relative (S ≈ days, M ≈ a week or two, L ≈ a month, XL ≈ longer), not dates.

## M0 — Configuration and skeleton · S/M

The repository, the build, and the thing everything else consumes.

- `Directory.Build.*`, `Skala.slnx`, NUKE targets, the project graph from
  [02](02-repository-layout.md) with the reference tests that keep it honest.
- `options.json` for the ~380 C# keys, seeded from the export by a one-off importer that reads
  ReSharper's documentation tables, with every entry tiered — most of them `D` initially, which is
  correct and honest.
- `Rikarin.Skala.Options.Generator`, producing the option struct, parser, docs and test stubs.
- `.editorconfig` ingestion over Roslyn's `AnalyzerConfigSet`, with the precedence rules from
  [03](03-configuration-model.md).
- `skala config explain | check | diff | distill | fix`, and `SK9001`–`SK9005`.

**Done when:** `skala config explain Core/Foo.cs` prints the effective 380 options with source
file:line and tier for the real template, `skala config check` names the three contradictions this
plan already found, and `skala config distill` round-trips.

## M1 — Formatter, phase 1 · L

Spaces, blank lines, braces, indentation. No wrapping. Plus the parts that never change again: the
IR, the emitter, the safety net, the harness.

- `Doc` IR and the fitting skeleton (groups resolve trivially without wrapping).
- Minimal `TextChange` emission with `Anchor` sync.
- Token-equivalence verification, `SK9099`, crash artefacts.
- Trivia model: comments, regions, directives, disabled text, formatter tags, `Verbatim` spans.
- `skala format [--check] [--diff] [--range] [--staged]`.
- The corpus (`constructs/`, `real/`, `pathological/`) and the oracle harness.

**Done when:** line fidelity ≥ 85 % on `corpus/real/`, idempotency and token-equivalence at 100 %,
and `skala format` has been run over Vixen with the diff reviewed and nothing semantically changed.

## M2 — Formatter, phase 2 · M/L ✅

Break presence *and position*: required and forbidden line breaks, `place_*`, `keep_existing_*`,
attributes.

- Owner-dependent groups for `if_owner_is_single_line` — ✅ and the "second pass" turns out to be a
  walk order rather than a traversal, because the owner is always the child's ancestor
  ([04](04-formatting-engine.md)).
- The `Preserve` group and the four-way preservation table — ✅ `constructs/preservation/` runs under
  all four combinations against committed oracle fixtures, and the table is not the one
  [05](05-csharp-formatting-rules.md) stated.
- A break-*position* model: a pre-pass that labels every gap `Point`, `Flat` or `Mandatory`, which is
  what `wrap_before_binary_opsign` and friends need and what M1's gap model had nowhere to put.
- The measure pass fused into the build pass ([13](13-performance.md)).

**Done when:** line fidelity ≥ 93 %, and formatting Vixen produces a diff small enough to read in one
sitting. ✅ **97.47 %**. ⚠ The Vixen diff is *not* small: 2 374 files of 4 703, against milestone 1's
999. Roughly half of that is a configuration artefact — Vixen sets none of the phase-2 keys, and
`options.json`'s `default` is the export's value rather than ReSharper's, so the two fall back
differently. Repairing `defaultSource` is M3's first job and it is worth 45 % of this diff. ✅ Done:
measured at the commit that introduced it, the derived table takes the Vixen diff from 2 700 files
to 2 506 and Skala's agreement with the oracle *under Vixen's own configuration* from 97.00 % to
97.84 % of lines. ⚠ The shipped build's number is 2 552, not 2 506: the four commits after it —
raw-literal realignment, the pattern chain's own level, the fill fix, comment trailing whitespace —
moved it, and the pair above is a controlled A/B at one commit rather than a running total.

## M3 — Formatter, phase 3–4 · L/XL

Wrapping, the fitting algorithm proper, xmldoc. **This is the milestone that makes the tool the thing
it is for.**

- The 47 `wrap_*` keys, the `max_*_on_line` counters, `Fill`, break-position keys.
- The xmldoc sub-formatter.
- Daemon, LSP, git hooks, the post-edit hook path and its 40 ms budget.

**Done when:** line fidelity ≥ 99.9 %, all divergences are documented `SK-DIV-*` entries, and
Vixen's `.editorconfig` is replaced by the export with `skala format --check` clean in CI.

⚠ **Measured: 98.86 %, not 99.9 %. The bar was unreachable as this milestone was scoped, and it was
set before anyone knew why.** Splitting the remaining gap by whether a file contains a `#if`:

| | line fidelity | divergent lines |
|---|---:|---:|
| Files containing `#if` (91 of 380) | 98.39 % | 263 |
| Files without | **99.05 %** | 556 |

⚠ M5 re-measured this split with a lexical `#if` test and gets **98.60 % / 98.93 %** over the same
91/289 population — which is what [../divergences.md](../divergences.md) § SK-DIV-0004 recorded at
the time, so the 98.39 / 99.05 pair above is the outlier and its population definition is not
recoverable. The M5 numbers are the ones M3.1 measures against.

A third of the gap is SK-DIV-0004 and cannot be closed here at all: without a project Roslyn hands
back `#if` bodies as disabled text and Skala correctly refuses to touch them, while the oracle runs
against a project with `DEBUG` defined and formats them. That is [07](07-analysis-host.md)'s project
loading, which is **M5**. A further share is SK-DIV-0005, a margin constant reverse-engineered by
sweeping the oracle, where ReSharper's actual computation is unknown and three alternatives were
measured.

**The revised bar, therefore, is two bars.** Without a compilation: **≥ 99.5 %** on the files that
contain no `#if`, which is 99.05 % today and is ordinary tail work. With one, after M5 supplies
symbols: the original ≥ 99.9 % overall, and [16](16-risks-and-open-questions.md) § R1's frequency
rule with it. Neither is dropped; they are sequenced behind the thing that makes them possible.

What landed, and what it is short of:

| | |
|---|---|
| Line fidelity, `corpus/real/` | **98.86 %** (M2 97.47 %), file 71.05 % (M2 49.47 %) |
| [16](16-risks-and-open-questions.md) § R1 | 27 of the 54 constructs occurring more than 50 times are at 100 % |
| Divergences | eight `SK-DIV-*` entries, each with a measurement; SK-DIV-0002 is resolved |
| Wrapping | ✅ `Fill`, the counters, the ordering rule, chains, ternaries, declarators, base lists |
| xmldoc | ⚠ the oracle does not format doc comments (SK-DIV-0006); the well-formedness hint is done |
| Daemon, LSP, hooks | ✅ all three, with tests |
| 40 ms warm | ⚠ 60–70 ms, of which ~60 is the client's process start; NativeAOT is the fix |
| Daemon lazy start | ✅ the first single-file format leaves one behind: 310 ms, then 70 ms |
| 20 s whole corpus | ✅ **11.9 s** over Vixen, from 34.2 s |
| `defaultSource` | ✅ derived from the oracle: 123 keys `oracle-probe`, `distill` drops 108 |
| Tier A | 201 options, up from 172, each pinned by a committed fixture |
| Vixen `.editorconfig` | prepared and measured — 2 717 files, 83 241 diff lines — **not committed, deliberately deferred** |

⚠ **The Vixen commit is deferred until the tail is closed, and that is a decision rather than a
delay.** At 98.86 % about one reformatted line in a hundred still disagrees with Rider, so opening
those files in the IDE reformats them back — the formatting ping-pong that
[16](16-risks-and-open-questions.md) § R1 names as worse than either tool alone. Committing 83 241
lines *before* the disagreement is closed converts a one-time commit into a recurring fight. The
diff is measured and reproducible; it is re-made when the number supports it.

**This is release 0.4 and the first one anyone else could use.** ⚠ It is offered as one on the
strength of the properties rather than the percentage: idempotency, token equivalence, parse
stability, determinism and whitespace absorption hold on every file of every corpus and on all
4 708 files of Vixen, and the fidelity gap is eight named, measured disagreements rather than an
unknown.

## M4 — Arrangement · M/L — ⚠ **deferred; M5 runs first**

**Decided after M3: M4 and M5 swap places.** M3 established the dependency inversion below — M4's
semantic half needs a compilation, and building one is M5's work — and the same compilation is what
closes SK-DIV-0004 and the `#if` third of M3's fidelity gap. One milestone unblocks both, so it goes
first. M4 then runs with semantics available from the start rather than shipping a syntactic subset
and revisiting.

The `arrange_*` and body-style settings from [06](06-arrangement-and-syntax-styles.md), plus the
syntactic subset that runs without a compilation.

- The three safety layers: conservative preconditions, re-bind diagnostic delta, symbol-identity
  check.
- `skala arrange [--check]`, `skala format --arrange=syntactic`.

**Done when:** the oracle's cleanup profile and Skala agree on `corpus/real/` at ≥ 99 % of changed
spans, and arrangement over Vixen introduces zero compiler diagnostics.

⚠ **What M4 needs that M3 did not provide**, written down at the end of M3 while it is still known:

1. **A different oracle profile.** Every fixture in the corpus is generated under
   `SkalaFormatOnly`, which is `CSReformatCode` and nothing else. Arrangement is a *cleanup* profile
   with `CSUseVar`, `CSOptimizeUsings`, `CSReorderTypeMembers` and the body-style actions in it, so
   M4's first act is a second profile and a second `.expected.cs` extension beside the first —
   `OracleRunner.Profile` is a constant today and `OracleFixture` assumes one fixture per file.
2. **A compilation, for the semantic half.** M3 formats a file with no project and no preprocessor
   symbols, which is already SK-DIV-0004's whole content. `arrange` re-binds each document to check
   that it did not change meaning; the "syntactic subset that runs without a compilation" is the
   part M3's infrastructure can carry, and the rest waits on [07](07-analysis-host.md)'s project
   loading, which is M5. ⚠ The roadmap has M4 before M5 and this is the dependency that argues for
   swapping them, or for M4 shipping only the syntactic subset.
3. **Multi-pass output.** Formatting is one document build and one emit. Arrangement moves members
   and then the result has to be re-formatted, so the pipeline needs a fixed point and a bound on
   it, and the idempotency property has to hold across the pair rather than across either half.
4. **Range mapping through an edit that moves text.** `--range` and the LSP's range formatting are
   filters over a whole-file fit, which is exact while every edit is local. A member that moves 200
   lines breaks that, and `skala arrange --check` on a range needs a real edit-to-span map.
5. **What M3 did provide and M4 can rely on:** the `.editorconfig` chain and the derived default
   table, the three-state group model, the daemon and the LSP transport, the property suite
   (idempotency, token equivalence, parse stability, determinism), and the `fidelity ask` harness —
   which is how M3's rules stopped being readings of option names, and is the tool the `arrange_*`
   family needs most, because its option names are vaguer than the formatter's.

## M5 — Analysis and the AI gate · L — ✅

The point at which Skala replaces Qodana for this workflow. ⚠ Run **before** M4, for the reason
M4's header gives.

- Binlog loading, the analyzer host, the incremental cache, `loose` mode.
- `rules.json`, the rule generator, `docs/rules/`, `skala explain`.
- `SK0xxx` + the `SK1xxx` modernization set — the differentiator, and the rules with the highest
  fix-to-report ratio.
- SARIF, renderers, exit codes.
- `skala verify`, `skala fix --safe`, the MCP server.

**Done when:** `skala verify` runs in under a second on a five-file change with no project loaded,
the Claude Code hooks are installed in Vixen, and a week of agent work produces no hand-formatting
and no unexplained suppressions.

| | |
|---|---|
| `skala verify`, 5 files, `--load=loose`, cold process | ✅ **0.39–0.54 s** on a clean tree, **0.50–1.02 s** when all five have findings (0.84 s on the very first run, page cache cold) |
| `skala check --load=binlog` over Vixen | ✅ **58–65 s** against a 4-minute budget; 4 688 files, 60 compilations |
| Load modes | ✅ all three, with a reported fallback ladder and `loadMode` in the SARIF |
| Rules shipped | ⚠ **six analyzers** + three formatter findings, not the thirty-six of doc 08 — [08](08-rule-catalogue.md) § "What M5 actually shipped" says which were cut and why |
| False positives | ✅ **zero**: 143 findings on `corpus/real/`, 12 on Vixen, every one reviewed; 170 fixes applied over `corpus/real/` produced **no `(file, id)` pair worse than before** |
| Fixture sets | ✅ 21 positive, 36 "should not fire" — every rule's negative set is larger than its positive one |
| SK-DIV-0004 | ✅ closed. `--define`, and symbols from a loaded compilation. Measured: 98.60 % → **98.92 %** on the 91 `#if` files, 98.86 % → **98.93 %** overall |
| Formatter | ✅ `DifferentialTests.Fidelity_DoesNotDecrease` passes; file fidelity **improved** 71.05 % → 71.32 % from a bug the symbols revealed |
| Properties | ✅ all six at 100 % on all three corpora — 4 745 conformance tests green |
| Claude Code hooks | ✅ in Vixen's `.claude/settings.json`, ⚠ with `format --check` rather than a write — see [10](10-ai-agent-integration.md) § "Hooks" |
| [16](16-risks-and-open-questions.md) § Q5 | ✅ resolved, written into [03](03-configuration-model.md) § "Severities" |

⚠ **Three things the plan had wrong, each found by measurement rather than by reading:**

1. **Generated sources are not on the `csc` command line**, whatever `EmitCompilerGeneratedFiles`
   says. Loading a binlog verbatim gave Vixen **1 675 compiler errors** and a semantic model over a
   program that does not compile. Re-running the generators — with the command line's
   `AdditionalFiles` and its `/analyzerconfig:` set, both of which the first attempt forgot — takes it
   to 20. [07](07-analysis-host.md) § binlog.
2. **Reading a binlog needs the SDK's MSBuild loadable**, which is the thing ADR-007 chose the binlog
   to avoid. `MSBuild.StructuredLogger` deserialises into MSBuild's own event types.
3. **`dotnet_diagnostic.SK….severity` needs a `SyntaxTreeOptionsProvider` on the compilation**, not
   on the `AnalyzerOptions`. Without it a scoped severity is silently ignored — the repository turns
   a rule off, the IDE agrees, and CI keeps reporting it.

⚠ **`./build.sh Lint` was red on `master` and nobody knew.** ADR-015 says the build fails if
`skala format --check` finds anything, and `Lint` does check `Core/`, `Formatting/`, `Testing/`'s two
harness projects and `Tools/`. Measured by building M3's own CLI at `d74779e` and running it against
that tree: **13 files in `Core/`, 15 in `Formatting/`, 7 in `Tools/`** would have been reformatted by
the formatter that shipped with them. M5 formats them — 51 pre-existing `.cs` files, every edit
token-equivalent by construction — so the target is green for the first time since the check covered
those directories. The lesson is the ordinary one: a gate that is not run in CI is not a gate.

⚠ **What is *not* done, against the stated bar:** "a week of agent work produces no hand-formatting
and no unexplained suppressions" is a claim about a week that has not happened. The hooks are
installed and the machinery to notice is there; the observation is not.

**Release 0.6.**

### ⚠ What M3.1 and M4 need that M5 did not provide

**M3.1 — the fidelity tail, now with symbols.**

1. **The symbols close less than the estimate.** M3 attributed "a third of the gap" to SK-DIV-0004
   and the measurement is 0.32 points on the `#if` files, 0.07 overall. The revised M3 bar —
   ≥ 99.5 % on the files with no `#if`, then ≥ 99.9 % overall with a compilation — is now **99.5 %
   from 98.93 %**, and none of the remaining 0.57 is preprocessor-shaped. The work queue is
   SK-DIV-0005's margin (the largest single class), the argument-list chop of SK-DIV-0007, and blank
   lines around directives.
2. **`fidelity preprocessor` is the harness M3.1 measures with**, and it takes symbols on the command
   line, so a hypothesis about which branch matters is one run.
3. ⚠ **The corpus can now hide a bug behind a `#if`.** The `>`-before-`(` defect survived from M1 to
   M5 because every corpus line that shows it is inside a conditional body. M3.1 should run the
   differential **under both symbol sets** and treat a divergence that only appears under one as the
   interesting kind.
4. **What M5 did provide:** `CSharpFormatter.ParseOptionsFor` memoises a symbol set into parse
   options, `FormatRequest.Define` and the daemon protocol both carry symbols, and the daemon's cache
   key includes them — so a file formatted for Debug and the same file formatted for Release are two
   answers rather than one stale one.

**M4 — arrangement.** M3 listed five needs; M5 met one and a half of them.

| M3's list | Status after M5 |
|---|---|
| 1. A second oracle profile (`CSUseVar`, `CSOptimizeUsings`, `CSReorderTypeMembers`) | ❌ untouched. `OracleRunner.Profile` is still a constant and `OracleFixture` still assumes one fixture per file. This is M4's first act and M5 gave it nothing. |
| 2. **A compilation, for the semantic half** | ✅ **done, and this was the reason for the swap.** `ProjectLoader.Load` hands back `CompilationUnit`s with a real `CSharpCompilation`, generators re-run, references cached by `(path, mtime, size)`, and three load modes with a reported fallback. `arrange` can re-bind. |
| 3. Multi-pass output, a fixed point across format-and-arrange | ❌ not started. ⚠ M5 does have the *shape* of it in `skala fix`: apply text edits, then run the formatter over every file touched, then re-verify. That is a two-pass pipeline with the formatter last, and it is the pattern arrangement wants — but it is one pass, not a fixed point, and it has no bound. |
| 4. A real edit-to-span map for `--range` | ❌ not started. `EditEmitter.Restrict` is still a filter over a whole-file fit, which is exact only while every edit is local. |
| 5. The M3 inheritance (config chain, three-state groups, daemon, properties, `fidelity ask`) | ✅ intact, and larger: the property suite still passes at 100 %, and `fidelity preprocessor` and `audit` join `ask` as measurement tools. |

Two more that M3's list did not anticipate:

6. ⚠ **Arrangement needs the fix-verification loop that `skala fix` has, and needs it stronger.**
   M5's `FixCommand` re-parses and compares the file's *syntactic* diagnostics, because a semantic
   re-bind per file turns a fifty-file fix pass into a minute. Arrangement moves members across a
   compilation and cannot use a per-file check; it needs the compilation-wide delta doc 06 § "Safety"
   describes, and that cost is M4's to pay.
7. ✅ **The rule that arrangement's findings are `SK0xxx` already has a home.** `FormattingFindings`
   turns the formatter's own edits into findings with `artifactChanges`, and the arrangement pass can
   emit into the same shape with no new reporting surface.

## M6 — The SonarQube replacement · L

- `SK2xxx` correctness and `SK3xxx` async/lifetime — the rules people actually want.
- Metrics, duplication detection and its index.
- Baselines, fingerprints, `--since`, gates, `--no-new-suppressions`.
- `skala report`, `skala trend`, CI wiring, GitHub SARIF upload.

**Done when:** a `ci` gate runs on Vixen's CI in place of everything else, with a baseline, and the
false-positive triage of a 200-finding sample is under 1 %.

**Release 0.8.**

## M7 — Hardening · M

- `SK4xxx` performance, `SK6xxx` design, `SK8xxx` tests.
- NativeAOT client, ReadyToRun daemon, the startup budget met end to end.
- Cross-platform CI matrix, the fuzzing job, the rule-count job.
- Documentation site generation from `rules.json` + `options.json`.

**Done when:** every budget in [13](13-performance.md) is met and asserted, and the tool has been
adopted by three of the author's repositories beyond Vixen.

**Release 1.0** — at which point rule IDs, option behaviour, exit codes and the SARIF shape are
compatibility surfaces (ADR-012).

## M8 — Security · M

`SK5xxx`, the taint table, the vulnerable/safe corpus, intra-procedural flow on
`ControlFlowGraph`. Last because a wrong security rule is worse than a missing one, and because it
is the only category where the corpus cannot validate correctness on its own.

## M9 — Web languages · XL

[14](14-web-languages.md). Gated on M3 being stable and on the `ISkalaLanguage` seam having been
exercised by lifting the XML sub-formatter out of the C# front end.

---

## The critical path, stated plainly

```
M0 ─▶ M1 ─▶ M2 ─▶ M3 ─▶ M5 ─┬─▶ M3.1 (the fidelity tail, with symbols) ─▶ M4 ─▶ adoption
   ✅     ✅     ✅    ⚠    ✅  └─▶ M6 ─▶ M7 ─▶ M8
                                  └──▶ M9

⚠ M4 and M5 are swapped against the original order. M5 builds the compilation that M4's semantic
half and M3's #if gap both wait on; see M4's header.
```

M3 is the milestone everything else waits on and the one most likely to overrun, because the fitting
engine is the only genuinely novel code in the project. M5 can start on top of an M2-quality
formatter if the analysis half becomes urgent — the two halves share only the configuration layer.

## What is explicitly not on the roadmap

- A web dashboard. [09](09-quality-gates-and-reporting.md) § "History" is the answer.
- A Rider plugin. [11](11-cli-and-integrations.md) § "LSP" says why there is nothing to build.
- VB.NET or F#. The export configures both; both are Tier C forever.
- Bundled third-party rules (ADR-008).
- Any form of telemetry.
