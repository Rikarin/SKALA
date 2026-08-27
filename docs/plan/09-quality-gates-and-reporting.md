# 09 — Quality Gates and Reporting

SonarQube's genuinely valuable parts are not its rules — it is the *lifecycle*: a baseline, a
new-code definition, a gate that fails a build, and a report a human can read in thirty seconds.
Replacing it means replacing those, without a server.

## SARIF is the report (ADR-009)

One `SarifLog` per run, written to `.skala/report.sarif` (or `--output`), containing:

- `runs[0].tool.driver` — Skala's version, the **configuration fingerprint** (hash of the effective
  option set and rule severities), the load mode, and whether any `--option` override was active.
- `runs[0].tool.extensions[]` — one per hosted third-party analyzer package, with version. A report
  that does not say which rules could have fired is a report that cannot be compared to another.
- `runs[0].rules[]` — full metadata for every rule that *could* fire, from `rules.json`.
- `runs[0].results[]` — the findings, each with `ruleId`, `level`, `message`, `locations`,
  `partialFingerprints` (below), `properties.tfms`, and, when a fix exists, `fixes[]` with
  `artifactChanges` — real, applicable edits, not prose.
- `runs[0].invocations[0]` — exit code, timing, whether the run was partial (cancelled, analyzer
  failure), and the list of skipped rules with reasons.

⚠ **M5 built this section and nothing below it.** The renderers, the exit codes and the SARIF are
M5's; baselines, the fingerprint's second half, `--since`, the full gate condition set, duplication,
history and `skala report` are M6's. Where the two meet, M5 chose to fail loudly rather than silently:
a gate definition naming `newIssues`, `baseline`, `metrics` or `ruleOverrides` **fails the gate** with
"not implemented in this build", because a gate that quietly drops the condition someone relies on
passes for the wrong reason.

Everything a human or a machine sees is rendered from this object, in `Rikarin.Skala.Reporting`:

| Renderer | Surface | Notes |
|---|---|---|
| `terminal` | default TTY output | ✅ grouped by file, fixable marked `⟳`. ⚠ Not Spectre: M5 writes plain text, because the only thing the dependency was buying at this size was colour |
| `plain` | `--no-color`, non-TTY | ✅ one finding per line, `path:line:col: level SKxxxx: message` — greppable, and the format every editor's error parser already understands |
| `json` | `--format=json` | ✅ the SARIF, verbatim |
| `github` | CI | ✅ annotations. ⚠ The `$GITHUB_STEP_SUMMARY` table is M6, with the CI wiring |
| `sarif-upload` | CI | the same file; no Skala-side integration needed, so nothing to build |
| `junit` | CI | M6 |
| `markdown` | `skala report --format=markdown` | M6, with `skala report` |
| `agent` | agent, and the MCP server | ✅ the three-bucket report of [10](10-ai-agent-integration.md) |

⚠ No renderer contains analysis logic. A renderer that decides what counts as a failure is a second
implementation of the gate. Renderers read; the gate decides.

## Fingerprints and baselines

### The fingerprint

A finding must survive a file being edited above it, reindented, or moved. `partialFingerprints`
carries:

```
skala/v1 = xxHash128( ruleId ⊕ normalizedSnippet ⊕ enclosingSymbolDisplayString ⊕ ordinalWithinSymbol )
```

⚠ **M5 emits `skala/v1` with the first two terms and the file name, not the last two.** The enclosing
symbol and the ordinal need the symbol display string, which is M6's work beside the baseline that
consumes it. The version tag is why that is safe to defer: adding them is a **new fingerprint
version**, not a silent change of meaning under baselines that already exist. What is already true is
the property the section is about — `ReportingTests.Fingerprint_SurvivesTheFindingMovingDownTheFile`
asserts that the same finding at line 12 and at line 480 hashes the same.

- `normalizedSnippet` — the finding's span, with whitespace collapsed and identifiers preserved.
- `enclosingSymbolDisplayString` — `Vixen.Core.Foo.Bar(int, string)`, stable across file moves.
- `ordinalWithinSymbol` — disambiguates two identical findings in one method.

No line numbers. A fingerprint that moves when a line moves is a baseline that expires every commit.

### The baseline

`.skala/baseline.sarif` is a normal SARIF file with the results the repository has *accepted for now*.
It is created by `skala baseline create`, updated by `skala baseline update`, and is a reviewed,
committed artefact — its diff in a PR is "we suppressed these", which is exactly the conversation
that should happen.

At report time, findings are partitioned:

| Bucket | Definition |
|---|---|
| **New** | fingerprint not in the baseline |
| **Existing** | fingerprint in the baseline, still firing |
| **Fixed** | in the baseline, no longer firing — reported as good news, and `baseline update` prunes them |

⚠ Pruning fixed findings must be explicit. A baseline that self-prunes lets a rule that silently
stopped working look like progress.

### New-code definition

Sonar's "clean as you code" is the right idea and needs no server: `--since=<git-ref>` computes the
changed line ranges from `git diff` and marks findings inside them. `skala check --since=origin/main
--gate=pr` is then a gate that only cares about what this PR touched — which is the only gate that is
adoptable on a tree with existing findings.

Three ways to scope, composable: `--since` (git ranges), `--baseline` (fingerprints), `--path`.

## Gates

Defined in `skala.jsonc`, named, and selected with `--gate`:

```jsonc
"gates": {
  "local": { "maxSeverity": "error" },
  "pr":    { "since": "origin/main", "newIssues": 0, "maxSeverity": "warning",
             "formatting": "clean", "coverage": null },
  "ci":    { "baseline": ".skala/baseline.sarif", "newIssues": 0,
             "maxSeverity": "warning", "formatting": "clean",
             "metrics": { "duplication": 3.0, "cognitiveComplexity": 15 } }
}
```

A gate is a set of conditions; failing any of them fails the run. Conditions:

| Condition | Meaning |
|---|---|
| `maxSeverity` | any finding at or above this level fails |
| `newIssues` | maximum count of *new* findings (relative to baseline and/or `--since`) |
| `formatting` | `clean` ⇒ `skala format --check` must produce no edits |
| `metrics.*` | thresholds on the aggregate metrics |
| `ruleOverrides` | per-rule tightening, e.g. `SK5*: 0` regardless of the rest |

### Exit codes

Fixed, documented, and depended upon by hooks, CI and agents:

| Code | Meaning |
|---|---|
| 0 | gate passed (findings may exist below the gate) |
| 1 | gate failed |
| 2 | formatting changes needed (`format --check` only) |
| 3 | configuration error (`SK9001`–`SK9005` under `--strict-config`) |
| 4 | load failure — no compilation could be built |
| 5 | internal error, including `SK9099` |
| 130 | cancelled |

⚠ 2 is distinct from 1 on purpose: a hook that wants to auto-format on exit 2 and stop on exit 1 is
a two-line hook.

## Duplication

The one Sonar feature with no Roslyn equivalent, and one the AI workflow needs more than most —
agents copy-paste by nature.

Algorithm: token-level, type-2 clone detection (identifiers and literals normalised, structure
compared).

1. Lex every file to a token stream, dropping trivia, mapping identifiers to a canonical class
   (`ID`), keeping keywords and punctuation exact.
2. Rolling hash over windows of `minTokens` (default 100 ≈ 25 lines, Sonar's default is 100 for C#).
3. Bucket by hash; verify candidates exactly; greedily extend matches in both directions.
4. Report each maximal clone group once, at the *first* occurrence, with the others as related
   locations — `SK7020`.

Index is persisted in `.skala/cache/clones.idx`, keyed by file content hash, so an unchanged file's
windows are not re-hashed. Whole-corpus cost is one pass and is bounded by I/O.

Reported as both findings and a percentage (`duplicatedLines / totalLines`), which is what the
`metrics.duplication` gate reads. Generated files are excluded from both numerator and denominator;
test files are counted separately, because test duplication is often deliberate and gating it drives
people to write worse tests.

## The human report

`skala check` on a TTY, default output, is the thing people will judge the tool by:

```
Vixen  ·  4691 files  ·  1 348 236 lines  ·  binlog artifacts/build.binlog (fresh)

  Core/Vixen.Ecs/Archetype.cs
    ⟳ 142:13  suggestion  SK1001  Use a collection expression
      warning     SK3002  Blocking on an async call inside a lock          ← 87:9

  Editor/Vixen.Editor.Profiler/GpuTimelineView.cs
    ⟳ 33:5    suggestion  SK1002  Use a primary constructor

  212 findings  ·  198 fixable (`skala fix`)  ·  4 new since origin/main
  duplication 1.8 % (gate 3.0 %)  ·  cognitive complexity p95 9 (gate 15)
  gate `ci`: PASS in 3 m 41 s
```

Rules for this output: findings that a fix exists for are marked and counted, because the next
command is obvious; the gate result is one line and is the last line; timing is always shown, because
a tool whose cost is invisible gets blamed for the build being slow; and the totals line is stable
enough to diff between runs.

`--summary` prints only the last three lines. `skala report` re-renders a stored SARIF without
re-running anything, which is what CI uses to produce a PR comment from an artifact.

## History

No database. `skala check --record` appends one line of JSON to `.skala/history.jsonl`: timestamp,
git SHA, totals per severity, metrics, gate result, duration. `skala trend` renders it.

That file is committed or not, per repository — committed, it gives a reviewable record of the
codebase's direction with no infrastructure; uncommitted, it is a local convenience. Either way the
answer to "is this getting better" is a `git log` away, which is the SonarQube dashboard's actual
job, minus the server.
