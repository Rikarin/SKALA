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
found one on its first run: `skala_space_in_singleline_method` carried a true reason and wiring that
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

⚠ **This number sits on a floor, and the floor is 90.95 %.** See
[§ The unformat differential](#-the-unformat-differential) below: the corpus inputs are already
mostly formatted, so most of what this measures is that Skala leaves good code alone. It is a real
requirement and it is not the requirement that retires ReSharper.

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

### ⚠ The basis, and why it is in the number's name

A population can change without a file being added: the *lines* counted can change. The differential
compares Skala against a committed fixture, and where the fixture answers a question Skala no longer
asks, those lines are not measuring fidelity.

That is the case for documentation comments. Skala formats them; the fixtures were generated by a
`jb cleanupcode` profile that does not (SK-DIV-0006). So the differential's basis is
`outside doc comments` — every `///` line removed from **both** sides before comparing — and the
basis is named everywhere the number appears: in `FidelityBasis`, in every message the ratchet
prints, and in `fidelity.json`'s own `Basis` field, which `FidelityBaseline.Read()` refuses to
compare across rather than silently ratcheting one population against another.

⚠ **An exclusion is a debt, and it is carried three ways.** A fidelity figure that quietly drops a
category is how a measurement stops meaning anything, so: the every-line number is asserted
alongside the ratchet by `TheEveryLineNumber_IsStillReported`, so the excluded category cannot grow
unwatched; `TheCodeAroundTheComments_IsUntouched` asserts over all 716 corpus files that the
sub-formatter moves nothing outside a `///` line, which is the only reason the exclusion is not a
hiding place; and the exclusion has a stated expiry — enabling `CSharpFormatDocComments` in
`OracleProfile.FormatOnly` and regenerating the fixtures returns the basis to every line.

⚠ The exclusion is drawn from both sides on purpose. Excluding "the lines Skala changed" would be
marking one's own homework; excluding "the files that have doc comments" would hide a real
regression in the code around them.

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

### ⚠ The unformat differential

`Testing/corpus/unformatted/`, `./build.sh Unformat`.

**The 99.63 % headline sits on a 90.95 % floor.** Score a formatter that returns its input unchanged —
compare each `corpus/real/` **input** directly against its `.expected.cs` — and it gets:

| | null hypothesis | Skala |
|---|---|---|
| line fidelity | **90.95 %** | 99.63 % |
| file fidelity | **26.84 %** | 85.26 % |

91 % of corpus lines never needed changing, so the entire discriminating power of that differential
lives in the other 9 %. Skala closes 96 % of the available line gap, which is real — but the test
mostly asks *"does Skala leave good code alone"* and only faintly asks *"does Skala make the same
decisions ReSharper makes"*. **The second question is the one that decides whether ReSharper can be
retired.**

So: degrade a corpus file's formatting, run **both** Skala and `jb cleanupcode` over the degraded
copy, and compare the two outputs. Same oracle, same fixtures-are-committed rule, new corpus.

#### Two modes, and why one is not enough

The export sets `skala_keep_user_linebreaks = true` and `keep_user_wrapping = true` — ADR-002's
whole preserve-and-repair model. **Destroying the author's line breaks destroys the input those
options act on**, so a collapse-everything test measures the *reflow* path and says nothing about the
*preserve* path, which is what runs in production.

| mode | what it does | what it exercises |
|---|---|---|
| `scramble` | random indentation, ~1 in 3 line breaks moved to a legal-but-wrong place, blank lines added and removed, inter-token spacing randomised. The author's breaks are **different**, not absent. | `keep_existing_*` and the preserve machinery — **the mode that matters most**, because it is what real input looks like and what an AI writes |
| `collapse` | minimal legal whitespace, everything joined onto as few lines as the language allows. Only a directive, its disabled text, a `//` comment and a `///` run force a line. | the fitting engine and the wrap options, from nothing |

Both are **parse-preserving and token-preserving**, and neither re-derives what is safe to touch:
`FuzzMutations.SourceMap` already knows which bytes are data, and it cost that agent 3 500 false
reports to learn. `Unformat.Degrade` refuses to emit a file whose token stream is not identical to
the original's **under both symbol sets** — which caught three real errors while this was written,
each recorded at the line that fixes it: structured doc trivia losing its `///` under `ToString()`,
an interpolated string decomposing into tokens a separator could be written between, and text that is
live code under one symbol set and disabled data under the other.

Measured over the degradation as committed: 33.8 % of the original's lines survive `scramble`
unchanged and 28.2 % survive `collapse`; no file in either mode is unchanged.

#### ⚠ The null hypothesis, beside every number

**Report what "change nothing" scores on the same population, always.** Its absence beside 99.63 % is
what made that number look better than it was. `./build.sh Unformat` prints it as the first row of
every table, and `UnformatTests.TheNullHypothesis_IsFarBelowSkala` asserts it stays low — a ratchet
on its own cannot tell a formatter that improved from a corpus that got easier.

380 files of `corpus/real/`, both modes. ⚠ Two columns per mode: the first measurement, at the commit
that added the corpus, and the current one — the wrapped file-scoped namespace was fixed one commit
later and `scramble` moved 24.8 points.

| | scramble (first) | **scramble (now)** | collapse (first) | **collapse (now)** |
|---|---|---|---|---|
| null hypothesis, line | 30.73 % | 30.73 % | 32.38 % | 32.38 % |
| **Skala, line (no symbols)** | 64.18 % | **89.00 %** | 91.74 % | **91.75 %** |
| Skala, line (symbols supplied) | 64.24 % | 89.06 % | 91.83 % | 91.84 % |
| null hypothesis, file | 0.00 % | 0.00 % | 0.00 % | 0.00 % |
| **Skala, file** | 2.11 % | **2.37 %** | 1.84 % | **1.84 %** |
| share of the available line gap closed | 48.3 % | **84.1 %** | 87.8 % | 87.9 % |
| oracle(degraded) vs oracle(original), line | 76.81 % | 76.81 % | 87.51 % | 87.51 % |

⚠ **One fix moved `scramble` 24.8 points and `collapse` 0.01.** That is not a rounding difference, it
is the two modes measuring different things: collapsing a file removes the very line break that
wrapped the namespace name, so the defect cannot fire there at all. A single-mode test would have
found it or missed it entirely depending on which mode was built.

The last row is a **ceiling, not a floor**: it is the oracle measured against its own answer for the
undegraded file. At 76.81 % on `scramble` the oracle does not recover the canonical form either,
because `keep_user_linebreaks` tells it not to — the scrambled breaks are the author's breaks now.
Nothing that agrees with the oracle could score higher against the original, and a differential that
compared Skala to `corpus/real/`'s fixtures instead would be charging Skala for the configuration's
own behaviour.

⚠ **`collapse` scores 27 points higher than `scramble`, which is the opposite of the intuition.**
Rebuilding a layout from nothing is the easier problem; agreeing about *which* of the author's breaks
to keep is the harder one, and it is the one that runs on every real invocation. A single-mode test
built on "destroy everything" would have reported 91.74 % and missed it.

#### The ranked divergence classes — the work queue

⚠ One defect dominated `scramble`, and the aggregate was unreadable without the split:

| scramble subset | files | line fidelity |
|---|---|---|
| input has a **wrapped file-scoped namespace** | 204 | **38.00 %** |
| input does not | 176 | **88.93 %** |

**A file-scoped namespace whose qualified name is wrapped made Skala indent the entire rest of the
file by one level.** Six lines reproduce it, and the oracle was asked directly rather than assumed:

```csharp
namespace Serilog
    .Configuration;

public class Foo {       // ← Skala emitted this, and everything after it, at +4
    public int Bar { get; set; }
}
```

✅ **Fixed.** A file-scoped namespace is a `MemberDeclarationSyntax`, so it owns a continuation
frame — and unlike every other member, the whole rest of the file is its *children* rather than its
siblings. The level the wrapped name spent was therefore closed at the end of the namespace node,
which is the end of the file. `VisitFileScopedNamespace` now closes it at the `;`, which is
`VisitChild`'s existing rule — *a body indents from its declaration's level* — applied to the one
declaration whose body has no braces. A braced namespace never showed it because `VisitBraced`
closes the frame at the `{`.

⚠ The prediction from this table was "roughly 0.89"; the measurement came to **0.8900** bare, and
the split is what made that prediction possible. The reproduction is pinned by name in
`pathological/wrapped-file-scoped-namespace-name.cs`, because 204 scrambled files pin it by weight
and not one of them says what it is.

⚠ **Nothing moved on pre-formatted input** — `constructs` 97.65 %, `real` 99.63 %/99.70 %, before
and after. No file in `corpus/real/` has a wrapped namespace name, which is both why the defect
survived nine milestones and why this differential was worth building.

Underneath it, over the 176 uncontaminated files (88.94 % line, null hypothesis 31.28 %), the ranked
classes are:

| lines | files | class |
|---|---|---|
| 1 564 | 148 | line break presence: Skala left a line the oracle joined |
| 1 265 | 157 | wrapping: one side continues where the other broke |
| 682 | 128 | wrapping: the oracle broke a line Skala left long |
| 662 | 128 | indentation (−4 columns) |
| 339 | 82 | other |
| 194 | 51 | indentation (+4 columns) |
| 149 | 81 | blank line: Skala has one, the oracle does not |

The first three are largely **one** decision, also confirmed against the oracle directly: given
`using System; using System.Text;` on one line, **the oracle puts each `using` on its own line and
Skala leaves them joined**. The second, from the same probe: given an accessor list the author broke
inside — `{ get; set\n ; }` — the oracle expands the whole list to a block and Skala keeps the shape
and re-indents it. Both are `keep_existing_*` questions, and neither is visible in `corpus/real/`
at all, because nothing in that corpus is written that way.

`collapse`'s own list is dominated by wrapping (2 193 lines across 321 files where the oracle broke a
line Skala left long, 666 where the two broke it in different places) and by blank-line insertion
between members (605 lines across 361 files where the oracle inserts a blank line and Skala does
not). That is the fitting engine and `blank_lines_around_*`, which is what a from-nothing test is
supposed to find.

#### Cost, and the sampling decision

⚠ Oracle runs dominate: `jb cleanupcode`'s startup is tens of seconds and its per-file marginal cost
is milliseconds, so the only variable worth tuning is the batch. **Measured on the reference machine
at a batch of 60: 0.37 s/file amortised, 14 invocations, 4 min 38 s for all 760 files.**

That is cheap enough that **there is no sample**: all 380 files of `corpus/real/`, in both modes.
The sampler is still there — `unformat generate --count=N` draws by `SHA-256(seed + "\n" + path)` so
a smaller draw survives the person who ran it — and it is what to reach for if the corpus grows or
the modes multiply. A full set needs no argument about which files it left out.

The committed cost is 15 MB and 1 520 files: 760 degraded inputs and 760 fixtures.

#### Regenerating

```
dotnet run --project Testing/Rikarin.Skala.Testing -- unformat generate [--count=N]  # ⚠ also deletes the fixtures
dotnet run --project Testing/Rikarin.Skala.Testing -- unformat oracle                # re-fixture
dotnet run --project Testing/Rikarin.Skala.Testing -- unformat regenerate            # both
./build.sh Unformat                                                                  # measure
```

⚠ The first three are deliberate reviewed actions in their own commit, like `oracle` and `sample`;
`./build.sh Unformat` and the conformance suite read committed files and need nothing installed
(ADR-011). The degraded inputs are **inputs**: `UnformatTests` re-checks every one of them against
its `corpus/real/` source from the committed bytes, because a hand-edit to a degraded file turns
every number after it into a comparison of two unrelated files and nothing else would notice.

### The key-flip sweep

`Testing/Rikarin.Skala.Conformance.Sweep/`, `./build.sh Sweep`.

| command | what it does |
|---|---|
| `plan [--family=…]` | what would be asked and what it would cost, without an oracle run |
| `sweep [--family=…] [--out=…]` | the measurement; writes `conformance-sweep.md` and its `.json` sidecar |
| `defaults [--family=…] [--apply]` | the bare-base pass; `--apply` writes verified defaults into `options.json` |
| `nightly [--family=…] [--apply]` | both, in one process, so the cross-check needs no sidecar round-trip |
| `verify <key>` | ⚠ one option, **unbatched**, both engines' output at every value printed in full |
| `pairwise [--family=…] [--out=…]` | ⚠ two keys at once over their whole grid — the interaction pass, below |
| `pairwise-plan [--family=…]` | what the pairwise pass would ask and what it would cost |

`verify` is how a row is checked before anything is demoted on the strength of it. The batching is
what makes a whole sweep affordable and it is also the part a suspicious verdict most wants ruled
out, so the confirmation deliberately does not use it.

Everything above this line measures Skala at **one** configuration — the values in the Rider export.
That measures the output and not the options, and the gap between the two is not academic:

- Skala reached **99.70 %** fidelity while respecting **205 of the 458** options the export sets. An
  unimplemented key whose configured value happens to coincide with Skala's behaviour costs nothing
  and is invisible.
- Flipping `skala_int_align` between `false` and `true` produced **byte-identical output**. The
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
the oracle moving while Skala does not — the `skala_int_align` shape, the defect a
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
`skala_enforce_line_ending_style` and `skala_insert_final_newline` — change nothing
that survives that normalisation, so the sweep falls back to raw bytes for them and marks the row
`raw`. ⚠ The trap either side of that is real and the harness has been on both sides of it: normalise
both and those two keys read `UNEXERCISED` for a reason that is about the instrument; normalise one
side only and `skala_insert_final_newline` reads `INERT` — *"ReSharper honours the key and Skala ignores
it"* — for a key `skala format --option` demonstrably honours. `SkalaSideTests` pins the units,
because Skala's whole side of a 258-option sweep runs in under a second and needs no oracle at all.

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

#### ⚠ A flags option's probe set tested no combination at all

An `int` has no finite domain, so the sweep offers a probe set rather than the domain, and an
`UNEXERCISED` verdict on one is correspondingly weaker. A `flags` option has the opposite problem: its
domain is the *power set* of its members, and what it is **for** is the combinations.

The probe set was every member singly, then the join of them all. For
`csharp_new_line_before_open_brace` that join is `all, none, accessors, …, types` — and `all` is one
of its members, so both engines parse the combination and `all` dominates every other member in it.
The probe was a second copy of the `all` singleton wearing fourteen names. Measured at `603fbd3`:
the oracle's output at that value is byte-identical to the fixture, and it accounted for one of the
option's three agreements out of fifteen values.

⚠ **The gap it left is the real defect.** Fourteen singletons plus one value equivalent to a singleton
means the probe set **never tested a combination**. A formatter that honours `methods`, honours
`types`, and mishandles `methods, types` passed every probe. That is the interaction hole one key
inwards, and it is why the probe now ends with a genuine two-member value —
`OptionDomain.CombinationProbe`, the two last-declared members that are not the domain's "everything"
or "nothing" spelling.

⚠ **The heuristic is named as one.** `all`, `none` and `false` are the three spellings the registry's
four flags enums use for the full and empty sets. It does **not** know about hierarchical aggregates:
`BinaryOperationGroup` has `arithmetic`, `bitwise` and `conditional`, each subsuming several leaves,
and nothing in `options.json` says so. Declaration order avoids them today; a fifth flags enum could
defeat it, and the fix then is to record aggregation in the registry rather than to grow the list.

⚠ `OptionDomain.EveryLegalValue` still offers the all-members join and should. Accepting it is a real
requirement of the parser, which is a different question from whether it is worth formatting at.

#### The two canaries, and why a fired one has to be committed

Both of this harness's confident-wrong-table bugs had one shape — a non-empty population in which
nothing was observed — so the check is a named predicate that a test can pin, because a healthy run
is exactly the run in which it stays silent. There are two of them and they ask different questions:

| predicate | fires when | the bug it caught |
|---|---|---|
| `IsBrokenMeasurement` | the oracle returned **nothing** for the whole population | "0/164 fixtures agree at the baseline" — a normalise-one-side-only bug |
| `IsUnvaryingRound` | the oracle answered, and answered with **the input**, for every option | M3's "197 options set, 0 fixtures unchanged" — a shared `.editorconfig` |

⚠ **`IsUnvaryingRound` is suppressed below a population of two, and that is a correction rather than
a softening.** The sweep batches by value index, so the widest option runs *alone* in every round
past every other option's arity — `csharp_new_line_before_open_brace` has fifteen values and rounds
5–15 hold nothing else. In a round of one, "no option moved" and "this option's value legitimately
reproduces its own fixture" are the same observation and the canary cannot tell them apart. It fired
on exactly that at `603fbd3`, the round was healthy, and `verify` settled it. The fifteenth value is
the flags domain's synthesised all-members join; both engines parse it, `all` is one of its members
and dominates the rest, and the fixture is already written with every brace on its own line — so the
oracle answered, and answered with the text it was given. A canary that cries wolf on every run containing a high-arity option is a canary that gets
skimmed, which is the failure mode both of these exist to prevent.

⚠ **A fired canary is written into `conformance-sweep.md`, above the outcomes.** Until `603fbd3` it
was a line on the console and nothing else — so the warning reached whoever ran the sweep and nobody
else, while the committed table the fast path reads looked exactly as confident as a healthy one.
The caveat travels with the numbers it qualifies or it does not exist.

#### The formatter the sweep measured, checked on every commit

The sweep's table is not informational: `OptionCoverageTests` reads `conformance-sweep.json` and lets
it decide which tier an option is entitled to. So a sweep measured against a formatter that has since
changed is not a stale note — it is the evidence two live invariants rest on.

Nothing checked that until `603fbd3`. `ProvenanceTests` checked that the sweep measured the
*configuration* in force; between `2a14dee` and `603fbd3` the tree moved **88 commits**, the
`.editorconfig` did not, and every tier decision in that window rested on a measurement no instrument
in the repository could connect to the code that produced it.

The oracle half cannot be re-asked on the fast path — JetBrains, minutes, ADR-011. **Skala's half
runs in under half a second**, so the archive records Skala's own output digest at every
configuration the run measured, and `ProvenanceTests.TheCommittedSweep_MeasuredTheFormatterInForce`
re-asks it on every commit. A row that still matches is a verdict about *this* formatter. A row that
does not is a verdict about a formatter that no longer exists — which does not make it wrong, only
unowned.

⚠ **The cost is stated rather than discovered: changing how a swept construct formats turns the suite
red until the sweep is re-run.** That is the same bargain the corpus provenance test already strikes,
and it is narrow in practice — the swept fixtures are one construct each, so implementing an option
drifts that option's own fixture and its neighbours rather than the corpus. The failure names them.
There is deliberately **no** button that re-stamps these digests without re-running the sweep:
`OracleFixture.Restamp` exists for the configuration digest because a configuration can provably
change without changing a byte of output, whereas a formatter whose output has changed has, by
construction, changed its output.

#### ⚠ Tier A is checked in three directions, not two

`TierA_IsWhatSkalaReads_AndTheSweepSubstantiates` compared two sets and needed three. `overclaimed`
is Tier A minus implemented; `underclaimed` is implemented minus Tier A minus the sweep's demotions.
An option that is **claimed Tier A *and* implemented *and* unsubstantiated by the sweep** is in
neither difference — it falls through both, and the suite goes green on a claim the committed
measurement contradicts.

The sweep's report has printed *"each is a dilution of the tier system and must be demoted"* since it
was written, and until `603fbd3` the demoting was a person reading a markdown table and remembering
to act on it. The run at `603fbd3` produced the first two options ever to sit in that state and both
assertions passed on them.

#### What the run at `603fbd3` measured

**283 options at 636 configurations**, in 3.6 minutes of oracle wall clock — **760 ms per option**.
The 33 not swept are arrangement keys, excluded below.

| outcome | options | |
|---|---:|---|
| ✅ `CONFORMANT` | 213 | both engines moved and every value agrees |
| ❌ `DIVERGENT` | 45 | both moved, at least one value disagrees |
| ❌ `SPURIOUS` | 25 | Skala moved, the oracle did not |
| ❌ `INERT` | 0 | |
| ⚠ `UNEXERCISED` | 0 | |
| ⚠ `NO FIXTURE` | 0 | |

⚠ **The unsubstantiated count was 70 before this run and 70 after, and that is a coincidence rather
than a null result.** Two options left the set and two joined it.

**The 25 options this run measured for the first time** had never been swept at all — they were Tier A
on a fixture and nothing else, and the previous run predates them. **23 were right.** The two that
were not, `skala_wrap_lines` and `skala_place_attribute_on_same_line`, are `DIVERGENT`
at their first measurement and are now Tier D, keeping their fixtures so a later sweep can reverse it.

**Two options were promoted.** `indent_size` and `skala_indent_size` moved `DIVERGENT` →
`CONFORMANT`: the formatter improved, and the previous run's demotion had gone stale.

⚠ **Only those two verdicts moved across the 88 commits between the runs.** The third change,
`csharp_new_line_before_open_brace` from `SPURIOUS` to `DIVERGENT`, is the *instrument* and not the
formatter: its enum gained members at `4d518f1f`, so the probe list went from 4 values to 15 and the
oracle that appeared not to move now moves five ways. Nothing about brace placement changed. A diff
of the outcome column invites the opposite reading.

#### What the pairwise run at `603fbd3` measured

**58 pairs, 342 corners**, 1.5 minutes of oracle wall clock, over all three named families.

| outcome | pairs | |
|---|---:|---|
| ✅ `CONFORMANT` | 15 | every corner of the grid agrees |
| ⚠ `INTERACTION` | **0** | |
| ❌ `DIVERGENT` | 0 | |
| ⚠ `INHERITED` | 31 | every disagreement is one a key already owns alone |
| ⚠ `BASELINE` | 12 | the fixture diverged before either key was set |

**No interaction was found in any of the three families.** The `keep_existing_*` family — the
documented four-way table, and the reason this phase was named — is **10 `CONFORMANT` and 3
`BASELINE`**. Its interior agrees with the oracle, which refutes the standing worry for that family.

⚠ **The `wrap_* × max_line_length` question is not answered, it is blocked.** 28 of its 37 pairs are
`INHERITED` and only 96 of their 207 corners agree, because `max_line_length` is itself `DIVERGENT`
and the int probe set gives it `120`, `0` and `1` — two of which are degenerate margins the two
engines already disagree about. The interaction question for that family needs a probe set of three
*legal* margins, which is the same defect as the flags probe above, one type over. It is not fixed
here because the int probe set is shared with the single sweep and moving it moves that table too.

⚠ **A demotion is not an admission that the key is unimplemented.** All 70 stay read and stay
implemented; per [03](03-configuration-model.md) a divergence is Tier D plus an `SK-DIV` entry, and
those entries are the work queue this run produced.

#### ⚠ Tier D is not a work queue, and the part of it that is has never been measured

**306 Tier D** after the demotions. That number is quoted as remaining work and most of it is not.
But the honest breakdown is not the tidy one either — the recorded reasons cover 97 entries, and the
sweep **cannot extend to the rest**: `oracle` is populated for exactly the Tier A entries, so a Tier D
option has no fixture and the key-flip sweep has nothing to flip it against.

| Tier D entries | what the evidence is |
|---:|---|
| 44 | read by the formatter, **measured** inert — `OfInert` in `PhaseOneOptions`, each with the probe recorded |
| 37 | named in a `PhaseOneOptions` prose list with a reason: never read by the C# formatter, masked at the export's own values, or observable-and-not-implemented |
| 15 | `xmldoc` — the oracle does not format doc comments at all (SK-DIV-0006) |
| 1 | an `inert` reason on the registry entry itself |
| 5 | a duplicate spelling whose `resharper_*` sibling is Tier A |
| 10 | `dotnet_*` — Roslyn analyzer keys, never the formatter's |
| 4 | EditorConfig core keys |
| **120** | ⚠ **no recorded reason anywhere** |

⚠ **120 is the number that decides whether finishing is weeks or months, and it is unmeasured.** Not
"observable and not implemented" — *unexamined*: no probe, no fixture, no recorded finding either
way. Its largest families are `indent_*` 11, `space_*` 9, `blank_lines_*` 9, `disable_*` 7,
`prefer_*` 6, `arguments_*` 6. Some fraction will prove unimplementable the way the measured 44 did;
nobody yet knows which fraction, and quoting either 306 or the recorded handful as the remaining work
is a guess in opposite directions.

The instrument that would settle it is the one that settled Tier A: give these entries `oracle`
fixtures and sweep them. That is a **third named phase**, and until it runs, the split above is the
most that can be claimed.

#### ✅ The third phase, part one: 91 of the unexamined, measured

**91 keys of that residue have now been measured** — the 83 Tier D entries with no `oracle` that
belong to no family another agent owns, plus the eight formatter-side members of `BinaryExpression`
and `MemberAccess`. What follows is their map. It is evidence, not inspection: **no key below is
classified on the strength of its name.**

##### The instrument, and why a flat result needed three more passes

The measurement is `ScratchTree.Format`'s technique driven directly — a directory per probe, each
with its own `.editorconfig` carrying the repository's export plus one overridden key, one
`jb cleanupcode` 2025.2.6 invocation per value-index round. Every batch carries two controls: a key
known to move the fixture and a probe with no override at all. **The negative control was flat and
the positive control moved in all six batches**, and every batch was re-run from a harness that
asserts the worktree it is pointed at before it starts; the two runs are byte-identical.

⚠ **The first pass's answer was wrong for 9 of the 91, and the shape of the error is the point.**
Asked once, at the export's own configuration, 76 of the 91 were flat — and "flat" conflates three
different things:

| what flat can mean | how it was separated |
|---|---|
| the C# formatter never reads the key | a **paired control**: a Tier A sibling spelling asked on the *same fixture* under the *same* configuration |
| another key in the export dominates it | the **unmasking pass**: every `keep_existing_*` / `keep_user_*` switched permissive, and adversarial input already carrying the shape one value wants |
| the fixture never reached the rule | **re-cutting** until a control moved; where no control moved, the row says *unresolved* rather than guessing |

Nine keys that read as unreachable at the export's values proved otherwise once unmasked, and the
paired control was decisive the other way for nineteen: a Tier A synonym moved the very fixture the
key left alone. ⚠ **A key and a control that are both flat proves nothing**, and four rows below say
so rather than claiming a verdict.

##### ✅ Observable under the format-only profile — 19

The oracle produces different output at different values, so these are behaviour Skala does not yet
have. **Six of them are gated on a new break point in `BreakPlan.cs`**, a seventh on the fitting pass
that does not exist, and three more on the polarity-aware `expands` the registry already records as
missing. Each is marked as such — that is a real gap, not a dismissal.

| key | verdict |
|---|---|
| `skala_blank_lines_inside_type` | ✅ **implemented** — see below |
| `skala_blank_lines_inside_namespace` | ✅ **implemented** — see below |
| `resharper_csharp_extra_spaces` | implementable — **4 distinct outputs**, the widest in the set; needs a preservation pass that does not exist |
| `csharp_space_around_binary_operators` | implementable, blocked — the polarity-aware `expands` the registry already records |
| `csharp_space_between_parentheses` | implementable, blocked — same missing mechanism |
| `csharp_new_line_before_members_in_object_initializers` | implementable, blocked — same; and only observable on an initializer the single-line joiner does not put back |
| `skala_int_align` | implementable — `IntAlign.cs`, another agent's file |
| `skala_int_align_binary_expressions` | implementable — `IntAlign.cs`, another agent's file |
| `max_line_length` | implementable-with-a-gap — a fitting pass |
| `csharp_preserve_single_line_blocks` | implementable-with-a-gap — forced expansion is a break point |
| `skala_force_chop_compound_if_expression` | implementable-with-a-gap — new break point |
| `skala_force_chop_compound_while_expression` | implementable-with-a-gap — new break point |
| `skala_force_chop_compound_do_expression` | implementable-with-a-gap — new break point |
| `resharper_csharp_nested_ternary_style` | implementable-with-a-gap — **3 distinct outputs**, break point |
| `resharper_csharp_wrap_verbatim_interpolated_strings` | implementable-with-a-gap — **3 distinct outputs**, break point |
| `end_of_line` | masked — `skala_enforce_line_ending_style = false` in the export; the mark on it was already right |
| `skala_max_attribute_length_for_same_line` | masked by `place_*_attribute_on_same_line = false`; moves at `1` once they are `always` |
| `skala_place_simple_list_pattern_on_single_line` | masked by `skala_keep_existing_list_patterns_arrangement = true` |
| `skala_space_in_singleline_method` | ⚠ **masked, not inert** — see the correction below |

⚠ **`skala_space_in_singleline_method`'s recorded reason is wrong and this is the second time.** M9's
`OptionObservabilityTests` already caught it once carrying "a true reason and wiring that contradicted
it". The reason it carries now — "the shape it governs no longer exists" — is a statement about
Skala at this export, and the oracle contradicts it: with
`place_simple_method_on_single_line = true` and `skala_keep_existing_declaration_block_arrangement = true`
the key moves, and so does its Tier A sibling `skala_space_in_singleline_anonymous_method` on the same
fixture. The `OfInert` mark is left in place because `AnInertKey_StillCannotBeObserved` asks about
*Skala*, and that half still holds; what is wrong is the sentence, and the sentence is what the next
agent reads.

##### The arranger's, not the formatter's — 5

`CSReformatCode` alone cannot move a qualifier, a `var`, a modifier order or a redundant
parenthesis, so a flat verdict under the format-only profile says nothing about these. Asked again
under `OracleProfile.Cleanup`, **five moved**:

| key | the cleanup task that moves it |
|---|---|
| `resharper_csharp_force_attribute_style` | `ArrangeAttributes` |
| `csharp_preferred_modifier_order` | `SortModifiers` |
| `resharper_csharp_builtin_type_apply_to_native_integer` | `CSFixBuiltinTypeReferences` |
| `resharper_parentheses_non_obvious_operations` | `RemoveRedundantParentheses` |
| `resharper_csharp_prefer_separate_deconstructed_variables_declaration` | `ArrangeVarStyle` |

They belong to `Arrangement/` and to the cleanup-profile sweep phase, not to this one. ⚠ Note the
split inside `BinaryExpression`: `parentheses_non_obvious_operations` is the arranger's and moves,
while `parentheses_same_type_operations` and `prefer_roslyn_rules_for_parentheses_clarity` are flat
on the *same* cleanup fixture that the first one moves — so the fixture reaches the task and those
two are not read.

##### Duplicate spellings — 19

For each of these a **Tier A sibling moved the same fixture under the same configuration** and this
spelling did not. The registry models them as separate entries with no `aliases` link, which is why
they read as unexamined work; they are the same option under a name the C# formatter does not answer
to.

| key | the spelling that works |
|---|---|
| `resharper_wrap_after_binary_opsign` | `skala_wrap_before_binary_opsign` |
| `resharper_wrap_after_dot` | `skala_wrap_after_dot_in_method_calls` |
| `csharp_space_after_dot` | `skala_space_around_dot` |
| `csharp_space_before_dot` | `skala_space_around_dot` |
| `skala_int_align_eq` | `skala_int_align_variables` |
| `skala_int_align_declaration_names` | `skala_int_align_fields` |
| `skala_int_align_enum_initializers` | `skala_int_align_fields` |
| `resharper_align_multiline_type_parameter` | `skala_align_multiline_type_parameter_list` |
| `resharper_align_multiline_implements_list` | `skala_align_multiline_extends_list` |
| `resharper_wrap_base_clause_style` | `skala_wrap_extends_list_style` |
| `resharper_wrap_ctor_initializer_style` | `skala_wrap_arguments_style` |
| `skala_space_within_new_parentheses` | `skala_space_within_parentheses` |
| `skala_space_within_spread_pattern` | `skala_space_within_slice_pattern` |
| `resharper_remove_blank_lines_near_braces` | `resharper_csharp_remove_blank_lines_near_braces_in_{code,declarations}` |
| `resharper_simple_block_style` | `resharper_csharp_place_simple_method_on_single_line` |
| `resharper_simple_embedded_statement_style` | `skala_place_simple_embedded_statement_on_same_line` |
| `resharper_simple_case_statement_style` | `skala_place_simple_case_statement_on_same_line` |
| `resharper_place_property_attribute_on_same_line` | `skala_place_field_attribute_on_same_line` |
| `resharper_place_event_attribute_on_same_line` | `skala_place_accessorholder_attribute_on_same_line` |

⚠ **The two attribute rows were checked one step further, because "there is no C# spelling" is a
strong claim.** `resharper_csharp_place_event_attribute_on_same_line` — the spelling the registry
does *not* contain — was probed as a control and is flat too, while
`skala_place_field_attribute_on_same_line` moved the same file. So it is not that the
registry has the wrong prefix: the C# formatter has no per-property and no per-event attribute
placement at all, and the export's `skala_place_attribute_on_same_line` expands list, which omits
both, is right.

##### Unreachable — 34

Flat at every value, on a fixture a control demonstrably moved, with no synonym that works. Each row
names the control, because that is the whole of the evidence.

| key | the control that moved the same fixture |
|---|---|
| `resharper_align_ternary` | `skala_int_align_nested_ternary`, `max_line_length` |
| `resharper_indent_aligned_ternary` | same |
| `resharper_outdent_ternary_ops` | same |
| `resharper_wrap_enumeration_style` | `skala_keep_existing_enum_arrangement` |
| `resharper_new_line_before_enumerators` | same |
| `resharper_indent_comment` | `indent_size`, `skala_allow_comment_after_lbrace` |
| `resharper_wrap_comments` | same |
| `resharper_place_namespace_definitions_on_same_line` | `indent_size` |
| `resharper_labeled_statement_style` | `indent_size` |
| `resharper_csharp_use_indent_from_previous_element` | `indent_size` |
| `trim_trailing_whitespace` | `indent_size` |
| `skala_remove_spaces_on_blank_lines` | `indent_size` |
| `resharper_expression_pars` | `skala_wrap_before_binary_opsign` |
| `resharper_continuous_line_indent` | `skala_wrap_before_binary_opsign` |
| `resharper_use_continuous_line_indent_in_method_pars` | `skala_wrap_parameters_style`, `skala_indent_method_decl_pars` |
| `resharper_alignment_tab_fill_style` | `skala_int_align_fields`, on a tab-indented fixture |
| `skala_int_align_fix_in_adjacent` | `skala_int_align_fields` |
| `resharper_blank_lines_around_global_attribute` | `skala_blank_lines_after_using_list` |
| `resharper_dont_remove_extra_blank_lines` | `skala_keep_blank_lines_in_code` |
| `resharper_align_multiline_type_parameter_constraints` | `skala_align_multiline_type_parameter_list` |
| `resharper_align_multiline_type_argument` | same |
| `resharper_align_multiline_ctor_init` | `skala_wrap_arguments_style` |
| `resharper_declaration_body_on_the_same_line` | `resharper_csharp_place_simple_method_on_single_line` |
| `resharper_keep_existing_line_break_before_declaration_body` | same |
| `resharper_treat_case_statement_with_break_as_simple` | `skala_place_simple_case_statement_on_same_line` |
| `skala_static_members_qualify_with` | `..._qualify_members`, under the **cleanup** profile |
| `resharper_csharp_instance_members_qualify_declared_in` | same |
| `resharper_csharp_use_roslyn_logic_for_evident_types` | `csharp_style_var_for_built_in_types`, under **cleanup** |
| `resharper_parentheses_same_type_operations` | `resharper_parentheses_non_obvious_operations`, under **cleanup** |
| `resharper_prefer_roslyn_rules_for_parentheses_clarity` | same |
| `csharp_style_prefer_utf8_string_literals` | the cleanup batch's own `indent_size` control |
| `dotnet_style_prefer_collection_expression` | same |
| `tab_width` | `indent_style`, on a tab-indented fixture — see the note below |
| `skala_tab_width` | same |

⚠ **`resharper_alignment_tab_fill_style` is worse than unreachable, and it is the one generalized
key here.** It `expands` into `skala_alignment_tab_fill_style`, which was probed as a
control and is *also* flat. A generalized key whose only C# target is itself unobservable has nothing
to inherit a claim from; `OfGeneralized` would throw on it today, and correctly.

⚠ **`tab_width` and `skala_tab_width` are inert by construction, and that was measured
rather than argued.** On a tab-indented fixture with `indent_style = tab`, `tab_width = 2` and
`tab_width = 8` returned the same bytes, while `indent_style` flipped to `space` moved it. A tab
width is a *display* width: one indent level is one tab whatever the number says. The reason recorded
in `PhaseOneOptions` — "no tabs in the output" — was true of Skala and would have stopped being true
the day it emitted a tab; the reason above does not expire.

##### Another subsystem's, or nobody's — 9

| key | whose |
|---|---|
| `resharper_apply_on_completion` | the IDE's completion, not a file transformation at all |
| `resharper_default_exception_variable_name` | code generation |
| `resharper_event_handler_pattern_long` | code generation / naming |
| `resharper_event_handler_pattern_short` | code generation / naming |
| `resharper_support_vs_event_naming_pattern` | code generation / naming |
| `skala_configure_await_analysis_mode` | the analyser — it selects an inspection, not a layout |
| `resharper_nullable_enable_for_new_files` | file templates |
| `charset` | flat under both profiles: `cleanupcode` does not re-encode a file |
| `file_header_template` | ⚠ flat **by construction** — both oracle profiles set `CSUpdateFileHeader` to `False`, so no fixture in this repository can ever exercise it |

##### ⚠ Unresolved — 5, and they are reported rather than guessed

| key | why no verdict |
|---|---|
| `resharper_csharp_indent_braces_inside_statement_conditions` | the paired control `skala_align_multiline_statement_conditions` was flat too; the fixture never chopped the condition |
| `resharper_use_indents_from_main_language_in_file` | no control moved on any fixture tried; the name suggests a mixed-language (Razor) key and **that is a guess, which is why it is here** |
| `skala_prefer_wrap_around_eq` | a `string` option with no documented domain — `default`, `true` and `false` were tried and nothing is known to be legal |
| `csharp_prefer_braces` | flat under both profiles, and no control on its own cleanup fixture moved |
| `skala_space_between_keyword_and_type` | its Tier A sibling `skala_space_between_keyword_and_expression` was flat on the same fixture; the oracle closed `typeof (int)` up at **both** values, so something else owns that gap. The `OfInert` reason on it — a type after a keyword is word-like, so the separation is mandatory — is consistent with everything seen and is not *established* by it |

##### What this changes about the 120

The 120-with-no-recorded-reason drops to **29**, and the honest headline is not the drop. Of the 91:

- **19** are real, observable formatter behaviour — but only **two of the nineteen could be built
  without opening something else first**: six need a new break point in `BreakPlan.cs`, three the
  polarity-aware `expands`, two `IntAlign.cs`, one a fitting pass, one a preservation pass, and four
  are masked at the export's own values. Those two are the two implemented here.
- **19** are duplicate spellings of options that are already Tier A. They were never work.
- **34 + 9** are unreachable or another subsystem's/nobody's.
- **5** are the arranger's and belong to that phase's sweep.
- **5** are unresolved and say so.

⚠ So the "weeks or months" question this section opened with resolves, for this slice, in the
direction the pessimists were wrong about and the optimists were also wrong about: **a quarter of it
is real, and the real quarter is concentrated in one file.** Extrapolating the ratio to the rest of
the residue is exactly the guess this measurement exists to stop, and it is not made here.

##### The two that were implemented

`skala_blank_lines_inside_type` and `skala_blank_lines_inside_namespace` were both `OfInert` on the reason
"`remove_blank_lines_near_braces` wins over it by the documented ordering, so no input distinguishes
its values". **That was true of Skala and false of the oracle.** Under this repository's own
`.editorconfig` — which sets `skala_remove_blank_lines_near_braces_in_declarations = true` and
`skala_keep_blank_lines_in_declarations = 2` — `jb cleanupcode` pads a type's braces with three blank lines
at `3` and five at `5`. The requirement outranks both the removal and the cap, which is a fourth
step in `ResolveBlankLines` and not a fourth requirement.

Which bodies have an "inside" was probed body kind by body kind rather than read off the name: class,
struct, interface, record **and enum** — `BaseTypeDeclarationSyntax` — and not a method body, an
accessor list or an `if` block. A file-scoped `namespace N;` gets nothing; it has no braces.

⚠ Both keys already had a `constructs/blank-lines/` file of exactly the right name, committed and
named by no `oracle` glob — a fixture that demonstrated the *removal* and was the evidence for the
inert claim. It was **extended rather than replaced**: the blank line after `{` that showed the
removal is still the first thing in each input, and the bodies that answer "which bodies have an
inside" are added below it.

⚠ **The corpus fixture cannot pin any of that, and a sabotage test proved it.** Both keys are `0` in
the export, so the committed `.expected.cs` is the oracle's answer at `0` — a file that is
byte-identical whether the rule reaches an enum, a namespace, both or neither. Narrowing
`BaseTypeDeclarationSyntax` to `TypeDeclarationSyntax`, which drops `enum`, left all 687 conformance
tests green. `BlankLinesInsideDeclarationTests` is what actually holds the shape, and the same
sabotage fails it. That is the general lesson for every option whose export value is the identity:
**a fixture at a value where the option does nothing is not evidence that the option does the right
thing**, and it is the same one-configuration fallacy § "The key-flip sweep" opens with.

`verify` reports `Conformant` for both — 3 distinct outputs from each engine, agreeing at 3 of 3
values — which is the sweep's own instrument rather than fixture agreement, and is what the Tier A
on them rests on. The committed sweep has never reached either key; the next run on master is what
confirms or reverses it.

#### ⚠ Interactions are out of the single sweep's scope, and have their own pass

One key at a time isolates cleanly, which is what makes a verdict a statement about *that option*. It
is also **provably incomplete**: § "`keep_existing_*`" in [05](05-csharp-formatting-rules.md) is a
four-way table across **two** keys, and no one-at-a-time sweep can reach three of its corners. A
family whose members interact can come back all-`CONFORMANT` and still be wrong in combination.

**`./build.sh Pairwise` is that second phase, and it exists.** `pairwise [--family=keep,wrap,align]`
sweeps the whole grid of two keys' values against the oracle, over the three families named above.
Its output is `conformance-pairwise.md` and a `.json` beside it, committed and reviewed in a diff
exactly as the single sweep's are.

⚠ **Its verdict has a fourth value the single sweep does not have, and that value is the point.**

| | verdict |
|---|---|
| every corner of the grid agrees | ✅ `CONFORMANT` |
| **every corner the single sweep reaches agrees, and an interior corner does not** | ⚠ **`INTERACTION`** |
| a corner disagrees, and the single sweep could have seen it too | ❌ `DIVERGENT` — that sweep already owns it |
| neither engine distinguished the corners | ⚠ `UNEXERCISED` — not a pass, same as there |
| every disagreement is one a key of the pair already owns alone | ⚠ `INHERITED` — not about the pair |
| the two engines already disagreed on the fixture before either key was set | ⚠ `BASELINE` — the grid answers nothing |

⚠ **What "reachable" means is narrower than it first looks, and getting it wrong voids the pass.** The
single sweep flips one key and leaves the rest at the export's value — which sounds like it covers the
grid's whole cross. It does not, because that sweep measures each key **on that key's own `oracle`
fixture**. On the primary's fixture it visits the primary's values against the secondary's export
value and nothing else; the column is measured on the *secondary's* fixture and says nothing about
this one. So half a bool × bool grid is interior, not a quarter of it, and
`PairwiseSweep.ReachedBySingleSweep` turns on the **secondary alone**.

⚠ The first implementation had it as "either key at the export's value". Measured cost of that error:
58 disagreeing corners at (primary at export, secondary moved) were classified reachable and reported
`DIVERGENT` — filed as duplicates of rows `conformance-sweep.md` already carries, when nothing had
ever measured them. **The pass reported zero interactions on its first run and the zero was an
artefact of that line.** It is pinned by a test now.

⚠ **`INHERITED` is what stops the pass inventing findings, and the first corrected run needed it
immediately.** That run reported **17 `INTERACTION` rows** across the `wrap_*` family — and every one
of them disagreed *only* where `max_line_length` was `0` or `1`, which are the two values at which
`conformance-sweep.json` records `max_line_length` disagreeing **measured alone, on its own fixture**.
Seventeen findings, one cause, and none of it about a pair; each would have been handed to somebody as
a subtle two-key defect. The pass now reads the single sweep's per-value agreement — recorded in the
archive for exactly this — and excuses a corner where either key is already known to diverge at the
value that corner gives it. ⚠ **Per corner, and every disagreement must be excused for the row to be**
`INHERITED`: one unattributable corner keeps it a finding, or a known divergence elsewhere in the grid
would hide a real interaction.

⚠ A key the single sweep never measured at that value excuses **nothing**. "Never measured" and
"measured and disagreed" are opposite states, and collapsing them is how a pass stops finding
anything.

⚠ **`BASELINE` exists because the first run needed it too**: 17 disagreeing corners sat at the base
configuration itself, both keys at the export's value and nothing set. `KeyFlipSweep` meets the same
case and handles it more weakly — it records `BaselineAgrees` and writes the caveat into the reason
text beside a `DIVERGENT` verdict. That is tolerable in a table of 283 rows read down a column; it is
not tolerable in a pass whose whole product is a handful of interior findings, where 43 inherited
divergences would bury them.

⚠ **It is a hypothesis list, not a search.** 283 sweepable options is 39 903 pairs. The three
families are the ones the design already says interact, `keep_existing_*` because M2 measured its
four-way table by hand. A fourth interacting family is a plan change, not a bigger run.

⚠ **`skala_keep_existing_linebreaks` is excluded as a primary.** [05](05-csharp-formatting-rules.md) warns
that it "reads like one of the family and is not" — it is the per-language form of the global
`keep_user_linebreaks`, so pairing them would measure a key against itself and report a guaranteed
interaction meaning nothing.

#### Arrangement options: the second phase, now built

Until this section was rewritten the sweep ran the **format-only** profile and nothing else, so its
output was byte-identical whatever an `arrange_*` or `csharp_style_*` key said and on Skala's side it
ran the formatter rather than the arranger. Sweeping those keys under that profile would have
reported every one of them `SPURIOUS`: the harness inventing divergences rather than finding any. So
all 44 were excluded by name — and the consequence, which the exclusion did not say, is that **15 %
of the Tier A claim rested on the hand-transcribed flipped-value readings in
`ArrangementOptionTests`**, which is the standard of evidence this sweep exists to replace.

That was a fact about the profile, and the profile is a parameter. The fixture now chooses it —
`OracleProfile.For`, read by *both* halves, so the oracle picks a `cleanupcode` profile and
`SkalaSide` picks between `CSharpFormatter` and `ArrangementPipeline` from the same answer. Two
spellings would compare an arranged output against a formatted one and blame the flipped key.

Three things the semantic profile needs that the whitespace profiles did not:

- **A batch holds each fixture at most once.** 44 keys point at 22 fixtures, so four of them name
  `redundancy/qualifiers-and-parentheses.cs`; a count-batched round would put four copies of one file
  in one scratch project and read `var`, qualifier and predefined-type rewrites off a compilation
  full of CS0101.
- **The whole subtree travels with the batch.** `usings/sort-and-remove.cs` imports `Alpha.Things`,
  which exists only because `usings/namespaces.cs` declares it. Without the context file the oracle
  deletes the import as unresolvable at *every* value and `skala_sort_usings` reads `DIVERGENT`
  at 0 of 2 — a verdict about the scratch directory.
- **The oracle runs to a fixed point, because Skala's half does.** Measured, not assumed:
  `sweep fixed-point` runs `cleanupcode` over the subtree and again over its own output. 27 of 27
  files move on pass 1; **one** moves again on pass 2 — `namespaces/file-scoped.cs`, where converting
  a block-scoped namespace leaves a blank first line that only a further invocation removes. That is
  the fixture `csharp_style_namespace_declarations` is pinned by, so a single-invocation oracle
  manufactures a divergence for exactly the key whose fixture exposes the defect: `DIVERGENT` at 0 of
  2 without the loop, `CONFORMANT` at 2 of 2 with it. Skala reaches its own fixed point in two passes
  on all 27.

⚠ **The canaries are counted per profile, not per round.** A round now holds three populations
answered by three profiles. Pooled, 44 arrangement options that answered nothing sit inside a round
of 378 whose whitespace half moved normally — `moved > 0`, both canaries silent, and 44 rows of
universal agreement about a profile that never ran. That is the shape `ScratchTree.ProfileFor`'s
remarks record for the doc-comment family, and a pooled count cannot see it.

⚠ **The pairwise pass is still excluded**, and now on its own reason rather than the single sweep's:
`PairwiseSweep.Run` batches by count without partitioning by profile, so an arrangement pair would
either trip the mixed-profile guard or land in a project holding two copies of one fixture. That is a
gap in the pairwise table, not a claim about the keys.

### 3. Properties — where the real bugs are

Run over every corpus file, every commit, and over generated input nightly:

| Property | Statement |
|---|---|
| **Idempotency** | `format(format(x)) ≡ format(x)`, byte-identical |
| **Token equivalence** | significant tokens of `format(x)` ≡ those of `x` ([04](04-formatting-engine.md)) |
| **Parse stability** | `format(x)` parses with the same diagnostics as `x` |
| **Range consistency** | `format(x, range)` ≡ `format(x)` restricted to that range's edits |
| **Determinism** | three runs, three thread counts ⇒ identical bytes |
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
| SK-FUZZ-0006 | pair idempotency | a comment between two usings, one of which carries interior whitespace: SK0210 applies and the second pipeline pass still wants an edit | 45 B |
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

⚠ **There are none, and the budgets they asserted are withdrawn.**

This section used to say that budgets from [13](13-performance.md) were asserted in CI with a 20 %
tolerance band, and M7 built exactly that: `Tools/Rikarin.Skala.Cli.Tests/PerformanceBudgetTests.cs`,
in its own CI job on its own runner, opt-in by `SKALA_PERF=1` so a contributor's `dotnet test` never
tripped it. Three rows — cold single file, warm single file, daemon RSS.

All three are deleted, with the daemon and the thin client they measured. The tightest budget in
doc 13 served a format-on-save consumer that does not exist; Skala runs ahead of test suites that
take about twenty minutes. Doc 13 § "Budgets" carries the withdrawal and the reason.

⚠ **A budget nothing asserts must not be left in a document as though something did.** That is the
failure this repository keeps hitting — a claim outliving its measurement — and it is why the tests
and the table were withdrawn in the same commit rather than one and then the other.

What is left below is the harness lesson, which is about measuring anything at all and does not
depend on there being a budget.

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

⚠ **And a performance test must prove it measured the thing it names.** The warm row asserted the
daemon's hit counter had moved before it believed its own number. Without that it measured **218 ms**
and reported it as a slow warm path; the truth was that the bed was not a git repository, so there
was no repository root, so there was no socket to look for, so the client execed the full tool every
time. A test that cannot tell "slow" from "not running" is not a test — and that generalises well
past performance.

## Cross-platform

The full suite runs on macOS, Linux and Windows. The Windows-specific hazards are enumerated and
each has a test: CRLF input with `end_of_line = lf`, paths in SARIF (must be repo-relative with
forward slashes), case-insensitive path comparison in the cache key, and long paths. ⚠ A fifth,
"the named-pipe daemon transport", is struck: there was never a named-pipe transport to test, and
there is no longer a daemon either.

### ✅ M7: the matrix, and what writing the five tests found

`.github/workflows/cross-platform.yml` — `dotnet test` over the whole solution on `ubuntu-latest`,
`macos-latest` and `windows-latest`, `fail-fast: false`, plus a `lint` job that CI was running
nowhere. (A `performance` job landed here too and has since been deleted with the budgets.) It is a separate file from `skala.yml` because that workflow's verdict
is one `skala check` exit code and a four-job conjunction would make "did the gate pass"
unanswerable from the workflow's result.

⚠ **Three of the five hazards were real defects, not hypotheticals.**

| Hazard | Test | Found |
|---|---|---|
| CRLF under `end_of_line = lf` | `Tools/…Cli.Tests/LineEndingTests.cs` | ⚠ **`end_of_line` is inert on its own.** The key that converts line endings is `skala_enforce_line_ending_style`, `false` by default; `end_of_line = lf` alone leaves CRLF exactly as it found it. A test written from this document's own headline would have asserted the wrong thing |
| SARIF paths repo-relative, forward slashes | `Tools/…Cli.Tests/SarifPathTests.cs` | ⚠ `SarifWriter.Relative` compared case-sensitively, had no component boundary, and took a non-nullable root that callers reach with a nullable one — all three printed absolute paths |
| Case-insensitive path in the cache key | `Analysis/…Tests/CacheKeyPathTests.cs` | ⚠ **The key hashed the path's raw UTF-8**, so `C:\Src\A.cs` and `c:\src\a.cs` — one file on every Windows volume and on a default macOS volume — produced two entries. Benign in direction (a miss, never a stale hit) and therefore invisible for four milestones, but *permanent*: paths from MSBuild and paths from a directory walk never share an entry, so the warm run [13](13-performance.md) budgeted at under 5 s was a cold one every time (that budget is now withdrawn) |
| Long paths | `Tools/…Cli.Tests/LongPathTests.cs` | 403-character path. Asserts the finding *appears*, not merely that nothing threw — the dangerous failure is swallowing `PathTooLongException` and reporting a clean tree |
| ~~Named-pipe daemon transport~~ | ~~`Tools/…Server.Tests/MemoryPolicyTests.cs` § `SocketPathTests`~~ | ⚠ **There was no named-pipe transport.** Both ends built `AddressFamily.Unix` unconditionally and only a comment in `Daemon.Restrict` claimed otherwise, so the hazard had nothing to test. The daemon is now deleted and so is the test |

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
