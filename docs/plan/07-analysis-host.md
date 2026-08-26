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
- **Generated sources.** `EmitCompilerGeneratedFiles` (which Vixen sets) puts them on disk and the
  command line references them; without it they are absent and Skala re-runs the generators via
  `CSharpGeneratorDriver` using the analyzer references from the same command line. ⚠ Either way,
  generated files are **analyzed but never reported on and never formatted** — a diagnostic the user
  cannot fix is noise. Exception: `SK7xxx` metrics count them separately, because a generator that
  emits 200 000 lines of pathological code is a fact worth having.
- **Staleness.** The binlog records the source hashes the build saw. If a file on disk differs,
  Skala substitutes the current text into the compilation and notes it (`SK9020`, info). If a file
  the binlog names is gone, or a new file exists that the binlog does not name, that is a
  `SK9021` warning telling the user to rebuild — because a *new* file is invisible to the compilation
  and silently unanalyzed, which is the worst possible failure.
- **Age.** A binlog older than the newest source file, by mtime, is reported. `--require-fresh-binlog`
  makes it an error; CI sets it.

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
  feature worth keeping: `#pragma warning disable` with no justification comment is `SK7050`.
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
   ⚠ `Justification` missing or `"<Pending>"` ⇒ `SK7051`.
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
partial. The daemon cancels an in-flight analysis when the file it was analyzing changes again —
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
