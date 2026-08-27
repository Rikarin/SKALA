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
| `unformatted/` | ⚠ `corpus/real/` with its formatting **destroyed** first, one subtree per mode, each with the oracle's answer for the *degraded* file. It exists because `real/`'s inputs are already 91 % line-identical to their fixtures, so that differential mostly measures whether Skala leaves good code alone. Provenance and numbers in [`unformatted/NOTICE.md`](unformatted/NOTICE.md). |
| `<file>.expected.cs` | The committed `jb cleanupcode` output, with a header naming the ReSharper version and the config hash. ⚠ Regenerating is `./build.sh Oracle` — a reviewed commit, never a test. |
| `fidelity.json` | The ratchet. CI asserts fidelity does not fall below it; raising it is a commit. |
| `syntax-kinds.txt` | Every `SyntaxKind` the pinned Roslyn declares, with the layout the document builder gives it. A package bump that adds a kind fails `SyntaxKindInventoryTests`. |

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
