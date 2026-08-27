# `corpus/real/` — provenance

Realistic input, vendored so that the differential number is measured against code somebody
actually wrote rather than against fixtures written to pass. Three origins, all kept under their
own licence:

| Directory | Origin | Licence | Files |
|---|---|---|---:|
| `serilog/` | [serilog/serilog](https://github.com/serilog/serilog) `src/` | Apache-2.0 | 70 |
| `newtonsoft/` | [JamesNK/Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) `Src/` | MIT | 110 |
| `vixen/` | the author's Vixen engine at `c688f62a`, sampled across the whole tree | Apache-2.0 | 200 |

⚠ The three are there for different reasons and the fidelity number should be read per origin, not
only in aggregate. **Vixen is already formatted by Rider with this exact `.editorconfig`**, so its
fidelity measures "does Skala leave already-conforming code alone" — a real and important
property, and a flattering one. Serilog and Newtonsoft.Json are formatted to *their* houses' styles
(Allman braces, different blank-line habits, aligned trailing comments), so they measure the harder
thing: does Skala move unfamiliar code to where Rider would put it.

Files are 40–900 lines, none generated. `.expected.cs` beside each file is the committed
`jb cleanupcode` output ([12](../../../docs/plan/12-conformance-and-testing.md)); regenerating it is
`./build.sh Oracle`, never a test.

## ⚠ The Vixen sample was re-based at milestone 3.1, and the reason is provenance rather than content

167 of the 200 files under `vixen/` had been vendored from `.claude/worktrees/` — agent scratch
checkouts of the same repository — rather than from the mainline tree. The content was real code and
barely duplicated, so the numbers those files produced stood; what did not stand is that Vixen is
over half the fidelity weight and "which 200 files" had no answer that would survive the checkouts
being deleted.

The sample is now drawn from `git archive c688f62ab5b25b5edcb8d5622409eb6bec788d29`, by
`Testing/Rikarin.Skala.Testing/CorpusSample.cs` (`sample <tree> <count> <destination>`). A file is
chosen by `SHA-256("skala-corpus-20260826\n" + relative path)`, sorted ascending, first 200 —
**a hash of the path rather than a seeded sequence**, because a pseudo-random draw depends on the
order the file system enumerated in and on how many candidates were rejected before it, while a hash
depends on nothing but the path. Excluded: `.claude/`, `bin/`, `obj/`, `artifacts/`, `*.Artifacts/`,
`*.g.cs`, `*.generated.cs`, `*.Designer.cs`, `*AssemblyInfo.cs`, and anything outside 40–900 lines.
200 of 4 347 candidates, 51 140 lines: Core 127, Editor 28, Platform 17, Raven 12, Live 7,
Gameplay 4, Samples 3, Tools 2.

⚠ **The number moved, and it moved up.** Measured at the same commit of Skala, immediately before
and after the swap:

| `corpus/real/` | line | file |
|---|---:|---:|
| old sample (167 worktree files) | 99.22 % | 77.63 % |
| re-based sample | **99.30 %** | **78.42 %** |
| — of which `vixen/` alone, old | 99.44 % | 80.00 % |
| — of which `vixen/` alone, re-based | 99.56 % | 81.50 % |

That is not an improvement in the formatter and should not be read as one: it is a different 200
files. The mainline tree is more uniformly Rider-formatted than an agent's working copy, which is
exactly what `vixen/` is in the corpus to measure — "does Skala leave already-conforming code
alone" — so the re-based sample measures that property on code that really is conforming.
