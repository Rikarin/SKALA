# 01 — Technology Decisions

## Platform baseline

| | |
|---|---|
| SDK | .NET 10, pinned in `global.json` to the same version Vixen pins (`10.0.301`, `rollForward: latestFeature`) |
| Language | C# 14, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable` |
| CLI target | `net10.0` |
| Analyzer target | `netstandard2.0` — analyzers load into the compiler and the IDE, both of which may be older than the tool |
| Runtime shape | Framework-dependent global tool; optionally NativeAOT for the pre-commit hook path ([13](13-performance.md) § "Startup") |
| OS | macOS, Linux, Windows — the author develops on macOS, CI runs Linux, no Windows-only API anywhere |
| License | Apache-2.0, matching Vixen |

## Dependency register

Every dependency is listed here with the reason it is present and the reason the obvious
alternative is not. Central package management (`Directory.Packages.props`) is on;
`CentralPackageVersionOverrideEnabled` is off, as in Vixen.

### Core — the engine

| Package | Version | Why |
|---|---|---|
| `Microsoft.CodeAnalysis.CSharp` | 5.9.0 | The C# parser, syntax tree, semantic model, `IOperation`, `ControlFlowGraph`, and `AnalyzerConfig`. Non-negotiable and irreplaceable. |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | 5.9.0 | `AdhocWorkspace`, `SyntaxAnnotation` plumbing, `Formatter` (used only for *validating* against Roslyn's whitespace behaviour, never for output — ADR-004), `Simplifier` for the arrangement pass. |
| `Microsoft.CodeAnalysis.Analyzers` | 3.3.4 | Analyzer-authoring analyzers. Skala's own rules are held to the same bar. |
| `System.IO.Hashing` | 10.0.10 | XxHash128 for the incremental cache keys. Vixen already uses it; same choice, same reason — it is the fastest non-cryptographic hash in the BCL and the cache is not a security boundary. |
| `System.Collections.Immutable` | in-box | Roslyn's currency. Not a choice. |

### Project loading

| Package | Version | Why |
|---|---|---|
| `MSBuild.StructuredLogger` | 2.3.246 | Reads a `.binlog` and hands back every `Csc` invocation with its full command line. This is the primary project-loading path (ADR-007). |
| `Microsoft.Build.Locator` | 1.11.2 | Finds the SDK's MSBuild so the fallback design-time build can run in-process. Only used on the fallback path. |
| `Microsoft.Build` / `.Framework` / `.Utilities.Core` | matched to SDK, `ExcludeAssets=runtime` | The fallback path only. Never redistributed — MSBuildLocator requires the SDK copy to win. |

### CLI, output, hosting

| Package | Version | Why |
|---|---|---|
| `System.CommandLine` | 2.0.10 | The stable v2. Vixen already uses it; one argument parser across the author's tools. |
| `Spectre.Console` | 0.57.2 | Human rendering only — tables, diffs, progress. Behind an interface so `--no-color`/CI paths never touch it, and so it can be dropped without touching the engine. |
| `Sarif.Sdk` (`Microsoft.CodeAnalysis.Sarif`) | 5.6.0 | SARIF 2.1.0 object model and validator. Writing SARIF by hand is how you produce SARIF that GitHub rejects. |
| `ModelContextProtocol` | 2.2.0 | The MCP server ([10](10-ai-agent-integration.md)). Official C# SDK. |
| `Microsoft.Extensions.Logging.Abstractions` + `ZLogger` | 10.0.10 / 2.5.10 | Same logging stack as Vixen. `ILogger` at the boundary, ZLogger as the sink, so Skala's own diagnostics are structured. |

### Test

| Package | Version | Why |
|---|---|---|
| `xunit.v3` | 3.2.2 | The author's test framework. |
| `xunit.runner.visualstudio` | 3.1.5 | |
| `Verify.XunitV3` | latest | Approval testing is the correct shape for a formatter: the expected output is a file, and reviewing a formatter change means reviewing a diff of thousands of snapshots. Hand-written `Assert.Equal` on formatted strings does not scale past a hundred cases. |
| `BenchmarkDotNet` | 0.15.8 | The performance budgets in [13](13-performance.md) are enforced, not aspirational. |
| `FsCheck` or hand-rolled generator | TBD | Property-based tests for idempotency and token-equivalence over generated trees. See [12](12-conformance-and-testing.md) § "Fuzzing". |

### Build

| Package | Version | Why |
|---|---|---|
| `Nuke.Common` | 10.1.0 | Same build system as Vixen — `./build.sh Lint`, `./build.sh Conformance`, `./build.sh Pack`. |

### Reference-only — read, not referenced

| What | Why it is not a dependency |
|---|---|
| **CSharpier** (MIT) | Structurally incompatible: it is a Prettier port that discards the author's line breaks, and the configuration says `keep_user_linebreaks = true`. Its *printer* is a model to learn from; its `DocPrinter` and its handling of `#region`/`#if` are worth reading closely. Forking it would mean deleting the half that makes it CSharpier. (ADR-002) |
| **`dotnet format`** | It is the reference for "what the Microsoft keys mean" and its `--fix-analyzers` design is the model for [06](06-arrangement-and-syntax-styles.md). It cannot wrap and it has no `resharper_*` support, which is the whole problem. |
| **ReSharper Command Line Tools** (`JetBrains.ReSharper.GlobalTools`) | Free to use, proprietary, ~400 MB, no incremental mode, no SARIF, JVM-hosted startup. It is the **conformance oracle** in Skala's test harness only, never a runtime dependency and never redistributed. (ADR-011) |
| **SonarAnalyzer.CSharp** | LGPL-3.0-only. Shipping it inside an Apache-2.0 tool is a licensing conversation the project does not need, and its rules are tuned for a server that owns the issue lifecycle. Skala hosts it happily if a *user* installs it; it is never bundled. (ADR-008) |
| **Roslynator / Meziantou.Analyzer / StyleCop** | MIT/Apache and excellent. Same decision as above for the same reason (bundling third-party rule sets makes their false positives Skala's false positives, and their rule IDs Skala's compatibility surface). Skala *hosts* them on request and maps their output into its report. Where a Roslynator rule and a Skala rule overlap, [08](08-rule-catalogue.md) says so explicitly. |
| **OmniSharp.Extensions.LanguageServer** | 0.19.9, last shipped 2023. The LSP surface Skala needs is four requests wide (`formatting`, `rangeFormatting`, `diagnostic`, `codeAction`). A ~600-line hand-written JSON-RPC loop over `System.Text.Json` has fewer moving parts than an unmaintained framework. Revisit only if the surface grows. |
| **AngleSharp / ExCSS** | Reference for [14](14-web-languages.md). Vixen already depends on ExCSS 4.3.2, which matters: if Skala needs a CSS parser, using the one the engine already parses `.vcss` with is worth more than using the best one. |
| **Tree-sitter** | Considered for the HTML/CSS front ends. Rejected for now: a native dependency in a global tool is a distribution problem, and error-tolerant hand-written parsers for two small languages are a week each. |

## Architecture Decision Records

### ADR-001 — `.editorconfig` is the only style configuration language, and Rider's dialect is the one Skala speaks

**Status:** decided.

Skala reads `.editorconfig`, understands `[section]` globbing exactly as the compiler does (by
reusing Roslyn's own `AnalyzerConfig.Parse` / `AnalyzerConfigSet`, which is public API), and treats
the `resharper_*` namespace as a first-class part of that file rather than as foreign junk to skip.

The alternative — a `skala.toml` with Skala's own names for the same 380 concepts — was rejected on
one argument: the author configures formatting in Rider's settings UI and exports. Any format that
is not the export is a format that has to be *kept in sync* with the export, by hand, forever, and
the first divergence silently reintroduces the exact problem the tool exists to remove.

`skala.jsonc` exists for what `.editorconfig` genuinely cannot express — scan roots, exclusions,
gate thresholds, baseline path, which analyzer packages to host — and a style key appearing there is
a `SK9003` error, not a convenience.

**Consequences.** Skala inherits `.editorconfig`'s weaknesses: no comments on the same line as a
value, no lists beyond comma-joined strings, no nesting, last-section-wins precedence that is easy
to get wrong. It also inherits its enormous strength: every editor already reads it.

### ADR-002 — ⚠ Preserve-and-repair, not print-from-scratch

**Status:** decided; this is the load-bearing decision.

A modern formatter (Prettier, CSharpier, `gofmt`, `rustfmt`) discards the input's line breaks and
prints the tree. The configuration this tool exists to serve says:

```ini
resharper_keep_user_linebreaks = true
resharper_keep_user_wrapping   = true
resharper_csharp_keep_existing_declaration_block_arrangement = false   # …and 14 more keep_existing_* keys
```

Those keys have no meaning in a print-from-scratch formatter — there is no "user wrapping" left to
keep. ReSharper's formatter instead walks the token stream and *decides each inter-token gap*:
required space, forbidden space, required newline, forbidden newline, or **free** — and a free gap
keeps whatever the author wrote unless the line does not fit.

Skala implements that model. The consequence that matters most is that the output diff on a
1.35 M-line tree is small: running Skala on Vixen must not rewrite every line, or nobody will run
it once, let alone in a hook.

The consequence that costs most is that the fitting algorithm is harder than Prettier's, because a
group can be in one of *three* states rather than two — flat, broken, or "as the author left it".
[04](04-formatting-engine.md) § "Three-state groups" is where that is paid for.

### ADR-003 — Roslyn is the only C# front end

**Status:** decided.

Vixen's ADR-009 chose hand-written recursive descent for its own languages, and that was right
there. It is wrong here: C# is not Skala's language, it is Microsoft's, it changes annually, and a
formatter that cannot parse the C# 14 `extension` block on the day it ships is a formatter that
corrupts files. Roslyn is also the only way to get a `SemanticModel`, which the arrangement pass and
every non-trivial rule need.

Skala parses with `CSharpSyntaxTree.ParseText` at `LanguageVersion.Preview` and treats any tree with
error diagnostics as **not formattable** — a file that does not parse is reported (`SK9010`) and
left byte-identical. This is the single most important safety property in the tool.

### ADR-004 — Wrapping is a fitting problem over a document IR; Roslyn's `Formatter` is never in the output path

**Status:** decided.

Roslyn's `Formatter` cannot enforce a column limit — [dotnet/roslyn#15406][r15406],
[#18282][r18282], [#47158][r47158] and [dotnet/format#246][f246] are the standing requests, open
since 2016, and the team's stated reason for declining is the "seemingly infinite tail of bugs and
giant set of additional preferences" that the feature drags in. That tail is precisely what
`resharper_csharp_max_line_length = 120` plus 47 `resharper_wrap_*` keys *is*.

So the output path is Skala's own: syntax tree → **document IR** (`Text`, `Space`, `Break`,
`Group`, `Indent`, `Align`, `Verbatim`) → fitting pass → text edits. Roslyn's `Formatter` stays in
the test harness as a cross-check for the Microsoft-key subset, and nowhere else.

The IR is deliberately Wadler-shaped, because the ReSharper wrap enums map onto Wadler/Prettier
group modes almost exactly:

| ReSharper value | Meaning | IR mode |
|---|---|---|
| `chop_always` | every item on its own line, always | `Group(Break)` — forced |
| `chop_if_long` | if it does not fit, *every* item breaks | `Group(Auto)` — classic Prettier group |
| `wrap_if_long` | fill: break only where the line runs out | `Fill` |
| *(keep_existing / keep_user_wrapping)* | as the author wrote it | `Group(Preserve)` — the third state, ADR-002 |

### ADR-005 — Output is a minimal text-edit list, and every write is verified

**Status:** decided.

The formatter produces `TextChange[]` against the original `SourceText`, not a new string. Three
reasons: the diff shown to a human and to an agent is exact; a range format (`--range`, LSP
`rangeFormatting`) is the same code path with a filter; and unchanged regions are provably
unchanged rather than accidentally identical.

Before any write, Skala compares the *significant token stream* — every token, its kind and its
text, with trivia reduced to "is there any" — of input and output. A mismatch is a bug, is fatal,
writes nothing, and dumps a reproduction file. This catches the entire class of formatter defects
that swallow a token, drop a comment, or move code across a `#if` boundary. Cost is one extra
parse per file, ~15 % of formatting time, and it is never disabled — not by a flag, not in CI.

### ADR-006 — Skala's own rules are ordinary Roslyn `DiagnosticAnalyzer`s

**Status:** decided.

Not a bespoke visitor framework. Consequences, all good: the rules run inside `csc` and inside
Rider unchanged when shipped as `Rikarin.Skala.Rules`; `TreatWarningsAsErrors` works on them; the
IDE shows the same squiggle CI shows; `dotnet_diagnostic.SK1042.severity` configures them for free;
and the analyzer-testing infrastructure the ecosystem already has applies.

Cost: analyzers must be `netstandard2.0`, must not use C# 14 features that need a newer compiler at
*their* build time (they can, the target is netstandard2.0 not an old language version), must be
concurrency-safe, and must not hold state across compilations. Accepted.

The CLI's analysis mode is then a *host* for analyzers — Skala's own plus any the user configures —
rather than a separate analysis engine. One rule implementation, three delivery mechanisms.

### ADR-007 — Compilations are reconstructed from compiler command lines, not from `MSBuildWorkspace`

**Status:** decided, with a fallback.

To run analyzers Skala needs a `Compilation`: sources, references, options, analyzer config. Three
ways to get one:

1. **`MSBuildWorkspace`.** Requires MSBuildLocator, loads MSBuild in-process, is sensitive to SDK
   drift, and fails opaquely on SDK-style projects with custom targets — which every repository
   here has (Vixen's `Directory.Build.props` is 300 lines with four profile blocks and analyzer
   `ProjectReference`s).
2. **Design-time build via the MSBuild API.** Same fragility, more code.
3. **⚠ Read the `Csc` invocations out of a binary log.** `dotnet build -bl:skala.binlog` (or
   `-t:Rebuild`), then `MSBuild.StructuredLogger` yields every `CscTask.CommandLineArguments`,
   which `CSharpCommandLineParser.Default.Parse` turns into exactly the arguments the compiler
   actually used — including generated sources, analyzer references, `AnalyzerConfigFiles`, and
   every conditional symbol. It is what the build did, not what a re-evaluation thinks the build
   would do.

(3) is primary. It costs one real build, which CI is doing anyway, and it is the only option that
is *definitionally* correct. (1) is the fallback for `skala check` invoked with no binlog available,
behind `--load=workspace`, and it warns that results may differ.

For repositories with no build at all — a loose folder of `.cs` files, or an agent's scratch
directory — there is a third mode, `--load=loose`: parse every file, reference the shared framework
assemblies, and run only the rules that declare themselves semantics-optional. Reported as such.

### ADR-008 — No third-party rules are bundled; all third-party rules can be hosted

**Status:** decided.

Bundling `SonarAnalyzer.CSharp` (LGPL-3.0-only), Roslynator or Meziantou.Analyzer would make Skala's
findings the union of four projects' opinions and Skala's false-positive budget the sum of four
projects' false positives — and in Sonar's case would put an LGPL obligation on an Apache-2.0 tool.

Instead: `skala.jsonc` lists analyzer packages to load, Skala restores them into a tool-local
package folder, loads their `analyzers/dotnet/cs/*.dll` into the analysis host, and reports their
diagnostics under their own IDs with their own severities from `.editorconfig`. The 253
`dotnet_diagnostic.*` keys already in the Rider export work unchanged.

The corollary is that Skala must be *worth using with nothing hosted*. [08](08-rule-catalogue.md)
is written against that bar.

### ADR-009 — SARIF 2.1.0 is the canonical report; everything else is a renderer

**Status:** decided.

One serialization, produced once, containing every diagnostic with its rule metadata, fixes
(`fixes[]` with `artifactChanges`), severities, and the run's configuration fingerprint. The
terminal output, the GitHub Actions annotations, the JUnit XML, the markdown summary and the MCP
tool response are all *renderers over the SARIF object model*, in one project, with no independent
formatting logic.

This is what makes "the agent and the human see the same finding" true by construction rather than
by discipline, and it is why GitHub code scanning and Rider's SARIF viewer work for free.

### ADR-010 — The CLI is the contract; the daemon is an implementation detail

**Status:** decided.

`skala format`, `skala check`, `skala verify` are the API. A background daemon that keeps parsed
trees and compilations warm is how the warm-path budgets in [13](13-performance.md) get met, but no
integration is ever allowed to require it: every command must work correctly, if slower, with
`SKALA_NO_DAEMON=1`. The daemon is started lazily, is per-repository, exits after idle timeout, and
its protocol is private and versioned by exact match — a mismatched daemon is killed, not
negotiated with.

### ADR-011 — ReSharper's CLI is the conformance oracle, in tests only

**Status:** decided.

`jb cleanupcode` produces the ground truth for "what would Rider do to this file with this
`.editorconfig`". The conformance harness ([12](12-conformance-and-testing.md)) runs it over the
corpus, stores the results as fixtures in the repository, and diffs Skala against them. It is a
developer-machine and nightly-CI dependency, never a runtime one, and the fixtures — not the tool —
are what the day-to-day test run reads.

This also gives an honest fidelity number to publish, per option, rather than a claim.

### ADR-012 — Rule IDs are permanent

**Status:** decided.

`SK1042` is allocated once. It may be improved, its severity may change, it may be deprecated and
stop firing — it is never reused for a different concept and its meaning never widens. Rule
metadata lives in a checked-in `rules.json` that a test asserts is append-only for IDs. The reason
is baselines: a baseline file is a set of (rule, file, hash) tuples, and a redefined rule silently
un-suppresses or wrongly suppresses findings across every repository that has one.

### ADR-013 — Languages are plugins behind one contract

**Status:** decided, unimplemented until [14](14-web-languages.md).

`ISkalaLanguage` — `CanFormat(path)`, `Parse`, `BuildDocument`, `Analyze` — with C# as the first
implementation and its own project. This is not speculative generality: `.vxml` and `.vcss` are
Vixen's dialects of HTML and CSS, Vixen already has parsers for them, and the *right* end state is
that Vixen supplies a `Vixen.Skala.Languages` plugin rather than that Skala grows a second-hand
implementation of someone else's file format. Defining the seam now costs one interface; retrofitting
it later costs the formatter's public surface.

### ADR-014 — The MCP server is the agent interface

**Status:** decided.

Agents in this ecosystem already talk to `vixen-mcp`. Skala ships `skala mcp`, exposing
`skala_format`, `skala_check`, `skala_explain`, `skala_fix` and `skala_config_explain`. The CLI's
`--format=json` remains a complete fallback for agents with no MCP, because an agent that can only
run shell commands is still the common case. [10](10-ai-agent-integration.md).

### ADR-015 — Skala formats Skala

**Status:** decided.

The repository's own `.editorconfig` is the Rider export, and the build fails if
`skala format --check` finds anything. Bootstrapping is done with the previous released version
(`dotnet tool` local manifest, pinned), never with the build's own output, so a formatting
regression cannot hide itself.

[r15406]: https://github.com/dotnet/roslyn/issues/15406
[r18282]: https://github.com/dotnet/roslyn/issues/18282
[r47158]: https://github.com/dotnet/roslyn/issues/47158
[f246]: https://github.com/dotnet/format/issues/246
