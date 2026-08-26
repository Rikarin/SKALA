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

## M2 — Formatter, phase 2 · M/L

Break presence: required and forbidden line breaks, `place_*`, `keep_existing_*`, attributes.

- Two-pass fitting for `if_owner_is_single_line`.
- The `Preserve` group and the four-way preservation table — with `constructs/preservation/` run
  under all four combinations.

**Done when:** line fidelity ≥ 93 %, and formatting Vixen produces a diff small enough to read in one
sitting.

## M3 — Formatter, phase 3–4 · L/XL

Wrapping, the fitting algorithm proper, xmldoc. **This is the milestone that makes the tool the thing
it is for.**

- The 47 `wrap_*` keys, the `max_*_on_line` counters, `Fill`, break-position keys.
- The xmldoc sub-formatter.
- Daemon, LSP, git hooks, the post-edit hook path and its 40 ms budget.

**Done when:** line fidelity ≥ 99.9 %, all divergences are documented `SK-DIV-*` entries, and
Vixen's `.editorconfig` is replaced by the export with `skala format --check` clean in CI.

**This is release 0.4 and the first one anyone else could use.**

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
