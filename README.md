# Skala

One configuration, the same formatting and analysis everywhere: a C# formatter and static-analysis
tool that reads the `.editorconfig` Rider exports, so the IDE and the gate agree by construction
rather than by discipline. See [`docs/plan/`](docs/plan/README.md).

**Status: milestone 5 of nine** (M4 and M5 are swapped; see
[15 § M4](docs/plan/15-roadmap.md)). The formatter does spaces, blank lines, braces, indentation,
break presence and position, and wrapping: it fills what `wrap_if_long` fills, chops what
`chop_if_long` chops, honours the `max_*_on_line` counters, and chooses *which* of a long line's
several candidate points to wrap at. The analysis half loads a compilation three ways, hosts
analyzers, writes SARIF, and answers an agent in three buckets.

```bash
skala verify                 # is this acceptable? exit 0 or it is not finished
skala fix --safe             # apply the fixes that are provably behaviour-preserving
skala explain SK1010         # why the rule exists, before arguing with it
skala mcp                    # the same six answers over the Model Context Protocol
```

## Where it is

| | |
|---|---|
| **Line fidelity vs. `jb cleanupcode`** on `corpus/real/` (380 files, 76 375 lines) | **99.70 %** with the oracle's symbols, **99.63 %** without (M3/M5: 98.86 %, M2: 97.47 %, M1: 94.28 %) |
| File fidelity, same corpus | **85.79 %** / 85.26 % (M5: 71.32 %, M2: 49.47 %) |
| … on `corpus/constructs/` (273 files) | 96.64 % line, 91.21 % file |
| … on `corpus/pathological/` (52 files) | 95.60 % line, 86.54 % file |
| … on `constructs/preservation/` under the four `keep_existing_*` combinations | 95.98 / 100 / 92.65 / 100 % line |
| Idempotency, token equivalence, parse stability, determinism, range consistency | 100 % of the corpus, every test run, in every configuration — and of all 4 708 Vixen files |
| **Tier A options** — implemented and pinned by an oracle fixture | **201 of 520** (M2: 172 of 483) |
| Defaults derived from the oracle rather than guessed | 123 keys `oracle-probe`; `config distill` drops 108 |
| Documented divergences (`SK-DIV-*`) | **12**, each with a measurement; SK-DIV-0004 closed at M5, SK-DIV-0008 half closed at M3.1 |
| Files with no `#if` (289 of 380) | **99.79 %** line, 89.97 % file — milestone 3's revised ≥ 99.5 % bar, met |
| **Rules shipped** — a fix, zero false positives, a negative fixture set at least as large | **6 analyzers** + 3 formatter findings |
| False positives on `corpus/real/` + 4 688 Vixen files | **zero**, over 155 findings, every one reviewed |
| `skala verify`, 5 files, no project, cold process | **0.39–0.54 s** against a 1 s budget |
| `skala check --load=binlog` over Vixen | **58–134 s** against a 4-minute budget |

Per origin, because the three measure different things:

| Origin | Line | File | What it measures |
|---|---:|---:|---|
| `vixen/` | 99.81 % | 90.00 % | Does Skala leave code that already conforms alone |
| `newtonsoft/` | 99.41 % | 80.91 % | Does Skala move Allman-braced, differently-spaced code to where Rider would put it |
| `serilog/` | 99.57 % | 81.43 % | Same, a second house style |

The remaining ~230 lines are twelve named disagreements, not an unknown. Split by cause: the 289
files with no `#if` are at 99.79 %, the 91 with one at 99.36 %, and the 11 with a raw literal at
99.68 %.

⚠ **The ≥ 99.9 % bar is not met and this is the second milestone to say so with a number.** The two
largest classes are [SK-DIV-0005](docs/divergences.md) — where ReSharper's wrap decision was swept
over a hundred cells and turns out not to be a function of anything this formatter measures — and
[SK-DIV-0011](docs/divergences.md), where the oracle sometimes breaks after a lambda's `=>` and
sometimes chops the body instead, and none of the obvious discriminators separates the two.

⚠ **The Vixen sample in the corpus was re-based at milestone 3.1.** 167 of its 200 files had come
from agent scratch checkouts rather than the mainline tree; the content was real and the numbers
stood, but "which 200 files" had no reproducible answer. It is drawn from a recorded commit by a
committed sampler now, and the swap on its own is worth +0.08 points — a different 200 files rather
than a better formatter.

⚠ [SK-DIV-0004](docs/divergences.md) — "Skala parses without a project, so a `#if DEBUG` body is
frozen" — **is closed**: `skala format --define`, and symbols taken from a loaded compilation. It was
worth less than M3 estimated. A third of the gap was the guess; 0.32 points on those 91 files and
0.07 overall is the measurement, and what remains in them turns out to be ordinary wrapping tail
rather than frozen text. It did uncover a formatter bug that had been invisible since M1 — `>`
followed by `(` was read as a call site, so `count > (n)` lost its space, and every corpus line that
shows it is inside a conditional body.

⚠ The number the project is judged on is not this one. [16 § R1](docs/plan/16-risks-and-open-questions.md)
asks a sharper question — *any construct occurring more than 50 times must be at 100 %* — and
`./build.sh Fidelity` answers it: **37 of 56**, up from 27 of 54. ⚠ Milestone 3.1 also established
that R1 as stated is equivalent to 100 % line fidelity, because every divergent line is attributed to
something that occurs more than fifty times; it needs re-stating as a rule about attributed *share*.

⚠ `./build.sh Fidelity` runs the differential under **both symbol sets** by default and names the
divergences that appear under one and not the other. Milestone 5's `>`-before-`(` defect survived four
milestones inside a `#if` body, which a single-symbol-set run cannot see at all.

Run over [Vixen](https://github.com/Rikarin/Vixen) — 4 711 files, 1 367 552 lines — in **14 s**.
Replacing Vixen's own `.editorconfig` with the export and formatting changes **2 527 files and
73 014 lines, 5.3 % of the tree** (M3: 2 717 files, 83 241 lines); a second pass is clean on all
4 711 files, and no file has a token stream that differs from the one it started with.

⚠ The number that decides whether to commit it is not that one. Measured over a 600-file sample of
the same tree, **the oracle itself would move 302 of them** and Skala would move 304 — the diff is
the configuration swap plus twenty years of drift, not Skala disagreeing. Skala against the oracle
over that sample is 99.4 % of lines and 87 % of files.

⚠ Part of that is a **configuration** result rather than a formatting one, and milestone 3 halved it.
`options.json`'s `default` used to be *the Rider export's value*, not ReSharper's built-in default,
so on a repository whose `.editorconfig` leaves a key unset — which is most repositories — Skala and
Rider fell back differently. The defaults are now derived from the oracle: a `jb cleanupcode` run
under a configuration carrying nothing but `root = true` is ReSharper-with-defaults by construction
(see [03](docs/plan/03-configuration-model.md) § "Deriving ReSharper's defaults"). Measured on 60
Vixen files under Vixen's *own* configuration, against the oracle under the same, Skala goes from
97.00 % to 97.84 % of lines and from 38.33 % to 51.67 % of files.

## Running it

```bash
skala format [paths…] [--check] [--diff] [--range a:b] [--staged] [--quiet] [--jobs n]
skala config explain|check|diff|distill|fix
skala daemon status|stop|run          # the per-repository format daemon
skala lsp                             # formatting, range formatting, diagnostics, code actions
skala hooks install [--apply]         # the pre-commit hook, unless a hook manager owns it
```

`--check` writes nothing and exits 1 when there is anything to do. `--staged` formats the git index
and writes back to both the worktree and the index; it refuses to run when a staged file also has
unstaged changes, unless you pass `--staged=worktree`.

The daemon is started lazily, exits after thirty minutes idle, and is only ever an optimisation:
`SKALA_NO_DAEMON=1` or `--no-daemon` produces byte-identical output, and a daemon that is absent,
stale or of another protocol version is a silent fallback rather than an error. A warm single-file
format is 60–70 ms against a 40 ms budget — essentially all of it the client's own process start,
which is what NativeAOT for the thin client is for and is not done.

## Building it

```bash
./build.sh Test          # everything
./build.sh Conformance   # the differential suite and the fidelity ratchet
./build.sh Fidelity      # the ranked divergence report — the work queue
./build.sh Oracle        # ⚠ regenerate the committed fixtures from `jb cleanupcode`
```

There are more, all developer-machine actions and none of them a test. `ask <dir>` runs the oracle
over a scratch directory so a question like "what does `wrap_if_long` do to a six-element array at
121 columns" gets an answer instead of a guess; `defaults` derives ReSharper's default table from it;
`margin` sweeps SK-DIV-0005's constant over eleven shapes, five depths and both values of
`wrap_before_eq`; `locate <set> <kind>` prints the divergent lines attributed to one construct, which
is what [R1](docs/plan/16-risks-and-open-questions.md) asks and the ranked report cannot answer;
`tree <dir> [n]` runs the oracle *and* Skala over an arbitrary repository and reports what each would
move; and `sample <tree> <n> <dest>` redraws a corpus sample reproducibly, by a hash of each file's
path rather than by a seeded sequence.

`Oracle` needs `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`. Nothing
else does: the day-to-day test run reads the committed `.expected.cs` fixtures, and regenerating
them is a reviewed commit of its own (ADR-011).

## The two promises

- **It never writes a file whose token stream differs from the input's.** Every write is verified;
  a mismatch is `SK9099`, writes nothing, and drops a reproduction under `.skala/crash/`. There is
  no flag that turns it off.
- **It never formats a file it could not parse.** `SK9010`, reported, left byte-identical.
