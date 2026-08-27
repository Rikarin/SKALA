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

Every **implemented** entry in `options.json` requires at least one corpus file in `constructs/`
that changes behaviour when the option changes. `OptionCoverageTests` generates a case per option
from the registry — one theory row per key, not a hand-written assertion — that:

- formats the option's `oracle` fixture with the option at each of its legal values, flipped from
  the repository's own configuration rather than from the registry defaults,
- asserts the outputs differ (an option with no observable effect is either unimplemented or
  wrongly wired — both are bugs),
- asserts a committed `.expected.cs` from the oracle exists beside the fixture.

For enums this is the whole domain; for booleans, two; for ints, three. The arranger's keys are
measured the same way against the arranger and a `cleanup` fixture, because a format-only run is
byte-identical whatever `arrange_*` says.

⚠ There is one snapshot per option, not one per value: the committed fixture is the oracle's output
at the repository's configuration, and the per-value outputs are compared with each other rather
than with a stored file. An earlier draft of this section promised ~950 per-value snapshots through
`Verify`; they were never written and `Verify` is not a dependency. What the per-value comparison
buys is the property that matters — that the option is observable at all — without a thousand files
whose diff nobody could review when the oracle version moves.

⚠ The inverse has to be asserted too, and until M9 it was not.
`OptionObservabilityTests.AnInertKey_StillCannotBeObserved` takes every key the formatter reads and
records as **inert** — honoured vacuously, because another rule decides first or because the oracle
ignores it as well — and fails if it *does* change anything. "Inert" is the sentence a key gets both
when it genuinely cannot be observed and when nobody looked, and only a test tells the two apart. It
found one on its first run: `space_in_singleline_method` carried a true reason and wiring that
contradicted it.

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

| command | what it does |
|---|---|
| `plan [--family=…]` | what would be asked and what it would cost, without an oracle run |
| `sweep [--family=…] [--out=…]` | the measurement; writes `conformance-sweep.md` and its `.json` sidecar |
| `defaults [--family=…] [--apply]` | the bare-base pass; `--apply` writes verified defaults into `options.json` |
| `nightly [--family=…] [--apply]` | both, in one process, so the cross-check needs no sidecar round-trip |
| `verify <key>` | ⚠ one option, **unbatched**, both engines' output at every value printed in full |

`verify` is how a row is checked before anything is demoted on the strength of it. The batching is
what makes a whole sweep affordable and it is also the part a suspicious verdict most wants ruled
out, so the confirmation deliberately does not use it.

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

**Both engines are asked in the same units.** Every other measurement here compares line-ending
*normalised* text, because a committed fixture may have been generated on another OS. Two options —
`resharper_enforce_line_ending_style` and `resharper_csharp_insert_final_newline` — change nothing
that survives that normalisation, so the sweep falls back to raw bytes for them and marks the row
`raw`. ⚠ The trap either side of that is real and the harness has been on both sides of it: normalise
both and those two keys read `UNEXERCISED` for a reason that is about the instrument; normalise one
side only and `insert_final_newline` reads `INERT` — *"ReSharper honours the key and Skala ignores
it"* — for a key `skala format --option` demonstrably honours. `SkalaSideTests` pins the units,
because Skala's whole side of a 201-option sweep runs in under a second and needs no oracle at all.

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

#### ⚠ Arrangement options are excluded, and are the other named second phase

The sweep runs the **format-only** profile, which is `CSReformatCode` and nothing else — so its
output is byte-identical whatever an `arrange_*` or `csharp_style_*` key says, and on Skala's side it
runs the formatter rather than the arranger. Sweeping those keys here would report every one of them
as `SPURIOUS`: the harness inventing divergences rather than finding any. They are excluded by name,
with the reason recorded in the report's "Not swept" table.

Doing them properly needs the cleanup profile on the oracle's side and `CorpusArranger` on Skala's,
which is the same substitution `OptionCoverageTests` already makes for its arrangement theories. Same
machinery, different subject.

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

Nightly, bounded by a wall clock rather than a case count, seeded and reproducible. The whole thing
is `Testing/Rikarin.Skala.Testing`'s `fuzz` subcommand:

```
fuzz [--seed=N] [--minutes=N | --cases=N] [--mode=mutate|generate|both]
     [--arrange-every=N] [--out=DIR] [--no-minimise] [--jobs=N]
fuzz --replay=SEED          re-execute one case from its seed alone and print it
fuzz --check=FILE           assert the seven properties over one file, read byte for byte
fuzz --grammar-check[=N]    does the generative grammar emit C# that parses?
fuzz --mutation-test        break the formatter deliberately; check the fuzzer notices
```

**Mutation fuzzing.** `FuzzMutations` — nineteen text mutations over a corpus file, each required to
keep the file parsing the way it parsed before, drawn by weight from a seeded stream:

| class | mutations |
|---|---|
| **absorbed** — whitespace and nothing else | `indent`, `trailing-space`, `widen-gap`, `collapse-gap`, `tabs` |
| **structural** — parse-preserving, information-bearing | `comment-line`, `comment-inline`, `trailing-comment`, `blank-lines`, `remove-blank-line`, `if-true`, `if-disabled`, `region`, `pragma`, `line-endings`, `bom`, `widen-identifier`, `join-line`, `split-line` |

The absorbed five carry the strong property, `format(mutate_whitespace(x)) ≡ format(x)`, which the
preserve-and-repair model of ADR-002 makes genuinely hard rather than trivially true.
`widen-identifier` is drawn as hard as they are and for a different reason: it is the only mutation
that changes a line's *width*, which is the input every decision of the fitting engine is a function
of — [16](16-risks-and-open-questions.md) § R2's argument that the fitter is the project's only
genuinely novel code is also the argument for that weight.

**Generative fuzzing.** `FuzzGenerator` — a grammar weighted toward what the formatter handles
specially: generics, lambdas, patterns, initializers, attributes, raw strings. Its contract is *no
parse errors, semantic nonsense welcome*: an unresolved type, an operator of the wrong arity and a
`yield return` outside an iterator all come from the binder, and the formatter is syntactic. A
**parse** error is different — ADR-003 leaves such a file byte-identical, so the case passes every
property while asserting none of them. `fuzz --grammar-check` is how that contract is checked rather
than assumed, and it earned itself immediately: the first draft of the grammar emitted a parse error
in **147 units of 300**, all of it from greedy productions — a lambda body, a query's `select`, a
switch arm list and a conditional's `:` each run until the parser cannot continue, so
`[from a in b select c, d]` is one query whose `select` swallowed the comma. Every operand position
is parenthesised now, and it is 0 of 1 500.

The generated tree is then "printed with random whitespace" by running it through the same mutation
catalogue. Two implementations of *where may whitespace go* is one more than the number that can be
kept correct.

**Corpus expansion.** Any crash, non-idempotent case or token-equivalence failure is minimised
(`FuzzMinimiser`, delta-debugging on the input) and committed to `corpus/pathological/`. The corpus
only grows. ⚠ Syntax-aware reduction first — whole members and whole statements, largest first, each
removal re-parsed — and lines second: removing an arbitrary *line* from C# almost always unbalances a
brace, the candidate stops parsing, the property stops failing for the reason it was failing, and
ddmin spends its budget being told no. Measured on the first idempotency finding: lines alone took
2 494 characters to 2 433; syntax first takes it to **38**.

**Reproducibility is the seed and nothing else.** Case *i* of a run is
`FuzzRandom.Derive(rootSeed, i)`, and everything inside the case — which corpus file, which
mutations, where they land — is a function of that one number. A run that stopped at a time budget
after 41 907 cases still names every case it executed, and `fuzz --replay=<seed>` rebuilds any of
them in a second. ⚠ `FuzzRandom` is SplitMix64 rather than `System.Random`, because `Random`'s
sequence for a given seed is an implementation detail .NET has changed before and is free to change
again — a seed recorded in a nightly log that replays a *different* run is a decoration. The stream
is pinned by a test vector.

#### Where the properties are not what this document said

Two of them, and both were found by pointing the fuzzer at the corpus.

⚠ **Whitespace absorption is false as stated, for one gap class.** `SpaceRules.Ungoverned` answers
`SpaceKind.Preserve` beside a `..` in a range or a spread, because no key in ReSharper's export
governs that gap and the oracle leaves whatever the author wrote there. Asked directly, `jb
cleanupcode` returns **byte-identical output to Skala** for every spelling of `buffer[1..^2]`,
`buffer[1 ..^2]` and `buffer[1.. ^2]` — each preserving its input. So asserting absorption there
would be asserting that Skala should diverge from the oracle. The absorbed mutations skip any gap
touching a `..`, excluded by token kind rather than by parent shape, so that a *new* preserve class
would be reported rather than absorbed into the exemption.

⚠ **Range consistency as first written could not fail.** "`format(x, range)` ≡ `format(x)` restricted
to that range's edits" is satisfied by an edit list collapsed into one whole-file edit: it intersects
the range, so the count matches; it is in the list, so containment holds; there is one of it, so
nothing overlaps. Range formatting could silently have become whole-file formatting with the property
green. It now also asserts that each edit is trimmed to what differs — no shared first or last
character with the text it replaces — and that the list, applied, reproduces the output. This was
found by `fuzz --mutation-test` rather than by reading: the `edit-merge` saboteur survived 400 cases.

#### Testing the fuzzer

⚠ **A fuzzer is the one piece of test code whose own defects are invisible.** A fuzzer whose
mutations never reach the formatter reports the same green run as a formatter with no bugs in it, so
"it found nothing" is not evidence of anything on its own. Three mechanisms make it evidence:

1. **The coverage half of the report**, which is printed whether or not anything was found: cases
   executed, how many produced at least one edit, how many distinct corpus files were mutated, how
   many units were generated, how many cases also ran the arrange-and-format pair, and the histogram
   of which mutations were drawn. A run where 96 % of cases produce an edit is a run that reached the
   formatter.
2. **`fuzz --mutation-test`**: six saboteurs, each a plausible defect that breaks exactly one
   property — an indentation that grows by one per pass, a dropped `;`, a dropped `}`, an output that
   counts its own calls, an output that echoes how much whitespace the *input* had, and an edit list
   collapsed to one edit. The property that should notice must notice, and the row says after how
   many cases. A property no saboteur can trip is a property that is not being asserted.
3. **`FuzzerTests`**, on every commit: the seed rebuilds the case byte for byte; the SplitMix64
   stream matches a pinned vector; the grammar emits no parse errors in 250 units; a whitespace-only
   mutation changes no token under **either** symbol set; the minimiser returns something smaller
   that still fails; every saboteur is caught; and a 250-case run reaches the formatter and draws
   every mutation in the catalogue.

⚠ Every one of those assertions exists because the fuzzer had that defect during the day it was
written. The two that mattered most, both of which reported in the thousands while the real findings
sat underneath:

- the protection map was built from **one** symbol set, and which text is `DisabledTextTrivia` is
  entirely a function of the set — the `#if` branch is data with no symbols and the `#else` branch is
  data with them. 1 639 absorption reports from one Serilog method. The absorbed mutations now obey
  the union of both sets; the structural ones deliberately do not, because a `#if` body is live under
  one of them and is the code path M3.1 opened up.
- a run of `///` lines is **one** `SingleLineDocumentationCommentTrivia`, not one per line, so
  protecting only the line it ends on left every line above it open to a trailing-space mutation, and
  the space landed inside an XML text token. 1 870 more.

#### `corpus/pathological/open/`

Where a minimised finding lives **before** the defect it pins is fixed, with
[`register.md`](../../Testing/corpus/pathological/open/register.md) beside it.

⚠ It is excluded from `Corpus.Files()`, and the exclusion is the point rather than a dodge: one of
the entries makes `skala format` throw, and a file that throws does not fail one assertion — it takes
down every harness path that formats the corpus, the fidelity number and the differential report
included. What holds those files to account instead is `OpenDefectTests`, which asserts of every
entry that it **still fails, in the way its register entry records**. A defect that gets fixed breaks
that suite and is told where its file goes next; a defect that changes shape breaks it too. It is
deliberately not an `[Fact(Skip = …)]`: a skipped test is invisible in a green run and stays skipped
for a year. The register is capped, because a handful of open findings is a queue and thirty are a
policy of not fixing them — and the cap is raised in a commit that argues for it rather than met by
dropping a finding, which would hide exactly what this directory exists to show.

#### What the first day found

Seven defects, all minimised and all reproduced through `skala format` itself rather than only
through the harness. In full in the register; in one line each:

| | property | shape | size |
|---|---|---|---|
| SK-FUZZ-0001 | crash | `@formatter:off` open at a whitespace-only end of file throws an **unhandled** `IndexOutOfRangeException` out of `EditEmitter` — past `FormatCommand`, past the `.skala/crash/` snapshot handler, out of the process | 32 B |
| SK-FUZZ-0002 | token equivalence | a `///` run whose first line begins on the same line as the `{` loses its continuation lines; SK9099 catches it and the file cannot be formatted at all | 79 B |
| SK-FUZZ-0003 | idempotency | mixed line endings converge in two passes, not one | 22 B |
| SK-FUZZ-0004 | idempotency | the closing `]` of an array-rank specifier split across lines is indented eight columns on the first pass and four on the second | 33 B |
| SK-FUZZ-0005 | token equivalence | an interpolated string inside a formatter-off span; found by `./build.sh Lint` refusing to format the fuzzer's own source | 74 B |
| SK-FUZZ-0006 | pair idempotency | a comment between two usings, one of which carries interior whitespace: SK2010 applies and the second pipeline pass still wants an edit | 45 B |
| SK-FUZZ-0007 | whitespace absorption | a blank line appears between two members because the **input** line was wider than the margin — from two files differing in one gap | 2×60 B |

⚠ **SK-FUZZ-0004 is the argument for this whole section in one case.** The *converged* answer is the
right one, which is exactly why no corpus file catches it: every file in `corpus/` has already been
through a formatter, so its `]` is already at four, the first pass agrees with it, and the property
holds. It takes an input whose `]` starts at column zero to make the first pass disagree with the
second, and nothing in a committed corpus is ever that input. SK-FUZZ-0003 makes the same point from
the other side: `pathological/mixed-crlf-and-lf.cs` exists, and does not catch it. The corpus had the
construct and not the shape.

⚠ **SK-FUZZ-0007 is [16](16-risks-and-open-questions.md) § R2's risk, in four lines.** The blank-line
decision is a function of whether a member is "wide", and the width it reads is the *input's* rather
than the output's — so a gap the formatter is about to collapse changes a decision about a different
line entirely. It was found by `widen-identifier` and `widen-gap`, the only mutations in the
catalogue that change a width, which is why they are weighted as heavily as they are.

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
