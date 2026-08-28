# 13 — Performance

Skala runs in a pre-commit hook and in CI, ahead of test suites that take about twenty minutes.
⚠ **This document used to open by naming a third consumer — a post-edit agent hook, firing after
every file write and needing to be invisible — and everything below was designed to serve it.** That
consumer does not exist, and the machinery built for it has been deleted. Read § "Budgets" first.

## Budgets

> ## ⚠ The budget table is **withdrawn**
>
> Every row below and every "✅ met" beside it was written for a workflow Skala does not have: an
> agent hook firing after every file write, and a format-on-save that had to be invisible. There is
> no such consumer. Skala runs ahead of test suites that take about twenty minutes, and a formatter
> that takes two seconds instead of forty milliseconds is not noticed by anything.
>
> So the daemon, the NativeAOT thin client and the protocol between them are deleted, and with them
> `PerformanceBudgetTests` and its CI job. **Nothing measures these numbers any more.** They are
> kept, below, as a record of what was measured and when — not as a claim about the tool today, and
> not as a gate anything can fail.
>
> What survives as a real constraint is coarse and is not asserted by a test: `format --check` over
> a large tree should stay in the seconds, and `check` in the minutes. Both are measured by running
> them ([README](../../README.md) § "Where it is").


Reference machine: Apple M-series, 10 cores, 32 GB. Reference corpus: Vixen — 4 708 C# files,
1 374 580 lines, ~48 MB of source.

| Operation | Cold | Warm (daemon) | Notes |
|---|---|---|---|
| `format` one 500-line file | 250 ms | **< 40 ms** | the agent hook; includes process start |
| `format --check` whole corpus | **< 20 s** | < 3 s | 10 threads |
| `format` whole corpus (writing) | < 25 s | — | one-off, adoption step 4 |
| `verify` on a 5-file change, `loose` | 900 ms | < 300 ms | the agent Stop hook |
| `check` whole corpus, cold, binlog present | **< 4 min** | — | analyzers dominate |
| `check` on a 5-file change | 40 s | **< 5 s** | cache hit on 4 686 files |
| `arrange --check` whole corpus | < 6 min | — | needs a re-bind per document |
| Duplication index, whole corpus | < 30 s | < 2 s | incremental by file hash |
| ~~Daemon RSS, idle after a corpus run~~ | ~~< 1.5 GB~~ | — | withdrawn — there is no daemon |

⚠ Each of these **used to be** a test ([12](12-conformance-and-testing.md) § "Performance tests")
with a 20 % band. That suite is deleted. The `Warm (daemon)` column describes a process that no
longer exists.

⚠ **M5 measured the analysis rows.** Reference machine, Release build, warm page cache:

| Row | Budget | Measured | |
|---|---|---|---|
| `verify` on a 5-file change, `loose`, cold process | 900 ms | **0.39–0.54 s** clean | ✅ 0.50–1.02 s when the five files actually have findings — the fixes and the formatter's edit list are the difference. 0.84 s on the very first run of all, which is the page cache and not the tool |
| `check` whole corpus, cold, binlog present | < 4 min | **58–134 s** | ✅ Vixen: 4 688 files, 60 compilations, generators re-run. The spread is other work on the machine, not the tool; the low end is an idle box |
| `check` on a 5-file change, warm | < 5 s | **not measured on Vixen** | ⚠ the cache writes `.skala/cache/` into the repository under test, and this milestone was under an instruction not to write into Vixen at all |
| `check`, Skala's own solution (24 compilations) | — | cold 6.8 s, warm **6.0 s** | ⚠ **the cache buys 12 %, not the order of magnitude the budget assumes** |

⚠ **M7 re-measured `verify`, because a report said a clean tree was slower than a dirty one — 0.89–1.34 s
clean against 0.55–0.62 s with findings. It is not.** Clean and dirty are indistinguishable; the
variable is the cache, and the two ranges are a cold run and a warm one that were compared to each
other. Four beds, N = 7–9 each, reference machine, Release:

| | cold cache (`--no-cache`) | warm cache |
|---|---:|---:|
| 5-file bed, **dirty** (5 files need formatting) | 0.929 s | 0.498 s |
| 5-file bed, **clean** (already formatted) | 0.926 s | 0.500 s |
| 5 files of the Skala tree, dirty | 0.968 s | 0.579 s |
| 5 files of the Skala tree, clean | 0.940 s | 0.574 s |

A cold-then-warm sequence from an empty `.skala/` is 0.918 s → 0.477 s dirty and 0.919 s → 0.475 s
clean; both write the same 2 137-byte cache file. There is no fast path that only exists when there
is work to do. The likely origin of the original claim is M6's own note that its Vixen measurements
ran with `--no-cache --output ""` to avoid writing into the tree under test: an uncached "clean"
number compared against a cached "dirty" one reproduces both ranges almost exactly.

⚠ **That last row is the interesting one and it is a warning about where the time is.** The
incremental cache removes the *analyzer* pass over unchanged files, and on a small solution the
analyzer pass is not the cost — reading the binlog, resolving references and re-running the
generators is, and no per-file diagnostic cache can remove any of it. The 5 s warm budget needs the
*compilation* cached, not the diagnostics, which needs a process that holds `CSharpCompilation`
objects across invocations. ⚠ That was going to be the format daemon, extended from format results
to compilations; the daemon is deleted, so the 5 s warm row has no path to being met and is
withdrawn with the rest of the table. `check` on a small change stays at the cost of loading the
projects.

⚠ M3 measures the first row at 280–320 ms cold and 60–70 ms warm, and the second at 11.9 s. The
whole-corpus budget is met; the warm single-file one is missed by the client's own process start,
which § "Startup" predicts exactly — `skala daemon status`, doing no work at all, is the same 60 ms.
NativeAOT for the thin client is the prescribed fix and it is not done: the client still carries the
full fallback path, and would have to stop.

### M7: the warm single-file row, met — and then deleted

⚠ **Historical.** This is what the daemon and the thin client bought, recorded because the
measurement was real and the harness lessons below are reusable. The row itself is withdrawn: the
40 ms budget served a format-on-save consumer that does not exist, and the client
(`Tools/Rikarin.Skala.Client`, NativeAOT), the protocol (`Tools/Rikarin.Skala.Protocol`) and the
daemon are gone. `format --check` on one file now costs what the "cold" row costs, every time, and
nothing waits on it.

Reference machine, 150 runs in a shell loop, wall time divided by N:

| | measured | budget | |
|---|---:|---:|---|
| `/usr/bin/true` — process-start floor | 1.9 ms | — | what any process costs here |
| AOT client, bare | **4.85 ms** | ~5 ms | § "Startup" mitigation 2 predicted this exactly |
| full tool, bare (`daemon status`, no work) | 79.5 ms | — | this was the whole of the old warm number |
| `format --check` one 453-line file, **warm, AOT client** | **8.65 ms** | **< 40 ms** | ✅ the agent hook |
| `format` one file (writing), warm, AOT client | 9.15 ms | < 40 ms | ✅ |
| `format --check` one file, warm, **full tool** | 66.9 ms | < 40 ms | ❌ — unchanged, and the reason the split was necessary |
| `format --check` one file, cold (`SKALA_NO_DAEMON=1`) | 134 ms | 250 ms | ✅ — **this is the only row left**, and it is what every single-file format costs now |
| daemon RSS after 200 formats | 160 MB | < 1.5 GB | ✅ |

⚠ **The harness is part of the measurement and nearly ruined it.** A Python `subprocess` harness
reports **38 ms for an empty NativeAOT binary** and 2 ms for `/usr/bin/true` on the same machine —
an artefact larger than the entire budget, which for several iterations looked like the client
being slow. A .NET `Process.Start` harness costs 10–22 ms per spawn, and draining its two pipes
before `WaitForExit` adds another ~20 ms. Every number above is a shell loop divided by N. The CI
assertions (`PerformanceBudgetTests`, doc 12 § "Performance tests") measure the spawn floor on each
run with the same spawner and subtract it, because a budget of 40 ms cannot be asserted by a
harness that costs 20. ⚠ `PerformanceBudgetTests` is deleted along with the budgets; the lesson
about harnesses is the part worth keeping.

⚠ **A defect the measurement found, in a component that no longer exists: the daemon could not
start in a deep directory at all.** Recorded because the shape recurs — a path-length limit that is a
kernel struct rather than a policy, and a failure that exits 0. The
kernel caps a Unix domain socket path at 104 bytes (macOS) or 108 (Linux) — `struct sockaddr_un`,
not a policy. `<repo>/.skala/daemon.sock` exceeds that for any repository nested deeper than about
eighty-five characters, so `Daemon.Listen` threw `ArgumentOutOfRangeException`, which
`DaemonCommands.RunAsync` did not catch: the daemon died with an unhandled exception *and exit code
0*, and every subsequent format silently took the cold path. CI workspaces, nested monorepos, paths
under `~/Library` and git worktrees all reach this. The socket now moves to a hashed name under the
temp directory when it will not fit, and both ends agree because both call
`DaemonProtocol.SocketPath`.

⚠ The daemon was also *started* lazily rather than assumed: the first single-file format in a
repository found no socket, did the work itself, and left one behind. The cold-to-warm sequence on a
615-line file was 310 ms, then 70 ms, then 70 ms. Without that, the warm row was unreachable without
a person running `skala daemon run` by hand, which is not a budget being met.

## Where the time goes, and what is done about it

### Startup

Cold `skala format <one file>` is dominated by process start, JIT and assembly load — for a
framework-dependent .NET tool, 120–200 ms before `Main` does anything.

⚠ **Two of the three mitigations this section prescribed are deleted, and the section is why.** They
were the daemon (a warm process holding parsed trees, spoken to over a Unix socket) and a NativeAOT
thin client in front of it (`skala`, 4.85 ms to start, with the full tool beside it as `skala-tool`
for everything else). Together they took a warm single-file format from 66.9 ms to 8.65 ms. Both
existed for the 40 ms budget, the 40 ms budget existed for a format-on-save consumer, and there is no
such consumer — so the 120–200 ms is simply paid, once per invocation, by something that is about to
wait twenty minutes for a test suite.

What that bought and what it cost is worth stating once, because the trade recurs: two binaries that
had to ship together and find each other by adjacency, a per-repository socket with a kernel path
limit, a second implementation of formatting, reporting, file writing and exit codes for one command
shape, a build-identity check so a rebuilt tool did not serve the old build's output, and a
RID-specific tool package because a native command cannot be `Runner="dotnet"`. All of that for one
number that nothing was reading.

The one mitigation that survives:

- **ReadyToRun** for the published tool, cutting JIT cost on the first analysis. ✅ Done in M7,
  conditioned on a `RuntimeIdentifier` so a plain `dotnet build` does not warn. The `dotnet tool`
  package is RID-agnostic and does not get it.

### Parsing

Roslyn parses ~1.35 M lines in roughly 6–9 s single-threaded, near-linearly parallel. It is not the
bottleneck for formatting. ⚠ It used to be cached by content hash in the daemon, so a warm re-format
of an unchanged file parsed nothing; with the daemon gone, the only process that keeps that cache is
`skala lsp`, where an editor asks for the same document repeatedly and it still pays.

⚠ The `SourceText` for a file is read once and shared between parse, format, verify and analysis.
Reading a 40 KB file three times is invisible per file and 140 MB of I/O over the corpus.

### The fitting pass

This is Skala's own code and therefore the part that can be got wrong. Design constraints that exist
for performance reasons:

- **Option lookup is an array index.** `FormattingOptions` is a generated struct-of-arrays keyed by
  a dense `OptionId` enum ([03](03-configuration-model.md)). The fitting pass reads options tens of
  millions of times over the corpus; a `Dictionary<string, object>` lookup there is a 3–4× slowdown
  on the whole operation. Measured before the design was fixed, not assumed.
- **Documents are built into pooled buffers.** `Doc` nodes are structs in a per-file arena, indexed
  by `int`, not class instances — a 1 000-line file produces ~40 000 nodes and the corpus produces
  ~110 M. Class allocation there is several GB of garbage per run.
- **Widths are computed once**, at `Text` construction, including grapheme-aware width. Recomputing
  during fitting turns a linear pass quadratic.
- **No LINQ in the fitting loop or the document builder.** Elsewhere, freely.
- **The measure pass is fused into the build pass** where a group's contents are already known,
  which removes one full traversal. ✅ Done in M2: `DocumentBuilder` accumulates each node's flat
  width and head width as the arena is filled, and `Fitter` has no measure traversal of its own.
- **The fitting pass is fused into the emit pass** for the same reason and a second one: the column a
  group is measured against is the writer's state, and a separate fitting pass has to reproduce it.
  See [04](04-formatting-engine.md) § "The pipeline".

⚠ **`skala format` was a sequential loop over files until M3, and the 20 s budget had never been
met.** Measured on Vixen (4 708 files, 1.36 M lines, Release, warm page cache), each step separately:

| | wall | CPU |
|---|---:|---:|
| M2: sequential, no configuration cache | 34.2 s | 34.6 s |
| + `ConfigurationCache`, still sequential | 30.9 s | 32.9 s |
| + ten-way parallelism, workstation GC | 19.5 s | 69.4 s |
| + server GC | **10.9 s** | 81.4 s |
| M3 as it ships, with the wrapping pass | **11.9 s** | 80.3 s |

⚠ The CPU number rising is not a mistake and is worth stating: ten threads each building a
~40 000-node document is a collector problem before it is a formatter problem, and the workstation
GC serialises them. The wall time is what the budget measures and what a hook waits on.

⚠ **The configuration was being re-resolved per file**, and this document did not say so because
nobody had looked. Every file re-read every `.editorconfig` above it, re-parsed ~900 assignment
lines, and allocated two 483-element arrays and 483 records — 4.3 M line parses and 2.3 M records
over Vixen for an answer that is the same for nearly every file in the tree. `ConfigurationCache`
keys a resolution on the *matched sections* and not on the directory, because an `.editorconfig` may
carry `[*.Designer.cs]` and keying on the directory would hand one file another's options.

### Analysis

Analyzers dominate `check` — typically 70–85 % of wall time, and the fraction rises with each hosted
third-party package. Levers:

- **The cache** ([07](07-analysis-host.md)) is the whole game for the warm path. The target is that a
  5-file change re-runs analysis on 5 files, not 4 691.
- **`--profile`** surfaces `logAnalyzerExecutionTime` output ranked by cost. This is how a rule that
  is accidentally O(n²) in a method's statement count gets found, and every Skala rule's cost is
  reviewed against it before release.

  ⚠ **It did not exist until M8, and this bullet had been describing it since M5.**
  `logAnalyzerExecutionTime: true` was set on every run and nothing ever read it. Two things it got
  wrong before it was right, both worth keeping written down because both produced *believable*
  output: `GetAnalyzerTelemetryInfoAsync` called after the run reports **0.0 ms for every analyzer**,
  because Roslyn returns the analyzer driver to a pool and the execution times go with it — the
  first profile ever printed was nineteen analyzers at 0.0 ms, which reads as a fast tool. And the
  profiled path initially ignored the changed-tree list, so `--profile` on a *warm* run measured a
  **cold** one, which is the single number the warm budget is about. Both fixed; the telemetry now
  comes off `AnalysisResult`, and `ForTrees` measures its syntax and semantic halves separately.

  Measured at M8, Skala checking itself through its own binlog:

  | | analyzer time | wall | top analyzer | the five `SK5xxx` rules |
  |---|---:|---:|---|---:|
  | cold, `--no-cache` | 2 249 ms | 9 783 ms | `MetricsAnalyzer` 77.0 % | 297 ms (13.2 %) |
  | warm, one file changed | 312 ms | 7 684 ms | `MetricsAnalyzer` 77.5 % | 26.7 ms (8.6 %) |

  ⚠ Taint analysis is the most expensive thing in the rule set and the warm path does not notice it,
  because the per-file cache means `SK5001`/`SK5002` see only the files that changed. The
  compilation-start gate is what makes even the cold number small: a tree that references no HTTP
  server or no sink type registers *no actions at all* rather than a cheap one.
- **Metadata reference cache** keyed on `(path, mtime, size)`, process-wide. 300 references × 60
  projects re-read is minutes.
- **Bounded compilation parallelism** — memory-bound, not CPU-bound; see the RSS budget.
- **Rules declare their scope.** A `Syntax`-scoped rule never causes a semantic model to be built for
  a file that needs nothing else.

### Duplication

O(total tokens) hashing plus candidate verification. The index is the memory concern: ~1.35 M lines
≈ 12 M tokens ≈ 12 M window hashes at `minTokens = 100`. Stored as a sorted `long[]` plus a parallel
`int[]` of positions, ~190 MB, memory-mapped and read on demand rather than held.

## Memory

⚠ **This section was about the daemon, and the daemon is deleted.** What it described —
`MemoryPolicy`, `RetainedCompilations`, an RSS cap with a three-step drop, and `daemon status`
reporting held bytes — is gone with it. Two things survive and are worth keeping written down.

**The CLI path streams.** Files are processed in batches with results written as they complete, so
peak RSS does not scale with corpus size. This was always true and was never the daemon's doing; it
is now the only path.

**`FormatService`'s bound survives, behind `skala lsp`.** Parsed results are held in an LRU by
content hash, capped at 400 MB.

> ⚠ **It was neither bounded by memory nor an LRU until M7**, and the defect is worth keeping because
> both halves were *documented* as being the thing they were not. `FormatService` held 4 096
> *entries* of unbounded size — over a corpus whose tail is "a handful of 20 000-line generated
> files" (§ "Parallelism"), that is several gigabytes, and the number that was supposed to be the
> memory bound bore no relation to memory. On overflow it called `Clear()`, defended by a comment
> arguing that "an LRU needs a lock on the hot path to be an LRU at all". It does not: the hit path
> stamps a monotonic tick with one interlocked write, and only the miss path — already running a full
> format — sorts or evicts. Clearing wholesale threw away the file the developer was editing along
> with the cold entries, which is how a warm process comes to feel colder than none.

⚠ The compilation store (`RetainedCompilations`, at most 4 retained) is deleted unused. It was
written so the memory policy's second step had something to drop, and nothing ever populated it: the
daemon served `format` and nothing else for its whole life. A store that was never filled, guarding a
policy step that was never taken, for a budget nothing measured — three layers of speculative
machinery, and the reason this section is now four paragraphs instead of a page.

## Parallelism

`Parallel.For` over files with `--jobs` (default `min(cores, 10)`), one file per work item.
Formatting is embarrassingly parallel and scales to ~2.8× on 10 cores measured — the collector, not
the loop, is what keeps it from eight — and the server GC is worth another 1.8× on top. The tail is
a handful of 20 000-line generated files, which is why generated files are excluded by default
(correct for two independent reasons).

⚠ **The writes happen inside the parallel body and every byte of reporting happens outside it**, in
collection order. A diff whose hunks arrive interleaved is not a diff, and three runs of
`format --check` over Vixen/Core produce byte-identical output. `git add` for `--staged` stays serial
and after the writes: 4 700 process launches against one `.git/index` is slower than doing it in
order, and git reports the contention as a failure rather than as a wait.

Analysis parallelism is inside Roslyn (`concurrentAnalysis: true`) plus bounded parallelism across
compilations. Over-subscribing these two against each other is the classic mistake: the outer degree
is `min(4, cores/2)` precisely because the inner one already saturates.

Determinism is restored by sorting after the fact ([07](07-analysis-host.md)), never by serialising.

## What is allowed to be slow

Stated so that effort goes to the right places:

- `skala arrange` on a whole tree — minutes, run rarely, re-binds every document.
- `skala baseline create` — minutes, run once per repository.
- The nightly fuzzing and rule-count jobs — hours, by design.
- `skala config distill` — seconds, run by hand.
- The oracle regeneration — tens of minutes, and it is JetBrains' time, not ours.

And what is never allowed to be slow: the post-edit hook, the pre-commit hook, and `skala explain`.
Those three are the ones a human or an agent waits on.
