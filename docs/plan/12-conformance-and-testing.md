# 12 — Conformance and Testing

A formatter is a program whose specification is another program's behaviour. That makes testing it a
different discipline from testing ordinary code: the interesting assertions are *differential* and
*property-based*, and the unit tests are a floor rather than the point.

## The oracle (ADR-011)

`jb cleanupcode` — the free ReSharper command-line tool — is the ground truth for "what would Rider
do to this file with this `.editorconfig`".

```bash
./build.sh Oracle          # regenerate fixtures; deliberate, reviewed, never automatic
```

For every file in `Testing/corpus/`, the harness runs `cleanupcode` with the repository's
`.editorconfig` and a cleanup profile that enables **formatting only**, and writes
`<file>.expected.cs` with a header.

⚠ **M4 added the second profile** the parenthetical above promised. `OracleProfile.Cleanup` enables
the arrangement half and writes `<file>.arranged.expected.cs`, for `corpus/real/` and
`constructs/arrangement/` only. Two things about it are worth knowing before touching it:

- **The cleanup profile runs as ONE project**, with every file at its own relative path — not the
  60-file batches of flattened `F0.cs … F59.cs` the format-only profile uses. Batching is free for
  whitespace and wrong for arrangement, because `var`, target-typed `new` and using removal are all
  questions about a *compilation*: `JObject o = JObject.Parse(json)` does not convert when
  `JObject`'s own declaration landed in another batch.
- **An unknown cleanup task is silently ignored**, so a profile that looks like it enables ten
  rewrites can be enabling three and nothing says so. The name list is not documented and was
  recovered from the tool's resource strings; the sweep is `docs/oracle-cleanup-profile.md`.

```
// skala-oracle: resharper=2025.2.6 config=sha256:1f3c… profile=format-only generated=2026-08-26
```

Fixtures are **committed**. The day-to-day test run reads files, not JetBrains — the oracle is a
developer-machine and nightly dependency, so that `dotnet test` works on a machine with no ReSharper
and in CI without a 400 MB download.

⚠ **Regenerating on failure is forbidden.** An oracle that updates itself when it disagrees is a
tautology. Regeneration is a separate commit whose diff is reviewed, and whose message says which
ReSharper version and why.

### Where the oracle is wrong

ReSharper has bugs, and some of its behaviours are undocumented and unstable across versions. When
Skala deliberately differs, the fixture carries a marker:

```
// skala-divergence: SK-DIV-0007  reason=oracle reindents disabled #if text; we never touch it
```

and `SK-DIV-*` entries live in `docs/divergences.md` with the argument for each. The count of
divergences is published alongside the fidelity number. A divergence is a decision; an unexplained
difference is a bug, and the harness cannot tell them apart without this file.

## The four levels

### 1. Option units — the floor

Every entry in `options.json` requires at least one corpus file in `constructs/` that changes
behaviour when the option changes. The option generator emits a test per option that:

- formats the fixture with the option at each of its legal values,
- asserts the outputs differ (an option with no observable effect is either unimplemented or
  wrongly wired — both are bugs),
- asserts each output matches its committed snapshot.

For enums this is the whole cross-product; for booleans, two. ~380 options × ~2.5 values ≈ 950
snapshots, which is exactly why `Verify` is a dependency and hand-written assertions are not.

### 2. Differential — the number that matters

Over `corpus/real/` (380 files including a 200-file Vixen sample), compare Skala's output with the
oracle's, and report:

| Metric | Definition |
|---|---|
| **Line fidelity** | matched lines ÷ oracle lines, where "matched" is an **LCS diff**, not a positional comparison ⚠ |
| **File fidelity** | byte-identical files ÷ total files |
| **Divergence classes** | differences grouped by the construct they occur in |

⚠ The diff basis is not a detail. Until M3 the oracle wraps and Skala does not, so the two outputs
have different line *counts*; comparing line *n* to line *n* misaligns everything after the first
wrap and charges every subsequent line as wrong. Measured on M1: the same output scored 53 %
positionally and 94 % by diff. The positional number is not a stricter measure of the same thing, it
is a measure of nothing.

Line fidelity is the headline (≥ 99.9 % is the bar from [00](00-vision-and-principles.md)). File
fidelity will be much lower for a long time and that is expected — one divergent construct spoils a
whole file — but its *trend* is the honest progress signal.

The output of a differential run is not pass/fail: it is a ranked report of divergence classes by
line count, which is the work queue. "Chained call wrapping after a conditional access: 412 lines
across 31 files" is a day's work, findable in no other way.

CI enforces a ratchet: fidelity may not decrease. Improving it is a commit; regressing it is a build
break.

⚠ **A ratchet compares numbers over the same population.** Adding fixtures to a set changes the
denominator, and a set that grows by thirty deliberately-hard files can lose aggregate percentage
while every file in it improves. When a set's population changes, the commit that changes it re-bases
the number and says so in `fidelity.json`'s `Milestone` field, *and* records what the old population
now scores — otherwise the ratchet has been quietly loosened rather than re-based.

### ⚠ Both symbol sets, by default

`./build.sh Fidelity` runs the whole differential **twice** — once with no preprocessor symbols and
once with the oracle's own eighteen, read out of a real binary log rather than typed — and reports
the two numbers side by side. It closes with the divergences that appear under **one** symbol set and
not the other.

The reason is a defect rather than a preference. Milestone 5 supplied symbols for the first time and
`count > (n)` came back `count >(n)`: every `>` was being read as a type-argument close. The bug had
survived M1, M2, M3 and M5 because every corpus line that shows it sits inside a `#if` body, which a
formatter with no symbols hands back as disabled text and copies verbatim. **A single-symbol-set run
cannot find that class of bug at all**, and there is no reason to believe it was the only one.

⚠ Both numbers are the truth about a real invocation, which is why neither is "the" number:
`skala format` on a loose file has no symbols and `skala format --load=binlog` has them. The
`fidelity.json` ratchet is the without-symbols number, because that is the weaker one and a ratchet
should hold the weaker claim.

⚠ The same applies to `dump` and to `constructs`, which take a `defined` switch and use the symbols
respectively; a construct report without them attributes a whole frozen `#if` file to whatever node
owns its lines, which measures SK-DIV-0004 and calls it `ClassDeclaration`.

### Redrawing a corpus sample

`corpus/real/vixen/` is a 200-file sample of a 4 711-file repository, and until milestone 3.1 the
answer to "which 200" was "whichever ones somebody copied". 167 of them had come from
`.claude/worktrees/` — agent scratch checkouts of the same tree, which duplicate content and record a
provenance that does not survive the checkout being deleted.

`sample <tree> <count> <destination>` draws one reproducibly. ⚠ A file is chosen by
`SHA-256(seed + "\n" + relative path)`, sorted ascending, first N — **a hash of the path rather than
a seeded pseudo-random sequence**, because a PRNG's answer depends on the order the file system
enumerated in and on how many candidates it rejected before, while a hash depends on nothing but the
path. The same commit and the same filters give the same files on any machine, in any order, forever.

Redrawing a sample re-bases the ratchet, so the commit that does it reports the number **before and
after** at the same commit of the formatter — otherwise a corpus that got easier reads as a formatter
that got better. `Testing/corpus/real/NOTICE.md` carries that pair.

### Beyond the corpus: `tree`

The corpus samples 200 files of Vixen. `tree <dir> [n]` runs the oracle *and* Skala over an arbitrary
repository and reports three things: how many files the oracle would move, how many Skala would move,
and Skala against the oracle over all of it. It is tens of minutes and a developer action, never a
test.

⚠ It exists because "should the `.editorconfig` be replaced" is a question about a tree and not about
a sample of it, and because the interesting denominator is the oracle rather than the tree as
committed. Measured over 600 files of Vixen at milestone 3.1: **the oracle would move 302 of them**
and Skala would move 299, and Skala reproduces the oracle on 99.47 % of the lines and 87.33 % of the
files. The diff a formatting commit produces is mostly the configuration swap and the drift, and
only the difference between those two numbers is Skala's.

### Alternative configurations

Most of the corpus is measured under whatever its `.editorconfig` chain resolves to, and for most
options that is enough: `OptionCoverageTests` flips one key at a time on a fixture and checks the
output moves. It is not enough for a question about two keys *in combination*, and
[05](05-csharp-formatting-rules.md) § "`keep_existing_*`" is exactly that question — a 2×2 whose
wrong reading is a first-run diff over every call site in a repository.

So a fixture set may declare *variants*: named sets of `.editorconfig` overrides under which the same
inputs are additionally run. `./build.sh Oracle` regenerates one `jb cleanupcode` fixture per
(file, variant) into `<file>.<variant>.expected.cs`, and the conformance suite measures each corner
with its own ratchet line in `fidelity.json`. `constructs/preservation/` is the first set to use it:
thirteen inputs × four combinations of `keep_user_linebreaks` × `keep_existing_*`.

⚠ The safety properties are asserted in **every** corner, not only the default one. A formatter that
corrupts a file when `keep_user_linebreaks = false` is still a formatter that corrupts files, and the
non-default corners are precisely where nobody looks.

### The key-flip sweep

`Testing/Rikarin.Skala.Conformance.Sweep/`, `./build.sh Sweep`.

Everything above this line measures Skala at **one** configuration — the values in the Rider export.
That measures the output and not the options, and the gap between the two is not academic:

- Skala reached **99.70 %** fidelity while respecting **205 of the 458** options the export sets. An
  unimplemented key whose configured value happens to coincide with Skala's behaviour costs nothing
  and is invisible.
- Flipping `resharper_int_align` between `false` and `true` produced **byte-identical output**. The
  key was ignored and no test noticed.
- M3.1 found options marked **Tier A — "pinned by an oracle fixture"** — that could not be observed
  at all.

The sweep is the instrument that makes Tier A mean something. For each option, for each of its legal
values: format that option's fixture with Skala, and with `jb cleanupcode` under the same
configuration, and compare.

⚠ **The verdict is three-way, and only one third of it is green.**

| | verdict |
|---|---|
| both engines moved, outputs agree | ✅ `CONFORMANT` — the option is honoured |
| both engines moved, outputs disagree | ❌ `DIVERGENT` — a real divergence, ranked like any other |
| **neither moved** | ⚠ **`UNEXERCISED` — not a pass.** Either the fixture does not exercise the option, or the option is inert |

Two more verdicts sit under `DIVERGENT` and are separated because the diagnosis differs. `INERT` is
the oracle moving while Skala does not — the `resharper_int_align` shape, the defect a
one-configuration measurement cannot see at all. `SPURIOUS` is Skala moving while the oracle does
not. **Treating "neither moved" as a pass rebuilds the exact defect this harness exists to detect**,
which is why `UNEXERCISED` has its own row in every table the sweep writes and never a tick beside
it.

**It sweeps fixtures, not the corpus.** `options.json` carries an `oracle` field per option naming
the fixture that exercises it. 380 real files × ~950 configurations is mostly wasted work, because
most files exercise no part of most options. An option whose fixture cannot distinguish its values
**is a finding**, reported per option, rather than a rounding error in an average.

**It batches by value index.** `cleanupcode`'s startup is tens of seconds and ~950 invocations one at
a time is not viable, so one round sets every option to its 1st value, the next to its 2nd, and the
round count is the widest option's value count rather than the total. ⚠ The hazard in that technique
is worth restating because M3 hit it: with a **shared** `.editorconfig` across the batch, every
fixture is moved by every other option in it, and the first attempt came back "197 options set, 0
fixtures unchanged". Each fixture gets **its own directory and its own `.editorconfig`**.

**Both engines are pinned, not defaulted.** The base configuration is the repository's export with
exactly one key overridden, so no key is left to fall back on a default — Skala's fallbacks and
ReSharper's differ, which is the whole reason `DefaultsProbe` exists, and a bare base would turn
every option's comparison into a measurement of the default table. A baseline pass runs both engines
with nothing overridden first, so that a fixture the two already disagreed on is reported as such
rather than blamed on the key that was flipped on it.

**It is a nightly job, not a commit gate.** It needs JetBrains installed, which is a developer-machine
and nightly dependency and never a runtime one (ADR-011). What the fast path gets is the committed
result table, `conformance-sweep.md`, reviewed in its diff exactly as the oracle fixtures are: an
option that was `CONFORMANT` yesterday and `UNEXERCISED` today is one line in a pull request rather
than a number nobody re-derived.

**Verified defaults are a by-product.** `skala config distill` may drop a key only where its default
is verified, because dropping one on a guessed default silently changes formatting in whoever's
repository accepted the file. The same machinery under a bare `root = true` base *is* the defaults
measurement: that run is ReSharper-with-defaults by construction, and the value reproducing it on the
option's own fixture is the default. ⚠ What the sweep adds over M3's probe is the cross-check. The
probe reported `Insensitive` whenever every value reproduced the baseline and could not say whether
that meant "the fixture is too weak" or "ReSharper's defaults mask this option"; an option the
export-base run watched the oracle distinguish is one the fixture *can* see, so `Insensitive` on it
is a masking fact about bare defaults and not a gap in the fixture. Those are marked `masked` and are
not evidence that a fixture needs replacing.

#### ⚠ Interactions are out of scope, and the sweep is therefore incomplete

One key at a time isolates cleanly, which is what makes a verdict a statement about *that option*. It
is also **provably incomplete**: § "`keep_existing_*`" in [05](05-csharp-formatting-rules.md) is a
four-way table across **two** keys, and no one-at-a-time sweep can reach three of its corners. A
family whose members interact can come back all-`CONFORMANT` and still be wrong in combination.

Pairwise sweeps of the known-interacting families — `keep_existing_*` × `keep_user_linebreaks`,
`wrap_*` × `max_line_length`, `align_*` × `indent_*` — are a **named second phase**. They are not
approximated here, and a green sweep must not be read as covering them.

### 3. Properties — where the real bugs are

Run over every corpus file, every commit, and over generated input nightly:

| Property | Statement |
|---|---|
| **Idempotency** | `format(format(x)) ≡ format(x)`, byte-identical |
| **Token equivalence** | significant tokens of `format(x)` ≡ those of `x` ([04](04-formatting-engine.md)) |
| **Parse stability** | `format(x)` parses with the same diagnostics as `x` |
| **Range consistency** | `format(x, range)` ≡ `format(x)` restricted to that range's edits |
| **Determinism** | three runs, three thread counts ⇒ identical bytes |
| **Daemon parity** | with and without the daemon ⇒ identical bytes |
| **Width monotonicity** | at width ∞ nothing wraps; at width 1 everything that can break, breaks |
| **Preservation** | with `keep_user_linebreaks = true` and a file already formatted at width ∞, no break is removed |
| **Arrangement safety** | `arrange(x)` has no new compiler diagnostics ([06](06-arrangement-and-syntax-styles.md)) |
| **Pair idempotency** | ⚠ M4: `pipeline(pipeline(x)) ≡ pipeline(x)`, where `pipeline` is arrange-then-format. Neither half being idempotent implies the pair is |
| **Convergence** | the pair reaches a fixed point within `ArrangementPipeline.MaxPasses`; not reaching it is `SK9097` and a reported failure, never a silent truncation |

Idempotency and token equivalence are the two that catch nearly everything. Both are cheap; both run
on every file in every test run.

### 4. Fuzzing

Nightly, unbounded, seeded and reproducible:

- **Mutation fuzzing.** Take a corpus file, apply random text mutations that keep it parseable
  (insert/delete whitespace, insert comments at random trivia positions, insert `#if` blocks, swap
  line endings, inject BOM, widen identifiers). Assert the properties above. Whitespace-only
  mutations must be *absorbed*: `format(mutate_whitespace(x)) ≡ format(x)` — a strong property that
  the preservation model makes non-trivial and interesting.
- **Generative fuzzing.** Build random syntax trees from a grammar weighted toward the constructs the
  formatter handles specially (generics, lambdas, patterns, initializers, attributes, raw strings),
  print them with random whitespace, and assert the properties.
- **Corpus expansion.** Any crash, non-idempotent case or token-equivalence failure is minimised
  (delta-debugging on the input) and committed to `corpus/pathological/` with the bug reference. The
  corpus only grows.

⚠ **M7 installed the nightly job and did not write the fuzzer, and the difference matters.**
`.github/workflows/nightly.yml` runs the property suite — all six properties over every corpus file
under both symbol sets, **8 981 cases** — and uploads any `.skala/crash/` artefacts. That is the
*assertion half* of what this section describes. What does not exist: a seeded mutation driver, the
weighted generative grammar, and the delta-debugging minimiser that turns a failure into a committed
`corpus/pathological/` entry. The only mutation function in the tree is
`PropertyTests.MutateIndentationOnly`, a deterministic transform applied once per file and private
to a test class — so there is no seed to pass and nothing to reproduce from. The workflow's header
says so rather than implying otherwise by existing, and a `--seed` flag was deliberately not
threaded through the YAML to a parameter nothing reads.

## Testing the rules

Standard Roslyn analyzer testing, with three additions that come from the false-positive bar.

⚠ **Not `Microsoft.CodeAnalysis.Testing`, as it turns out.** That package's model is a source string
with `{|SK1010:…|}` markup inside it, which is fine for a handful of cases and wrong for the shape
this bar needs: a "should not fire" fixture is a *file that compiles and produces nothing*, and the
markup model has nowhere to put "and here is why". `Rules/Rikarin.Skala.Rules.Tests/fixtures/` is one
directory per rule with `positive/` and `negative/` beside each other, one real `.cs` file per case,
named for the reason it exists — `user-defined-equality.cs`, `expression-tree.cs`,
`receiver-is-a-call.cs`. The file *is* the documentation of the guard, and the reviewer reads C#
rather than markup.

⚠ **A fixture that does not compile proves nothing**, and that is asserted before the rule is run: a
semantic rule reading an error type answers "no finding" for the wrong reason, and the negative case
passes for free. `RuleFixtureTests` fails the fixture rather than the rule when that happens.

⚠ **`fidelity audit <dir>` is the corpus-scale instrument**, and it deliberately runs the semantic
rules under a *loose* compilation, which the product refuses to do. For an audit the asymmetry is in
the safe direction: every finding it produces is one to check by hand, and the ones it misses are
misses rather than false positives. It also applies every fix it found and compares compiler-error
counts per `(file, diagnostic id)` before and after — per `(file, id)` and not per `(file, line,
id)`, because a fix that deletes the namespace braces moves every error in the file down a line and
a line-keyed comparison reports dozens of regressions that are all the same shrug.

The three additions:

1. **Every rule has a "should not fire" fixture set** at least as large as its "should fire" set.
   `rules.json`'s `falsePositives` field must be non-empty, and the cases described there must exist
   as tests.
2. **Every rule is run over the whole reference corpus** in a nightly job, and its finding count is
   recorded in `.skala/rule-counts.json`. A rule whose count changes by more than 10 % between
   commits without an intentional change is flagged. This is how a rule that quietly starts
   over-firing gets caught before a release rather than after adoption.
3. **Every fix is round-tripped**: apply the fix, re-parse, re-bind, assert no new diagnostics, and
   assert the rule no longer fires (a fix that does not fix is a common and embarrassing bug).
   ⚠ M5 does the first half at the unit level (`EveryFix_ProducesTextThatStillParses`) and the second
   at corpus scale (`fidelity audit`'s before/after). The *re-bind* half is the expensive one — a
   semantic re-check per file rebuilds the compilation — and `skala fix` therefore compares syntactic
   diagnostics per file and reverts on regression. The compilation-wide delta is M4's, beside the
   arrangement pass that cannot avoid it.

For `SK5xxx`, additionally: a corpus of known-vulnerable and known-safe samples, kept apart from the
main corpus, with a required 100 % on the safe side. A security rule that cries wolf is uninstalled
within a week.

## Performance tests

BenchmarkDotNet for micro (document build, fitting, option lookup) and a wall-clock harness for
macro (whole-corpus format, whole-corpus check, warm single file). Budgets from
[13](13-performance.md) are asserted in CI with a 20 % tolerance band; exceeding it fails the build,
because performance regressions in a tool that runs in a pre-commit hook are user-visible within a
day and untraceable a month later.

✅ M7: `Tools/Rikarin.Skala.Cli.Tests/PerformanceBudgetTests.cs`, in its own CI job on its own
runner, opt-in by `SKALA_PERF=1` so a contributor's `dotnet test` never trips them. Three rows —
cold single file, warm single file, daemon RSS.

⚠ **The harness is part of the measurement, and two harnesses lied before one told the truth.** A
Python `subprocess` harness reports **38 ms for an empty NativeAOT binary** and 2 ms for
`/usr/bin/true` on the same machine — an artefact larger than the entire 40 ms budget under test. A
.NET `Process.Start` harness costs 10–22 ms per spawn, and draining its two pipes to EOF *before*
`WaitForExit` — the obvious way to write it — waits for stderr's EOF after stdout's and charges
another ~20 ms to the process being measured. So:

- the spawn floor is **measured every run with the same spawner** and subtracted, never assumed;
- the clock stops at process exit and the pipes are drained afterwards;
- the numbers quoted in [13](13-performance.md) are a shell loop over N, which is the cheapest
  spawner available and the one closest to how a hook actually invokes the tool.

⚠ **And a performance test must prove it measured the thing it names.** The warm row asserts the
daemon's hit counter moved before it believes its own number. Without that it measured **218 ms**
and reported it as a slow warm path; the truth was that the bed was not a git repository, so there
was no repository root, so there was no socket to look for, so the client execed the full tool every
time. A test that cannot tell "slow" from "not running" is not a test.

## Cross-platform

The full suite runs on macOS, Linux and Windows. The Windows-specific hazards are enumerated and
each has a test: CRLF input with `end_of_line = lf`, paths in SARIF (must be repo-relative with
forward slashes), case-insensitive path comparison in the cache key, long paths, and the named-pipe
daemon transport.

### ✅ M7: the matrix, and what writing the five tests found

`.github/workflows/cross-platform.yml` — `dotnet test` over the whole solution on `ubuntu-latest`,
`macos-latest` and `windows-latest`, `fail-fast: false`, plus a `lint` job and a `performance` job
that CI was running nowhere. It is a separate file from `skala.yml` because that workflow's verdict
is one `skala check` exit code and a four-job conjunction would make "did the gate pass"
unanswerable from the workflow's result.

⚠ **Three of the five hazards were real defects, not hypotheticals.**

| Hazard | Test | Found |
|---|---|---|
| CRLF under `end_of_line = lf` | `Tools/…Cli.Tests/LineEndingTests.cs` | ⚠ **`end_of_line` is inert on its own.** The key that converts line endings is `resharper_enforce_line_ending_style`, `false` by default; `end_of_line = lf` alone leaves CRLF exactly as it found it. A test written from this document's own headline would have asserted the wrong thing |
| SARIF paths repo-relative, forward slashes | `Tools/…Cli.Tests/SarifPathTests.cs` | ⚠ `SarifWriter.Relative` compared case-sensitively, had no component boundary, and took a non-nullable root that callers reach with a nullable one — all three printed absolute paths |
| Case-insensitive path in the cache key | `Analysis/…Tests/CacheKeyPathTests.cs` | ⚠ **The key hashed the path's raw UTF-8**, so `C:\Src\A.cs` and `c:\src\a.cs` — one file on every Windows volume and on a default macOS volume — produced two entries. Benign in direction (a miss, never a stale hit) and therefore invisible for four milestones, but *permanent*: paths from MSBuild and paths from a directory walk never share an entry, so the warm run [13](13-performance.md) budgets at under 5 s was a cold one every time |
| Long paths | `Tools/…Cli.Tests/LongPathTests.cs` | 403-character path. Asserts the finding *appears*, not merely that nothing threw — the dangerous failure is swallowing `PathTooLongException` and reporting a clean tree |
| Named-pipe daemon transport | `Tools/…Server.Tests/MemoryPolicyTests.cs` § `SocketPathTests` | ⚠ **There was no named-pipe transport.** Both ends built `AddressFamily.Unix` unconditionally and only a comment in `Daemon.Restrict` claimed otherwise, so the hazard had nothing to test |

⚠ A sixth, found by building the matrix rather than by the list: `.gitattributes` marked
`editor_config_template` as `-text` but not `.editorconfig`, so under git's default
`core.autocrlf=true` one arrived CRLF'd and the other did not, and an ingestion test comparing them
failed **on Windows only**.

⚠ And a seventh, found by running the suite from an agent worktree: **three test classes matched
their path exclusions against absolute paths**, so from inside `<repo>/.claude/worktrees/<name>/`
they scanned the parent checkout, or nothing at all. One was `ToolDiagnosticIdTests`, the guard
ADR-012 rests on — it was passing without reading the files under test. All three now match relative
to the root, and each has an "the scan found something" assertion beside it, because every other
assertion in those classes is of the "nothing is wrong" shape and passes happily over an empty
sequence.

## What is deliberately not tested

- **That Skala agrees with `dotnet format`.** It does not, and it should not — `dotnet format` cannot
  wrap. Comparison against it exists as a *diagnostic* tool for the Microsoft-key subset only.
- **That Skala agrees with CSharpier.** Different model entirely (ADR-002).
- **Rule coverage against SonarQube's rule list.** Coverage is not the goal; findings per false
  positive is. A rule is added because it caught something real in the corpus.
