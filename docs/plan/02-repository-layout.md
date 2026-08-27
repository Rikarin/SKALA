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
│   └── Rikarin.Skala.Rules.Metadata/        # rules.json + the generator that reads it
├── Reporting/
│   ├── Rikarin.Skala.Reporting/             # SARIF model, renderers, baselines, gates
│   └── Rikarin.Skala.Reporting.Tests/
├── Tools/
│   ├── Rikarin.Skala.Cli/                   # dotnet tool `skala`
│   ├── Rikarin.Skala.Cli.Tests/
│   ├── Rikarin.Skala.Server/                # daemon + LSP        ← exists from M3
│   ├── Rikarin.Skala.Server.Tests/
│   ├── Rikarin.Skala.Mcp/
│   ├── Rikarin.Skala.Mcp.Tests/
│   └── Rikarin.Skala.MSBuild/               # targets + task, packaged
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

⚠ **`Rikarin.Skala.Formatting` knows nothing about C#.** The IR and the fitting algorithm are the
part that HTML and CSS reuse ([14](14-web-languages.md)); the moment `SyntaxKind` appears in that
project the language-plugin seam is gone. Also enforced by a reference test.

⚠ **Nothing references `Cli`.** Not tests (they reference a `CliRunner` in `Testing`), not `Server`.
A tool whose logic lives in its entry-point assembly cannot be embedded, and embedding is exactly
what MSBuild and MCP need.

## Package boundaries — what ships to NuGet

| Package | Kind | Contents | Consumer |
|---|---|---|---|
| `Rikarin.Skala.Cli` | .NET tool (`skala`) | Everything, self-contained-ish | `dotnet tool install -g` |
| `Rikarin.Skala.Rules` | Analyzer | `analyzers/dotnet/cs/*.dll`, `.editorconfig` defaults | `PackageReference` with `PrivateAssets=all` |
| `Rikarin.Skala.MSBuild` | Build | `build/*.targets`, the task, a tool reference | `PackageReference` in `Directory.Build.props` |
| `Rikarin.Skala.Sdk` | Meta | References the two above and drops a starter `.editorconfig` | One-line adoption in a new repo |

The API packages (`Core`, `Formatting*`, `Analysis`) are **not published** until something outside
this repository needs them. A published assembly is a compatibility promise, and the option model
is going to churn hard through Milestone 3.

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

- **Trunk-based.** `main` is releasable. Feature work is short-lived branches.
- **Every change to formatting behaviour updates the conformance fixtures in the same commit**, and
  the review reads the fixture diff. A formatter PR whose fixture diff is unexamined is a formatter
  PR that has not been reviewed.
- **`rules.json` changes are append-only for IDs** (ADR-012), asserted by a test.
- **Version scheme:** `0.x` until Milestone 4. After that, semver where a *formatting output change*
  is a minor bump at minimum and is listed in `CHANGELOG.md` with a corpus diff summary — because
  downstream, a formatting change means a repository-wide commit.
