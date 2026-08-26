# Skala

One configuration, the same formatting and analysis everywhere: a C# formatter and static-analysis
tool that reads the `.editorconfig` Rider exports, so the IDE and the gate agree by construction
rather than by discipline. See [`docs/plan/`](docs/plan/README.md).

**Status: milestone 1 of nine.** The formatter does spaces, blank lines, braces and indentation.
There is no wrapping yet, which means it never moves a line to fit a width — the fitting engine is
milestone 3.

## Where it is

| | |
|---|---|
| **Line fidelity vs. `jb cleanupcode`** on `corpus/real/` (380 files, 76 684 lines) | **94.28 %** |
| File fidelity, same corpus | 36.84 % |
| … on `corpus/constructs/` (228 files) | 93.20 % line, 85.53 % file |
| … on `corpus/pathological/` (52 files) | 84.93 % line, 63.46 % file |
| Idempotency, token equivalence, parse stability, determinism, range consistency | 100 % of the corpus, every test run |
| **Tier A options** — implemented and pinned by an oracle fixture | **130 of 483** |
| Documented divergences (`SK-DIV-*`) | 4 |

Per origin, because the three measure different things:

| Origin | Line | File | What it measures |
|---|---:|---:|---|
| `vixen/` | 96.25 % | 43.00 % | Does Skala leave code that already conforms alone |
| `newtonsoft/` | 90.32 % | 23.64 % | Does Skala move Allman-braced, differently-spaced code to where Rider would put it |
| `serilog/` | 89.89 % | 40.00 % | Same, a second house style |

Most of the remaining 5.7 % is wrapping, which milestone 1 does not do: three quarters of the
divergent lines are a line the oracle broke at 120 columns and Skala left whole
([SK-DIV-0002](docs/divergences.md)).

Run over [Vixen](https://github.com/Rikarin/Vixen) — 4 700 files, 1 353 090 lines — 1 000 files
change, 7 880 lines, **0.58 % of the tree**, in 37 s. No `SK9099`, no `SK9010`, no crash artefacts:
every file written has the token stream it started with.

## Running it

```bash
skala format [paths…] [--check] [--diff] [--range a:b] [--staged] [--quiet]
skala config explain|check|diff|distill|fix
```

`--check` writes nothing and exits 1 when there is anything to do. `--staged` formats the git index
and writes back to both the worktree and the index; it refuses to run when a staged file also has
unstaged changes, unless you pass `--staged=worktree`.

## Building it

```bash
./build.sh Test          # everything
./build.sh Conformance   # the differential suite and the fidelity ratchet
./build.sh Fidelity      # the ranked divergence report — the work queue
./build.sh Oracle        # ⚠ regenerate the committed fixtures from `jb cleanupcode`
```

`Oracle` needs `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`. Nothing
else does: the day-to-day test run reads the committed `.expected.cs` fixtures, and regenerating
them is a reviewed commit of its own (ADR-011).

## The two promises

- **It never writes a file whose token stream differs from the input's.** Every write is verified;
  a mismatch is `SK9099`, writes nothing, and drops a reproduction under `.skala/crash/`. There is
  no flag that turns it off.
- **It never formats a file it could not parse.** `SK9010`, reported, left byte-identical.
