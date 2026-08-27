# Skala

**One configuration, the same formatting and analysis everywhere.**

Skala is a C# formatter, linter and quality gate that reads the `.editorconfig` Rider exports — the
real one, all 4 226 assignments of it — so the IDE, the command line, CI and an AI agent agree about
what the code should look like by construction rather than by discipline.

## What it replaces

| Today | What Skala does instead |
|---|---|
| **ReSharper / Rider code cleanup** for formatting | Reads the same `resharper_*` keys, from the same file, and matches the output — **99.70 % of lines** on the reference corpus |
| **`dotnet format`** | Actually wraps. Roslyn's formatter adjusts whitespace between tokens and preserves the line breaks it is given; `max_line_length = 120` with `chop_if_long` cannot be built on top of it, so Skala has its own line-fitting engine |
| **CSharpier** | Configurable, and **preserve-and-repair** rather than print-from-scratch. The export sets `keep_user_linebreaks = true`; Prettier-lineage tools discard the author's line breaks by design |
| **Qodana** | Hosts analyzers over a real compilation, writes SARIF, and is not a 400 MB proprietary download |
| **SonarQube** | Metrics, duplication detection, baselines, fingerprints, `--since` scoping and gates |

**The one-sentence problem it exists for:** ReSharper's own CLI is the only thing that reads
`resharper_*` keys, and it has no machine-readable diff output — so there is currently no way to make
a build agent, a CI job and an AI agent agree with Rider about what the code should look like.

## Where it is

**Milestone 7 of nine, released as 1.0.** See [`docs/overview.md`](docs/overview.md) for what is
built, measured against the code — it is the only place with per-feature status, and every number in
it was produced by running something.

| | |
|---|---|
| **Line fidelity vs. `jb cleanupcode`**, `corpus/real/` (380 files, 76 375 lines) | **99.70 %** with the compilation's preprocessor symbols, 99.63 % without |
| File fidelity, same corpus | **85.79 %** — 54 of 380 files differ at all |
| Files containing no `#if` (289) | **99.79 %** line — milestone 3's revised ≥ 99.5 % bar, met |
| ⚠ The ≥ 99.9 % overall bar | **Not met**, at 99.70 %, and missed with a measurement twice running |
| Idempotency, token equivalence, parse stability, determinism, range consistency | **100 %** of all three corpora, under both symbol sets — 8 981 conformance cases |
| Documented divergences (`SK-DIV-*`) | **12**, each with a current measurement: 3 resolved, 2 half closed, 7 open |
| **Tier A options** — implemented and pinned by an oracle fixture | **201 of 520** |
| Documentation comments | ⚠ **not formatted by default** — Rider's cleanup does not format them (SK-DIV-0006). `format --xmldoc` turns on the sub-formatter: 17 of the 27 `resharper_xmldoc_*` keys honoured, 10 refused with a reason, none Tier A and none able to become Tier A. Worth **−3.59 points** of line fidelity when on, and **zero** on the lines it may not touch |
| Defaults derived from the oracle rather than guessed | **123** keys |
| **Rules shipped** | **24 of the 109** the catalogue names — 13 analyzers, 8 metrics, 3 formatter findings — plus 8 tool diagnostics |
| False positives on the reference trees | **zero**, every finding read at the milestone that shipped it |
| Warm single-file format | **12.4 ms** against a 40 ms budget |
| `format --check` over 4 717 files | **12.4–13.8 s** against a 20 s budget |
| Tests | **9 795 passing**, 0 failing |

⚠ **A rule ships when it has a fix, zero false positives on the reference corpus, and a "should not
fire" fixture set at least as large as its positive one.** Twenty-four of a hundred and nine is that
bar working, not the plan falling short — and [`docs/plan/08`](docs/plan/08-rule-catalogue.md)
§ "Rule status" says, per rule, which are shipped, which were cut and why, and which are outstanding.

⚠ **The reference trees are a test subject, not a specification.** Skala is measured against
[Vixen](https://github.com/Rikarin/Vixen) and against vendored Serilog and Newtonsoft.Json because
they are real code, not because their present habits are the standard. Where they do not follow a
rule, they change. A low finding count on one of them was never evidence that a rule is good.

## Installing it

⚠ **Nothing is published yet.** There is no NuGet feed, no GitHub release and no publish workflow, so
the only way in is to build from source. Three packable projects exist (`Rikarin.Skala.Cli` as a
`dotnet tool`, `Rikarin.Skala.Rules` as analyzers, `Rikarin.Skala.Canonical` as the
`.editorconfig` payload) and none of them is pushed anywhere.

```bash
git clone https://github.com/Rikarin/Skala && cd Skala
./build.sh Native          # the shipping layout: an AOT `skala` beside a ReadyToRun `skala-tool`
artifacts/native/<rid>/skala --help
```

You need the .NET SDK pinned in [`global.json`](global.json) and nothing else. `./build.sh Oracle`
additionally wants `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`, and
nothing in the day-to-day loop does: the test run reads committed `.expected.cs` fixtures, and
regenerating them is a reviewed commit of its own.

⚠ **Two binaries, and the reason is the 40 ms budget.** `skala` is a NativeAOT thin client that
references a socket and a JSON writer; `skala-tool` beside it is the full tool, which is also what
the daemon runs as and what the client execs for anything that is not a warm single-file format.
Before the split, `skala daemon status` — a command that does no work at all — cost **43 ms** before
`Main` ran, because the one binary referenced Roslyn.

## Using it

```bash
skala verify                 # is this acceptable? exit 0, or it is not finished
skala fix --safe             # apply the fixes that are provably behaviour-preserving
skala explain SK1010         # why the rule exists, before arguing with it
skala mcp                    # the same six answers over the Model Context Protocol
```

```bash
skala format [paths…] [--check] [--diff] [--range a:b] [--staged] [--jobs n]
skala check  [paths…] [--load binlog|workspace|loose] [--gate ci] [--since <ref>] [--baseline]
skala config explain | check | diff | distill | fix | sync
skala daemon status | stop | run       # the per-repository format daemon
skala lsp                              # formatting, range formatting, diagnostics, code actions
skala hooks install                    # the pre-commit hook, unless a hook manager owns it
```

`--check` writes nothing and exits non-zero when there is anything to do. `--staged` formats the git
index and writes back to both the worktree and the index; it refuses when a staged file also has
unstaged changes, unless you pass `--staged=worktree`.

The daemon is started lazily, exits after thirty minutes idle, and is **only ever an optimisation**:
`SKALA_NO_DAEMON=1` or `--no-daemon` produces byte-identical output, and a daemon that is absent,
stale or of another protocol version is a silent fallback rather than an error.

⚠ **Rider needs no integration and will not get one.** Rider already implements this
`.editorconfig` — it is where the file came from. If Rider and Skala disagree, the fix is in Skala.

## The two promises

- **It never writes a file whose token stream differs from the input's.** Every write is verified; a
  mismatch is `SK9099`, writes nothing, and drops a reproduction under `.skala/crash/`. There is no
  flag that turns it off.
- **It never formats a file it could not parse.** `SK9010`, reported, left byte-identical.

## Building it

```bash
./build.sh Test          # everything — 9 802 tests
./build.sh Conformance   # the differential suite and the fidelity ratchet
./build.sh Fidelity      # the ranked divergence report — the work queue
./build.sh Native        # the AOT client beside the full tool
./build.sh Docs          # regenerate docs/rules/ and docs/site/ from the two registries
./build.sh Oracle        # ⚠ regenerate the committed fixtures from `jb cleanupcode`
```

⚠ **The performance budget assertions do not run under `./build.sh Test`.** They need
`SKALA_PERF=1` and a published native layout, because a wall-clock assertion on a shared runner is a
flaky test. CI runs them in a job of their own, on its own runner.

Beyond the Nuke targets, `Testing/Rikarin.Skala.Testing` is a harness of developer-machine actions
and none of them is a test: `ask <dir>` runs the oracle over a scratch directory so that "what does
`wrap_if_long` do to a six-element array at 121 columns" gets an answer instead of a guess;
`defaults` derives ReSharper's default table from it; `margin` sweeps the constant in
[SK-DIV-0005](docs/sk-div-0005-margin-sweep.md); `locate <set> <kind>` prints the divergent lines
attributed to one construct; `tree <dir> [n]` runs both tools over an arbitrary repository; and
`sample <tree> <n> <dest>` redraws a corpus sample reproducibly, by a hash of each file's path.

## Release 1.0 — what became a contract

⚠ **Four things, and the rest of the tool deliberately did not.**

| | What that means |
|---|---|
| **Rule IDs** | `SK1010` means what it means for ever. An id is never re-purposed; a withdrawn rule is marked `retired`, not deleted. The reason is baselines: a fingerprint carries the rule id, so one number with two meanings silently un-suppresses one finding and wrongly suppresses another in every repository holding a baseline |
| **Option behaviour** | A key that does something keeps doing that thing. New keys may be added; an existing key's effect on an existing file does not change without a new key to ask for the change |
| **Exit codes** | `0` nothing to do · `1` gate failed · `2` formatting needed · `3` configuration error · `4` load failure · `5` internal error · `130` cancelled |
| **The SARIF shape** | Fields present at 1.0 stay present and keep their meaning. Paths are repository-relative with forward slashes on every platform |

**Not a contract, and it will change:** the formatter's output (closing the last 0.3 % means files
formatted at 1.0 are formatted differently at 1.1 — pin the version if that matters); which rules
exist; a rule's default severity; the daemon protocol; and every `Testing/Rikarin.Skala.Testing`
subcommand.

**Known gaps at 1.0**, because a version number is not a claim of completeness: `skala arrange` (M4)
does not exist; `SK5xxx` security rules (M8) do not exist; the nightly job runs the property suite
and **there is no fuzzer**; web languages (M9) are postponed to last and the language seam they are
gated on has not been written; and **the tool is not yet adopted by any repository, including the two
it is measured against**. [`docs/overview.md`](docs/overview.md) § "What is not built" is the full
list, and [`docs/plan/15`](docs/plan/15-roadmap.md) is where each one sits in the order.

## The documents

| | |
|---|---|
| [`docs/overview.md`](docs/overview.md) | **What is built**, checked against the code. Start here |
| [`docs/plan/`](docs/plan/README.md) | The design record — what Skala is meant to be, and why. Seventeen documents |
| [`docs/divergences.md`](docs/divergences.md) | Every deliberate difference from the oracle, with a current measurement |
| [`docs/rules/`](docs/rules/README.md) | One page per rule, generated from `rules.json` |
| [`docs/site/`](docs/site/index.html) | The same, plus a page per option, as a static site |

Where a document under `docs/plan/` and `docs/overview.md` disagree, **the overview wins** — it is
the one that was checked.
