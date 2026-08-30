# `Testing/corpus/`

The three sets from [docs/plan/02](../../docs/plan/02-repository-layout.md) § "The corpus", the
degraded fourth that [docs/plan/12](../../docs/plan/12-conformance-and-testing.md) § "The unformat
differential" adds, plus the two files that make the measurement reproducible.

| Path | What it is |
|---|---|
| `constructs/` | One C# construct each, named after the option it pins. `blank-lines/` is deliberately over-populated (118 files) because that is where the bug density is. |
| `real/` | Vendored from three real trees. Provenance and licences in [`real/NOTICE.md`](real/NOTICE.md). |
| `pathological/` | The formatter's enemies: a 4 000-character line, thirty-deep nesting, a `#if` splitting a method signature, raw strings full of braces, mixed line endings, a BOM, tabs in a spaces file, a file that does not parse. |
| `pathological/open/` | ⚠ Minimised fuzz findings whose defect is **not fixed yet**, with [`register.md`](pathological/open/register.md). Excluded from `Corpus.Files()` — one of them makes `skala format` throw, and a file that throws takes down every path that formats the corpus rather than failing one assertion. `OpenDefectTests` asserts instead that each still fails the way its entry records, so a fix breaks that suite and is told where the file goes next. Byte-significant: an editor that trims on save destroys three of them. |
| `sweep/` | ⚠ The key-flip sweep's per-configuration outputs, **frozen**, so that the guarantee they carry survives ReSharper's uninstallation. 682 distinct outputs over 850 configurations, one file per distinct output, indexed by [`sweep/manifest.json`](sweep/manifest.json). `FrozenSweepTests` replays it with no oracle; `./build.sh Freeze` writes it. See "What `sweep/` can and cannot answer" below. |
| `unformatted/` | ⚠ `corpus/real/` with its formatting **destroyed** first, one subtree per mode, each with the oracle's answer for the *degraded* file. It exists because `real/`'s inputs are already 91 % line-identical to their fixtures, so that differential mostly measures whether Skala leaves good code alone. Provenance and numbers in [`unformatted/NOTICE.md`](unformatted/NOTICE.md). |
| `<file>.expected.cs` | The committed `jb cleanupcode` output, with a header naming the ReSharper version and the config hash. ⚠ Regenerating is `./build.sh Oracle` — a reviewed commit, never a test. |
| `fidelity.json` | The ratchet. CI asserts fidelity does not fall below it; raising it is a commit. |
| `syntax-kinds.txt` | Every `SyntaxKind` the pinned Roslyn declares, with the layout the document builder gives it. A package bump that adds a kind fails `SyntaxKindInventoryTests`. |

## What `sweep/` can and cannot answer

Every other set here pins Skala at **the export's values** — one configuration, the one this
repository ships. `sweep/` pins it at **every other value the sweep asked about**: `indent_style =
tab`, `max_line_length = 1`, each enum member of each placement key. That was checked live against
`jb cleanupcode` and never committed, so it was the one guarantee that would have evaporated with
the oracle's uninstallation.

⚠ **It is a recording, not the oracle**, and the difference is worth stating before somebody
discovers it. It can answer exactly one question: *does Skala still produce, at a configuration the
sweep measured, what ReSharper produced there?* It cannot answer a question nobody asked ReSharper
while it was installed —

- a value outside `OptionDomain.Probes`. An int has no finite domain, so it is probed at three
  values: the frozen answers for `max_line_length` are 0, 1 and 120, and there is **no** answer for
  80. An int option's row here is weaker than a bool's or an enum's for exactly that reason.
- a fixture added after the freeze, or an existing fixture edited. The frozen bytes are the oracle's
  answer for the file as it stood; change the input and there is nothing to compare against.
- **two keys at once.** These rows hold one override each. `conformance-pairwise.json` measures
  pairs and is not frozen here; the `Overrides` list is a list so that it can be.
- the **135 options the sweep never asked about**, every one of them because the registry names no
  `oracle` fixture for it — and any option the registry gains after the freeze. `sweep plan` lists
  them. This is the largest hole by some distance: 375 of 510 options are frozen here.
- whether a divergence is *right*. A `divergent` row freezes the oracle's answer and points at the
  `docs/divergences.md` entry that argues it. The argument is the evidence; the bytes are only what
  it is about.

Those are permanently unanswerable once `jb` is gone — not by this corpus and not by any other. The
only defence is to re-run `./build.sh Sweep` and re-freeze **while ReSharper is still installed**,
which is why both are reviewed commits and neither is a test.

⚠ **Never re-freeze to make a red replay green.** The frozen bytes are ReSharper's answer and a
replay failure is a regression until somebody has shown otherwise. `./build.sh Freeze` refuses to
write a byte that does not hash to what `conformance-sweep.json` already recorded, so it cannot
launder a drifted formatter into the standard — but the judgement about *when* to run it is a
person's, exactly as it is for `./build.sh Oracle`.

## Reading the numbers

Line fidelity is *matched lines ÷ total lines* over a diff, not a positional comparison. The oracle
wraps and milestone 1 does not, so one wrapped call would otherwise desynchronise the rest of a file
and turn an honest number into a meaningless one.

`real/` should be read per origin as well as in aggregate: `vixen/` is already formatted by Rider
and measures "does Skala leave conforming code alone", while `serilog/` and `newtonsoft/` are
formatted to other houses' styles and measure the harder thing.

⚠ **Read every number beside its null hypothesis** — what a formatter that returns its input
unchanged scores on the same population. On `real/` that is 90.95 % of lines and 26.84 % of files,
which is the floor the 99.63 % headline sits on; on `unformatted/scramble` it is 30.73 % and on
`unformatted/collapse` 32.38 %. `./build.sh Unformat` prints it as the first row of every table.
