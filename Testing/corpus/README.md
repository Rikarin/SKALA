# `Testing/corpus/`

The three sets from [docs/plan/02](../../docs/plan/02-repository-layout.md) § "The corpus", plus the
two files that make the measurement reproducible.

| Path | What it is |
|---|---|
| `constructs/` | One C# construct each, named after the option it pins. `blank-lines/` is deliberately over-populated (118 files) because that is where the bug density is. |
| `real/` | Vendored from three real trees. Provenance and licences in [`real/NOTICE.md`](real/NOTICE.md). |
| `pathological/` | The formatter's enemies: a 4 000-character line, thirty-deep nesting, a `#if` splitting a method signature, raw strings full of braces, mixed line endings, a BOM, tabs in a spaces file, a file that does not parse. |
| `pathological/open/` | ⚠ Minimised fuzz findings whose defect is **not fixed yet**, with [`register.md`](pathological/open/register.md). Excluded from `Corpus.Files()` — one of them makes `skala format` throw, and a file that throws takes down every path that formats the corpus rather than failing one assertion. `OpenDefectTests` asserts instead that each still fails the way its entry records, so a fix breaks that suite and is told where the file goes next. Byte-significant: an editor that trims on save destroys three of them. |
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
