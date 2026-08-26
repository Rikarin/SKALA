# Skala

One configuration, the same formatting and analysis everywhere: a C# formatter and static-analysis
tool that reads the `.editorconfig` Rider exports, so the IDE and the gate agree by construction
rather than by discipline. See [`docs/plan/`](docs/plan/README.md).

**Status: milestone 2 of nine.** The formatter does spaces, blank lines, braces, indentation, and
which gaps of a construct may hold a line break. It fits the constructs whose break points it knows —
a call that does not fit is chopped — but it does not yet choose between a long line's several
candidate wrap points, which is milestone 3.

## Where it is

| | |
|---|---|
| **Line fidelity vs. `jb cleanupcode`** on `corpus/real/` (380 files, 76 970 lines) | **97.47 %** (M1: 94.28 %) |
| File fidelity, same corpus | 49.47 % (M1: 36.84 %) |
| … on `corpus/constructs/` (260 files) | 93.05 % line, 86.15 % file — 93.64 % on M1's own 228 files, up from 93.20 % |
| … on `corpus/pathological/` (52 files) | 88.43 % line, 67.31 % file |
| … on `constructs/preservation/` under the four `keep_existing_*` combinations | 81.01 / 87.65 / 85.82 / 88.44 % line |
| Idempotency, token equivalence, parse stability, determinism, range consistency | 100 % of the corpus, every test run, in every configuration |
| **Tier A options** — implemented and pinned by an oracle fixture | **172 of 483** (M1: 130) |
| Documented divergences (`SK-DIV-*`) | 4 |

Per origin, because the three measure different things:

| Origin | Line | File | What it measures |
|---|---:|---:|---|
| `vixen/` | 98.01 % | 57.00 % | Does Skala leave code that already conforms alone |
| `newtonsoft/` | 95.90 % | 32.73 % | Does Skala move Allman-braced, differently-spaced code to where Rider would put it |
| `serilog/` | 97.24 % | 54.29 % | Same, a second house style |

Most of the remaining 2.5 % is choosing *which* of a long line's candidate points to wrap at, which
is milestone 3 ([SK-DIV-0002](docs/divergences.md)): 747 lines, 0.97 % of the corpus.

Run over [Vixen](https://github.com/Rikarin/Vixen) — 4 703 files, 1 371 995 lines — 2 374 files
change, 36 364 lines added and 24 254 removed, in 34 s. Milestone 1 on the same tree changed 999
files. No `SK9099`, no `SK9010`, no crash artefacts: every file has the token stream it started with.

⚠ Most of that growth is a **configuration** result, not a formatting one, and it is worth
understanding before adopting. Vixen's `.editorconfig` sets no `wrap_*`, `keep_*` or `place_*` key,
so Skala falls back to `options.json`'s `default` — which is *the Rider export's value*, not
ReSharper's built-in default (`defaultSource` is `template`/`unknown` for every entry; see
[03](docs/plan/03-configuration-model.md)). Rider falls back to its own. Told the six phase-2 keys
ReSharper actually defaults to, the same run changes 1 301 files and 13 127/11 860 lines — 45 % less.
On a repository that *has* the export in its `.editorconfig`, which is the case Skala is built for,
the corpus number above is the one that applies.

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
