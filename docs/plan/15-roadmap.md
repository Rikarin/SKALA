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

⚠ **Measured: 98.86 %, not 99.9 %.** What landed, and what it is short of:

| | |
|---|---|
| Line fidelity, `corpus/real/` | **98.86 %** (M2 97.47 %), file 71.05 % (M2 49.47 %) |
| [16](16-risks-and-open-questions.md) § R1 | 27 of the 54 constructs occurring more than 50 times are at 100 % |
| Divergences | eight `SK-DIV-*` entries, each with a measurement; SK-DIV-0002 is resolved |
| Wrapping | ✅ `Fill`, the counters, the ordering rule, chains, ternaries, declarators, base lists |
| xmldoc | ⚠ the oracle does not format doc comments (SK-DIV-0006); the well-formedness hint is done |
| Daemon, LSP, hooks | ✅ all three, with tests |
| 40 ms warm | ⚠ 60–70 ms, of which ~60 is the client's process start; NativeAOT is the fix |
| 20 s whole corpus | ✅ **11.9 s** over Vixen, from 34.2 s |
| `defaultSource` | ✅ derived from the oracle: 123 keys `oracle-probe`, `distill` drops 108 |
| Tier A | 201 options, up from 172, each pinned by a committed fixture |
| Vixen `.editorconfig` | prepared and measured — 2 717 files, 83 241 diff lines — **not committed** |

**This is release 0.4 and the first one anyone else could use.** ⚠ It is offered as one on the
strength of the properties rather than the percentage: idempotency, token equivalence, parse
stability, determinism and whitespace absorption hold on every file of every corpus and on all
4 708 files of Vixen, and the fidelity gap is eight named, measured disagreements rather than an
unknown.

## M4 — Arrangement · M/L

The `arrange_*` and body-style settings from [06](06-arrangement-and-syntax-styles.md), plus the
syntactic subset that runs without a compilation.

- The three safety layers: conservative preconditions, re-bind diagnostic delta, symbol-identity
  check.
- `skala arrange [--check]`, `skala format --arrange=syntactic`.

**Done when:** the oracle's cleanup profile and Skala agree on `corpus/real/` at ≥ 99 % of changed
spans, and arrangement over Vixen introduces zero compiler diagnostics.

## M5 — Analysis and the AI gate · L

The point at which Skala replaces Qodana for this workflow.

- Binlog loading, the analyzer host, the incremental cache, `loose` mode.
- `rules.json`, the rule generator, `docs/rules/`, `skala explain`.
- `SK0xxx` + the `SK1xxx` modernization set — the differentiator, and the rules with the highest
  fix-to-report ratio.
- SARIF, renderers, exit codes.
- `skala verify`, `skala fix --safe`, the MCP server.

**Done when:** `skala verify` runs in under a second on a five-file change with no project loaded,
the Claude Code hooks are installed in Vixen, and a week of agent work produces no hand-formatting
and no unexplained suppressions.

**Release 0.6.**

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
M0 ─▶ M1 ─▶ M2 ─▶ M3 ─────────────▶ M4 ─▶ (adoption complete for formatting)
                   └──▶ M5 ─▶ M6 ─▶ M7 ─▶ M8
                                     └──▶ M9
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
