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
`.editorconfig` and a cleanup profile that enables **formatting only** (the arrangement half is
compared separately, with a profile that enables the `arrange_*` settings), and writes
`<file>.expected.cs` with a header:

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

Over `corpus/real/` (~600 files including a 200-file Vixen snapshot), compare Skala's output with the
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

## Testing the rules

Standard Roslyn analyzer testing (`Microsoft.CodeAnalysis.Testing`), with three additions that come
from the false-positive bar:

1. **Every rule has a "should not fire" fixture set** at least as large as its "should fire" set.
   `rules.json`'s `falsePositives` field must be non-empty, and the cases described there must exist
   as tests.
2. **Every rule is run over the whole reference corpus** in a nightly job, and its finding count is
   recorded in `.skala/rule-counts.json`. A rule whose count changes by more than 10 % between
   commits without an intentional change is flagged. This is how a rule that quietly starts
   over-firing gets caught before a release rather than after adoption.
3. **Every fix is round-tripped**: apply the fix, re-parse, re-bind, assert no new diagnostics, and
   assert the rule no longer fires (a fix that does not fix is a common and embarrassing bug).

For `SK5xxx`, additionally: a corpus of known-vulnerable and known-safe samples, kept apart from the
main corpus, with a required 100 % on the safe side. A security rule that cries wolf is uninstalled
within a week.

## Performance tests

BenchmarkDotNet for micro (document build, fitting, option lookup) and a wall-clock harness for
macro (whole-corpus format, whole-corpus check, warm single file). Budgets from
[13](13-performance.md) are asserted in CI with a 20 % tolerance band; exceeding it fails the build,
because performance regressions in a tool that runs in a pre-commit hook are user-visible within a
day and untraceable a month later.

## Cross-platform

The full suite runs on macOS, Linux and Windows. The Windows-specific hazards are enumerated and
each has a test: CRLF input with `end_of_line = lf`, paths in SARIF (must be repo-relative with
forward slashes), case-insensitive path comparison in the cache key, long paths, and the named-pipe
daemon transport.

## What is deliberately not tested

- **That Skala agrees with `dotnet format`.** It does not, and it should not — `dotnet format` cannot
  wrap. Comparison against it exists as a *diagnostic* tool for the Microsoft-key subset only.
- **That Skala agrees with CSharpier.** Different model entirely (ADR-002).
- **Rule coverage against SonarQube's rule list.** Coverage is not the goal; findings per false
  positive is. A rule is added because it caught something real in the corpus.
