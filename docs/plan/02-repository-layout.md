# 02 — Repository Layout

## Shape

Top-level folders by role, as in Vixen — not `src/`+`tests/`. Tests live beside the code they test
(Vixen ADR-014, same reasoning: a test project that is a sibling of its subject is found, and one
that is in a parallel tree is not).

```
Skala/
├── .editorconfig                     # the Rider export, unmodified — ADR-015
├── .config/dotnet-tools.json         # pinned previous Skala, for bootstrap
├── Directory.Build.props             # identity, language, analysis profiles
├── Directory.Build.targets           # analyzer wiring, packaging defaults
├── Directory.Packages.props          # central package versions
├── global.json                       # SDK 10.0.301
├── Skala.slnx
├── build/                            # NUKE
│   ├── Build.cs                      # Compile, Test, Conformance, Bench, Pack, Lint
│   └── _build.csproj
├── docs/
│   ├── plan/                         # this directory
│   ├── rules/                        # generated: one page per SK rule
│   └── options/                      # generated: the option matrix, per tier
├── Core/
│   ├── Rikarin.Skala.Core/
│   ├── Rikarin.Skala.Core.Tests/
│   ├── Rikarin.Skala.Options/
│   ├── Rikarin.Skala.Options.Generator/
│   └── Rikarin.Skala.Options.Tests/
├── Formatting/
│   ├── Rikarin.Skala.Formatting/            # language-agnostic IR + fitting
│   ├── Rikarin.Skala.Formatting.Tests/
│   ├── Rikarin.Skala.Formatting.CSharp/     # C# document builder + arrangement
│   ├── Rikarin.Skala.Formatting.CSharp.Tests/
│   ├── Rikarin.Skala.Formatting.Xml/        # xmldoc, .csproj/.props/.targets
│   └── Rikarin.Skala.Formatting.Xml.Tests/
├── Analysis/
│   ├── Rikarin.Skala.Analysis/              # loading, host, cache, metrics
│   ├── Rikarin.Skala.Analysis.Tests/
│   ├── Rikarin.Skala.Analysis.Duplication/
│   └── Rikarin.Skala.Analysis.Duplication.Tests/
├── Rules/
│   ├── Rikarin.Skala.Rules/                 # ← the shippable analyzer package
│   ├── Rikarin.Skala.Rules.Tests/
│   ├── Rikarin.Skala.Rules.Metadata/        # rules.json, allocated-ids.txt, the generated catalogue
│   └── Rikarin.Skala.Rules.Generator/       # ⚠ the generator is its own project, see below
├── Reporting/
│   ├── Rikarin.Skala.Reporting/             # SARIF model, renderers, baselines, gates
│   └── Rikarin.Skala.Reporting.Tests/
├── Distribution/
│   ├── Rikarin.Skala.Canonical/             # the canonical .editorconfig, packed and embedded
│   └── Rikarin.Skala.Sdk/                   # the meta package: three dependencies + starters
├── Tools/
│   ├── Rikarin.Skala.Cli/                   # `skala`, the tool and the `Rikarin.Skala.Cli` package
│   ├── Rikarin.Skala.Cli.Tests/
│   ├── Rikarin.Skala.Server/                # LSP + git hooks     ← exists from M3
│   ├── Rikarin.Skala.Server.Tests/
│   ├── Rikarin.Skala.Mcp/
│   ├── Rikarin.Skala.Mcp.Tests/
│   └── Rikarin.Skala.MSBuild/               # build/ and buildTransitive/ targets, packaged
├── Testing/
│   ├── Rikarin.Skala.Testing/               # harness: fixtures, oracles, generators
│   ├── Rikarin.Skala.Conformance.Tests/     # the Rider differential suite
│   └── corpus/                              # committed fixtures, see below
└── Benchmarks/
    └── Rikarin.Skala.Benchmarks/
```

## The project graph

The arrows that matter, and the ones that are forbidden.

```
Options.Generator ─(analyzer)─▶ Options ◀── Core
                                   ▲          ▲
                                   │          │
              Formatting ──────────┘          │
                   ▲                          │
                   │                          │
      Formatting.CSharp ──────────────────────┤
      Formatting.Xml ───────────────────────  │
                                              │
                       Rules ─────────────────┤   (netstandard2.0, Roslyn only)
                                              │
                    Analysis ─────────────────┤
        Analysis.Duplication ─────────────────┤
                   Reporting ────────────────-┘
                        ▲
      ┌─────────────────┼──────────────────┐
     Cli              Server              Mcp
      │                                    │
      └──────────▶ Formatting.CSharp ◀─────┘
      └──────────▶ Analysis          ◀─────┘
```

⚠ **`Rikarin.Skala.Rules` may not reference anything but Roslyn and `Rules.Metadata`.** It is loaded
into `csc` and into Rider. A transitive reference on `Spectre.Console`, on `System.CommandLine`, or
on anything that is not `netstandard2.0` makes the analyzer package fail to load with an error
message that names none of those things. This is enforced by a test that walks the package's
dependency closure, not by discipline.

⚠ **Where that test lives, since this document calls it a "reference test" and that is not a
mechanism name.** It is `ProjectGraphTests` in `Core/Rikarin.Skala.Core.Tests` — an ordinary xUnit
class, seven facts, run by `./build.sh Test` along with everything else. It is **not** a step in
`build/Build.cs`, and nothing outside the test run enforces the project graph. That is fine and is
the right place for it; the phrase is worth pinning because "reference test" reads like a build
target somebody could look for and not find. [14](14-web-languages.md) repeats the phrase.

⚠ **And it passes vacuously if its path filter is wrong, which it once was.** Every absolute path
inside an agent worktree — `<repo>/.claude/worktrees/<name>/` — contains a `.claude` segment, and
`IsScratch` excluded any path containing one. Matched against the absolute path, that excluded
*every* project, `LoadAll` returned nothing, and the assertions passed over an empty set. The fix
was to match against the path relative to the repository root, and a `TheProjectGraph_IsNotEmpty`
fact now fails when fewer than ten projects are found. It examines 31 in this worktree, which is
every project in the solution plus the build script. Same bug and same fix as
`ToolDiagnosticIdTests.SourceFiles` — an exclusion is only as good as the frame of reference it is
matched in, and a test that guards a rule needs a second test that it is guarding anything.

⚠ **`Rules.Metadata` is a third MSBuild profile, and it had to be.** It is `netstandard2.0` like the
analyzer profile — `Rikarin.Skala.Rules` references it and that assembly loads into `csc` and into
Rider — but it is an ordinary library rather than a Roslyn component, so it is packed and has no
analyzer wiring. And the *generator* is a separate project rather than living inside it, for the same
reason `Options.Generator` is separate from `Options`: a generator cannot generate into itself.
`Rules.Generator` links `Json.cs` from `Options.Generator` rather than copying it, because a source
generator may not reference another assembly that is not already in `csc`'s load context.

⚠ **`Reporting` does not reference `Analysis`; the arrow runs the other way.** `Analysis` produces a
`RunReport` and hands it to `Reporting`, which renders it. That direction is what makes
[09](09-quality-gates-and-reporting.md)'s "no renderer contains analysis logic" a compile-time fact
rather than a review comment.

⚠ **`Rikarin.Skala.Formatting` knows nothing about C#.** The IR and the fitting algorithm are the
part that HTML and CSS reuse ([14](14-web-languages.md)); the moment `SyntaxKind` appears in that
project the language-plugin seam is gone. Also enforced by a reference test.

⚠ **Nothing references `Cli`.** Not tests (they reference a `CliRunner` in `Testing`), not `Server`.
A tool whose logic lives in its entry-point assembly cannot be embedded, and embedding is exactly
what MSBuild and MCP need.

## What `netstandard2.0` costs a rule author

Rule authors keep rediscovering this list by hitting a compile error mid-rule, and the errors do not
say "wrong target framework". ⚠ **Everything below was compiled rather than remembered**, one
construct per compilation against a project replicating the analyzer profile exactly, built outside
this repository so that nothing above it could contribute. A list somebody guessed at would be worse
than none, because it would be trusted.

**Why it is `netstandard2.0`, so that nobody "fixes" it by retargeting.**
[01](01-technology-decisions.md) § ADR-006 makes Skala's rules ordinary Roslyn `DiagnosticAnalyzer`s
so they run inside `csc`, inside Rider and inside `skala check` from one implementation, and records
the price: *"analyzers must be `netstandard2.0`, must not use C# 14 features that need a newer
compiler at their build time (they can, the target is netstandard2.0 not an old language version),
must be concurrency-safe, and must not hold state across compilations."* The parenthesis is the part
that surprises people — `LangVersion` is `latest`, so the *language* is current and only the
*runtime surface* is old.

⚠ **The test project beside the analyzer is `net10.0`**, because `Directory.Build.props` assigns the
profile by project name and `.Tests` does not end in `.Rules`. A construct that compiles in
`Rikarin.Skala.Rules.Tests` therefore says nothing about whether it compiles in
`Rikarin.Skala.Rules`, which is why this is confusing rather than merely restrictive.

**Not available.** Each row is one compilation of one construct:

| What you write | What you get |
|---|---|
| `xs is [1]` — list patterns | `CS0518`, `System.Index` is not defined |
| `xs[^1]` — index from end | `CS0518` and `CS0656` |
| `xs[1..]` — range | `CS0518`, `System.Range` is not defined |
| `dictionary.TryAdd(k, v)` on a `Dictionary<,>` | `CS1061` |
| `foreach (var (k, v) in dictionary)` | `CS0411`, `CS8129`, `CS8130` — no `KeyValuePair` deconstruction |
| `s.Contains('c')`, `s.StartsWith('c')`, `s.Split(',', options)` | `CS1503` |
| `xs.ToHashSet()`, `d.EnsureCapacity(n)` | `CS1061` |
| `HashCode.Combine(…)` | `CS0103` |
| `ArgumentNullException.ThrowIfNull(o)` | `CS0117` |
| `Enum.GetValues<E>()` | `CS0308` |
| `required` members | `CS0656`, and the `IsExternalInit` shim does not help |

⚠ **`System.Index` exists and is `internal`, so "does the type exist" is the wrong question.** Naming
it directly is **`CS0122`** — inaccessible — not `CS0246`, because `System.Memory` ships an internal
shim for `netstandard2.0`. `Compilation.GetTypeByMetadataName` finds a symbol on a framework where
`x[^1]` does not compile, which is how `SK1060` came to report sixteen findings on Skala's own
projects whose fixes did not build. `IsSymbolAccessibleWithin` is the compiler's own test and is what
the analyzer asks now.

⚠ **`s.Contains('c')` starts compiling the moment `using System.Linq;` is in scope** — measured — and
it then binds `Enumerable.Contains<char>`, an O(n) walk of the string rather than `string.Contains`.
It is the one row here that fails quietly instead of loudly.

**Refuted — these are *not* constraints, and treating them as such costs real expressiveness.**
`record`, `record struct` and `init` accessors all work, because `Compat.cs` declares
`IsExternalInit` in this assembly; the analyzer source uses both. `ConcurrentDictionary.TryAdd`
works, and all six `.TryAdd` call sites under `Rules/Rikarin.Skala.Rules/` are on a
`ConcurrentDictionary` — so the constraint is `Dictionary.TryAdd` specifically, and the unqualified
form reads as a ban on a call the codebase makes constantly. `ValueTuple` deconstruction in a
`foreach` works, so "no deconstruction" would be wrong; only `KeyValuePair`'s is missing. Collection
expressions, file-scoped namespaces, raw string literals, target-typed `new()`, `is not` and
`string.Contains(string, StringComparison)` all compile.

⚠ **`if (false) { … }` is `CS0162` and that is not a `netstandard2.0` constraint at all.** It is
`TreatWarningsAsErrors`, which `Directory.Build.props` sets for every project except `_build` —
the `net10.0` ones included. It matters because the shipping bar requires sabotage testing and
short-circuiting a guard is the obvious way to perform one. Two spellings work: delete the guard, or
use a false the compiler cannot fold, such as `if (symbol.Name.Length < 0)`. ⚠ **A sabotage that does
not compile goes red with *zero* failing tests**, so the exit code is not the result and the failure
*count* is what has to be read. And deleting a `if (x is null)` guard leaves the later `x.Name` as
`CS8602` under `Nullable=enable`, so that sabotage needs a second edit — `x?.Name` — in the same
patch.

⚠ **Verify the instrument before trusting what it prints.** A first probe putting all forty
constructs in one file reported six errors, which read as "almost everything compiles". It was
measuring nothing: Roslyn skips method-body binding once the declaration phase has errors, so a
single `init` accessor near the bottom suppressed every body error above it. One construct, one
compilation.

## Package boundaries — what ships to NuGet

✅ **All five exist and are built by `./build.sh Pack`.** Sizes are the measured `.nupkg`, Release,
`osx-arm64`, at 1.0.0.

| Package | Kind | Contents | Size | Consumer |
|---|---|---|---|---|
| `Rikarin.Skala.Cli` | .NET tool (`skala`) | `tools/net10.0/any/`: the framework-dependent tool, `Runner="dotnet"`, RID-agnostic | **15.2 MB** | `dotnet tool install`, global or into a manifest |
| `Rikarin.Skala.Rules` | Analyzer | `analyzers/dotnet/cs/Rikarin.Skala.Rules.dll` + `…Rules.Metadata.dll` | 74 kB | `PackageReference` with `PrivateAssets=all` |
| `Rikarin.Skala.MSBuild` | Build | `build/` + `buildTransitive/` props and targets | 9.8 kB | `PackageReference` in `Directory.Build.props` |
| `Rikarin.Skala.Canonical` | Content | `content/canonical.editorconfig`, its manifest, and a check-only target | 43 kB | `PackageReference`; installed by `skala config sync` ([03](03-configuration-model.md) § "Canonical distribution") |
| `Rikarin.Skala.Sdk` | Meta | Dependencies on the three above, plus starter `.editorconfig` and `skala.jsonc` | 5.2 kB | One line of adoption in a new repo |

The API packages (`Core`, `Formatting*`, `Analysis`) are **not published** until something outside
this repository needs them. A published assembly is a compatibility promise, and the option model
is going to churn hard through Milestone 3.

⚠ **`Rikarin.Skala.Rules.Metadata` is not a package, and the analyzer package used to say it was.**
`Rules.csproj` has a `ProjectReference` to it, which NuGet turns into a `.nuspec` dependency on an id
nobody publishes. Nothing in this repository could see it — the first `dotnet build` of the first
repository that referenced the package got `NU1101: Unable to find package
Rikarin.Skala.Rules.Metadata`, and the analyzer package had been unrestorable by anybody since the
day it was written. `./build.sh Pack` sets `SuppressDependenciesWhenPacking` for it. The dependency
was redundant as well as fatal: the assembly is already packed into `analyzers/dotnet/cs` beside the
analyzer's own, which is where Roslyn looks for it.

⚠ **`Rikarin.Skala.Sdk`'s three dependencies carry `PrivateAssets="none"`, and that is load-bearing.**
NuGet's default private set is `contentfiles;analyzers;build` — exactly what these three packages
consist of — so with the default a consumer restores three dependencies and receives nothing from any
of them: no `SK` diagnostics, no build target, no canonical check. What to look for in the `.nuspec`
is `include="All"` on each dependency. `ReferenceOutputAssembly="false"` looks correct on references
that exist only to become dependencies, and is worse than the default: it drops them from the
`.nuspec` entirely and the dependency group comes out empty.

⚠ **The MSBuild package has no task assembly**, though the row above once said "the task". A task
would have to load into MSBuild's own process on three hosts — `dotnet build`, MSBuild.exe under
Visual Studio, and Rider's build host — which means `netstandard2.0`, a pinned
`Microsoft.Build.Utilities.Core`, and a load failure that breaks every project in a repository at
once. Everything it would do is start `skala` and read an exit code, which `Exec` already does, and
[11](11-cli-and-integrations.md)'s first line says the CLI is the contract and nothing may have
behaviour it does not.

⚠ **`Rikarin.Skala.Cli` was produced by `Tools/Rikarin.Skala.Client` and is produced by
`Tools/Rikarin.Skala.Cli` again.** A .NET tool's command *is* its entry point; from M7 the thing that
had to be on the hook path was the NativeAOT thin client, so the package was built by the project
that built that binary, was RID-specific (`tools/any/<rid>/`, `Runner="executable"` — new in .NET 10),
carried the full `skala-tool` beside the command as payload, and a multi-RID pack emitted a
RID-agnostic wrapper package naming per-RID package ids. Publishing that wrapper without every
package it names is an install that fails on whichever platform is missing, so `./build.sh Pack`
defaulted to the host RID and took `--rids` for a matrix.

All of that served an 8.65 ms warm single-file format, for a format-on-save consumer that does not
exist. With the daemon and the client deleted the package is an ordinary portable `PackAsTool` again:
one .nupkg, `Runner="dotnet"`, every platform, no `--rids`.

⚠ **`Rikarin.Skala.MSBuild`, `Rikarin.Skala.Sdk` and `Rikarin.Skala.Canonical` target
`netstandard2.0`.** None of the three has an assembly in its package; the framework is a
compatibility declaration, and it is the dependency group's framework that a consumer's restore
matches against. A `net10.0` group would refuse all three to a `net8.0` project, and every project in
a repository has one `.editorconfig` including the old ones.

## Naming

- Assemblies and namespaces: `Rikarin.Skala.<Area>`, matching the folder.
- The tool is `skala`, lowercase, one word, everywhere a user types it.
- Rule IDs: `SK` + four digits, ranges in [08](08-rule-catalogue.md). Never `SKALA0001`.
- Option keys: the `.editorconfig` spelling, verbatim, as the dictionary key; the generated C#
  property is `PascalCase` of the key with the `resharper_`/`csharp_`/`dotnet_` prefix retained as a
  group, e.g. `resharper_csharp_wrap_arguments_style` → `Options.ReSharper.CSharp.WrapArgumentsStyle`.
  ⚠ The mapping is generated and reversible; a hand-written alias is a `SK9004` build error in the
  generator, because a second name for an option is a second thing to keep in sync.
- Test names: `Method_Condition_Expectation`, xUnit v3, one `[Theory]` per option value where the
  option is an enum — the enum's arity *is* the test matrix.

## The corpus

`Testing/corpus/` is checked in, and it is three things:

| Set | Size | Purpose |
|---|---|---|
| `constructs/` | ~1 200 small files, one C# construct each | Every option × every value, hand-written, the unit level |
| `real/` | ~400 files vendored from real trees with permissive licences, plus a snapshot of ~200 Vixen files | Realistic input: long generic signatures, LINQ chains, `#if`, regions, big initializers |
| `pathological/` | ~80 files | The formatter's enemies: 4 000-character lines, 30-deep nesting, `#if` splitting a method signature, raw string literals containing `}}`, `#pragma` between attributes, mixed line endings, BOM + no BOM, tabs in a spaces file |

Oracle output from `jb cleanupcode` is committed beside each file as `<name>.expected.cs` with a
header recording the ReSharper version and the config hash that produced it. Regenerating is
`./build.sh Oracle`, a deliberate, reviewed action — never automatic, because an oracle that
regenerates on failure is not an oracle.

## Shared MSBuild

Two profiles, following Vixen's pattern of conditioning on project name:

**Analyzer profile** — `*.Rules`, `*.Generator`:
`TargetFramework=netstandard2.0`, `EnforceExtendedAnalyzerRules=true`, `IsRoslynComponent=true`,
no `PackageReference` that is not `PrivateAssets=all`, `IncludeBuildOutput=false`.

**Tool profile** — everything else: `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`,
`AnalysisLevel=latest-recommended`, `InvariantGlobalization=true`, `Deterministic=true`,
`ContinuousIntegrationBuild` under CI.

`TreatWarningsAsErrors=true` is inherited from Vixen's non-negotiables and applies here with extra
force: a static-analysis tool that ships with warnings has an argument to lose.

## Repository policy

- **Trunk-based.** `master` is releasable. Feature work is short-lived branches.
- **Every change to formatting behaviour updates the conformance fixtures in the same commit**, and
  the review reads the fixture diff. A formatter PR whose fixture diff is unexamined is a formatter
  PR that has not been reviewed.
- **`rules.json` changes are append-only for IDs** (ADR-012), asserted by a test.
- **Version scheme:** `0.x` until Milestone 4. After that, semver where a *formatting output change*
  is a minor bump at minimum and is listed in `CHANGELOG.md` with a corpus diff summary — because
  downstream, a formatting change means a repository-wide commit.

  ⚠ **[18](18-versioning-and-release.md) supersedes this bullet and the two notes below it**, and
  keeps the rule they state. What changed at M10 is who decides that a formatting output change
  happened: the release job builds the previous release's tool beside this one's and **measures** it
  over the corpus, along with four other compatibility surfaces. Nothing reads a commit message.
  ⚠ Run against the tree that declared 1.0, that measurement reports that **two of the four surfaces
  1.0 froze had moved within 125 commits** — the `format` check's exit code inverted from 1 to 2, and
  `dotnet_style_require_accessibility_modifiers` changed type. The line is `2.0.0` and the first
  published artefact is `2.0.0-alpha.N`; doc 18 § "Why the first published artefact is a pre-release"
  has the argument, including why `0.x` was the wrong instrument for it.

  ⚠ **1.0 was declared at M7, not at M4**, and the sentence above is what it superseded. M4 remains
  unfinished (`arrange` is partial); what made 1.0 the right number was ADR-012 freezing four
  surfaces — rule ids, option behaviour, exit codes and the SARIF shape — and those became fixed when
  the baselines that depend on them did, which was M6. `CHANGELOG.md` records each milestone at the
  number it reached rather than the one it aimed at, and M3's `99.9 %` and M3.1's `99.9 %` are the
  two that did not.

  ⚠ **Two version numbers, deliberately.** `VersionPrefix` in `Directory.Build.props` is the version
  of all five packages. The canonical *payload* has its own, stamped into `canonical.json` by
  `./build.sh Canonical --canonical-version` and currently `0.1.0`, because a canonical bump is a
  repository-wide reformatting commit and a tool bump is not; a repository must be able to take a
  bug fix without taking the reformat. The package that carries the payload rides the tool's number,
  so `Rikarin.Skala.Canonical 1.0.0` carries canonical `0.1.0` — the marker in a repository's own
  `.editorconfig` names the payload version and its SHA-256, which is the identity that decides
  whether anything needs to change.
