# 13 — Performance

Skala runs in a pre-commit hook, in a post-edit agent hook, and in CI. Those three have wildly
different budgets, and the design has to serve the tightest one — the agent hook, which fires after
every file write and must be invisible.

## Budgets

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
| Daemon RSS, idle after a corpus run | < 1.5 GB | — | compilations dropped under pressure |

Each is a test ([12](12-conformance-and-testing.md) § "Performance tests") with a 20 % band, not an
aspiration.

⚠ M3 measures the first row at 280–320 ms cold and 60–70 ms warm, and the second at 11.9 s. The
whole-corpus budget is met; the warm single-file one is missed by the client's own process start,
which § "Startup" predicts exactly — `skala daemon status`, doing no work at all, is the same 60 ms.
NativeAOT for the thin client is the prescribed fix and it is not done: the client still carries the
full fallback path, and would have to stop.

⚠ The daemon is also *started* lazily rather than assumed: the first single-file format in a
repository finds no socket, does the work itself, and leaves one behind. The cold-to-warm sequence
on a 615-line file is 310 ms, then 70 ms, then 70 ms. Without that, the warm row was unreachable
without a person running `skala daemon run` by hand, which is not a budget being met.

## Where the time goes, and what is done about it

### Startup

Cold `skala format <one file>` is dominated by process start, JIT and assembly load — for a
framework-dependent .NET tool, 120–200 ms before `Main` does anything. Against a 40 ms budget that is
already lost.

Three mitigations, in order of effect:

1. **The daemon.** The hook path is a client that connects, sends a path, receives edits. The client
   is the only thing that starts cold, and it does nothing but talk over a socket.
2. **NativeAOT for the client.** A ~5 ms start for the thin client, which cannot reference Roslyn
   (Roslyn is not AOT-friendly) and does not need to — it is a socket and a JSON writer. ⚠ This
   splits the CLI into `skala` (AOT client + full fallback) and the daemon; the fallback path when no
   daemon can start must still be the full tool, which means shipping both, which means the package
   is larger. Accepted.
3. **ReadyToRun** for the daemon and the fallback, cutting JIT cost on the first analysis.

### Parsing

Roslyn parses ~1.35 M lines in roughly 6–9 s single-threaded, near-linearly parallel. It is not the
bottleneck for formatting, and it is cached by content hash in the daemon, so a warm re-format of an
unchanged file parses nothing.

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

The daemon's job is to hold things; its risk is holding everything. Policy:

- Parsed trees: LRU by content hash, capped at 400 MB, dropped first.
- Compilations: at most 4 retained; the rest rebuilt on demand. A Vixen-sized compilation with
  references is 200–400 MB.
- `MemoryPressure` handling: on `GCNotification` / RSS above the cap, drop the tree cache, then
  compilations, then exit rather than swap. ⚠ A daemon that pushes a laptop into swap is worse than
  no daemon, and the failure is silent and blamed on the editor.
- The CLI (non-daemon) path streams: files are processed in batches with results written as they
  complete, so peak RSS does not scale with corpus size.

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
