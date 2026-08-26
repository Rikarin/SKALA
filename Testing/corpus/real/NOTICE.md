# `corpus/real/` — provenance

Realistic input, vendored so that the differential number is measured against code somebody
actually wrote rather than against fixtures written to pass. Three origins, all kept under their
own licence:

| Directory | Origin | Licence | Files |
|---|---|---|---:|
| `serilog/` | [serilog/serilog](https://github.com/serilog/serilog) `src/` | Apache-2.0 | 70 |
| `newtonsoft/` | [JamesNK/Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) `Src/` | MIT | 110 |
| `vixen/` | the author's Vixen engine, sampled across `Core/`, `Editor/`, `Platform/`, `Raven/` | Apache-2.0 | 200 |

⚠ The three are there for different reasons and the fidelity number should be read per origin, not
only in aggregate. **Vixen is already formatted by Rider with this exact `.editorconfig`**, so its
fidelity measures "does Skala leave already-conforming code alone" — a real and important
property, and a flattering one. Serilog and Newtonsoft.Json are formatted to *their* houses' styles
(Allman braces, different blank-line habits, aligned trailing comments), so they measure the harder
thing: does Skala move unfamiliar code to where Rider would put it.

Files are 40–900 lines, none generated, sampled with a fixed seed (`20260826`) so the set is
reproducible. `.expected.cs` beside each file is the committed `jb cleanupcode` output
([12](../../../docs/plan/12-conformance-and-testing.md)); regenerating it is `./build.sh Oracle`,
never a test.
