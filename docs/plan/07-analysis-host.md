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

`auto` is a selector used by `verify` and `fix`, not a fourth produced load mode. `verify` uses the
single highest-priority workspace target (`.slnx`, then `.sln`, then `.csproj`) when discovery is
unambiguous and otherwise uses loose mode. Multiple candidates fail with an instruction to pass
`--project`; a target that is found and then fails to load never falls through to a green loose run.
`fix --include IDE1006` infers workspace directly, while ordinary safe fixes keep the loose fast path.

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
and CI keeps reporting it. Mechanism 3 in § "Suppression" above is that provider, and the same
provider is where the opt-in `resharper_*_highlighting` mapping lands
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
