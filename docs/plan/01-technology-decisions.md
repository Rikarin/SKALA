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
| `Microsoft.CodeAnalysis.CSharp.CodeStyle` | 5.9.0 | Roslyn's supported IDE1006 naming analyzer and solution-wide rename fix. Loaded from an isolated payload so the rest of the IDE rule set does not become Skala's rule set. |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | 5.9.0 | `AdhocWorkspace`, `SyntaxAnnotation` plumbing, `Formatter` (used only for *validating* against Roslyn's whitespace behaviour, never for output — ADR-004), `Simplifier` for the arrangement pass. |
| `Microsoft.Bcl.AsyncInterfaces` | 10.0.1 | Private implementation dependency of the Roslyn 5.9 CodeStyle fixer assemblies. Their analyzer-only package does not declare it, so Skala copies the exact referenced assembly (`10.0.0.1`) into the isolated CodeStyle payload explicitly. |
| ⚠ `Microsoft.CodeAnalysis.Analyzers` | ~~3.3.4~~ | **Not referenced, and the pin is wrong.** Roslyn 5.9.0 requires a prerelease of it, which the 3.3.4 in this row predates; `Rikarin.Skala.Options.Generator`'s csproj records the decision not to reference it explicitly. The intent — "Skala's own rules are held to the same bar" — is unmet, not deferred. |
| `System.IO.Hashing` | 10.0.10 | XxHash128 for the incremental cache keys. Vixen already uses it; same choice, same reason — it is the fastest non-cryptographic hash in the BCL and the cache is not a security boundary. |
| `System.Collections.Immutable` | in-box | Roslyn's currency. Not a choice. |

### Project loading

| Package | Version | Why |
|---|---|---|
| `MSBuild.StructuredLogger` | 2.3.246 | Reads a `.binlog` and hands back every `Csc` invocation with its full command line. This is the primary project-loading path (ADR-007). |
| `Microsoft.Build.Locator` | 1.11.2 | Finds the SDK's MSBuild so the fallback design-time build can run in-process. Only used on the fallback path. |
| ⚠ `Microsoft.CodeAnalysis.Workspaces.MSBuild` | 5.9.0 | `MSBuildWorkspace` itself — the `--load=workspace` path. **Referenced by `Rikarin.Skala.Analysis` and missing from this register until M9**, which is the more serious direction of drift: an unreferenced row is clutter, an unregistered reference is a dependency nobody decided on. |
| `Microsoft.Build` / `.Framework` / `.Utilities.Core` / `.NET.StringTools` | 17.14.28, `ExcludeAssets=runtime` + `PrivateAssets=all` | ⚠ **Both** paths, not only the fallback: `MSBuild.StructuredLogger` deserialises a binlog into MSBuild's own event types, so `Microsoft.Build.Framework` must be loadable to *read* one. Never redistributed — MSBuildLocator requires the SDK copy to win, and `MSBL001` fails the build for shipping ours. The 17.11.x the register first pinned is a known high-severity advisory and `TreatWarningsAsErrors` refuses it. |

### CLI, output, hosting

| Package | Version | Why |
|---|---|---|
| `System.CommandLine` | 2.0.10 | The stable v2. Vixen already uses it; one argument parser across the author's tools. |
| ⚠ `Spectre.Console` | ~~0.57.2~~ | Human rendering only — tables, diffs, progress. Behind an interface so `--no-color`/CI paths never touch it, and so it can be dropped without touching the engine. ⚠ **Not referenced as of M5.** The terminal renderer is plain text; at this size the dependency was buying colour and nothing else, and `Rikarin.Skala.Rules` may not have it in its closure anyway (doc 02). It is added when there is a table worth the byte. |
| `Sarif.Sdk` (`Microsoft.CodeAnalysis.Sarif`) | 5.6.0 | SARIF 2.1.0 object model and validator. Writing SARIF by hand is how you produce SARIF that GitHub rejects. ⚠ 5.6.0 and not 4.x: the older line pins `Newtonsoft.Json` 9.0.1, which is a known high-severity advisory. |
| `ModelContextProtocol.Core` | 2.2.0 | The MCP server ([10](10-ai-agent-integration.md)). ⚠ The `.Core` package, not `ModelContextProtocol`: the latter is the DI/hosting layer and drags `Microsoft.Extensions.Hosting` into a tool that has one process, one transport and six tools. |
| ⚠ `Microsoft.Extensions.Logging.Abstractions` + `ZLogger` | ~~10.0.10 / 2.5.10~~ | **Neither is referenced.** Skala has no `ILogger` anywhere; its diagnostics are `SkalaDiagnostic` values that travel in the `RunReport` and are rendered from it (ADR-009), which is a better answer for a tool whose output *is* its report — a log line is a second channel nothing can gate on. The row stayed for four milestones describing a stack that was never wired. Registered again only if something needs a sink the report cannot be. |

### Test

| Package | Version | Why |
|---|---|---|
| `xunit.v3` | 3.2.2 | The author's test framework. |
| `xunit.runner.visualstudio` | 3.1.5 | |
| ⚠ `Verify.XunitV3` | ~~latest~~ | **Not referenced, and the need it names is met differently.** The approval corpus is `Testing/corpus/<file>.expected.cs` — committed oracle output, regenerated by `./build.sh Oracle` as a reviewed commit, never by a test. That *is* approval testing; it simply predates the package and does not need it. The row implied a mechanism that does not exist. |
| ⚠ `BenchmarkDotNet` | ~~0.15.8~~ | **Not referenced, and now not wanted.** This entry used to say the budgets in [13](13-performance.md) were enforced by `PerformanceBudgetTests` rather than by a microbenchmark harness. That test is deleted and the budgets are withdrawn — they served a format-on-save consumer that does not exist. Two claims in a row about enforcement, neither surviving contact with what the tool is for. |
| ⚠ `FsCheck` or hand-rolled generator | TBD | Still TBD. See [12](12-conformance-and-testing.md) § "Fuzzing" — the fuzzer is being built now, and this row is a genuine open choice rather than drift. |

⚠ **Four rows in this register described packages nothing referenced, and one reference was in no
row.** A dependency register that is not checked is a list of intentions; this one was
last reconciled by hand in M9. The cheap guard — assert every `PackageReference` in the tree has a
row and every row has a reference — is not written, and until it is, this table will drift again.
Note that `Directory.Build.targets` injects `xunit.v3`, `xunit.runner.visualstudio` and
`Microsoft.NET.Test.Sdk` into every `*.Tests` project, so such a check must read the targets file
and not only the `.csproj`s. `System.Security.Cryptography.Xml` and `NuGet.Packaging`
(`build/_build.csproj`) are also referenced and unregistered.

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
| **SonarAnalyzer.CSharp** | ⚠ **SONAR Source-Available License v1.0**, not LGPL-3.0-only as this register said until the parity map checked `LICENSE.txt` directly. The conclusion is unchanged and *strengthened*: that licence defines "Competing" as substituting for SonarQube's functionality, which is what [`README`](README.md)'s first paragraph says Skala does. Its **rule list** — ids, titles, types, tags — is public documentation and is used as a checklist ([17](17-inspection-parity.md)); its rule *descriptions* are never copied and `analyzers/src/**` is never read. ⚠ **That was asserted in code by `Testing/parity-analysis/fetch_sonar.py`, and that file is deleted with the rest of the parity analysis ([17](17-inspection-parity.md)) — so the constraint now rests on this paragraph alone.** It is a licensing constraint, not a measurement: nothing in the repository reads Sonar sources today, and nothing should start. Shipping it inside an Apache-2.0 tool is a licensing conversation the project does not need, and its rules are tuned for a server that owns the issue lifecycle. Skala hosts it happily if a *user* installs it; it is never bundled. (ADR-008) |
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

The alternative — a `skala.toml` with Skala's own names for the same concepts — was rejected on one
argument: the author configures formatting in Rider's settings UI and exports. Any format that is
not the export is a format that has to be *kept in sync* with the export, by hand, and the first
divergence silently reintroduces the exact problem the tool exists to remove.

⚠ **That argument was written with the word "forever" in it, and the word was wrong.** This ADR was
drafted by an AI and assumed the export is a permanent input contract, on the reasoning that the
author will always edit settings in Rider and re-export. The author's actual intent is the
opposite: **ReSharper is to be replaced by Skala** once Skala produces identical results. The
export is a **bootstrap with an end date**, not a standing contract.

The decision itself survives — reading the export unchanged is still right *while it exists*, and
for exactly the sync argument above. What changes is everything downstream of "forever":

- ⚠ **The oracle disappears at replacement** (ADR-011). `jb cleanupcode` is currently the
  *definition* of correct formatting. Afterwards, Skala's committed fixtures are — so the fixtures
  have to be sufficient to stand alone **before** the switch, not after. That turns doc 12's
  key-flip conformance sweep from an audit into a **precondition**.
- ⚠ **The unimplemented options get worse, not better.** Today a key Skala ignores is still honoured
  by Rider in the editor, so the cost is invisible; `skala config check` reports 243 such keys set
  on this repository's own export. After replacement nobody honours them and they silently do
  nothing for ever. Option coverage is therefore a **precondition for replacement**, not polish —
  [03](03-configuration-model.md) § "Option tiers" says the same.
- **`skala config distill` changes role.** Today it is optional tidying. At replacement it is the
  *migration step* that converts a ReSharper configuration into a Skala one — and stripping the
  keys Skala will never own becomes permanent and safe, because no re-export will put them back.

⚠ **Not imminent, and the documents must not imply that it is.** Replacement is conditional on
producing identical results. Skala is at **99.70 %** line fidelity and honours **205 of the 458
options** this repository's export sets. The end date is real; what has to be true first is written
above, and none of it is true yet.

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

### ADR-010 — The CLI is the contract

**Status:** decided.

`skala format`, `skala check`, `skala verify` are the API. No integration — LSP, MSBuild, MCP,
hooks — may have behaviour the CLI does not, and every one of them reaches the same code rather than
a copy of it.

⚠ **This ADR used to name a second thing: a background daemon holding parsed trees and compilations
warm, which is how [13](13-performance.md)'s warm-path budgets got met, with the rule that no
integration may *require* it — `SKALA_NO_DAEMON=1` had to produce the same answer.** The daemon is
deleted ([11](11-cli-and-integrations.md) § "The daemon, and why it is gone") and the budgets are
withdrawn. The rule survives its subject: it was really the general rule above, stated once about the
one integration that was tempted to break it.

### ADR-011 — ReSharper's CLI is the conformance oracle, in tests only

**Status:** decided.

`jb cleanupcode` produces the ground truth for "what would Rider do to this file with this
`.editorconfig`". The conformance harness ([12](12-conformance-and-testing.md)) runs it over the
corpus, stores the results as fixtures in the repository, and diffs Skala against them. It is a
developer-machine and nightly-CI dependency, never a runtime one, and the fixtures — not the tool —
are what the day-to-day test run reads.

This also gives an honest fidelity number to publish, per option, rather than a claim.

⚠ **The oracle has an end date, and this ADR was written as though it did not.** ReSharper is to be
replaced by Skala (ADR-001, as corrected), and on the day it is, `jb cleanupcode` stops being
available as the definition of correct formatting. What remains is the committed fixtures — which is
why they are stored rather than regenerated per run, and why regenerating them is a reviewed commit.

The consequence is a **precondition, not a follow-up**: the fixture set has to be sufficient to
define the formatter on its own *before* the switch, because afterwards there is nothing to widen it
from. A construct with no fixture is a construct whose behaviour becomes whatever the code happens
to do. [12](12-conformance-and-testing.md)'s key-flip sweep is what establishes sufficiency, and it
is therefore on the critical path to replacement rather than being an audit of it.

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
`skala_verify`, `skala_format`, `skala_check`, `skala_fix`, `skala_explain` and
`skala_config_explain` — **six**, as the dependency register above says and as
[10](10-ai-agent-integration.md) § "The MCP surface" lists. ⚠ This paragraph named five and omitted
`skala_verify` until M9, which is the one that matters most: it is the tool the server's own agent
instructions tell a model to call before claiming work is finished. The CLI's
`--format=json` remains a complete fallback for agents with no MCP, because an agent that can only
run shell commands is still the common case. [10](10-ai-agent-integration.md).

### ADR-015 — Skala formats Skala

**Status:** decided.

The repository's own `.editorconfig` is the Rider export, and the build fails if
`skala format --check` finds anything.

⚠ **The second sentence of this ADR used to claim a bootstrap that has never existed, and could
not.** It said: "Bootstrapping is done with the previous released version (`dotnet tool` local
manifest, pinned), never with the build's own output, so a formatting regression cannot hide
itself." There is no `.config/dotnet-tools.json` anywhere in the tree, no `dotnet tool restore` in
`build/Build.cs` or in any workflow, and — decisively — **no previous released version to pin to**:
the version is 1.0.0, there are no tags, and `release.yml` deliberately publishes nothing. `Lint`
runs the CLI this build just compiled, from its own `bin/`. The build formats itself with its own
output, which is precisely what the sentence said it never does.

**The decision, taken in M9 rather than restated:**

1. **Self-formatting stays, and is honestly described as a *consistency* check.** `./build.sh Lint`
   asserts the tree matches what this build's formatter produces. That catches an unformatted file
   and a merge that reintroduced one. It cannot catch a formatter regression, because a regressed
   formatter is self-consistent — it reformats the tree its new way and `--check` is satisfied.
2. ⚠ **The regression guard is the oracle corpus, and it always was.** `Testing/corpus/<file>.expected.cs`
   holds `jb cleanupcode`'s output, committed. A formatter regression changes the diff against those
   fixtures and `./build.sh Conformance` fails on the ratchet in `fidelity.json`. That is a
   *stronger* guard than bootstrapping, because the reference is external to Skala entirely rather
   than being an older Skala. ADR-015 was solving a problem ADR-011 already solved better, and
   nobody noticed because the mechanism it proposed was never built.
3. **The pinned bootstrap is adopted at the first published release, if it still earns its place.**
   It is not possible before then. ⚠ And by then ADR-011's oracle is on its way out (see above), so
   the honest question at that point is whether a pinned previous Skala should become the reference
   the oracle used to be — which is a decision for the release, not a sentence to leave standing in
   the meantime.

[r15406]: https://github.com/dotnet/roslyn/issues/15406
[r18282]: https://github.com/dotnet/roslyn/issues/18282
[r47158]: https://github.com/dotnet/roslyn/issues/47158
[f246]: https://github.com/dotnet/format/issues/246
