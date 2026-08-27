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

⚠ **M5 built this section; M6 built everything below it.** The renderers, the exit codes and the
SARIF are M5's; baselines, the fingerprint's second half, `--since`, the full gate condition set,
duplication, history and `skala report` are M6's and are now in. M5's rule that a gate condition it
could not evaluate **fails the gate** rather than being ignored is kept and still applies — to a
condition *this* build does not understand, and to one that names a metric the run did not measure.
A gate asking about duplication in a run without `--duplication` has not been satisfied; it has been
skipped, and the two must not look the same from outside.

Everything a human or a machine sees is rendered from this object, in `Rikarin.Skala.Reporting`:

| Renderer | Surface | Notes |
|---|---|---|
| `terminal` | default TTY output | ✅ grouped by file, fixable marked `⟳`. ⚠ Not Spectre: M5 writes plain text, because the only thing the dependency was buying at this size was colour |
| `plain` | `--no-color`, non-TTY | ✅ one finding per line, `path:line:col: level SKxxxx: message` — greppable, and the format every editor's error parser already understands |
| `json` | `--format=json` | ✅ the SARIF, verbatim |
| `github` | CI | ✅ annotations, **and** the `$GITHUB_STEP_SUMMARY` table. ⚠ The summary is written by the renderer itself rather than by a separate step, because a step that can be forgotten is a run that looks clean |
| `sarif-upload` | CI | ✅ the same file, uploaded by `github/codeql-action/upload-sarif`; no Skala-side integration needed, so nothing was built. `.github/workflows/skala.yml` is the wiring |
| `junit` | CI | ✅ one test case per finding, suites per rule. ⚠ Not per file: CI systems dedupe on the case name, so a file with twelve findings would report one |
| `markdown` | `skala report --format=markdown` | ✅ bounded at 50 rows, with the elision saying what was elided |
| `agent` | agent, and the MCP server | ✅ the three-bucket report of [10](10-ai-agent-integration.md) |

⚠ No renderer contains analysis logic. A renderer that decides what counts as a failure is a second
implementation of the gate. Renderers read; the gate decides.

## Fingerprints and baselines

### The fingerprint

A finding must survive a file being edited above it, reindented, or moved. `partialFingerprints`
carries:

```
skala/v2 = xxHash128( ruleId ⊕ normalizedSnippet ⊕ enclosingSymbolDisplayString ⊕ ordinalWithinSymbol )
skala/v1 = xxHash128( ruleId ⊕ normalizedMessage ⊕ fileName )            // M5's, still emitted
```

⚠ **M6 emits `skala/v2` with all four terms, and `skala/v1` beside it.** M5's v1 was the rule id,
the normalised *message* and the file name; adding the enclosing symbol and the ordinal changes what
the hash means, so it is a new version rather than a redefinition — which is exactly what the version
tag was put there for. Reading a baseline matches on v2 and falls back to v1, one-directionally: a v2
hash never matches a v1-only entry the other way round, because v1 is the weaker identity and letting
it match would silently widen what the baseline suppresses.

⚠ **The snippet, not the message, and this is load-bearing.** A message can carry a line number, a
count or a path; the source text of the span cannot. `LifecycleTests` pins both halves of the
property the section is about: the same finding at line 12 and at line 4 801 hashes the same, *and*
the same finding in `Core/Foo.cs` and in `Engine/Renamed/Foo.cs` hashes the same.

⚠ **The cache has to carry the fingerprint's terms too, and M6 shipped that bug before it shipped
the fix.** `CachedFinding` was written in M5 without them, so a warm run rehydrated findings with an
empty enclosing symbol and an empty snippet and hashed them differently from a cold run. Measured on
Vixen's `Core/`: a baseline of 686 accepted findings matched **nothing** on the next run — 686
reported "fixed" and 686 reported "new", on a tree where not one byte had changed. It is the same
failure the no-line-numbers rule exists to prevent, arriving through the cache instead of through a
line number, and it is invisible from outside because a baseline that matches nothing looks exactly
like a repository where everything is new. The cache key carries a `cache/v2` tag so entries written
before the fix are discarded rather than deserialised with both fields empty.

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
stopped working look like progress. M6 makes this four verbs — `create`, `update`, `prune`, `show` —
and `update` never removes an entry, so running it can only ever widen what is suppressed and the
diff shows by exactly how much. Every writing verb needs `--apply`.

⚠ The demonstration that the distinction is not theoretical: running `check` against a baseline that
included `SK7020` entries, but *without* `--duplication`, reported 308 findings as **fixed**. Nothing
had been fixed; a rule had not run. A self-pruning baseline would have deleted all 308 and called it
progress.

### New-code definition

Sonar's "clean as you code" is the right idea and needs no server: `--since=<git-ref>` computes the
changed line ranges from `git diff` and marks findings inside them. `skala check --since=origin/master
--gate=pr` is then a gate that only cares about what this PR touched — which is the only gate that is
adoptable on a tree with existing findings.

⚠ **Three details, each of which was a bug before it was a rule.** All three are pinned by tests that
drive a real git repository, because every one of them was a defect in the *interface* to git rather
than in the logic around it, and a mocked git cannot have them:

1. **`--unified=0`.** `git diff` defaults to three lines of context either side. With them, a finding
   on an untouched line three above an edit counts as new code, and a gate built on that fails a PR
   for something it did not do.
2. **The merge base, diffed against the working tree, which takes two commands.** `git diff ref` with
   two dots has the wrong base — on a branch whose base has moved on it reports every change the base
   picked up, someone else's commits attributed to this branch. `git diff ref...` with three dots has
   the right base and the wrong right-hand side: three-dot syntax needs two commits, so `ref...` means
   `ref...HEAD` and **the working tree is excluded entirely**. Uncommitted work — most of what a
   developer runs this against — was invisible. The merge base is resolved explicitly and then diffed
   with two dots, which is the only spelling with both halves right.
3. **Untracked files are entirely new code.** `git diff` reports tracked files only, so a file the
   branch *added* produces no hunk and every finding in it falls outside the ranges. A PR gate that
   ignores the files the PR added is worse than no gate: it is quiet in exactly the case it exists
   for, and the quiet reads as approval. `git ls-files --others --exclude-standard` supplies them, so
   that a `.gitignore`d build artefact is still not counted as somebody's new code.

Two ways to scope, composable: `--since` (git ranges) and `--baseline` (fingerprints).

⚠ This said "three … `--path`", and there is no `--path` option on `check`. Path scoping is the
variadic `<paths>` argument — `skala check --since=origin/main Core/` — which composes with both of
the others and always did. The third name was never needed and, until M9, naming it produced
`SK9023: no C# files were found` and exit 4, because `<paths>` swallowed the unknown flag as a
directory name. That is now a configuration error that names the token.

## Gates

Defined in `skala.jsonc`, named, and selected with `--gate`:

```jsonc
"gates": {
  "local": { "maxSeverity": "error" },
  "pr":    { "since": "origin/master", "newIssues": 0, "maxSeverity": "warning",
             "formatting": "clean", "coverage": null },
  "ci":    { "baseline": ".skala/baseline.sarif", "newIssues": 0,
             "maxSeverity": "warning", "formatting": "clean",
             "metrics": { "duplication": 3.0, "cognitiveComplexity": 15 } }
}
```

A gate is a set of conditions; failing any of them fails the run. Conditions:

⚠ **`maxSeverity` is scoped when the gate is scoped, and M6 had to decide this.** The table below
said "any finding at or above this level fails", and read literally that makes the `ci` gate in the
example above — a baseline *plus* `maxSeverity: warning` — unsatisfiable on every repository that has
ever had a warning. Measured on Vixen's `Core/`: 994 findings accepted into a baseline, 0 new, and a
literal reading still failing on 308 of the accepted ones. A baseline whose entries keep failing the
gate has not accepted anything, and "adoptable on a tree with existing findings" is the whole point
of § "New-code definition". So: with no baseline and no `--since`, every reportable finding counts,
which is unchanged from M5 and is what `local` gets; with either scoping in play, the condition
applies to the findings that scoping calls new. The severity bar and the new-code bar then compose
instead of contradicting each other.

| Condition | Meaning |
|---|---|
| `maxSeverity` | any finding at or above this level fails — scoped to *new* findings when a baseline or `--since` is in play, see above |
| `newIssues` | maximum count of *new* findings. ⚠ The **intersection** of the scopings in play, never the union: with a baseline and `--since` both active a finding is new only if it is absent from the baseline *and* on a line the branch touched, because a gate firing on either would fail a PR for a pre-existing finding that happens to sit near an edit. ⚠ Naming `newIssues` with neither scoping is a configuration error and is reported as one, rather than counting every finding in the repository as new |
| `formatting` | `clean` ⇒ `skala format --check` must produce no edits — **`SK0001` and only `SK0001`**, see below |
| `metrics.*` | thresholds on the aggregate metrics |
| `ruleOverrides` | per-rule tightening, e.g. `SK5*: 0` regardless of the rest. ⚠ A prefix glob or an exact id, not a regular expression — the only shapes the section asks for are a range and an id, and a regular expression in a configuration file is a thing people get wrong silently |

⚠ **`formatting: clean` is `SK0001`, and M9 had to decide this too.** The formatting half of a run
returns three ids, and only one of them is an edit. `SK0001` carries the formatter's own `TextChange`
list — that *is* "`format --check` would edit this file". `SK0002` (a line over the limit with no
break point anywhere in it) and `SK0003` (a doc comment that is not well-formed XML) are reported
precisely *because* the formatter refuses to touch them: there is no safe change to make, so both
are emitted at hidden severity and left alone.

The first implementation counted all three, and the condition was then unsatisfiable. Measured on
Vixen's `Core/Vixen.Water` after a full `skala format`: `format --check` reports **0 files would be
reformatted**, and the `ci` gate still failed with *"formatting is not clean; run `skala format`"* —
on **23 `SK0002` hints** that `skala format` cannot clear, that do not appear in the default report,
and that a baseline could not absorb because the bit was computed before scoping. Any repository
containing one unbreakable long line — a long URL in a comment, a wide string literal — was blocked
out of the gate entirely, and `--no-formatting` was the only way through.

`SK0002` and `SK0003` remain findings and remain subject to `maxSeverity`, `newIssues` and the
baseline like everything else. They are simply not an answer to "would the formatter edit this".

⚠ **And a gate that names `formatting` fails under `--no-formatting`.** The condition used to default
to satisfied when the run never collected formatting, so the flag that suppressed the measurement
also suppressed the check — the one shape of "passing for the wrong reason" this section's opening
paragraph already forbids for an unrecognized condition. It now fails the same way, naming the flag.

### `--no-new-suppressions`

⚠ **A grep for `#pragma` is not a constraint.** There are four ways to make a finding go away without
fixing it, and the pragma is the only one that reads as a suppression in review:

| | Mechanism | Why it is the harder one |
|---|---|---|
| 1 | `#pragma warning disable` | Visible and local. The one everybody checks for. |
| 2 | `[SuppressMessage]` | Visible, but attached to a symbol and easy to read past. |
| 3 | ⚠ An `.editorconfig` severity turned down | **The widest by a long way.** One line under a section header silences a rule for a whole directory tree, and its diff looks like configuration. The section header is part of the suppression's identity: moving a severity line from `[Tools/**/*.cs]` to `[**/*.cs]` changes nothing textually and changes everything about what it silences. |
| 4 | ⚠ A baseline addition | Invisible in the source entirely. The baseline's diff is meant to be the conversation, and this is what makes it one. |

"Turned down" is a comparison rather than a membership test — `warning` → `suggestion` is a downgrade
even though neither end is `none` — and a severity turned *up* is not a suppression at all.

⚠ The audit reads each tree with **one** `git grep` rather than one `git show` per file. The obvious
implementation measured **3 m 19 s** on a 2 705-file tree, and a gate condition that costs three
minutes is a gate condition somebody deletes; after, it is under a second. ⚠ And the pathspec is not
shared between the two sides: `git ls-files "*.cs"` matches nested paths and `git ls-tree -r -- "*.cs"`
does **not**, so the first attempt read the old side as empty and reported all 1 012 pre-existing
suppressions in the tree as newly added.

### Exit codes

Fixed, documented, and depended upon by hooks, CI and agents:

| Code | Meaning |
|---|---|
| 0 | gate passed (findings may exist below the gate) |
| 1 | gate failed |
| 2 | formatting changes needed (`format --check` only) |
| 3 | configuration error — an unrecognized option, a path that does not exist, an invocation the tool refuses, and `SK9001`–`SK9005` under `skala config check --strict` |
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
windows are not re-hashed. ⚠ It stores the **normalised token stream** rather than the window hashes:
verification needs the stream anyway, and a cached hash with no stream behind it could only be
trusted — which is the one thing this rule promises not to do. The lexer is the expensive half and is
what gets skipped. Whole-corpus cost is one pass and is bounded by I/O: measured at **1.45 s** over
4 700 files and 1.33 M lines, and **37 s** over Vixen inside a full `check`.

⚠ Greedy extension has to spend every window *overlapping* a reported occurrence, not only those
starting inside it. Without that, three files sharing 120 tokens where two also share the token before
them produce a 120-token group of three **and** a 121-token group of two saying the same thing.

Reported as both findings and a percentage (`duplicatedLines / totalLines`), which is what the
`metrics.duplication` gate reads. Generated files are excluded from both numerator and denominator;
test files are counted separately, because test duplication is often deliberate and gating it drives
people to write worse tests. ⚠ Production and test files are also *matched* separately — a production
file is never compared against a test one — so a group's bucket is never ambiguous, and a line that
takes part in several groups is counted once, because a line counted twice makes a percentage that can
exceed 100.

⚠ **Which files are offered decides whether the number means anything**, and two of the three ways to
get it wrong are not obvious:

- A multi-targeted project holds the same file in two compilations. Feeding each compilation's trees
  in reports every file as a perfect clone of itself and takes the percentage to 100.
- Deliberate near-duplicate corpora — this repository's own rule fixtures, `Testing/corpus` — take
  Skala's tree from 3.8 % to **70.9 %** if they are included. Whatever set is handed to the detector
  has to exclude them, or the metric is noise.
- ⚠ Test detection cannot rely on the assembly name alone. Under `--load=loose` there are no projects,
  so every file arrives in one synthetic unit and an entire test suite lands in the production
  numerator: measured on Vixen, that is the difference between **6.1 %** and **4.8 %**. The path
  convention (`*.Tests`) is the fallback that makes the loose number mean the same thing as the binlog
  one.

## The human report

`skala check` on a TTY, default output, is the thing people will judge the tool by:

```
Vixen  ·  4691 files  ·  1 348 236 lines  ·  binlog artifacts/build.binlog (fresh)

  Core/Vixen.Ecs/Archetype.cs
    ⟳ 142:13  suggestion  SK1001  Use a collection expression
      warning     SK3002  Blocking on an async call inside a lock          ← 87:9

  Editor/Vixen.Editor.Profiler/GpuTimelineView.cs
    ⟳ 33:5    suggestion  SK1002  Use a primary constructor

  212 findings  ·  198 fixable (`skala fix`)  ·  4 new since origin/master
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

⚠ Every line carries the configuration fingerprint, and `skala trend` marks the rows whose fingerprint
differs from the newest. Two reports with different fingerprints are not comparable, and a trend is
nothing but a comparison — so a row that is not comparable has to say so rather than being quietly
plotted beside the ones that are, which is how an improvement somebody made by turning a rule off
looks like an improvement.

⚠ Appended, never rewritten. The file's whole value is that it is an unedited record, and a partial
run must not be able to truncate the history it failed to extend. A malformed line is skipped rather
than fatal: the file is appended to by concurrent CI jobs and hand-edited by people, and one torn line
must not lose six months of history.
