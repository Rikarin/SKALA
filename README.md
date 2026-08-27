# Skala

One configuration, the same formatting and analysis everywhere: a C# formatter and static-analysis
tool that reads the `.editorconfig` Rider exports, so the IDE and the gate agree by construction
rather than by discipline. See [`docs/plan/`](docs/plan/README.md).

**Status: milestone 3 of nine.** The formatter does spaces, blank lines, braces, indentation, break
presence and position, and wrapping: it fills what `wrap_if_long` fills, chops what `chop_if_long`
chops, honours the `max_*_on_line` counters, and chooses *which* of a long line's several candidate
points to wrap at.

## Where it is

| | |
|---|---|
| **Line fidelity vs. `jb cleanupcode`** on `corpus/real/` (380 files, 76 660 lines) | **98.86 %** (M2: 97.47 %, M1: 94.28 %) |
| File fidelity, same corpus | 71.05 % (M2: 49.47 %) |
| … on `corpus/constructs/` (271 files) | 95.97 % line, 89.67 % file |
| … on `corpus/pathological/` (52 files) | 94.73 % line, 80.77 % file |
| … on `constructs/preservation/` under the four `keep_existing_*` combinations | 90.86 / 100 / 92.65 / 100 % line |
| Idempotency, token equivalence, parse stability, determinism, range consistency | 100 % of the corpus, every test run, in every configuration — and of all 4 708 Vixen files |
| **Tier A options** — implemented and pinned by an oracle fixture | **201 of 520** (M2: 172 of 483) |
| Defaults derived from the oracle rather than guessed | 123 keys `oracle-probe`; `config distill` drops 108 |
| Documented divergences (`SK-DIV-*`) | 8, each with a measurement |

Per origin, because the three measure different things:

| Origin | Line | File | What it measures |
|---|---:|---:|---|
| `vixen/` | 98.93 % | 68.50 % | Does Skala leave code that already conforms alone |
| `newtonsoft/` | 98.81 % | 75.45 % | Does Skala move Allman-braced, differently-spaced code to where Rider would put it |
| `serilog/` | 98.51 % | 71.43 % | Same, a second house style |

The remaining 874 lines are eight named disagreements, not an unknown. Split by cause: the 274 files
with neither a `#if` nor a raw literal are at 99.02 %, the 91 with a `#if` at 98.60 %
([SK-DIV-0004](docs/divergences.md) — Skala parses without a project, so a `#if DEBUG` body is
frozen), and the 15 with a raw literal at 97.81 %.

⚠ The number the project is judged on is not this one. [16 § R1](docs/plan/16-risks-and-open-questions.md)
asks a sharper question — *any construct occurring more than 50 times must be at 100 %* — and
`./build.sh Fidelity` answers it: **27 of 54**.

Run over [Vixen](https://github.com/Rikarin/Vixen) — 4 708 files, 1 374 580 lines — in **11.9 s**,
down from 34.2 s at milestone 2. Replacing Vixen's own `.editorconfig` with the export and formatting
changes 2 717 files and 83 241 lines, 6.1 % of the tree; the result is idempotent on all 4 708 files
and no file has a token stream that differs from the one it started with.

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

There are two more, both developer-machine actions and neither a test: `fidelity ask <dir>` runs the
oracle over a scratch directory so a question like "what does `wrap_if_long` do to a six-element
array at 121 columns" gets an answer instead of a guess, and `fidelity defaults` derives ReSharper's
default table from it.

`Oracle` needs `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`. Nothing
else does: the day-to-day test run reads the committed `.expected.cs` fixtures, and regenerating
them is a reviewed commit of its own (ADR-011).

## The two promises

- **It never writes a file whose token stream differs from the input's.** Every write is verified;
  a mismatch is `SK9099`, writes nothing, and drops a reproduction under `.skala/crash/`. There is
  no flag that turns it off.
- **It never formats a file it could not parse.** `SK9010`, reported, left byte-identical.
