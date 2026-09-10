# 07 — Analysis Host

Everything between "here is a repository" and "here is a list of diagnostics". The rules themselves
are [08](08-rule-catalogue.md); this is the machinery that runs them, and the machinery is where the
performance budget and most of the operational failure modes live.

## Three load modes

```
skala check --load=binlog   (default)   a real build's compiler command lines
skala check --load=workspace            MSBuildLocator + MSBuildWorkspace
skala check --load=loose                parse files, reference the shared framework
```

`auto` is the default selector used by `arrange`, `verify` and `fix`, not a fourth produced load
mode. They use the single highest-priority workspace target (`.slnx`, then `.sln`, then `.csproj`)
when discovery is unambiguous and use loose mode only when no target exists. Multiple candidates
fail with an instruction to pass `--project`; a target that is found and then fails to load never
falls through to a green loose run. This lets ordinary `fix` runs apply semantic fixes such as
`SK4020` without an explicit `--load=workspace`. `fix --include IDE1006` always requires workspace.

### `binlog` — the default (ADR-007)

```
dotnet build -bl:artifacts/skala.binlog   →   BinaryLog.ReadBuild()
                                          →   every CscTask.CommandLineArguments
                                          →   CSharpCommandLineParser.Default.Parse(args, baseDir, sdkDir)
                                          →   sources, references, options, analyzers, editorconfigs
                                          →   CSharpCompilation.Create(...)
```

This is what the build actually compiled: generated sources included, conditional symbols correct,
analyzer references as configured, multi-targeting expressed as one `Csc` invocation per target
framework. No design-time build, no MSBuild evaluation, no SDK-version sensitivity beyond the one
that already produced the binlog.

Details that matter:

- **Reference resolution is from disk paths in the command line.** They exist, because the build just
  ran. `MetadataReference.CreateFromFile` with a process-wide cache keyed on `(path, mtime, size)` —
  a large solution references the same 300 assemblies from every project and re-reading them is the
  single biggest avoidable cost.
- **Generated sources.** ⚠ **This paragraph was wrong and M5 measured how wrong.** It said
  `EmitCompilerGeneratedFiles` (which Vixen sets) "puts them on disk and the command line references
  them". It does the first and not the second: that property makes `csc` *write* the generated files
  beside the build output, and the compiler still produces them in-process — nothing puts them on a
  source line. Loading the command line verbatim therefore gives a compilation missing every
  generated member. On Vixen that is **1 675 compiler errors**: 894 `CS0103`, 227 `CS8795` (a partial
  method with no implementation), 137 `CS9248` (a partial property with no implementation), and not
  one of them about the user's code.

  So the generators are **always** re-run, via `CSharpGeneratorDriver` over the analyzer references
  from the same command line — and with two things the first attempt forgot, each worth hundreds of
  errors on its own:
  - the command line's `AdditionalFiles`, without which a generator that reads a `.vsl` shader or a
    `.g4` grammar produces nothing;
  - the command line's `/analyzerconfig:` set, which is where `build_property.RootNamespace` and
    every other MSBuild property a generator reads actually lives.

  With all three, Vixen goes from 1 675 compiler errors to **20**, all from one incremental generator
  whose output depends on another's. ⚠ It is not only noise: every semantic rule reads a model built
  over that program, so without the generators a rule is silent for a reason nothing in the report
  names.

  ⚠ Either way, generated files are **analyzed but never reported on and never formatted** — a
  diagnostic the user cannot fix is noise. Exception: `SK7xxx` metrics count them separately, because
  a generator that emits 200 000 lines of pathological code is a fact worth having.

- ⚠ **Reading a binlog needs MSBuild on disk, which ADR-007 chose the binlog to avoid.**
  `MSBuild.StructuredLogger` deserialises the log into MSBuild's own event types, so
  `Microsoft.Build.Framework` has to be loadable at run time. Skala cannot ship its own copy —
  `MSBuildLocator` requires the SDK's to win, and `MSBL001` fails the build for shipping one — so the
  locator is registered before the first read, for both load modes. What ADR-007 actually buys is
  intact: nothing evaluates a project, runs a target, or asks MSBuild what the build *would* do.
- **Staleness.** The binlog records the source hashes the build saw. If a file on disk differs,
  Skala substitutes the current text into the compilation and notes it (`SK9020`, info). If a file
  the binlog names is gone, or a new file exists that the binlog does not name, that is a
  `SK9021` warning telling the user to rebuild — because a *new* file is invisible to the compilation
  and silently unanalyzed, which is the worst possible failure.
- **Age.** A binlog older than the newest source file, by mtime, is reported. `--require-fresh-binlog`
  makes it an error; CI sets it.
- ⚠ **Completeness, which is the one age cannot see.** A binlog from an *incremental* build contains
  only the projects MSBuild actually rebuilt. It is not stale — its mtime is seconds old — and every
  command reported success against it. Measured on Vixen, 4 717 source files:

  | binlog | files the binlog covers | `arrange --check` says |
  |---|---|---|
  | `dotnet build` after touching one file | **52 (1 %)** | 1 067 files would be arranged |
  | `dotnet build --no-incremental` | **4 717 (100 %)** | the whole tree, 3 389 findings |
  | `dotnet build`, cold | 4 642 (98 %) | the 2 % is one project the solution does not build |

  A gate that analyses a fiftieth of the tree and comes back green is worse than no gate, because it
  is believed. So the load now reports **coverage as a ratio** — "the binary log covers 52 of 4 717
  selected source file(s) (1 %)" — scoped to the paths the run selected, so `skala check Core/`
  against a binlog covering `Core/` is complete whatever else the repository holds.

  ⚠ **`--require-fresh-binlog` refuses below 90 % coverage**, and the floor is measured rather than
  chosen: a complete build sits at 98–100 % and an incremental one at 1 %, so anything in that gap
  separates them, and 90 leaves room for a repository with several projects outside its solution.
  Refusing on *any* gap would make the flag unsatisfiable on Vixen — the same "gate nobody can turn
  green" mistake that made doc 09's `formatting: clean` unusable. The per-file `SK9021` lines stay
  warnings; the ratio is the verdict.

  ⚠ **The denominator is a filesystem walk, and that is how this failed on Skala's own repository
  for eleven consecutive pushes.** A repository can hold `.cs` files that are deliberately in no
  compilation — Skala's tree holds **1 924** of them across `Testing/corpus/`,
  `Rules/Rikarin.Skala.Rules.Tests/fixtures/` and that project's `corpus/`, each declared as data by
  a `<Compile Remove>` in the project that owns it. Nothing outside MSBuild can see that
  declaration, so the walk counted them, the ratio read **294 of 2 220 — 13 %** against a binlog
  that had compiled everything there was to compile, and `check` exited 4 before an analyzer ran.
  The floor was not wrong and neither was the exit code; the denominator was.

  So the walk honours `skala.jsonc`'s `"exclude"` (doc 03 § "What lives in `skala.jsonc`"), which is
  where "where to look" was always specified. Every walk in the tool reads the same predicate —
  `SourceExclusions` — so `format`, `arrange`, `check` and `fix` agree about what the repository's
  source code is, and a repository declares it once rather than being named directory by directory
  on four command lines. The built-in exclusions (`obj`, `bin`, `.git`, `.claude`, `artifacts`,
  `.skala`) are not configurable and are matched against the path *below the root being walked*, so
  a sweep from above never descends into an agent worktree while a run *inside* one still works; a
  declared pattern is anchored to the repository root, so `skala check Testing/` honours
  `Testing/corpus/**` exactly as `skala check .` does.

⚠ **`--require-fresh-binlog` did not fail anything until M9.** It raised a diagnostic's severity, and
nothing downstream reads a load diagnostic's severity — the gate reads *findings*. So the flag CI
sets in order to refuse a bad load produced an error-coloured line and **exit 0**. A load the caller
told us to refuse is a load failure: **exit 4**, before a single analyzer runs.

⚠ **And `arrange` was throwing these diagnostics away entirely.** The CLI built the compilations and
kept `loaded.Units`, dropping `loaded.Diagnostics`, so neither `SK9020` nor `SK9021` ever reached the
command that exposed the defect. Its only signal was the "N files were in no loaded compilation"
line — correct, easy to read past, and not a sentence that says *a fiftieth of your tree*.

### `workspace` — the fallback

`MSBuildLocator.RegisterDefaults()` then `MSBuildWorkspace.Create()`. Present because "I have a
solution and no binlog" is a real situation, especially in an IDE-adjacent context. It is slower,
it is sensitive to custom targets, and its `WorkspaceDiagnostics` are surfaced verbatim rather than
swallowed — a partially-loaded workspace that silently analyzes half a solution is the thing to
avoid. Reported in the SARIF run properties so a result set can never be mistaken for a binlog run.

### The ladder's contract under fallback (#361)

`ProjectLoader.Load` runs the rungs in order — `binlog → workspace → loose` for the default, `workspace
→ loose` for `--load=workspace`, `loose` alone — and a rung can end three ways: it produced
compilations (stop), it ran and found nothing (`IsEmpty`: fall through with an info `SK9025`), or it
**failed** (`LoadedProject.Failed`: MSBuild could not be located, the named target would not open,
every project evaluated to a placeholder, the generators are not on disk). `Failed` is fatal for the
rung the caller named — exit 4, before any analyzer runs — and for a *middle* rung it is not: the
default `skala check` on a machine with no MSBuild has to reach loose rather than refuse to run.

⚠ **What that fallback was allowed to claim was never decided, and it was claiming a pass.** The failed
rung's diagnostics were kept in the report at the severity they failed with, and nothing downstream
read them. Measured on 2026-09-11 through the binary, over `One.cs` and a `Broken.csproj` whose `Sdk`
does not exist, with no binlog:

| command | mode that ran | what it printed | exit |
|---|---|---|---|
| `check --load=binlog --gate=local --format=agent` | loose | `INCOMPLETE  1 of 1 file was not checked — this is a Skala bug` over `SK9024 Broken.csproj`, then `SKIPPED 260 rule(s) did not run (loose load)`, then `One.cs`'s finding | **0** |
| `check --load=workspace` (same tree) | refused | `no compilation could be built`, the two `SK9024` lines | 4 |
| `verify --format=agent` (same tree; `auto` chose workspace) | refused | the same refusal | 4 |
| `check --load=binlog` over `One.cs` **with no `.csproj` at all** | loose | `One.cs`'s finding, no banner | 0 |
| `check --load=binlog` over two `.csproj` and no solution | loose | `INCOMPLETE  this run did not finish — this is a Skala bug` over `multiple '*.csproj' workspace targets were found; choose one with --project` | **0** |

Two verbs disagreed about one repository, the "1 file" was a project, the "Skala bug" was an
instruction to pass `--project`, and 260 rules had not run over a tree that has a project. The
decision between the two designs the issue offered:

1. *The fallback is fine and the report should say so* — downgrade the failed rung's errors to
   warnings beside the `SK9025`, report the run as complete-in-loose.
2. *A failed rung is a reliability failure even under fallback* — keep the severity, fail
   `Gate.EvaluateReliability` on error-severity `SK9024`/`SK9029` as #358 did for `SK9028`, and give
   the banner a sentence outside the `N of M files` arithmetic as #360 did for the baseline.

**(2), on evidence already in the tree.** The deciding question is what the loose rung delivered: it
delivered the syntactic rules and skipped every rule that `requiresSemantics`, and the report said so
in the `SKIPPED` line — so the run was strictly less than what a loaded project gives, over a tree
that *has* a project. That is the fourth row's zero and the first row's zero being the same zero
(CLAUDE.md: "a zero from a disabled check and a zero from clean code are the same zero"), and the
control row is what keeps the two apart: with **no** project the workspace rung is *empty*, not
*failed* — "there is no project here" is a fact about the repository and the documented reason to
choose loose (`LoadModel.cs`, the `Failed` remarks: "this .csproj could not be opened" is a fact about
the tool, and "no amount of falling through makes the answer they were supposed to produce appear").
This document has said since the ladder was designed that *"a target that is found and then fails to
load never falls through to a green loose run"* (§ "Three load modes", about `auto`); `verify` kept
that promise and `check` did not, because under the binlog ladder workspace is the middle rung.
Design (1) would have written the disagreement into the report format instead of removing it.

What (2) means in the code:

- **The ladder is unchanged.** `attempted.AddRange(loaded.Diagnostics)` stays *before* the `Failed`
  test on purpose, and the `SK9025` still says "could not run; falling back". The run still reaches
  loose and still reports every finding the syntactic rules produce, which is the whole reason the
  fallback exists.
- **`Gate.EvaluateReliability` fails on error-severity `SK9024`/`SK9029`** — the fourth unconditional
  condition beside partial (#309), crashed analyzer (#295) and unreadable gate input (#358), with the
  reason in the verdict so `skala report` re-renders it: *"a project or solution was found and could
  not be loaded, so the run fell back to loose and every rule that needs a compilation reported
  nothing; their zero means nothing"*. **Exit 1**, not 4, because the gate is the one place allowed to
  reach a verdict (ADR-009) and the syntactic half is worth delivering under an honest verdict.
  ⚠ Error severity only: the same `SK9024` at warning is MSBuild's relayed `workspace:` line (this
  repository prints three on every workspace load) or "no .slnx, .sln or .csproj was found", and
  `SK9029` at warning is the binlog rung's per-assembly line — states the repository is in, and
  failing on any of them would fail every workspace load of this repository.
- **The banner treats the project as #360 treats the baseline.** `IncompleteCause.LoadRung` is the
  sibling of `GateInput`; `Renderer.IsAboutAFile` is the one place the split is made, and both
  `BlockedFiles` and `Causes` exclude what it rejects. The sentence names the project and says where
  the rules went, because the `agent` surface prints no gate verdict: `INCOMPLETE  Broken.csproj
  could not be loaded, so the run fell back to loose and the rules that need a compilation did not
  run. Every file was checked by the rules that could run; the SKIPPED line names the rules that did
  not run.` The root-located ambiguity case reads `no project could be loaded (SK9024 below), …`.
- ⚠ **Only the fallen-through workspace rung can put these ids in front of the gate.** Error-severity
  `SK9024` is emitted at five sites in `WorkspaceLoader` and every one sets `Failed`; error-severity
  `SK9029` only from its `ReportMissingAnalyzerAssemblies`, which also sets `Failed`; the binlog rung
  emits `SK9029` at warning only. A first-rung failure returns at exit 4 above the render, so the gate
  clause is reached by exactly the shape in the first table row and nothing else.

⚠ **The `Scale` guard is gone, and this is the third time it was asked.** #356 kept `FileCount <
blocked` because `SK9028` at the baseline reached it; #360 made that a gate input and re-pinned the
branch on `SK9024` at a `.csproj`; #361 makes that a load rung and re-enumerated every `SK9xxx` id in
`rules.json` against the code — the per-file ids (`SK9010`, `SK9015`, `SK9095`–`SK9099`) sit at a path
the loader put into `FileCount` (reportable or unreadable, #356); `SK9023` and the arrange summary sit
at the root; `SK9024`/`SK9028`/`SK9029` are not about a file; `SK9020`/`SK9021` are refused at exit 4
before a renderer runs; the config ids never enter a `RunReport`. Nothing reaches the branch, so
`blocked <= FileCount` is an invariant of `BlockedFiles` and the branch is deleted rather than kept on
inertia — a guard prints a plausible sentence over a broken denominator, and `2 of 1` is the kind of
wrong that gets reported the day it appears. `VerifyCommand.PartialVerdict`'s `Math.Max(0, …)` clamp,
kept by #356 for the same reason, goes with it. The invariant is asserted by
`IncompleteBannerTests.EveryBlockingToolId_IsEitherACountedSourceFileOrOutsideTheFraction`: every
registered `SK9xxx` id must be on exactly one of three named lists, and a new tool id fails the test
until somebody says which — the decision #356, #360 and #361 each had to make after the fact.

⚠ **Refuted in passing:** `rules.json`'s `SK9024` rationale said "under `--load=auto` the ladder
continues to loose with `SK9025`". It does not: `auto` resolves to workspace whenever a target exists
and workspace is then the *first* rung, so `verify`, `arrange` and `fix` refuse at exit 4 (the third
table row). It is the default `--load=binlog` ladder that continues, and the registry now says so.

### `loose` — no project at all

Parse the files, add `MetadataReference`s for the running framework's reference assemblies, build one
`CSharpCompilation` per directory root. Most type resolution fails; that is expected. Rules declare
`RequiresSemantics` in their metadata, and in loose mode only the ones that do not are run — roughly
the syntactic modernization set and the formatting-adjacent rules.

This mode exists for one consumer: **an AI agent that has just written a file and wants to know
whether it is acceptable, before anything is wired into a project**. It is fast (no build), it is
honest (the SARIF says `loadMode: loose` and lists the rules that were skipped), and it is the
default for the MCP `skala_check` tool when no project is specified. [10](10-ai-agent-integration.md).

⚠ **Compiler diagnostics are dropped in this mode**, which is the one place the "compiler diagnostics
are part of the report" rule below does not apply. There is no project, so half the references are
missing and `CS0246` is the expected state rather than a finding; reporting the compiler's opinion
here would bury the rules the mode exists to run under a few hundred complaints about code that is
fine. Roslyn will not let an *error* be suppressed through `specificDiagnosticOptions`, so the filter
is in the host and is conditioned on the load mode.

⚠ **The rule set is thin here and the honesty is what makes it usable.** At M5 only `SK0001`,
`SK0002`, `SK0003`, `SK1005` and `SK1030` declare no need for semantics, so a loose run is those five
rules and a `SKIPPED` line naming the other four. That is a real limitation of the mode rather than a
gap in the rules: the alternative — running the semantic rules and letting them answer "no finding"
because a symbol did not resolve — makes a clean report mean two different things depending on
something invisible.

## Running analyzers

```csharp
var withAnalyzers = compilation.WithAnalyzers(
    analyzers,                       // Skala's + hosted third-party
    new CompilationWithAnalyzersOptions(
        options:            analyzerOptions,       // AnalyzerConfigOptionsProvider over the .editorconfig chain
        onAnalyzerException: RecordAndContinue,    // never abort the run for one bad analyzer
        concurrentAnalysis:  true,
        logAnalyzerExecutionTime: true,            // feeds `skala check --profile`
        reportSuppressedDiagnostics: true));       // baselines need to see what was suppressed
var diagnostics = await withAnalyzers.GetAllDiagnosticsAsync(ct);
```

Points of substance:

- **`reportSuppressedDiagnostics: true`.** Skala needs to distinguish "not found" from "found and
  suppressed by `#pragma`". A suppression audit (`skala check --show-suppressions`) is a SonarQube
  feature worth keeping. ⚠ This sentence used to end "`#pragma warning disable` with no
  justification comment is `SK7050`", in the present tense. **`SK7050` does not exist** — no
  analyzer, no `rules.json` entry, no allocation in `allocated-ids.txt`. It is allocated in
  [08](08-rule-catalogue.md), whose status table correctly records it as not started;
  this document promoted a plan to a fact. `--show-suppressions` itself is real and does what the
  first half says.
- **Analyzer exceptions never fail the run.** They are recorded as `SK9030` (warning) naming the
  analyzer and the rule, the analyzer is disabled for the remainder of the run, and everything else
  continues. A third-party analyzer that throws on one syntax shape must not be able to turn a CI
  gate red for unrelated reasons — or, worse, green by aborting early.
- **Compiler diagnostics are part of the report.** `compilation.GetDiagnostics()` gives CS-codes,
  which the export configures 253 severities for. `skala check` reports them alongside `SK` rules,
  so one command answers "does this build and is it clean". ⚠ With the caveat that Skala's compile is
  not the build's compile; it does not emit, so `CS` errors from emit-time (unsafe/interop layout)
  do not appear. Stated in the SARIF, stated in `--help`.
- **Multi-targeting.** One compilation per TFM produces near-duplicate diagnostics. They are merged
  on `(ruleId, file, line, column, message)`, with the TFM list carried as a property, so a finding
  that only occurs under one target is visibly a one-target finding.
- ⚠ **But the merge is a *union*, and for a framework-dependent rule the union is the wrong
  quantifier** (#343, #351). One set of source files compiled by several monikers means
  `GetTypeByMetadataName` and the language-version gate both answer **"some moniker can do this"**
  where a rewrite needs **"every moniker can"**. `SK1023` rewrote to `System.Threading.Lock` and the
  `netstandard2.1` leg stopped building after `skala fix --safe`. `MultiTargetLink.Apply` groups the
  units by `ProjectPath` at `ProjectLoader.Load`'s single funnel — ⚠ by `ProjectPath` and not `Name`,
  because the workspace decorates the name with the moniker and the binlog does not, so a name-keyed
  grouping would pass on one loader and silently fail on the other. Both loaders are now pinned; the
  binlog half is the one the self-gate and CI actually run.
- ⚠ **The two guards are split by whether the predicate is declarative, and this is the decision
  #351 took rather than the one it was briefed to take.** The brief was to route every offending rule
  through `FrameworkAvailability`, which would have meant ~44 near-identical per-rule edits.
  Measured instead: a `netstandard2.0;net10.0` project with **no** `<LangVersion>` evaluates
  `LangVersion` to **7.3** and **14.0** respectively (`dotnet msbuild -getProperty:LangVersion
  -p:TargetFramework=…`), and roughly forty `SK1xxx` rules gate on exactly that — so the
  language-version axis was both the larger half of the bug and *already declared* as
  `rules.json`'s `languageVersion`. `MultiTargetLanguageFloor` therefore settles it centrally, after
  the per-unit loop and so after the incremental cache (the sibling set is not in the cache
  fingerprint, so a guard applied inside an analyzer can be baked into a cached result). **A rule
  added tomorrow with a `languageVersion` is guarded the day it lands**, which no convention policed
  by a test achieves. `FrameworkAvailability` keeps the cases a rule alone can state — `SK1023`'s
  `Lock` shape checks, `SK1060`'s *accessibility* test against `System.Memory`'s internal
  `System.Index` shim — and `FrameworkAvailabilityReachTests` is the ledger that stops a new one
  landing unguarded, because #343 shipped the mechanism without one and a sweep a release later found
  `SK1023` was still its only consumer.
- ⚠ **Most `GetTypeByMetadataName` calls are not this bug, and the sweep mattered mainly for saying
  so.** A lookup that *recognises* a type the analysed source already references cannot differ
  usefully across monikers: if the source names it, every moniker resolves it or the project does not
  build. Of 337 rules, four needed the type-availability guard — `SK4031`, `SK2182`, `SK1060`,
  `SK1092`. All 23 `Security/*` lookups are recognition, and the issue's named candidates
  (`WorldWritableFileMode`, `RefStructOwnedDisposable`) were refuted. ⚠ The trap is the lookup that
  looks like recognition and is not: `SK2182` resolves a **string literal** from the source, so the
  literal compiles under every moniker while the type it names may exist under one.

### Loading third-party analyzers (ADR-008)

`skala.jsonc` lists packages. Skala restores them with `dotnet restore` into a tool-local folder
(`~/.skala/packages`), reads `analyzers/dotnet/cs/*.dll`, and loads them through
`AnalyzerFileReference` with a per-package `AssemblyLoadContext` so that two analyzers depending on
different versions of the same helper library do not collide — which they will, because half of them
bundle a Newtonsoft or a `System.Collections.Immutable`.

Failure to load is `SK9031` and is never fatal.

## Suppression

Four mechanisms, in precedence order, all honoured:

1. `#pragma warning disable SK1042` — file/span scoped. Roslyn handles it.
2. `[SuppressMessage("Skala", "SK1042:…", Justification = "…")]` — symbol scoped. Roslyn handles it.
   ⚠ `Justification` missing or `"<Pending>"` **would be** `SK7051`, which is allocated in
   [08](08-rule-catalogue.md) and **not built**. Nothing reports it today.
3. `dotnet_diagnostic.SK1042.severity = none` in a scoped `.editorconfig` section — the right way to
   turn a rule off for a folder, and the reason `[Testing/**]` sections exist.
4. **Baseline** — the SonarQube replacement, and the only mechanism that is *not* in the source.
   [09](09-quality-gates-and-reporting.md) § "Baselines".

Skala adds no fifth mechanism. In particular there is no `skala-disable-next-line` comment: C#
already has `#pragma`, and a second syntax means two things to grep for.

## The incremental cache

The budget is "warm analysis of changed files in under 5 s" on a 4 691-file tree
([13](13-performance.md)). That requires not re-running analyzers over unchanged files.

**Cache key**, per (file, compilation):

```
xxHash128(
    file content
  ⊕ effective .editorconfig options for that file      (already computed, hashed once per section)
  ⊕ rule set fingerprint: ids + severities + analyzer assembly MVIDs
  ⊕ compilation fingerprint: reference MVIDs + parse options + preprocessor symbols
  ⊕ Skala version
)  →  the diagnostics produced for that file
```

Stored in `.skala/cache/` as a single append-only file per compilation plus an index, mmap-read on
startup. Invalidation is by key mismatch only — no timestamps, no watchers, no partial states.

⚠ **The correctness condition is that a rule's output for a file depends only on the key's inputs.**
That is false for whole-compilation rules: a "this public member is never used" rule reads every
file. Rule metadata therefore carries a `Scope` — `Syntax`, `Semantic`, or `Compilation` — and
`Compilation`-scoped rules are excluded from per-file caching and re-run whenever *any* file in the
compilation changes. There are few of them and they are cheap; getting this wrong produces stale
findings, which is the failure mode that destroys trust in a cache.

⚠ **Three things M5 got wrong on the first attempt and that the tests now pin**, because each of them
is a stale finding that looks exactly like a real one:

1. **A file with no findings needs a cache entry too.** Without one, "clean" is indistinguishable
   from "not in the cache", every clean file is a miss forever, and on a tree that is mostly clean
   that is the whole cache.
2. **The `.editorconfig` fingerprint is over the raw text, not the resolved global options.** The
   resolved view is per source path — that is what scoped sections are *for* — so hashing the global
   view leaves every key unmoved when only a `[Testing/**]` section changed.
3. **The warm path has to run the semantic actions, not only the syntactic ones.** Running only
   `GetAnalyzerSyntaxDiagnosticsAsync` over the changed trees silently drops every semantic rule from
   a warm run, so a file produces different findings depending on whether the cache was cold — the
   cache lying, in the direction that looks like progress.

`AnalysisTests.Cache_ASecondRunAgreesWithAnUncachedOne` is the property that catches all three: a warm
run over a changed tree must produce byte-identical findings to a run with `--no-cache`.

⚠ **`dotnet_diagnostic.SK1010.severity` needs a `SyntaxTreeOptionsProvider` on the *compilation*.**
That is where Roslyn's driver reads rule severities from; `csc` sets one from its analyzer config and
a hand-built `CSharpCompilation` does not. Without it, a repository turns a rule off, the IDE agrees,
and CI keeps reporting it. Mechanism 3 in § "Suppression" above is that provider. ⚠ It used to carry
a second, opt-in axis reading `resharper_*_highlighting`; **that bridge has been removed** and
`dotnet_diagnostic.SK….severity` is now the only way to set a Skala rule's severity
([03](03-configuration-model.md) § "Severities").

Duplication detection ([09](09-quality-gates-and-reporting.md)) has its own index with its own
invalidation, because a clone is a property of a pair of files.

`skala cache clear`, `skala cache stats`, and `--no-cache` exist. Cache corruption is never a
failure: a bad read discards the cache and re-runs.

## Parallelism and determinism

Analyzers run concurrently inside Roslyn; compilations run concurrently with a degree of parallelism
of `min(cores, 8)` bounded by memory rather than CPU — each large compilation holds hundreds of MB of
metadata, and eight Vixen-sized compilations at once is where a 16 GB laptop starts swapping.

**Determinism is enforced after the fact, not during**: the merged diagnostic list is sorted by
`(path, line, column, ruleId, message)` before it reaches the reporter. Parallelism is never allowed
to be observable in output ([00](00-vision-and-principles.md) non-negotiable 8), and the conformance
suite asserts byte-identical SARIF across three runs with different thread counts.

## Cancellation and interactivity

Every stage takes a `CancellationToken`. Ctrl-C cancels and prints what was found so far, marked
partial. The LSP server cancels an in-flight analysis when the file it was analyzing changes again —
which, for an agent editing a file three times in ten seconds, is the difference between a responsive
tool and a queue.

## Metrics

Computed in the same pass, from the same trees, because a second traversal of 1.35 M lines to count
things is a second traversal:

| Metric | Definition | Rule |
|---|---|---|
| Cyclomatic complexity | Roslyn `ControlFlowGraph` basic blocks + conditional edges | `SK7001` |
| Cognitive complexity | Sonar's published definition — nesting-weighted, no penalty for a `switch` | `SK7002` |
| Method length | statements, not lines | `SK7003` |
| Type size | members, and fields separately | `SK7004` |
| Parameter count | including primary-constructor parameters | `SK7005` |
| Nesting depth | block depth | `SK7006` |
| Maintainability index | the classic Halstead-derived formula, reported not gated | metrics only |
| Comment density | doc-comment coverage of public API | `SK7010` |

All thresholds are `.editorconfig` options in Skala's own namespace
(`dotnet_code_quality.SK7002.threshold = 15`), which is the standard mechanism Roslyn analyzers
already use for configuration and therefore needs no invention.

⚠ **One analyzer reporting seven rules, and one walker computing seven numbers per member.** Seven
analyzers means seven visits of the same node; seven walkers means seven traversals of the same body.
`MemberMetrics.Compute` is the single visit and `MetricsAnalyzer` is the single analyzer.

⚠ **This broke loose mode, and the fix is worth recording.** `AnalyzerHost.Select` dropped an
analyzer if *any* of its supported descriptors needed semantics — and `SK7001` does, for the
control-flow graph. So one semantic metric silenced the other six under `--load=loose` while the
SARIF's skipped-rules list named only `SK7001`: a clean report meaning two different things, which is
exactly what the loose-mode honesty rule exists to prevent. The filter is now per *descriptor* — the
analyzer runs and the findings of rules that could not honestly answer are dropped — so what a loose
run reports is exactly what it says it reports.

⚠ **The aggregates are a second walk, and the design could not avoid it.** A `DiagnosticAnalyzer` can
report diagnostics and cannot publish anything else out of Roslyn's driver, so an aggregate computed
inside the analyzer has no way out. The alternatives were worse: a hidden diagnostic per member turns
1.35 M lines into a diagnostic per member, and computing the aggregate from a *different* walker is
how the gate and the findings come to disagree about the same method. So `MetricsPass` folds the same
`MemberMetrics` over the trees the loader already parsed — no re-parse, syntax only, and it uses the
syntactic cyclomatic count rather than building a control-flow graph per member, because a percentile
over 1.35 M lines does not move when one `foreach` scores 2 instead of 3. The *findings* still use
the graph, and `CyclomaticFromControlFlowGraph` is how a reader tells the two numbers apart.

⚠ Percentiles rather than means, and nearest-rank rather than interpolated. A mean is dominated by
the thousands of three-line members every codebase has and moves by 0.01 when something terrible is
added; and an interpolated percentile over integers produces a number that is not any member's actual
score, when the point of every one of these numbers is that it is traceable back to the member that
produced it.
