# Divergences from the oracle

`jb cleanupcode` is the conformance oracle (ADR-011), not a master. Where Skala deliberately
differs, the difference gets an `SK-DIV-` number and the argument for it lives here. The count is
published alongside the fidelity number, because **a divergence is a decision and an unexplained
difference is a bug, and the harness cannot tell them apart without this file**
([12](plan/12-conformance-and-testing.md) § "Where the oracle is wrong").

Format: `## SK-DIV-nnnn — one line`, then the argument, then the option keys it touches.

⚠ **Re-measured in full at `8cbd66d`.** Every number in this file below this line was produced by
running the harness at that commit rather than carried over from the milestone that first recorded
it; where an entry's class cannot be isolated by any instrument the repository ships, it says so
instead of repeating an old figure. The commands are named beside the numbers.

`corpus/real/` is **99.70 %** of lines and **85.79 %** of files identical to the oracle over 380
files and 76 375 lines, with the oracle's own preprocessor symbols supplied — **99.63 % / 85.26 %**
without them. ⚠ Both numbers are reported because both are true of a real invocation: `skala format`
on a loose file has no symbols and `skala format --load=binlog` has them, and `./build.sh Fidelity`
prints the pair.

⚠ **Those two figures, and every per-class count below them, are over `every line` and predate the
documentation-comment default.** They are kept because the classes they rank are still the work
queue and re-deriving each one is a separate measurement. The differential's basis is now
`outside doc comments` (SK-DIV-0006), and on that basis the same corpus at the same commit is
**99.61 % / 85.79 %** with symbols and **99.53 % / 85.26 %** without. The gap is `///` lines leaving
the denominator, where they had been counted as agreeing because neither side touched them — not a
regression, and not an improvement either. ⚠ Read any unqualified percentage in this file as
`every line` unless it says otherwise.

```
dotnet run --project Testing/Rikarin.Skala.Testing -c Release -- fidelity
dotnet run --project Testing/Rikarin.Skala.Testing -c Release -- preprocessor
```

| Files | Line fidelity | File fidelity | Divergent lines | What the residue is |
|---|---:|---:|---:|---|
| all (380) | **99.70 %** | **85.79 %** | 231 | 54 files diverge |
| containing a `#if` (91) | 99.36 % | 72.53 % | 106 | SK-DIV-0001 and ordinary tail |
| containing a raw literal (11) | 99.68 % | 90.91 % | 12 | SK-DIV-0003's interpolated half |
| no `#if` (289) | **99.79 %** | 89.97 % | 125 | SK-DIV-0005 and SK-DIV-0011, mostly |
| neither (279) | 99.78 % | 89.61 % | 125 | |

Per origin, because the three measure different things
([`Testing/corpus/real/NOTICE.md`](../Testing/corpus/real/NOTICE.md)):

| Origin | Files | Line | File | Divergent lines |
|---|---:|---:|---:|---:|
| `vixen/` | 200 | 99.81 % | 90.00 % | 97 of 51 527 |
| `newtonsoft/` | 110 | 99.41 % | 80.91 % | 97 of 16 323 |
| `serilog/` | 70 | 99.57 % | 81.43 % | 37 of 8 525 |

⚠ **`vixen/` is the flattering third and it is the one to read last.** Those 200 files were already
formatted by Rider under this `.editorconfig`, so their fidelity measures "does Skala leave
conforming code alone". Serilog and Newtonsoft.Json are formatted to their own houses' styles and
measure the harder thing. Neither tree is a specification —
[16](plan/16-risks-and-open-questions.md) § "The reference trees are a test subject, not a
specification" — and a divergence is not excused by the corpus not containing much of it.

⚠ **The revised milestone-3 bar of ≥ 99.5 % on files with no `#if` is met at 99.79 %. The ≥ 99.9 %
overall bar is not met at 99.70 %,** and the entries below are what stands between the two:
**231 divergent line slots across 54 files** — `fidelity` reports 54 of 380 identical at 326, and
the "51 files" this paragraph carried from milestone 3.1 is superseded. Of those, 106 are in a file
that also contains a `#if`, and most of that is ordinary tail rather than preprocessor-shaped.

The ranked classes the harness itself reports, which is the work queue:

| Lines | Files | Class |
|---:|---:|---|
| 63 | 38 | wrapping: one side continues where the other broke (phase 3) |
| 47 | 22 | other |
| 45 | 21 | line break presence: Skala left a line the oracle joined (phase 2) |
| 35 | 18 | wrapping: the oracle broke a line Skala left long (phase 3) |
| 25 | 8 | indentation (−4 columns) |
| 11 | 11 | blank line: Skala has one, the oracle does not |
| 7 | 6 | blank line: the oracle has one, Skala does not |
| 5 | 4 | brace placement |
| 2 | 1 | indentation (+4 columns) |
| 1 | 1 | inter-token spacing |

⚠ **These classes are not the entries below, and nothing maps one onto the other.** A class says what
a difference *looked like*; an entry says what rule produced it. The repository ships `constructs`
and `locate` to attribute a divergence to a syntax node, and it ships nothing that attributes one to
an `SK-DIV` number. That is why several entries below carry a class and a signature rather than an
exact count, and saying so is better than repeating a figure nobody can reproduce.

## The register at a glance

⚠ Every row re-measured at `8cbd66d`. "Signature" means a lexical test over the diverging files that
`dump real <dir> defined` writes; it is stated in the entry so the number can be re-derived.

| | Entry | Status | Current measurement |
|---|---|---|---|
| 0001 | oracle rewrites disabled `#if` whitespace | open | 14 lines, 14 files |
| 0002 | no preference among break points | **resolved at M3** | — nothing left to measure |
| 0003 | interpolated raw literal emitted verbatim | open | 11 raw-literal files at 99.68 % / 90.91 %, 12 divergent lines |
| 0004 | no preprocessor symbols without a project | **closed at M5** | the six-cell table re-run and identical |
| 0005 | the ordering rule's margin is a fitted constant | open | 63 lines, 38 files — the largest class. 21 hunks, 14 files by signature |
| 0006 | ⚠ the pinned oracle *profile* does not format doc comments; Rider does, and Skala now does too | open | 21 keys honoured and observable, 11 refused, none Tier A until the fixtures are regenerated |
| 0007 | an argument list around a broken chain does not chop | half closed | 8 hunks, 39 lines, 6 files |
| 0008 | alignment keys | half closed | statement conditions Tier A; `for` header 5 hunks, 13 lines, 3 files; three keys at 0 lines |
| 0009 | `space_within_spread_pattern` is inert | **resolved at M3.1** | Tier D confirmed; 0 spread-spacing divergences |
| 0010 | breaks of last resort | open, deliberate | 5 lines, 4 files; 2 hunks by the narrow signature |
| 0011 | a lambda body may leave the arrow's line | open | 12 hunks, 40 lines, 7 files — second largest |
| 0012 | three small shapes | open | 1 line / 0 lines / 13 lines respectively |

**Three are resolved or closed (0002, 0004, 0009); two are half closed (0007, 0008); the other seven
are open, and two of them — 0005 and 0011 — are between them most of the residue.**

⚠ **Two entries carried a number that was the enclosing *class*'s rather than their own**, and the
audit found it by re-measuring rather than by re-reading: SK-DIV-0011's "45 lines across 21 files" is
the phase-2 class it shares with SK-DIV-0007, and SK-DIV-0010's "12 lines across 4 files" is the
brace-placement class, now 5. Both are corrected above. The general lesson is the one the header
already gives: the harness attributes a difference to a *shape*, never to an entry, so any entry-level
count in this file is a signature and has to say which one.

The trajectory, so that "asymptotic" is a measurement rather than an adjective:

| | line | file | corpus |
|---|---:|---:|---|
| M1 | 85 % bar | — | 380 files |
| M2 | 97.47 % | 49.47 % | 380 files |
| M3 | 98.86 % | 71.05 % | 380 files |
| M5 | 98.93 % | 71.58 % | 380 files, symbols supplied |
| M3.1 | 99.70 % | 85.79 % | 380 files, Vixen sample re-based |
| **`8cbd66d`** | **99.70 %** | **85.79 %** | 380 files — unchanged through M6 and M7, which added no formatter work |

---

## SK-DIV-0001 — the oracle rewrites whitespace inside disabled `#if` text; Skala never touches it

ReSharper edits blank lines inside an inactive preprocessor branch. On
`corpus/real/newtonsoft/…/DeserializeComparisonBenchmarks.cs` it deletes the blank line between
`#if HAVE_BENCHMARKS` and the first disabled `using`, and it will reindent disabled lines given the
chance. It also *adds* one: `using …LinqBridge;` immediately above a `#else` comes back with a blank
line between them, and for the oracle that `using` is disabled text.

Skala does not. Roslyn hands the inactive branch back as `DisabledTextTrivia`, an unstructured
string; Skala emits it byte-for-byte and copies every gap that touches it verbatim
([04](plan/04-formatting-engine.md) § "Trivia"). The reason is not fastidiousness — it is that the
disabled branch is the branch nobody compiled, so nobody would notice if it were mangled, which is
exactly the property that makes mangling it unacceptable. A formatter that edits code it cannot
parse is a formatter that will eventually edit it wrongly.

Measured on `corpus/real/`: 141 lines across 73 files at M2; 18 lines across 17 files at M3.1.

⚠ **Current, at `8cbd66d`: 14 lines across 14 files.** Signature: a hunk in the `dump real … defined`
output whose whole content on both sides is blank lines, and where a preprocessor directive appears
within two lines of it. The whole blank-line residue is 18 lines across 17 files
(11 "Skala has one, the oracle does not" + 7 the other way, from `fidelity`), so **four blank-line
differences in three files are not next to a directive and belong elsewhere.** The class shrank
because the rest of the tail shrank around it, not because it changed.

⚠ **Re-measured 2026-08-30, and the oracle's edit is worse than this entry recorded.** On a probe
carrying an inactive `#if HAVE_BENCHMARKS` branch, `jb cleanupcode` under `SkalaFormatOnly` deletes
the blank line after the opening directive *and* rewrites the spacing of the code inside:

```
   int    y   =   2 ;      →      int    y = 2 ;
```

That is the whole of the argument in one line. It normalised the runs around `=` and left the run
after `int` and the three-space indent exactly where they were, because there is no tree to walk —
it is a spacing rule fired at text nobody parsed. A formatter that edits code it cannot see will
eventually edit it wrongly, and the evidence that it already does is in the output.

⚠ **And Skala is now consistent about it, which it was not.** The one hole was
`skala_blank_lines_around_region` firing on the gaps around a `#region` that stands *inside* the inactive
branch: Roslyn keeps such a directive structured, so the piece stream could not tell it from one
governing real code, and the blank line Skala wrote there re-parsed as a `DisabledTextTrivia` that
had not been in the input — SK-FUZZ-0016, and the file aborted with SK9099. `Piece.Inactive`
(`DirectiveTriviaSyntax.IsActive`) and `EmitGap`'s `TouchesInactiveBranch` close it: every piece of
the branch is opaque whether or not Roslyn kept it structured. Re-measured on a probe with two
`#region` pairs and a nested `#if` inside an inactive branch, live members either side and an
`#else` arm that *is* active: Skala reformats every active line, including the `#else` arm, and
returns the inactive branch byte for byte. The only remaining difference from the oracle is the
oracle's own edit inside the disabled text.

- options: `skala_keep_blank_lines_in_code`, `skala_keep_blank_lines_in_declarations`
- ⚠ status: **open**, measured
- ⚠ **triage 2026-08-30: `deliberate`.** The argument does not need the oracle to state it: *the
  inactive branch of a `#if` is text the compiler never sees, so an edit to it can only be checked
  by a human reading it, and the whole point of a formatter is that nobody reads its output.* It is
  the same instinct as ADR-003's byte-identical unparseable file, and this repository has already
  paid once for the converse — SK-FUZZ-0016 is what happens when one rule reaches inside. Skala
  keeps the branch, and the entry survives the oracle's retirement as a stated property rather than
  as a difference from a tool.

## SK-DIV-0002 — resolved at milestone 3; kept for the number it recorded

Milestone 2 knew every candidate break point of a long line and had no preference among them, so it
left the line whole rather than break at the first point it could reach — measured, breaking at the
first available point cost 0.24 points of line fidelity against leaving it alone.

Milestone 3 implements the ordering (`GroupFacts.PrefersOuterBreak`), and the class this entry named
is gone. What replaced it is SK-DIV-0005, which is a much smaller and much better characterised
thing. The measurements are kept because the trajectory is the argument for the design:

| Milestone | lines | files | % of corpus |
|---|---:|---:|---:|
| M1 | 3 180 | 205 | 4.1 % |
| M2 | 747 | 175 | 0.97 % |
| M3 | — | — | — (see SK-DIV-0005) |

- ⚠ status: **resolved at M3.** Confirmed still resolved at `8cbd66d`: `GroupFacts.PrefersOuterBreak`
  exists and the whole corpus residue is 231 lines, an order of magnitude below the 747 this entry
  named at M2. There is nothing left to re-measure.

## SK-DIV-0003 — an interpolated raw string literal is still emitted verbatim

`skala_indent_raw_literal_string = align` asks the formatter to move the closing
delimiter and the content of a `"""` literal, and milestones 1 and 2 declined on the grounds that
the transformation changes the string's value if it is done wrong.

Milestone 3 does it, for the uninterpolated case, because there is a form of it that *cannot* be
done wrong: C# strips the closing delimiter's own whitespace prefix from every line, so a **uniform
shift** — every interior line and the closing delimiter by the same number of columns — leaves the
stripped result identical, character for character. The token-equivalence check would abort the file
if that were untrue.

⚠ What remains is the **interpolated** literal, and the plain interpolated string with it.
`$"""…{x}…"""` is not one token but a run of them with expressions between, and it stays on the
verbatim path [04](plan/04-formatting-engine.md) puts it on — "where a moved space changes the
value". The option is Tier A on the strength of
`constructs/trivia/skala_indent_raw_literal_string.cs`, and this entry is what its Tier B
caveat in doc 04 was pointing at.

Measured on `corpus/real/`: files containing a raw literal went 94.41 % (M1) → 97.81 % (M3) →
99.68 % of lines and 90.91 % of files at M3.1, over the re-based sample's 11 such files.

⚠ **Current, at `8cbd66d`: unchanged — 99.68 % of lines and 90.91 % of files over 11 files, 12
divergent lines of 3 744.** Measured over the `dump real … defined` output by selecting the files
whose oracle text contains `"""`, which reproduces the harness's own line-fidelity arithmetic
(matched lines over `max(len(oracle), len(skala))`) and its file test (the two texts are equal).
The option is Tier A in the registry, confirmed. `corpus/pathological/`'s
`interpolated-raw-string-with-nested-braces.cs` is **14.29 % of lines and 0 % of files** and is the
single worst file in any corpus — which is this entry, in its purest form and on purpose.

⚠ **C# 11 made this reachable from ordinary code and it broke a property test rather than the
formatter.** A newline is legal inside an interpolation hole, so a multi-line interpolated string is
now something people write; `PropertyTests.MutateIndentationOnly` walked into one — it is a run of
tokens rather than one token, so the per-token guard missed it — and added whitespace that neither
Skala nor the oracle absorbs. The mutation now leaves the whole expression alone, the same way it
already left raw strings and disabled text alone.

⚠ **Re-measured 2026-08-30, and the recorded argument does not survive it.** The entry's reason for
the interpolated case is "where a moved space changes the value". It does not. Asked directly:

| shape | oracle | Skala |
|---|---|---|
| `"""…"""` at column 24, opener moved to 20 | content and delimiter shifted −4 | the same, byte for byte |
| `$"""…"""` at column 24, opener moved to 28 | content and delimiter shifted +4 | **unchanged** |
| `$"""` with a multi-line interpolation hole | the literal's own lines shift; the **hole's** lines do not | unchanged |
| `$"""` with a nested `$"""` inside a hole | outer shifts +1, inner shifts +7 to its own anchor | unchanged |
| `@"…"` and `$@"…"` | untouched | untouched |

The stripping rule C# applies to a raw literal — the closing delimiter's whitespace prefix is
removed from every content line — holds for an interpolated literal exactly as it holds for a plain
one, so a shift that is uniform *within one literal* is value-neutral in both. The oracle performs
it, on a nested literal too, and the result compiles to the same strings. **What is missing is
machinery, not a proof.** An interpolated raw literal is not one token but a run of them:
`LayoutWriter.ShiftRawLiteral` rewrites one token's text, and reaching this case means shifting only
the literal's own text tokens, leaving the lines that belong to an interpolation hole alone,
recursing into a nested literal in a hole against *its* anchor, and teaching the per-token
equivalence guard about all three.

⚠ The `@"…"` half of this entry is **not a divergence at all** and should stop being carried as one.
A verbatim string has no stripping rule, so its content is literal and neither engine can move it;
both return it untouched. Only the raw-literal half is open.

- options: `skala_indent_raw_literal_string` (Tier A, `default = align`, from the template)
- ⚠ status: **open**, measured
- ⚠ **triage 2026-08-30: `debt`, size M.** Not defensible to a user: "Skala aligns your raw string
  literals, except the interpolated ones" is a bug report, not a policy, and the entry's own
  justification is refuted above. It costs 12 lines over 11 files of `corpus/real/` and owns the
  single worst file in any corpus (`pathological/interpolated-raw-string-with-nested-braces.cs`, at
  14.29 % of lines and 0 % of files). Paying it: extend the shift from a token to an
  `InterpolatedStringExpressionSyntax`'s text-token run, anchor each nested literal separately,
  exclude hole lines, and widen the equivalence guard's raw-literal exemption to the run. ⚠ Needs
  the oracle, for the fixtures — the four rows above are the specification and are recorded here so
  that at least the *shape* survives its retirement.

## SK-DIV-0004 — ✅ closed at milestone 5, and the residue is not preprocessor-shaped

`skala format --define A,B` supplies preprocessor symbols, and `--load=binlog|workspace` takes them
from what the build actually compiled. `fidelity preprocessor` measures the result against the same
symbol set the oracle itself had — read out of a real binary log of the same scratch project
`OracleRunner` builds, rather than from a list someone typed, which makes the measurement and the
binlog loader test each other:

```
DEBUG TRACE NET NET10_0 NETCOREAPP
NET5_0_OR_GREATER … NET10_0_OR_GREATER
NETCOREAPP1_0_OR_GREATER … NETCOREAPP3_1_OR_GREATER          (18 symbols)
```

| `corpus/real/` | no symbols | with symbols |
|---|---:|---:|
| the 91 files containing a `#if` | 99.04 % line / 70.33 % file | **99.36 %** / 72.53 % |
| the 289 that do not | 99.79 % / 89.97 % | 99.79 % / 89.97 % |
| overall (380) | 99.63 % / 85.26 % | **99.70 %** / 85.79 % |

⚠ **The branches nobody compiles stay frozen for both tools.** The oracle had these eighteen symbols
and no more, so `#if HAVE_BENCHMARKS` in Newtonsoft is disabled text for ReSharper too. The entry is
closed in the sense that Skala now sees whatever the oracle sees; it does not follow that either of
them formats every branch, and neither does. Measured at M3.1: of 271 divergent line slots,
**27 of 271 sat inside a branch neither tool compiles** when it was last attributed — the rest of the `#if` files' residue is ordinary
tail that happens to live in a file that also has a `#if` in it.

⚠ **The symbols also uncovered a formatter bug that had been invisible**, which is why the
differential now runs under both symbol sets by default. With `#if` bodies live, `count > (n)` came
back as `count >(n)`: `IsCallSite` treated every `>` as a type-argument close. Every corpus line
that shows it is inside a `#if` body. See [12](plan/12-conformance-and-testing.md) § "Both symbol
sets"; the report's closing section names the divergences that appear under one and not the other,
and at M3.1 it reads **0 with-symbols-only, 65 without**.

⚠ **Current, at `8cbd66d`: the table above is a re-run, not a copy.** `preprocessor` reads the
eighteen symbols out of `artifacts/skala.binlog` through the loader `skala check` uses and prints
the six cells; all six reproduce M3.1's exactly. The both-symbol-set line reads **0 with-symbols-only
and 65 without**, also unchanged, and all 65 are in one file
(`newtonsoft/Newtonsoft.Json.Tests/Issues/Issue2504.cs`).

- options: none
- commands: `skala format --define`, `skala format --load=`, `fidelity preprocessor`
- ⚠ status: **closed at M5**, re-verified

## SK-DIV-0005 — the ordering rule's margin is a fitted constant, and the sweep says it is not a rule

Milestone 3's ordering rule (`GroupFacts.PrefersOuterBreak`) decides which of a long line's
candidate points is wrapped at. Its first question is "does this break alone finish the job", and
the budget that question is asked against is **not** `max_line_length`: the oracle stops taking the
`=` break well before the continuation line reaches 120, and the result it declines fits with room
to spare.

⚠ **Milestone 3 read a formula off three cells and milestone 3.1 swept it properly.**
`Testing/Rikarin.Skala.Testing/MarginSweep.cs`, run as `margin`, writes
`var <name> = <rhs>;` with the right-hand side padded to a known length and the *name* padded so
that the flat line comes to a chosen total — which sweeps the continuation width independently of
how far over the margin the line was, something the milestone-3 experiment could not do. Eleven
right-hand-side shapes, five block depths, both values of `skala_wrap_before_eq`, one character at a time.
The result is in [sk-div-0005-margin-sweep.md](sk-div-0005-margin-sweep.md), and it contradicts the
milestone-3 note in three ways:

1. **The threshold does not depend on the nesting depth.** At a flat width of 121 the last
   continuation line the oracle still writes is 112 columns at block depth 2, 3, 4, 5 and 6 alike.
   The `column / indent` term milestone 3 derived was read off three cells that were confounded with
   the shape's own width.
2. **It does depend on the flat width, and not monotonically.** Same shape, same depth, sweeping the
   flat width: 122 → 113, 124 → 115, 126 to 140 → 116, then back down, 146 → 112, 158 → 107.
3. **It depends on the shape.** At a flat width of 137 and depth 2 the threshold is 116 for
   `Convert.FromBase64String("…")`, 117 for a call on an identifier, 118 for a binary chain, 120 for
   a cast; an object initializer and an array initializer go the other way, 107 and 101. And under
   `skala_wrap_before_eq = true` the whole table moves down by two to four columns.

**So the constant stays, and it is now honestly a fitted constant rather than a derived one.**
Fitted against `corpus/real/` with everything else in the ordering rule held fixed:

| Rule | line | file |
|---|---:|---:|
| never prefer the outer break | 99.11 % | 76.05 % |
| margin 0 | 99.02 % | 72.37 % |
| margin 4, a constant | 99.37 % | 78.95 % |
| margin 8, a constant — what the sweep supports | 99.42 % | 80.00 % |
| `8 + column/indent` — milestone 3's | 99.51 % | 82.11 % |
| **`11 + column/indent` — ships** | **99.53 %** | **82.63 %** |
| `16 + column/indent` | 99.51 % | 81.84 % |
| `24 + column/indent` | 99.48 % | 80.79 % |

⚠ **And the two measurements disagree, which is the finding worth carrying forward.** The isolated
sweep says the threshold is depth-independent and near 112 — that is the `margin 8` row, and it is
0.11 points and eleven files *worse* on real code than a depth-dependent constant the sweep does not
support. The margin is therefore absorbing error from the rest of the ordering rule rather than
reproducing a rule of ReSharper's, and no value of it closes the last of this class.

⚠ Two candidate second terms were measured and neither helps. Requiring the break to *save* at least
N columns — which is a test on the left-hand side's width, and is what the sweep's rising region
looks like — is inert at every N from 1 to 26 in combination with the shipping margin, and worse in
combination with a smaller one. A hard cap at `120 − k` with a saving term, which fits the sweep's
plateau, tops out at 99.50 % against 99.53 %.

⚠ It has a known counter-example, and it is the largest single class left:
`byte[] data = Convert.FromBase64String("…");` at 123 columns comes back from the oracle broken
after the `=` with the call whole on a 113-column continuation line, and the margin declines that
break and chops the call instead. That shape and its siblings were 64 lines across 38 files at M3.1.

⚠ **Current, at `8cbd66d`: still the largest class, at 63 lines across 38 files.** That is the
harness's own top-ranked class, *wrapping: one side continues where the other broke (phase 3)*, and
this entry is what dominates it. Within it, the sub-population whose signature is unambiguous — a
hunk where a line the oracle wrote ends in `=` and Skala's does not — is **21 hunks across 14
files**. The named counter-example itself, `Convert.FromBase64String`, occurs in **one** file of the
380: "and its siblings" is carrying the count, and no shipped instrument separates the siblings from
the rest of the class. The 38 files are the class's; the 14 are what can be named.

⚠ **Triage at `425b01d0`: `debt`, size L, and it is a member of the preference fact** — SK-DIV-0050
§ "The two facts this family is made of". Re-measured, not inherited: the named counter-example was
swept at ten widths of the same shape — `byte[] data = Convert.FromBase64String("…");` padded from
116 to 153 columns — and it diverges at **exactly one** of them, 123, where the oracle takes the `=`
and Skala chops the argument list. Every wider row agrees. So the entry reproduces at the recorded
width and the class is a narrow band around it, not a slope.

The verdict is `debt` and the reason is not the output. Skala's answer at 123 columns —
`Convert.FromBase64String(\n    "…"\n);` — is a perfectly good thing for a C# formatter to write,
and if the rule that produced it were "chop the argument list, always" this entry would be
`deliberate`. The rule is instead `11 + column/indent`, a constant fitted to three repositories,
which this entry's own sweep shows is not reproducing any rule of ReSharper's and is absorbing error
from elsewhere in the ordering rule. **A fitted constant is not a decision; it is a decision
deferred**, and the moment the oracle is uninstalled the number becomes unfalsifiable — there is no
experiment left that can say whether 11 was right. That is the definition of what has to be paid
first.

Size **L**, and the work is not "pick a better constant" — the sweep already says no value closes
it. It is the preference fact: `Fitter` resolves the outer group before the inner one and carries
one flat width per node, so "which of these two constructs gives" cannot be asked at all. The margin
is the stand-in for that missing question, which is why it wanders.

⚠ **Swept as a surface, and the surface has a rule in it.**
[sk-div-preference-sweep.md](sk-div-preference-sweep.md) probes total width against the right-hand
side's own width one column at a time, at four constructs, under four filler profiles. For this
entry's shape — `var value = <name>(<args>);` — the oracle's answer is reproduced by a sentence:
**break the argument list exactly when breaking it brings the head line within the margin, and take
the `=` when it does not.** 5 184 of 5 186 cells under `uniform-5` and `varied-short`, the two
misses at totals 129 and 130 and one column each. Under `varied-long` the law alone is 98.65 % and
becomes **100.00 %** once a floor of 17 columns is added — and 17 is exactly the width at which that
profile's list stops being a single argument, so the residue is "a list of one has nothing to chop"
and not a second rule. The same law is exact at SK-DIV-0024 and holds at
`var value = new <name>[] { … }` above a floor of 29.

⚠ **Re-swept at fourteen shapes, and `F` turns out to be a fact about the inner construct's
*contents* rather than about the statement it sits in.** The sweep's version 2 adds every shape
[sk-div-0005-margin-sweep.md](sk-div-0005-margin-sweep.md) named. Four of them are this entry's
`=` against an argument list with something different in front of it — a qualifier, a cast, a type
argument list — and all four return the *same* floor at the same content shape:

| shape | `single-literal` | `uniform-5` | `varied-long` | `varied-short` |
|---|---:|---:|---:|---:|
| `eq` — `var value = <name>(<args>);` | 29 | 0 | 17 | 0 |
| `call-member` — `Utility.<name>(<args>)` | 29 | 0 | 17 | 0 |
| `cast-call` — `(I<name>)service.Resolve(<args>)` | 29 | 0 | 17 | 0 |
| `generic-call` — `Deserialize<<name>>(<args>)` | 29 | 0 | 17 | 0 |
| `eq-array` — `new <name>[] { <elements> }` | 29 | 29 | 29 | 29 |
| `object-initializer` — `new <name> { <members> }` | 29 | 29 | 29 | 29 |

So there are **two** numbers here, not six: a parenthesised argument list floors at 0 when it holds
several arguments, 17 when the profile's identifiers make it one, and 29 when it is a lone string
literal; a braced initialiser floors at 29 whatever is in it. Nothing to the left of the inner
construct moves either. `binary-chain` and `ternary` need no floor at all — the margin law alone is
**100.00 %** of both grids, 15 552 cells each.

⚠ **And two of version 1's numbers were wrong. Both are corrected in place, and what they were is
recorded here because the wrong ones were quoted.**

1. `BracedLiteral` built `{ "…" }` subtracting eight columns of delimiter where the text has six,
   so every `eq-array` × `single-literal` cell was filed two columns narrower than it was. Version 1
   reported that profile's floor as **31**; it is **29**. The three word-length profiles were never
   affected, so this entry's "above a floor of 29" was right for the reason it was quoted — but
   version 1's own table showed the four content profiles *disagreeing*, 31 against 29, and that
   disagreement was the bug and nothing else. They agree now. `PreferenceSweep.Build` gained an
   arithmetic check that throws when a generated line is not exactly the width it is filed under,
   which is the only reason this was found; a probe that is two columns out is otherwise silent and
   moves every boundary it reports.
2. The model was scored over every generated cell, so cells where the oracle declined **both**
   constructs counted as agreements whenever the model happened to say "outer". They are now held
   out of the denominator. It does not move this entry — its shapes have no third break — but it
   moves SK-DIV-0024 and SK-DIV-0050, and the percentages in version 2 are **not comparable** with
   version 1's anywhere a construct has one.

⚠ **So the margin is not modelling an unknowable preference; it is standing in for a question
`Fitter` cannot ask.** `11 + column/indent` is a fitted constant because the ordering rule is asked
"is the outer break worth it" and answers from the node's own width. The law above is asked "would
the inner break be *enough*", which is a question about the head line the inner break would leave —
and that is a fact the fitter has, not one it has to guess. This does not close the entry, and the
work is still the preference fact's size L; what it changes is that the target is now statable
without ReSharper installed, which is what the entry said was missing.

- options: `skala_prefer_wrap_around_eq` (Tier D), `skala_wrap_before_eq` (Tier D)
- ⚠ status: **open**, measured. The margin's own sweep is
  [sk-div-0005-margin-sweep.md](sk-div-0005-margin-sweep.md) and it says no value of the constant
  closes this; the width surface is [sk-div-preference-sweep.md](sk-div-preference-sweep.md) and it
  says what to replace the constant with. Not tail work

## SK-DIV-0006 — the pinned oracle profile does not format documentation comments; Rider does, and so does Skala

⚠ **This entry's title used to be "`jb cleanupcode` does not format documentation comments, so
neither does Skala", and both halves have now been measured false.** The measurement behind it was
real and is reproduced below; the conclusion drawn from it was not. Read this entry as the record of
a wrong inference being corrected, because the way it was wrong is more useful than the fact.

[05](plan/05-csharp-formatting-rules.md) § "Phase 4" describes an xmldoc sub-formatter: parse the
comment as XML, re-wrap text to `skala_xmldoc_max_line_length = 120`, break before
`summary,remarks,example,returns,param,typeparam,value,para`. It is implemented, and it runs by
default.

### What was measured, and what was concluded

Asked directly at M3, with the export's whole `resharper_xmldoc_*` family in force, the oracle
returned every one of these exactly as written:

```csharp
///<summary>No space after the marker.</summary>
/// <summary>A summary line 128 columns wide …</summary>
/// <param name="x">…</param><param name="y">…</param>
/// <summary>Text</summary><remarks>…</remarks>
```

That is still true, at 2025.2.6, from a committed fixture rather than a remembered probe:
`constructs/trivia/a-malformed-doc-comment-is-left-alone.cs` goes through the oracle and comes back
byte-identical.

The conclusion drawn was "the oracle declines to format documentation comments", and from it, "a
Skala that formatted them would diverge from Rider on every doc comment in every repository". The
twelve `resharper_xmldoc_*` keys were left Tier D on that basis and
`skala_space_after_triple_slash` was **demoted** from Tier A — milestone 1 inserted the space,
the oracle did not, and it was worth 79 lines across 15 files of `corpus/real/`.

### ⚠ What the measurement actually showed

**`CSharpFormatDocComments` is a `jb cleanupcode` cleanup task, and `OracleProfile.FormatOnly` does
not enable it.** ReSharper's built-in `Reformat Code` profile sets it false and `Full Cleanup` sets
it true; `OracleProfile.FormatOnly` is `<CSReformatCode>True</CSReformatCode>` and nothing else,
which is `Built-in: Reformat Code` exactly. JetBrains documents the same thing in prose twice — "the
Built-in: Reformat Code profile does not reformat XML doc comments", and "to reformat XML doc
comments, use code cleanup".

So M3 measured a profile and reported it as a property of the tool. Add one element to the profile
and the oracle reformats the comment, honouring `space_after_triple_slash`, `max_line_length`,
`skala_xmldoc_linebreak_before_elements` and `skala_xmldoc_max_blank_lines_between_tags` from this repository's own
`.editorconfig`. The negative control — the same element under a name the tool does not know —
changes nothing, which is what makes the positive result mean something. The full probe, its
commands and two incidental ways to get a false "no change" out of `jb cleanupcode` are in
[oracle-cleanup-profile.md](oracle-cleanup-profile.md).

**Skala formats documentation comments by default.** Not formatting them was the divergence.
`skala format --no-xmldoc` is the escape hatch, and it is a flag rather than
`skala_xmldoc_wrap_lines = false` for two reasons: that key means "do not wrap long lines" and
would still leave the comment re-indented and its marker respaced, and it is not a documented
ReSharper key at all — `wrap_lines` appears nowhere in JetBrains' `.editorconfig` index, for any
language, though the export writes it.

⚠ **`skala_space_after_triple_slash` is Tier A again.** This paragraph used to end "the key
cannot return to Tier A … That is a fact about the fixtures, and it expires when they are
regenerated." It has expired. `constructs/xmldoc/skala_space_after_triple_slash.cs` carries the
fixture, generated under `OracleProfile.DocComments`, and Skala reproduces it byte for byte.

⚠ The fixture is narrower than it looks, and the narrowing is a measured shape rather than a
caveat. **The oracle does not rewrite a `///` marker on a comment it is otherwise leaving alone.**
The first cut of that fixture was a single short `///<summary>Docs.</summary>`, which needs no
wrapping, no element split and no blank-line removal — and it came back from
`CSharpFormatDocComments` byte-identical, marker included. The fixture that measures the key is two
crammed elements on one line, which the oracle has to rebuild, and the marker space appears with
the rebuild.

⚠ **That last shape is now closed, and closing it was the second-largest finding of the sweep's
doc-comment batch.** The paragraph used to end here with "So the 79 lines M3 charged to Skala are
*still* not fully re-explained: `corpus/real/`'s fixtures are generated under a profile that rebuilds
nothing, and on a comment that needs no other change the oracle and Skala genuinely differ about the
marker." They no longer differ. `XmlDocFormatter.Replacement` compares its rendered lines to the
source's line for line, modulo one space after the `///`, and puts the source back when that is the
only difference — which is exactly what the oracle does, measured on one file holding two comments
with the same two blank `///` lines between the same two tags, under
`skala_xmldoc_max_blank_lines_between_tags = 3` so neither line had to go:

```csharp
/// <summary>A summary written at enough length that it cannot possibly fit …</summary>   →  rebuilt, and its blank lines come back as `/// `
///
///
/// <returns>A value.</returns>

/// <summary>A summary.</summary>                                                          →  byte-identical, bare `///` and all
///
///
/// <returns>A value.</returns>
```

⚠ **The row this was found from was `skala_xmldoc_max_blank_lines_between_tags`, and the key had
nothing to do with it.** The sweep reported it `Divergent` at `3`, where Skala wrote `/// ` and the
oracle wrote `///`; the count was right on both sides and the *marker* was the disagreement, on a
comment the oracle was not otherwise touching. A verdict on one key was a fact about a rule three
keys away, which is the failure mode this file exists to catch.

Worth +31 exact lines of `corpus/real/` on the every-line basis (74 436 → 74 467 of 77 312, 96.28 % →
96.32 %), and nothing on the outside-doc-comments basis, which by construction cannot see it. It also
retired SK-FUZZ-0015 — see `pathological/open/register.md`.

What is implemented is the half [05](plan/05-csharp-formatting-rules.md) calls the hazard and that
needs no oracle: a doc comment that is not well-formed XML is left exactly as it is and reported at
`hint` (`SK0003`), never "fixed".

⚠ "Does not format" is stricter than it sounds, and Skala was not honouring it: a comment's **own
trailing whitespace** is part of the comment. `/// Gets the path of the current JSON token. ` comes
back from the oracle with the trailing space, and so does `// … during and `, and so does a
trailing tab. Skala trimmed the right-hand end of every documentation line while splitting the
trivia into pieces, which cost 2 lines and 2 files of `corpus/real/`; it no longer does, and
`constructs/trivia/a-comment-keeps-its-trailing-space.cs` pins it.

⚠ This makes comment text the one and only thing in Skala's output that can carry trailing
whitespace — the writer still cannot produce any of its own, which is why
`remove_spaces_on_blank_lines` stays inert. It also settles `trim_trailing_whitespace`, whose value
the export sets to `false`: probed at `= true` on this fixture, the oracle **returns the trailing
space anyway**. Skala follows the oracle rather than the key, so the key is inert in both
directions and stays Tier D — implementing it would create a divergence in exchange for nothing
anyone asked for.

⚠ **Registry state.** `skala_space_after_triple_slash` is **Tier A** (see above),
`skala_xmldoc_wrap_lines` is **Tier D** — SK-DIV-0019 is closed and its fixture agrees, but the
key has never been swept, so nothing has yet made a claim about it away from the export's value;
`XmlDocOracleTests.Unswept` holds it and its six siblings there until the sweep does.
`trim_trailing_whitespace` is **Tier D** with
`defaultSource: oracle-probe` — the probe that established it is recorded in the registry entry
itself — and `skala_remove_spaces_on_blank_lines` is **Tier D**, inert as this entry says.

### The sub-formatter is the default

`XmlDocFormatter` re-wraps documentation comments on every run of `skala format`, `skala arrange`,
the LSP server and the MCP server. `--no-xmldoc` is the only thing that turns it off, and the only thing
that still reproduces the pinned oracle profile's answer.

⚠ **RETRACTED, and this is the second retraction in the same entry.** What follows in this section
was written when the corpus had no fixture that could show the oracle formatting a documentation
comment, and it concluded that no fixture ever could. That conclusion was the original mistake at
one level down: a limitation of the *profile*, read as a limitation of the corpus.
`OracleProfile.DocComments` is `OracleProfile.FormatOnly` plus
`<CSharpFormatDocComments>True</CSharpFormatDocComments>` and nothing else, `./build.sh Oracle`
regenerates `constructs/xmldoc/*.xmldoc.expected.cs` under it, and the family is measured. **13 of
the 22 keys are Tier A**, pinned exactly the way every other option in the registry is. The nine
that are not had measured shapes and entries of their own: SK-DIV-0019 through SK-DIV-0023. ⚠ **All
nine now reproduce their fixtures, and the split is 22 of 22.** Seven were one arithmetic — SK-DIV-0019
wearing five names, SK-DIV-0021 turning out to be the same arithmetic, and SK-DIV-0020 needing one
structural fix beside it; the other two were SK-DIV-0022 and SK-DIV-0023's surviving half.

⚠ **Only four of the nine are keys the export sets**, and this has to be said before "enforced at the
export's own value" is said of any of them. `.editorconfig` carries twelve `resharper_xmldoc_*` lines,
and of the nine only `max_line_length = 120`, `wrap_lines = true`, `skala_xmldoc_wrap_tags_and_pi = true` and
`skala_xmldoc_wrap_text = true` are among them. `skala_xmldoc_spaces_inside_tags`, `skala_xmldoc_blank_line_after_pi`,
`skala_xmldoc_linebreak_before_multiline_elements`, `skala_xmldoc_linebreak_before_singleline_elements` and
`skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than` are **not in the export at all**; the value in play
is the registry `default`. So for those five the fixtures do not say "Skala enforces the standard" —
they say **Skala's recorded default matches ReSharper's built-in default on this construct**, which is
a different claim and, on these constructs, a stronger one. ⚠ Their `defaultSource` reads `template`
and they are not in `editor_config_template` either, so that provenance does not hold up and is worth
a pass of its own; nothing here rests on it, because every measurement in this family compares Skala
and `jb cleanupcode` under the *same* `.editorconfig`, each falling back to its own defaults.

⚠ **The nine are still Tier D, and the distinction is the whole point of this section.** Agreement on
a fixture is agreement at one value of one key on one construct under one configuration; Tier A is a
claim about the option across its domain, and the instrument that makes it is the key-flip sweep,
which has no row for any of the nine. Six `resharper_xmldoc_*` keys were promoted on exactly this
evidence and demoted the same afternoon — every one agreeing at the export's value and diverging away
from it. `XmlDocOracleTests.Unswept` holds the nine, asserts in both directions that they still agree,
and shrinks to nothing as the sweep reaches them.

The three things below still pin the sub-formatter and are still worth having — the round trip in
particular is checked on every comment of every run, which no fixture can be — but they are no
longer *instead of* an oracle fixture.

⚠ **`OfUnoracled` survives, with a narrower meaning.** It used to mean "the oracle cannot be asked".
For the nine keys that keep it, it now means "the oracle was asked and said something else", which
is a stronger statement and a checkable one: `XmlDocOracleTests` asserts that a Tier D key in this
family still fails its fixture, so a divergence that gets fixed cannot quietly stay Tier D.

⚠ **`OfUnoracled` is a third mark and it had to be added.** These ids were `OfInert` — "read, and
unable to change anything" — which was true only while nothing ran them, and
`AnInertKey_StillCannotBeObserved` would have failed on seven of them the moment the default
flipped, correctly. Inert and unoracled are opposite claims about the same kind of key: the inert
theory asserts a key changes nothing, the unoracled theory asserts it changes something. Both fail
loudly, which is the point of having two.

What pins them instead is three things:

1. **Hand-written fixtures** (`Formatting.CSharp.Tests/XmlDocFormatterTests.cs`) asserting the
   semantics JetBrains' own settings pages state, one per key.
2. **A round trip, checked on every comment of every run** rather than on a fixture. The re-wrapped
   comment is re-parsed and reduced to a signature — prose whitespace-normalised, `<code>` and `<c>`
   bodies byte-for-byte, tag names and attribute source text exact — and if it differs from the
   original's by one word the comment is put back exactly as written. A re-wrap that cannot prove
   itself does not happen.
3. **Four corpus-wide properties** (`Conformance.Tests/XmlDocPropertyTests.cs`) over all 716 corpus
   files: token equivalence, idempotency, the round trip, and — the one that matters most —
   *the code around the comments is untouched*, asserted by comparing the non-`///` lines of the
   output with and without the flag.

⚠ **What the sub-formatter costs against the pinned profile, measured rather than asserted.** This
entry used to say a re-wrap "would diverge from the oracle on every doc comment in the corpus" and
nobody had put a number on it. `harness xmldoc` does, over `corpus/real/`'s 380 files and 3 032 doc
comments:

| | line | file |
|---|---|---|
| `--no-xmldoc`, every line | **99.63 %** | 85.26 % |
| default, every line | **96.04 %** | 47.89 % |
| `--no-xmldoc`, outside doc comments | 99.53 % | 85.26 % |
| default, outside doc comments | 99.53 % | 85.26 % |

The 3.59 points between the first two rows are **not** a fidelity cost. They are the fixtures
answering a question the formatter no longer asks: the profile that produced them does not run
`CSharpFormatDocComments`, so on those lines the two sides are not disagreeing about how to format
a doc comment, they are disagreeing about whether to. The last two rows being identical is the
claim that matters — with every `///` line removed from *both* sides, nothing the sub-formatter is
not allowed to touch has moved, over all 716 corpus files.

⚠ **That is why `outside doc comments` is the differential's default basis and is named in
`FidelityBasis`, in every message the ratchet prints, and in `fidelity.json`'s own `Basis` field,
which `FidelityBaseline.Read()` refuses to compare across.** A fidelity figure that silently
excludes a category is how a measurement stops meaning anything; the every-line number is asserted
alongside it by `TheEveryLineNumber_IsStillReported` so that the excluded category cannot grow
unwatched. The exclusion is drawn from both sides on purpose: excluding "the lines Skala changed"
would be marking one's own homework, and excluding "the files with doc comments" would hide a real
regression in the code around them.

⚠ **The exclusion is temporary and its expiry is known.** Adding
`<CSharpFormatDocComments>True</CSharpFormatDocComments>` to `OracleProfile.FormatOnly` and running
`./build.sh Oracle` regenerates the 716 fixtures under a profile that formats doc comments, at which
point `///` lines become comparable, the basis returns to every line, and these keys become
promotable to Tier A. It was not done in the same commit as the default flip: a corpus-wide fixture
rewrite and a default change in one diff is not a reviewable diff.

Of the 3 032 comments, **3 030 are re-wrapped and round-trip clean and 2 are left exactly as
written**, both because they are not well-formed XML. The first run of that measurement refused 16,
and the fourteen extra refusals were two defects the round trip caught before anything was written:
a self-closing `<code source="…" title="…" />` 130 columns wide was being treated as multi-line and
rewritten into a start tag with a closing tag that never existed, and `<i>…</i>.` with three lines
of italic text was putting the sentence's full stop on a line of its own.

⚠ **The safety net's allowance for this did not exist.** [04](plan/04-formatting-engine.md) §
"The safety net" says comment texts are "normalised for the intentional xmldoc rewrap"; the code
trimmed each line and the one space after a marker, which no re-wrap survives, because a re-wrap
moves the line breaks. It exists now, only under the flag, only for `///` comments, and it is the
sub-formatter's own signature rather than "comments are exempt" or "the words in order" — the
latter would have to be widened again for `skala_xmldoc_space_before_self_closing` and again for
`skala_xmldoc_spaces_inside_tags`. The signature is *tighter* than a word sequence where it counts: a `<code>`
body is compared byte-for-byte, which it was not before.

**Twenty-one of the thirty-two `resharper_xmldoc_*` keys are honoured** and eleven are refused. Each
of the twenty-one is asserted observable by `AnUnoracledKey_IsObservable`, against a hand-written
probe rather than against `constructs/` — nine of them cannot be seen there, because the constructs
fixtures carry short, already-tidy doc comments written when nothing read them.

⚠ **The counts were 17 of 27 and 10, and the arithmetic moved for two reasons that are both worth
reading.** Four keys were promoted out of the refusal list after being measured; and the family
itself grew by five, because the five processing-instruction keys had been *excluded from the
count* on the grounds that "a processing instruction in a C# documentation comment is not a thing
that occurs". It is — Roslyn parses one, and `skala_xmldoc_blank_line_after_pi` acts on one at its default
`true`. The exclusion meant five keys carried no decision at all, because the partition test that
would have demanded one did not look at them. One of the five turned out to be implementable, and
had been silently missing from Skala's default output the whole time.

### ⚠ Four refusals that were the same mistake one level down

**`XmlDocIds.Refused` said "no oracle can settle this" about eight keys when what was true is that
the profile the oracle runs under never asked.** Run `jb cleanupcode` 2025.2.6 with
`<CSharpFormatDocComments>True</CSharpFormatDocComments>` and it rewrites tag headers freely. All of
the following are measured, not read off a settings page:

| Probe input | Oracle output |
|---|---|
| `<param name="a" >` | `<param name="a">` |
| `<param name = "b">` | `<param name="b">` |
| `<param   name="d"    other="x"  >` | `<param name="d" other="x">` |
| `<param name='single'>` | `<param name='single'>` — quote character kept |
| `<customElement a="1" … e="5">` past the margin | wrapped, last attribute on a continuation line at one indent |
| `<?xml-stylesheet …?>` | followed by a blank `///` line |

So `skala_xmldoc_space_after_last_attribute` and `skala_xmldoc_spaces_around_eq_in_attribute` were refused for a reason that
described Skala's implementation — "Skala emits a tag header byte-for-byte" — dressed as a property
of the key. Both are implemented now: the renderer re-emits the header from a name/value split taken
at Roslyn's `EqualsToken`, so an attribute's **value, quote character included, is still the source
bytes** and only the whitespace around the `=` is chosen.

`skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than` was refused because "JetBrains' own reference page
does not say what is measured against it". True, and it did not need to: **the tool says.** The
element's *flat inner content* — its text and its child markup, neither tag — is compared **strictly
greater** than the threshold. At 12, twelve characters stay inline and thirteen do not. ⚠ `0`
therefore means "always", not "never"; `options.json` asserted the opposite in its `boundsBecause`,
and that is corrected.

`skala_xmldoc_blank_line_after_pi` was never refused, considered, or counted.

### The refusals that stand

- `skala_xmldoc_attribute_indent`, `skala_xmldoc_attribute_style`, `alignment_tab_fill_style`, `allow_far_alignment` —
  **pending, not refused.** All four describe a wrapped tag header's continuation line, and Skala
  never breaks inside a header. The oracle does, so the subject exists; the prerequisite is a
  renderer that can wrap a header, and until then the reason is Skala's shape and says nothing about
  the keys.
- `skala_xmldoc_pi_attribute_style`, `skala_xmldoc_pi_attributes_indent`, `skala_xmldoc_space_after_last_pi_attribute`,
  `skala_xmldoc_spaces_around_eq_in_pi_attribute` — ~~pending on the same prerequisite one construct over: a
  processing instruction is emitted verbatim, so its header has no attributes to space out.~~
  ⚠ **Corrected 2026-08-31: not pending on anything, because the oracle does not parse a processing
  instruction's header either.** The sentence above described *Skala* and was read as a property of
  the four keys. Measured under `OracleProfile.DocComments` on
  `<?skala-probe first = "1" second="2"   third='3'?>`, a 160-column instruction, one the author had
  already broken across two lines, and one written tight — **one output across every value of all
  four keys**, singly and all four at once: the spaces around `=` survive, the double space survives,
  the quote characters survive, and nothing is wrapped. `skala_xmldoc_blank_line_after_pi = false` removes the
  blank `///` line after the same instruction in the same run, so the doc-comment task does see it.
  All four are inert in the oracle, and Skala's verbatim path is not a gap against it.
- `skala_xmldoc_wrap_around_elements` — ⚠ **refused, and now for a measured reason.** With the doc-comment task
  enabled, at both values, over prose containing inline `<see/>`, `<c>` and `<b>` elements both long
  enough to wrap and short enough not to, the oracle's output is **byte-identical**. Either it is
  subsumed by `skala_xmldoc_wrap_tags_and_pi` in this build or its subject is a construct no C# doc comment
  produces. The previous two reasons for this key were both guesses about documentation; this one is
  a diff.
- `tab_width` — it only changes how wide a tab is when measuring, and the only tab a re-wrap can
  meet is inside a `<code>` block, which is verbatim and never measured.
- `skala_insert_final_newline` — a `///` comment has no file end to put a newline at, and JetBrains' key
  index does not list XMLDOC among the languages that accept the key at all. ⚠ Measured 2026-08-31
  rather than argued: one oracle output at both values on a file carrying a type-level `<summary>`, a
  member `<remarks>`/`<returns>` pair and a trailing comment that ends the type, with
  `skala_xmldoc_wrap_text = false` moving the same file in the same run.

⚠ **One deliberate narrowing against the oracle, recorded rather than hidden.** The threshold key
applies to `<c>` in the oracle's output; Skala exempts verbatim elements from it, because breaking
one open would move a byte-for-byte code body onto a re-indented line, which is the single thing the
verbatim rule exists to prevent.

⚠ **One defect found while measuring and left alone, because it is not one of these keys.** A
processing instruction is emitted through the verbatim path, and a verbatim line deliberately does
not get the `///` marker space re-applied — a rule that is right for `<code>` and wrong here. Skala
writes `///<?pi?>` where the oracle writes `/// <?pi?>`. The same applies to a CDATA section and an
XML comment. Fixing it means giving the verbatim path an indent and a marker of its own, which is a
change to how three constructs are emitted rather than to how one key is read.

⚠ Two readings the sub-formatter had to choose and no oracle settles, recorded because they are
choices: `skala_xmldoc_linebreak_before_elements` is read as "this element owns its own line", a break before it
*and* before what follows it, because the strict reading leaves `</param><param …>` sharing a line
with the text after them; and `skala_xmldoc_indent_child_elements = do_not_touch` is mapped to "no indent"
rather than "keep the author's", because under a re-wrap the author's indentation no longer exists
to keep.

⚠ [14](plan/14-web-languages.md) § "Why they are later" describes the sub-formatter as already
existing and makes lifting it out the exercise that proves the `ISkalaLanguage` seam. It exists now
— in `Formatting.CSharp`, as four files that share no state with the document builder — but
`ISkalaLanguage` still does not, and doc 14 still has no correction note.

- options: `skala_space_after_triple_slash`, `skala_xmldoc_wrap_lines`, `skala_xmldoc_max_line_length`, `skala_xmldoc_linebreak_before_elements`, `trim_trailing_whitespace`
- ⚠ status: **open, and no longer deliberate.** The sub-formatter is the default and Skala follows
  Rider. Seventeen keys honoured and asserted observable, ten refused with a reason, none Tier A —
  and, unlike before, all of them *able* to become Tier A. What is left is one element in
  `OracleProfile.FormatOnly` and a fixture regeneration; the `outside doc comments` fidelity basis
  is scaffolding that stands until then. ⚠ This entry is also the second instance behind
  [16](plan/16-risks-and-open-questions.md) § Q1, which had been recorded as narrowed

## SK-DIV-0007 — an argument list around a chain the author broke does not chop

`Use(a > 0\n    && b > 0)` comes back from the oracle with the argument on a line of its own,
because a `chop_if_long` list containing something multi-line chops. Skala leaves it as the author
wrote it.

The obvious fix is the one milestone 3 already uses for delimited lists — let a group that is
certain to break hide its flat width from whatever contains it — and it is wrong here. An operator
group is nested *inside* the next operator's group, so an unbreakable inner one drags the outer one
with it and `a && b\n || c` comes back chopped at both operators instead of unchanged. That is the
behaviour `keep_user_linebreaks = true` exists to guarantee, and it is pinned by
`constructs/breaks/binary-operators.cs` and `constructs/wrapping/binary-chains.cs`.

Getting it right needs the enclosing list to ask "will anything inside me break" without the
operator groups asking it of each other — a question the current one-flat-width-per-node measure
cannot express. Measured: the wrong fix buys 0.01 points of line fidelity and loses two committed
fixtures.

⚠ Milestone 3.1 gave the same fact to the **chain** group, where the objection does not apply —
a chain is one group rather than a nest of them — and it is what makes
`static void Member(Packer p) =>\n    p.Enum(a)\n        .Enum(b);` come out with the arrow broken.
That half is done; the binary chain's half is not.

⚠ **Current, at `8cbd66d`: the binary chain's half is still open, and it is 8 hunks over 39 lines
across 6 files.** Signature: a hunk in which a line the oracle wrote ends in an open parenthesis —
the list it chopped — and Skala's does not. This entry sits inside the harness's *line break
presence: Skala left a line the oracle joined (phase 2)* class, which is 45 lines across 21 files in
total and which it shares with SK-DIV-0011. ⚠ Neither half of that class can be separated from the
other by any shipped instrument, so the two entries are measured by signature and the class total is
the honest upper bound for the pair.

The chain half stays done: `constructs/breaks/binary-operators.cs` and
`constructs/wrapping/binary-chains.cs` are both in the corpus and both at 100 %.

⚠ **Triage at `425b01d0`: `debt`, size M, and it is the containment fact's first member** —
SK-DIV-0050 § "The two facts this family is made of", where the size is paid once for all three.
Re-measured: `Use(a > 0\n    && b > 0);` still comes back from the oracle with the argument on lines
of its own and Skala still leaves it hugging the parenthesis, and the same holds for a three-operand
chain. The control the entry names is still a control: `var x = a > 0 && b > 0\n    || c > 0;` is
byte-identical from both engines, so the nested-operator objection to the obvious fix stands.

⚠ **`deliberate` was the tempting verdict and it does not survive the test.** There is a real
argument available — `keep_user_linebreaks = true` promises to keep the author's break and add
nothing, and `Use(a > 0\n    && b > 0);` is what the author wrote. But that argument is
reconstructed after the fact. What the entry actually records is "the wrong fix buys 0.01 points and
loses two committed fixtures", which is a report of an attempt failing, not of a position being
taken. And the same missing capability produces, in SK-DIV-0077, output nobody would defend: a block
body joined onto the closing parenthesis of a parameter list that was broken. **A capability that is
absent cannot be a style choice in one place and a defect in another.** It is one absence and it is
debt.

Size **M**, once, shared with SK-DIV-0077 and SK-DIV-0078: a group needs to be able to ask "will
anything inside me break" without the operator groups asking it of each other. ⚠ It is the *cheap*
half of this batch's debt, and the reason is worth stating plainly: **the containment fact can be
settled after the oracle is gone.** "A construct that spans lines makes its container span lines" is
a principle a person can hold and a test can pin; it needs the oracle for confirmation, not for
derivation. The preference fact is the opposite, and that is what makes it the urgent one.

- options: `skala_wrap_arguments_style` (Tier A, `chop_if_long`), `skala_keep_user_linebreaks` (Tier A, `true`)
- ⚠ status: **half closed** (the chain, at M3.1), half **open**, measured

## SK-DIV-0008 — ⚠ half closed: statement conditions are aligned, four other keys are not

`int_align` and all eight `int_align_*` sub-keys are `false`, and so are `align_multiline_argument`,
`…_parameter`, `…_calls_chain`, `…_expression` and `skala_align_multiline_binary_expressions_chain`.

⚠ **Milestone 3 said four keys survive and there are nine**, which is the first correction:
`skala_align_multiline_statement_conditions`, `align_multiline_type_argument`,
`align_multiline_type_parameter`, `align_multiline_ctor_init`, `align_multiline_array_initializer`,
`align_multiline_implements_list`, `skala_align_multiline_comments`, `align_first_arg_by_paren`'s
companion `align_ternary = align_not_nested`, and `skala_int_align_fix_in_adjacent`.

**Measured before building, which is what decided it.** Of 313 divergent line slots at the time,
**40 across 11 files** were a line the oracle had put at a column that is not a multiple of the
indent width, and all forty were one key:

```csharp
else if (ReflectionUtils.ImplementsGenericDefinition(
             NonNullableUnderlyingType,          ← the `(`'s column plus one continuation level
             typeof(IEnumerable<>),
             out tempCollectionType
         )) {                                    ← the `(`'s column
```

That is 12.8 % of the residue and 17 % of the files still diverging, so it was built.
`IndentKind.Align` is the node [04](plan/04-formatting-engine.md) reserved, and the writer's indent
stack now holds **columns rather than levels** — after which an alignment scope is a block scope
whose column is absolute and no new stack semantics were needed. Covered: `if`, `while`, `do`,
`for`, `foreach`, `using`, `fixed`, `lock`, `switch`, and `catch … when`, which is the one condition
`VisitEmbedded` never saw because a catch clause has no embedded statement. Worth 0.06 points of
line fidelity and 1.3 of file fidelity.

⚠ What is still not implemented, and what each is worth on `corpus/real/`:

| key | shape | residue |
|---|---|---:|
| `align_multiline_for_stmt` | `for (;\n     cond;\n     step)` — the clauses chop | 4 lines, 2 files |
| `align_multiline_array_initializer` | `new[] {\n     a,` aligned to `new[]` | 0 lines |
| `align_multiline_type_argument`, `…_type_parameter` | a type argument list broken across lines | 0 lines |
| `align_multiline_ctor_init` | `: base(\n      a)` | 0 lines |

⚠ **Current, at `8cbd66d`:** `skala_align_multiline_statement_conditions` is **Tier A**,
so the half that was built stays built. `align_multiline_for_stmt`, `align_multiline_array_initializer`,
`align_multiline_type_argument` and `align_multiline_ctor_init` are all still **Tier D**. The `for`
header's residue is **5 hunks over 13 lines across 3 files** — signature: a hunk mentioning a
`for (` header — up from the 4 lines across 2 files recorded at M3.1, and the increase is the
signature being broader rather than the formatter regressing: the whole of `corpus/real`'s
`ForStatement` attribution is 2 lines ([16](plan/16-risks-and-open-questions.md) § R1's table), so
three of the three files are the same generated `DataSet` and its neighbours. The other three keys
are still worth 0 lines: the corpus contains no instance of their shapes.

⚠ **The `for` row's attribution was wrong, and milestone 3.2 measured it.** The residue was never
`align_multiline_for_stmt`'s: that key is **masked** by `skala_align_multiline_statement_conditions = true`,
which the export sets, and both of its values return the same file byte for byte. What was missing
was a **break point at the header's `;`** — Skala broke inside the incrementor expression instead,
producing `i +=\n 1` where the oracle chops the clauses. `skala_wrap_for_stmt_header_style` is the key that
governs it, it is now implemented and **Tier A**
(`constructs/wrapping/for-header.cs`), and the residue by the same signature is **2 hunks in 1 file**.
What is left there is not a `for` rule: the initializer's own `=` continuation lands on the align
column where the oracle puts it one level further in, which reproduces on the pre-change formatter
and belongs to `PlanAroundEquals`. `align_multiline_for_stmt` stays **Tier D**, now for the reason it
always had rather than for this one — reaching it takes two flips and the per-option unit makes one.

The consequence [05](plan/05-csharp-formatting-rules.md) § "Alignment" claims — "with column
alignment off, laying out line *n* never requires knowing the contents of line *n−1*" — **survives
the change**, and that is worth stating: an alignment scope's column is the column the writer is
already at when the scope opens, which is on the current line. The fitting pass is still linear.

⚠ **Re-measured 2026-08-30 and the other half is closed too.** Four probes, each carrying the shape
the four remaining keys name — an overflowing array initializer, a `: base(…)` initializer, a broken
type argument list, a five-parameter type parameter list — formatted by Skala and by
`jb cleanupcode` under `SkalaFormatOnly`, first at the export's values and then with all four keys
flipped to `false`:

| | result |
|---|---|
| Skala vs oracle, export values | **byte-identical on every shape**, the `for` header included |
| oracle at `true` vs `false`, all four keys | **byte-identical** — the keys move nothing |
| same, with `skala_align_multiline_array_and_object_initializer = true` unmasking the family | still byte-identical |

The last row is the one that matters, because it is the mask the `for` row was caught by. Turning
the family's general key on *does* move the oracle — an overflowing `{ … }` goes from block indent to
the initializer's own column — and with the family unmasked the specialised key still makes no
difference at either value. So `align_multiline_array_initializer`, `…_ctor_init`,
`…_type_argument` and `…_type_parameter` are the same shape `align_multiline_for_stmt` turned out to
be: keys the C# formatter does not answer to on this configuration, recorded as divergences because
nothing had asked them directly.

⚠ What is *left* is not a divergence but a registry fact: Skala declares four keys it never reads,
and flipping one silently does nothing. That is the shape `resharper_remove_this_qualifier` had
before it was deleted, and it is a question for the registry rather than for this file.

- options: `skala_align_multiline_statement_conditions` (now Tier A), `resharper_csharp_align_multiline_for_stmt`, `resharper_align_multiline_array_initializer`, `resharper_align_multiline_type_argument`, `resharper_align_multiline_ctor_init`
- ⚠ status: **half closed** (statement conditions, at M3.1), four keys still **open**, measured
- ⚠ **triage 2026-08-30: `moot`.** Every half of this entry is now accounted for: the statement
  conditions were built and are Tier A; the `for` residue was reattributed to
  `skala_wrap_for_stmt_header_style`, which is implemented and Tier A, and the header reproduces byte for
  byte; the other four keys are worth 0 lines over 380 real and 324 construct files *and* move the
  oracle at neither value on any shape probed, masked or unmasked. ⚠ Stated as a limit rather than a
  proof: four probes are not the shape space. What is claimed is that nothing this repository can
  measure separates the two engines on these keys, which is the same standard the `INERT` verdicts
  elsewhere in this file are held to.

## SK-DIV-0009 — `space_within_spread_pattern` is inert, and the gap it names is not governed at all

The export sets `skala_space_within_spread_pattern = true` and Skala honoured it, putting a
space after the `..` of every collection expression's spread element. The oracle does not, and
neither value of the key changes anything it writes. Asked directly at both:

```csharp
[1, .. xs, 2]     stays [1, .. xs, 2]
[1, ..xs, 2]      stays [1, ..xs, 2]
[1, ..   xs, 2]   comes back [1, .. xs, 2]
```

That is not a rule with a value. It is `extra_spaces = remove_all` collapsing a run of spaces in a
gap nobody legislated — the same shape as `a[1..3]` and `a[1  ..  3]`, which come back closed up and
as `a[1 .. 3]` respectively. `SpaceKind.Preserve` has existed in the IR since milestone 1 and
nothing produced it; these two gaps are the first, and the C# front end resolves them against the
source rather than carrying the third state into the writer.

⚠ A slice pattern is **not** in this set and looks as though it should be: `a is [1, ..var r]` comes
back `.. var r`, a space the oracle inserts, because `skala_space_within_slice_pattern = true` really does
govern its own construct and stays Tier A. Reading `space_within_spread_pattern` as the
collection-expression twin of it — which is what its name says — put a space Skala had no evidence
for into 58 lines of `corpus/real/`.

`skala_space_within_spread_pattern` is demoted from Tier A to Tier D and its fixture withdrawn,
for the same reason `trim_trailing_whitespace` is Tier D under SK-DIV-0006: an option Skala honours
and Rider ignores is a divergence wearing a tier badge.

⚠ **Current, at `8cbd66d`: the demotion holds and the entry is resolved.**
`skala_space_within_spread_pattern` is **Tier D**, `defaultSource: unknown`, with no fixture —
which is the withdrawal this entry describes, verified in the registry rather than remembered. Its
twin `skala_space_within_slice_pattern` is **Tier A**, which is the other half of the
argument. `skala_remove_spaces_on_blank_lines` remains Tier D and inert. The 58 lines this cost
`corpus/real/` are gone: sweeping all 380 files' divergent hunks for a `..` inside brackets returns
two lines in one file, and both are a *range* expression (`line[TerminatorPrefix.Length..]`) whose
divergence is SK-DIV-0008's indentation, not a spread element's spacing.

- options: `skala_space_within_spread_pattern` (Tier D), `skala_space_within_slice_pattern` (Tier A)
- ⚠ status: **resolved at M3.1**, re-verified

## SK-DIV-0010 — the oracle has break points of last resort that Skala does not

When nothing else will make a line fit, the oracle breaks in places no option names and no
construct owns. Three of them occur in `corpus/real/`, all in code that was generated or that has
very long identifiers:

```csharp
public partial class                       // between `class` and the type's name
    CustomersDataTable : global::System.Data.DataTable, global::System.Collections.IEnumerable {

global::System.Xml.Schema.XmlSchemaSequence    // between a type and the declarator's name
    sequence = new global::System.Xml.Schema.XmlSchemaSequence();

JsonConvert                                    // at the *only* dot of a one-dot chain
    .DeserializeObject<PublicParameterizedConstructorRequiringConverterWithParameterAttribute>(json);
```

Skala has no break point at any of these gaps, and adding one is not a matter of a missing option:
the first two are gaps between a keyword and an identifier, which the break-position model
([04](plan/04-formatting-engine.md)) has no vocabulary for, and the third contradicts
`skala_wrap_before_first_method_call = false`, which is the key that says the first dot stays with its
receiver — the oracle honours it right up to the point where the line cannot be made to fit and then
breaks there anyway.

⚠ The argument for leaving them is that all three produce output the author did not write and would
not want, and Skala's answer is a legal line that is merely too long. The counter-argument is R1:
these are `ClassDeclaration` and `IdentifierName`, which are not rare. It was 12 lines across 4 files
at M3.1.

⚠ **Current, at `8cbd66d`: the harness's *brace placement* class, which this entry dominates, is
5 lines across 4 files** — down from 12, and the same 4 files. The narrowest signature within it —
a hunk where the oracle left `class`/`struct`/`record`/`interface` alone at the end of a line — is
**2 hunks over 4 lines across 2 files**. The other two shapes (a declarator's name on its own line,
and a break at the only dot of a one-dot chain) have no signature that separates them from ordinary
wrapping, and no shipped instrument isolates them. ⚠ Under R1 as re-stated
([16](plan/16-risks-and-open-questions.md) § "The rule, re-stated") `ClassDeclaration` is at
**0.07 %** attributed share and passes; this entry is no longer an R1 objection, which is a change in
the *rule* rather than in the code and is worth saying rather than quietly dropping the sentence.

- options: `skala_wrap_before_first_method_call` (Tier A), `skala_wrap_multiple_declaration_style`
- ⚠ status: **open and deliberate** — a decision not to implement, measured

## SK-DIV-0011 — ⚠ MOOT, folded into SK-DIV-0050: the same fact measured on a sole-argument lambda

The oracle sometimes breaks after a lambda's `=>` and sometimes chops the body instead:

```csharp
var geometry = Build(list =>                      Assert.Throws<ArgumentException>(() => UvStacking.Fold(
    list.Add(Sliced(0, 0, 200, 100) with { … })           islands,
);                                                        [new(0, 1, false, 0f)],
                                                          out _
                                                      )
                                                  );
```

Both bodies fit on a continuation line; both calls are the sole argument, so
`place_single_method_argument_lambda_on_same_line` does not separate them; both lambdas are
parenthesis-free or parenthesised in either direction, so the parameter form does not either. Both
of the ordering rule's two questions give the same answer for the two shapes, and the layout the
oracle picks is the one with *more* lines in the second case, so a line-count preference does not
explain it.

⚠ Implemented as the `=`'s rule — a break point after the arrow with `PrefersOuterBreak` — it fixes
five files and breaks five others, and costs 0.02 points of line fidelity and 0.5 of file fidelity
on `corpus/real/`. That is the measurement, and it is why the gap has no rule rather than the wrong
one. It was recorded at 45 lines across 21 files at M3.1.

⚠ **Current, at `8cbd66d`: 12 hunks over 40 lines across 7 files.** Signature: a hunk in which a
line the oracle wrote ends in `=>` and Skala's does not. ⚠ **The "45 lines across 21 files" this
entry carried was the whole *line break presence: Skala left a line the oracle joined (phase 2)*
class, which is still 45/21 and which this entry shares with SK-DIV-0007** — so the old figure was
the class's, not the entry's, and the entry's own signature accounts for 40 of the class's 45 lines
but only 7 of its 21 files. It remains the second largest single class after SK-DIV-0005.

⚠ **Triage at `425b01d0`: `moot`, superseded by SK-DIV-0050. Not because it stopped reproducing —
because it turned out to be the same fact under a different shape.** Re-measured with a generated
sweep: `Outer(() => Fold…(islnds, se…));` — a lambda that *is* the sole argument, which is the case
this entry says separates it from everything else — held at a fixed total while width is moved from
the body call's name into the body call's argument list.

- At a total of 131 the oracle never takes the arrow at any inner width, and neither does Skala:
  **conformant across the whole sweep**.
- At a total of 155 the oracle takes the arrow at inner widths 20, 24 and 28 and declines it from 32
  up; Skala takes it at none.

That is a threshold on the inner argument list's own width, and it is the *same* threshold
SK-DIV-0050's sweep finds for an assigned lambda. The "discriminator is unknown" this entry was
opened for is SK-DIV-0050's discriminator; the "both are the sole argument, so
`place_single_method_argument_lambda_on_same_line` does not separate them" observation is correct
and is exactly why it does not need its own entry — SK-DIV-0050 already records that restricting the
break point to non-sole arguments moves `corpus/real/` not at all, 380 files byte-identical.

⚠ **The count moves with it, it is not lost.** The 12 hunks over 40 lines across 7 files —
signature: a line the oracle wrote ends in `=>` and Skala's does not — is the corpus evidence for
SK-DIV-0050's missing break point, and is recorded there. This entry keeps the two hand-found shapes
because they are the shapes that first showed the arrow moving, and they still reproduce.

- options: `skala_place_single_method_argument_lambda_on_same_line` (Tier A, `true`, `oracle-probe`)
- ⚠ status: **moot** — superseded by SK-DIV-0050, which carries the fact, the size and the count

## SK-DIV-0012 — three small shapes, each measured, each left

Collected rather than given entries of their own, because each is one or two lines and the argument
for all three is the same: the rule is known, the implementation is not free, and the residue is
smaller than the risk.

1. **A cast before a collection expression that breaks takes a space.** `(Kind[])[a, b]` closes up
   and `(Kind[]) [\n    a,\n    b\n]` does not — the space depends on the *resolved mode* of the
   group after it, which the space rules cannot see. Implementing it means an `IfBroken` node around
   a gap. Worth 1 line of `corpus/real/`, and adding the space unconditionally costs 6 lines and 5
   files.
2. **A single-statement anonymous function's block is joined onto one line.**
   `Action a = () => {\n    Write("a");\n};` comes back `Action a = () => { Write("a"); };`, and two
   statements do not. `skala_keep_existing_embedded_block_arrangement` governs it — flipped to `true`, the
   oracle keeps the break — and there is no `place_simple_anonymousmethod_on_single_line` key in the
   export to hang it on. Worth **0 lines** of `corpus/real/`: nobody in the corpus writes one.
3. **A `for` header's clauses chop.** `for (init;\n     cond;\n     step)` rather than filling.
   Worth 4 lines across 2 files at M3.1, both of them the same generated `DataSet`.

⚠ **Current, at `8cbd66d`**, each measured over the 380-file dump by its own signature:

| | Signature | Now | Was (M3.1) |
|---|---|---|---|
| 1. cast before a collection expression | a hunk with `) [` on the oracle's side and `)[` on Skala's | **1 hunk, 1 line, 1 file** | 1 line |
| 2. single-statement anonymous function block joined | a hunk whose oracle side contains `=> { … }` on one line | **0 hunks — the corpus still contains none** | 0 lines |
| 3. `for` header chops | a hunk mentioning a `for (` header | **5 hunks, 13 lines, 3 files** | 4 lines, 2 files |

⚠ Item 2's zero is a *corpus* fact and not a quality one: nobody in 380 files writes the shape, so the
rule is untested rather than unneeded — the same distinction
[16](plan/16-risks-and-open-questions.md) § R3 keeps making about a rule that fires nowhere. It is
not evidence that the divergence does not exist.

`skala_keep_existing_embedded_block_arrangement` is **Tier A** (`false`, `oracle-probe`),
so item 2's governing key is implemented; what is missing is the placement decision it interacts
with. `resharper_csharp_align_multiline_for_stmt` is **Tier D**.

⚠ **Item 3 is closed at milestone 3.2, and it was filed against the wrong key.** It is not
`align_multiline_for_stmt` — that key is masked by `skala_align_multiline_statement_conditions = true` and
returns the same file at either value. Skala had **no break point at the header's `;`** and broke
inside the incrementor expression instead. `skala_wrap_for_stmt_header_style` is the key
that governs it, it is now implemented and **Tier A**, and the signature is down from 5 hunks over 13
lines across 3 files to **2 hunks in 1 file** — neither of which is a `for` rule any more: the
initializer's own `=` continuation lands on the align column where the oracle puts it one level
further in, unchanged by that branch and owned by `PlanAroundEquals`.

- options: `skala_keep_existing_embedded_block_arrangement` (Tier A), `skala_wrap_for_stmt_header_style` (Tier A, item 3, at M3.2), `resharper_csharp_align_multiline_for_stmt` (Tier D, and never the cause of item 3)
- ⚠ status: **items 1 and 2 open**, item 3 **closed** at M3.2, all three measured

## SK-DIV-0013 — three configured rewrites the oracle would not perform

At the time of this measurement, `skala_null_checking_pattern_style = not_null_pattern`,
`skala_empty_string = empty_literal` and `skala_braces_redundant = true` were all set in the export, all listed in
[06](plan/06-arrangement-and-syntax-styles.md), and `jb cleanupcode` 2025.2.6 performs **none** of
them. Swept as elements and as `CSCodeStyleAttributes` attributes, with the corresponding
`resharper_arrange_*_highlighting` keys left at their exported severities and raised to `warning`:
`if (p != null)` stays, `string.Empty` becomes `string.Empty` and stops, and `{ { x; } }` keeps both
pairs. The sweep is `docs/oracle-cleanup-profile.md`.

The reading that fits is that `skala_null_checking_pattern_style` and `empty_string` govern the pattern
ReSharper **generates** — in a quick-fix, a generated `Equals`, a "check parameter for null" action —
rather than a cleanup of code that already exists. They are code-*generation* settings that happen to
live in the same file as the cleanup settings.

Skala performs all three, because the export asks for them and doc 06 lists them. They are pinned by
hand-written fixtures in `ArrangementRuleTests` rather than by the oracle, and they are **excluded
from the M4 changed-span agreement number**: measuring a correct rewrite against an oracle that never
moves would score it as a divergence and make the number say the opposite of what it means.

⚠ The `is not null` rewrite carries a *second*, deliberate divergence on top of this one, and it is
the one doc 06 § "Null and pattern style" asks for: Skala skips it when the operand's type — or any
of its base classes — declares a user-defined `operator ==`. The operator form calls the user's
operator and the pattern form is a reference comparison the language performs itself, so the rewrite
changes which code runs while leaving code that still compiles. Layer 2 cannot see it (no diagnostic)
and layer 3 cannot either (no identifier changed meaning); only the precondition stops it.

- options: `skala_null_checking_pattern_style`, `skala_empty_string`, `skala_braces_redundant`

## SK-DIV-0014 — ⚠ RETIRED. Parenthesis removal was gated behind `--aggressive`; the gate is lifted

**This divergence no longer exists.** It is kept as a record because the reasoning that closed it is
the part worth having.

The oracle's cleanup profile removes redundant parentheses and Skala's default did not.
[06](plan/06-arrangement-and-syntax-styles.md) asked for exactly that gate "for the first release
regardless", and named the condition for revisiting: "when the corpus differential shows zero
divergences".

⚠ **That condition could never be met, and it took a second measurement to see why.** A gated rule
contributes divergences *by being gated*, so "wait until `--aggressive` shows zero divergences before
un-gating" is a test the gate itself keeps failing. The original entry recorded the symptom without
noticing it: "The condition for revisiting is in the doc and is not yet met: `--aggressive` is not at
zero divergences either."

What settled it instead was the price, re-measured over 401 files against the cleanup profile:

| | changed spans agreed |
|---|---|
| gate on | **59.43 %** (2 035 / 3 424) |
| gate off | **63.68 %** (2 183 / 3 428) |

4.25 points, against an oracle whose own profile performs the rewrite, on the single largest item in
[17](plan/17-inspection-parity.md)'s parity measurement — `ArrangeRedundantParentheses` fires more
often on Vixen than any other inspection Skala did not perform.

⚠ **The decisive change is not the number.** The gated rule carried a precedence table and was
arithmetic-only; the caution was really about that table. The rule now proves each removal by
re-parsing the enclosing expression and comparing the tree, so "these parentheses are redundant" is
checked rather than asserted. The gate was protecting against a mechanism that is gone.

⚠ The earlier numbers in this entry (77.61 % / 81.63 % over 391 files, a 4.02-point gate) are not
comparable with the pair above: the cleanup profile has since gained `ArrangeNamespaces` and
`ArrangeArgumentsStyle`, so the oracle changes more and there are more spans to agree about.

- options: `dotnet_style_parentheses_in_arithmetic_binary_operators`, `dotnet_style_parentheses_in_other_binary_operators`, `skala_parentheses_redundancy_style`

## SK-DIV-0015 — the oracle inserts a blank line before the first type; Skala preserves the source

A file that opens with a comment block and then declares a type, with **no blank line between them in
the source**, comes back from `jb cleanupcode` with one inserted. Skala leaves the gap as written.

```csharp
// … the last line of a leading comment block.
class C {          // ← the oracle puts a blank line above this; Skala does not
```

⚠ **Found by a fixture written for something else**, which is the argument for adding fixtures that
are not about the thing you are working on. `constructs/trivia/a-malformed-doc-comment-is-left-alone.cs`
was written to pin hazard 2 of the xmldoc work and happens to open this way; nothing in 324 other
construct fixtures or 380 real files had the shape. It is one line and one file.

The rule behind it is not established. `skala_blank_lines_around_type = 1` would explain the
oracle's answer if the leading comment counts as the preceding *member*, but
`skala_stick_comment = true` says a comment binds to what follows it, which would make the blank
line belong *above* the comment instead — and there is nothing above it. Whether the oracle special-cases
a file-scope comment block, or treats the compilation unit's start as a member boundary, wants a sweep
of its own before anything is implemented.

Measured: 1 line, 1 file of `constructs/` (324 files). Not observed in `corpus/real/`.

⚠ **Re-measured 2026-08-30: it does not reproduce.** Skala formats
`constructs/trivia/a-malformed-doc-comment-is-left-alone.cs` into the committed
`.expected.cs` **byte for byte** — the blank line the oracle inserts is now Skala's too. Confirmed a
second time on a fresh two-line probe (`// comment` / `// comment` / `class C {`), where Skala and
the oracle both insert the blank line and the two outputs are identical. `RequirementFor` states
`BlankLinesAroundType` at the compilation unit's first member, and the leading comment block no
longer suppresses it.

- options: `skala_blank_lines_around_type`, `skala_stick_comment`,
  `skala_blank_lines_before_single_line_comment`
- ⚠ status: **open**, pre-existing, exposed at the M9 merge
- ⚠ **triage 2026-08-30: `moot`.** It no longer reproduces, on the fixture that recorded it or on a
  minimal probe. ⚠ The entry's open question — whether the oracle special-cases a file-scope comment
  block or treats the compilation unit's start as a member boundary — was never answered and is now
  academic: the two engines agree, and the entry is kept for the record that a fixture written for
  something else is what found it.

## SK-DIV-0016 — the oracle's cleanup profile ignores `@formatter:off`; Skala honours it everywhere

`skala_formatter_tags_enabled = true`, `skala_formatter_off_tag = @formatter:off`,
`skala_formatter_on_tag = @formatter:on` and `skala_formatter_tags_accept_regexp = false` are
all in the export, and the two `jb cleanupcode` profiles disagree about them:

```
dotnet run --project Testing/Rikarin.Skala.Testing -- ask <dir>
dotnet run --project Testing/Rikarin.Skala.Testing -- ask <dir> --profile=SkalaCleanup
```

| profile | tasks | the region between the tags |
|---|---|---|
| `SkalaFormatOnly` | `CSReformatCode` | **byte-identical** |
| `SkalaCleanup` | the arrangement half M4 built | **rewritten** |

On one probe — `constructs/arrangement/formatter-tags/a-region-survives-arrangement.cs`, and the
`.arranged.expected.cs` beside it is the oracle's committed answer — the cleanup profile reached
inside the tags and:

- dropped the trailing comma from the hand-aligned `int[,]` initializer,
- folded `public  int  Old( )   { return 1; }` into `public  int  Old( ) => 1;`,
- folded `{ return new List<int>(); }` into `=> new()`,
- and rewrote `private System.Int32 Width() { return 3; }` into `int Width() => 3;` — three separate
  rules, in a region whose author had said not to.

`a-node-straddling-a-tag-is-skipped-whole.arranged.expected.cs` is worse to read: both tag comments
end up dangling in the middle of expression bodies the profile created around them.

⚠ **Skala respects the tags under both.** [00](plan/00-mandate-and-non-negotiables.md)'s
non-negotiable 9 — the reference tool is a test subject, not a specification — is the whole argument.
`@formatter:off` is not a formatting preference that arrangement may take a different view of; it is
the one place in the tool where a person has said in words what they want, and the pass that ignores
it is the pass whose output `git revert` is the only way back from
([06](plan/06-arrangement-and-syntax-styles.md) § "The line between `format` and `arrange`").

⚠ **It costs nothing measurable against the oracle's *formatting* number**, because the format-only
profile agrees: all three fixtures in `constructs/arrangement/formatter-tags/` come back byte-identical
from `CSReformatCode` and Skala reproduces all three exactly. What it costs is agreement on the
*arrangement* differential over those three files, which is the point of the entry.

- options: `skala_formatter_tags_enabled`, `skala_formatter_off_tag`,
  `skala_formatter_on_tag`, `skala_formatter_tags_accept_regexp`
- ⚠ status: **permanent**. There is no configuration under which Skala should arrange inside the tags.

## SK-DIV-0017 — a comment that *mentions* the tag is prose; the oracle says it is a directive

`skala_formatter_tags_accept_regexp = false` makes the match literal. The oracle reads "literal"
as a plain substring test over the comment's whole text — measured, not inferred, over nine shapes:

| comment | oracle | Skala |
|---|---|---|
| `// @formatter:off` | tag | tag |
| `//@formatter:off` | tag | tag |
| `// @formatter:off because the table is hand-aligned` | tag | tag |
| `void A() { } // @formatter:off` (trailing) | tag, from the next line | same |
| `// we support @formatter:off here` | **tag** | prose |
| `// see @formatter:off` | **tag** | prose |
| `` // `@formatter:off` `` | **tag** | prose |
| `/// <c>@formatter:off</c>` | **tag** | prose |
| `/* @formatter:off */` | tag | tag |

Skala's rule: **the tag must be the first thing in the comment**, after the marker and any
whitespace. Deliberately not an equality test — a reason written after the tag is the commonest way
anyone writes one, and refusing that would trade this footgun for a worse one.

⚠ **The footgun is not hypothetical and it fired inside this repository.** Four of Skala's own source
files carry a comment discussing the directive, and under the oracle's rule the half of each file
below that comment was silently not being formatted; nothing reported it. The fuzzer found the same
thing from the other end — `./build.sh Lint` refused to format `Testing/Rikarin.Skala.Testing/Fuzzer.cs`,
because a paragraph explaining the directive switched formatting off and an interpolated string below
it then tripped `SK9099` (SK-FUZZ-0005). A file that documents a directive should not be governed by it.

Measured cost, at the commit that made the change: **zero lines.** `skala format --check` over
`Analysis Core Distribution Formatting Reporting Tools` is clean before and after — the newly-visible
tails were already in canonical form — and no file in `corpus/real/`, `corpus/constructs/` or
`corpus/pathological/` contains a comment that mentions the tag without being it. So this buys the
refusal and pays nothing for it today; what it costs is that a repository shared with Rider could
disagree about one file, in the direction of Skala formatting more.

⚠ **A second, smaller difference in the same machinery, measured at the same time and *not* changed.**
The oracle protects the `off` comment's own line including its indentation, and re-indents the `on`
comment's line to the surrounding level. Skala protects both. Skala therefore leaves untouched one
line the oracle moves:

```
class C {                              class C {
            // @formatter:off                      // @formatter:off      ← both keep this
    void  M( ) { }                         void  M( ) { }                 ← both keep this
            // @formatter:on                       // @formatter:on       ← oracle moves to 4
```

Skala matched the oracle on the second line and not the first before this milestone, which was the
worst of the three possible answers; it now protects both. Protecting more than the oracle is the
safe direction for an escape hatch, and a person reading the two tag lines as the boundary of their
own block expects neither to move.

- options: `skala_formatter_tags_enabled`, `skala_formatter_off_tag`,
  `skala_formatter_on_tag`, `skala_formatter_tags_accept_regexp`
- ⚠ status: **permanent**, pinned by `Formatting.CSharp.Tests/FormatterTagTests` rather than by a
  corpus fixture — a fixture recording the oracle's answer here would lower the format-fidelity
  ratchet to pin a divergence that costs nothing on any real file.

## SK-DIV-0018 — on a file with mixed line endings the oracle normalises; Skala keeps each gap's own

`skala_enforce_line_ending_style = false` means an existing line ending is kept rather than
rewritten. Skala reads that per **gap**: every break in the output ends the way that break ended in
the input, and only a break the formatter *inserts* has to choose. The oracle reads it per **file**:
asked directly, `class C { // fuzz<CRLF>} <CR>` comes back with all three of its breaks as lone
`<CR>`, the CRLF included.

```
input          class C { // fuzz<CRLF>} <CR>

oracle         class C {<CR>    // fuzz<CR>}<CR>          ← one ending, chosen for the file
Skala          class C {<LF>    // fuzz<CRLF>}<CRLF>      ← each gap as the author left it
```

⚠ The disagreement is only reachable on a file whose endings are **already** mixed, which is a
corrupt file rather than a style. Every file in `corpus/real/` is internally consistent, and on a
consistent file the two readings give the same answer on every line. `pathological/mixed-crlf-and-lf.cs`
and `pathological/crlf-throughout.cs` are both exact.

Skala's reading is kept, because per-gap preservation is what "keep the existing ending" says and
because normalising is the one thing a formatter must not do to a file it was told not to normalise:
a repository with a deliberate CRLF fixture inside an LF tree would have it silently rewritten.

⚠ **This entry is the reason `pathological`'s ratchet fell** from 0.9636 to 0.9589 when
`mixed-line-endings-after-a-trailing-comment.cs` was committed — SK-FUZZ-0003's 22-byte
reproduction, which the tool could not process idempotently until it was fixed and which the corpus
therefore never held. The three lines it loses are this divergence and nothing else. See
`Testing/corpus/fidelity.json`.

- options: `skala_enforce_line_ending_style`, `end_of_line`, `skala_insert_final_newline`
- ⚠ status: **permanent**, pinned by the fixture above.

## SK-DIV-0019 — the wrap column is measured from after the `///`, not from column 0 — **CLOSED**

⚠ **This entry's title used to be "the oracle keeps the word that crosses `max_line_length`; Skala
breaks before it", and that model was wrong.** It was consistent with all five committed fixtures
and with nothing else. What follows is the record of a reading that fitted the evidence it was built
from, because the way it was wrong is the useful part.

### What it said

> Skala fills a documentation line while the *whole line* — code indent, `///`, marker space, content
> indent and text — stays within `skala_xmldoc_max_line_length`. The oracle fills while the line
> is **strictly under** the limit and then keeps the word that crosses it, breaking after it.

and, immediately after it, the part that should have been read as a refutation rather than a caveat:

> ⚠ **One part of the shape is measured and not explained.** Probed at three nesting depths with a
> `<summary>` of sixty identical five-letter words, the oracle produced content lines of 125, 129 and
> 133 columns — a constant 113 columns of *content* — but the **first** content line of each was one
> word shorter than the rest, at every depth. The first-line reservation is unexplained.

### What it is

Three rules, each probed against `OracleProfile.DocComments` at more than one value rather than read
off the fixtures. ⚠ The probe uses **single-character words**, and that is the whole methodological
point: with five-letter words the fill can only land every six columns, so a budget of 113 and a
budget of 118 produce identical output and the old model could not be told from this one.

1. **The measured width excludes the code indentation and the three slashes, and includes the
   marker's space.** A line is inside the margin when `1 + indent + content ≤ max_line_length`.
   Probed at code indents 4, 8 and 12 the content widths are *identical*, which is exactly what the
   old note recorded as a constant 113 and read as a coincidence; probed at `max_line_length = 100`
   they move to `1 + indent + content ≤ 100`. So the same sentence wraps identically however deeply
   its declaration is nested, and the file's own columns run `codeIndent + 3` past the margin.
2. **An element's content is laid out starting at the column its start tag closes at**, and moving
   the start tag onto a line of its own does not reset that. Probed at start-tag widths 6, 7, 9 and
   45 at four content indents, the first content line's fill begins at `tagIndent + tagWidth` every
   time — `<param name="averyveryverylongparametername">` gives away 36 columns more than `<value>`.
   That is the "first content line is one word shorter" shape, and it is not a reservation: the
   content never left the tag's line as far as the fill is concerned. A break the *author* wrote
   between the tag and its content removes it, which is the control that makes it a rule.
3. **An element is opened up when its content overflows from that column, and the end tag is not in
   the comparison.** `<summary>` closes at column 9, so 110 columns of content stay flat at 136 file
   columns and 111 are opened. The `</summary>` rides past the margin on the last line.

### What it cost, and what it closed

One arithmetic in `XmlDocFormatter` (`budget = max_line_length − marker`, dropping `− indent − 3`)
and a carried column in `XmlDocRenderer`. Seven of the family's nine failing fixtures agree byte for
byte afterwards: the five here, plus SK-DIV-0021's, which was rule 3 all along, plus SK-DIV-0020's,
which needed one further structural fix. `harness xmldoc --oracle` goes 13/22 → 20/22.

⚠ **It moved no line outside a doc comment**, which the containment property asserts and
`./build.sh Fidelity` confirms: `constructs` 98.53 %, `real` 99.64 %, `pathological` 95.45 %,
unchanged to the line. `harness xmldoc real` rises 96.05 % → 96.28 % of lines and 47.89 % → 48.95 %
of files against the format-only oracle, which measures how much the sub-formatter rewrites rather
than whether it is right.

⚠ **The fix is not a Tier A claim and none of the seven was promoted.** None has ever appeared in
the key-flip sweep, so no instrument that can make a claim about the option across its domain has
spoken; six `resharper_xmldoc_*` keys were promoted on exactly this kind of evidence and demoted the
same afternoon. `XmlDocOracleTests.Unswept` names the seven, asserts in both directions that they
still agree, and shrinks to nothing when the sweep reaches them.

⚠ **One consequence to know before the next `skala format` of this tree.** Doc comments re-wrap
seven columns wider at a code indent of four, so the 17 files SK-DIV-0023 lists as carrying damage
from `1aad86f8` re-wrap again. The damage is unchanged in kind — it is a lost column inside a
verbatim region, which no wrap column touches — but the surrounding prose moves, so a repair commit
should revert those comments to their pre-`1aad86f8` form and re-format rather than diffing against
what is there now.

- options: `skala_xmldoc_max_line_length`, `skala_xmldoc_wrap_lines`,
  `skala_xmldoc_wrap_text`, `skala_xmldoc_wrap_tags_and_pi`,
  `skala_xmldoc_linebreaks_inside_tags_for_elements_longer_than`
- ⚠ status: **closed**, pinned by the five fixtures above, by
  `Conformance.Tests/XmlDocOracleTests` and by `XmlDocColumnTests`, which carries the probe
  arithmetic so the model cannot drift back without a diff.

## SK-DIV-0020 — the oracle opens an element that holds text *and* children; Skala opens one that holds only children

`skala_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements = true` puts an element's
children on lines of their own. Skala applies it to an element whose content is *only* elements, and
leaves mixed content — prose with an element inside it — on one line while it fits. The oracle
applies it to mixed content too, and hoists the prose onto its own line as it goes:

```
input    /// <remarks>Some leading prose. <list><item>Short.</item></list></remarks>

oracle   /// <remarks>
         ///     Some leading prose.
         ///     <list>
         ///         <item>Short.</item>
         ///     </list>
         /// </remarks>

skala    /// <remarks>Some leading prose. <list><item>Short.</item></list></remarks>
```

The pure-children case agrees exactly — `constructs/xmldoc/…_with_child_elements` is Tier A — so the
key itself is honoured and the disagreement is about what counts as "an element with child
elements".

⚠ **Fixed, and the rule is narrower than "mixed content".** Probed against
`OracleProfile.DocComments`, what opens the parent is not prose beside *an* element but prose beside
a **multi-line** one. The control is one element substituted for another:

```
input    /// <remarks>Some leading prose. <para>Short.</para></remarks>
oracle   /// <remarks>Some leading prose.
         ///     <para>Short.</para>
         /// </remarks>                          ← the prose stays on the start tag's line
```

`<list>` holds only `<item>`s, so `with_child_elements` opens it, and an element holding one that is
open cannot itself be flat; `<para>` holds only text and fits, so nothing about `<remarks>` has to
open and its prose stays put. The fix is `Structural` — the structure half of `IsMultiline`, split
out so `FlatNodes` can ask it of a child without asking the width question, which is the parent's
own.

⚠ **The `<para>` line above is a shape Skala still gets wrong**, and it is recorded rather than
fixed: Skala breaks after `<remarks>` and the oracle does not. It has no key of its own and no
fixture reaches it — it is the same "content never left the start tag's line" mechanism as
SK-DIV-0019's rule 2, applied to a break the width did not force.

- options: `skala_xmldoc_linebreak_before_singleline_elements`
- ⚠ status: **closed on the fixture**, pinned by
  `constructs/xmldoc/skala_xmldoc_linebreak_before_singleline_elements.xmldoc.expected.cs` and by
  `XmlDocColumnTests.AnElementHoldingAMultilineChild_HoistsItsProseToo`; the `<para>` shape above is
  open and unpinned.

## SK-DIV-0021 — ~~the oracle leaves an unlisted element's content on one line however long~~ — **REFUTED, and it was SK-DIV-0019**

⚠ **The reading this entry recorded is measured false, and the entry itself said which measurement
would settle it.** It ran:

> `skala_xmldoc_linebreak_before_elements` names eight elements … and `item` is not one of them.
> Asked to format a `<list>` whose single `<item>` runs to 131 columns, the oracle **leaves it at 131
> columns**. ⚠ The reading this suggests — that the oracle wraps *inside* an element only when
> `skala_xmldoc_linebreak_before_elements` names it — is consistent with every fixture in the subtree, `<summary>`
> included, but it is a reading of five files rather than a probe of the rule, and it is written down
> as such.

Probed: the **same `<item>`** with longer content is opened up and wrapped, and so are `<exception>`
and `<description>`, none of which the key names. Adding `item` to `skala_xmldoc_linebreak_before_elements` is not
what decides it. What decides it is SK-DIV-0019's third rule — an element is opened when its
*content* overflows from the start tag's closing column, end tag excluded. The fixture's `<item>`
closes at column 14 and carries 102 columns of content, which is 116 against a budget of 119, so it
stays flat and the `</item>` rides past the margin. At 107 columns of content it opens. Nothing about
`skala_xmldoc_linebreak_before_elements` is involved.

⚠ The entry's own last sentence — "note that it also overshoots the margin, which is SK-DIV-0019
again; the two are separable because this line is 11 columns over and no single word explains that" —
is where the join was available and was declined. No single word explained it because the "crossing
word" model was wrong; the overshoot is the end tag, exactly as SK-DIV-0019 now says.

- options: `skala_xmldoc_linebreak_before_multiline_elements`
- ⚠ status: **retired into SK-DIV-0019**, pinned by
  `constructs/xmldoc/skala_xmldoc_linebreak_before_multiline_elements.xmldoc.expected.cs`, which
  now agrees, and by `XmlDocColumnTests.AnElementIsOpened_WhenItsContentOverflows_NotItsEndTag`,
  which carries both sides of the threshold.

## SK-DIV-0022 — `skala_xmldoc_spaces_inside_tags = false` means "do not add one", not "remove the author's"

Skala reads `skala_xmldoc_spaces_inside_tags` as a statement about the output: false means the
gap between a tag and its content is empty, whatever the author wrote. The oracle reads it as a
statement about what it may *insert*: false means it will not add a space, and a space already there
survives — even while the same run is rebuilding the comment's line structure around it.

```
input    /// <summary> Text … </summary><returns> A value. </returns>

oracle   /// <summary> Text … </summary>          ← spaces kept, elements still split
         /// <returns> A value. </returns>

skala    /// <summary>Text …</summary>            ← spaces removed
         /// <returns>A value.</returns>
```

⚠ The fixture is deliberately two crammed elements rather than one tidy line, for the reason
SK-DIV-0006 now records under `space_after_triple_slash`: on a comment the oracle is otherwise
leaving alone it changes nothing at all, and a "no change" that means "not asked" is exactly the
reading this project has been burned by twice.

⚠ **Fixed, and both values were probed rather than one inferred from the other.** "Do not add one"
and "add exactly one" are not symmetric readings, and taking the second from the first is how a key
gets demoted:

| author wrote | at `false` | at `true` |
|---|---|---|
| `<summary> Text. </summary>` | `<summary> Text. </summary>` | `<summary> Text. </summary>` |
| `<summary> Text.</summary>` | `<summary> Text.</summary>` | `<summary> Text. </summary>` |
| `<summary>Text.</summary>` | `<summary>Text.</summary>` | `<summary> Text. </summary>` |
| `<summary>  Text.  </summary>` | `<summary>  Text.  </summary>` | `<summary> Text. </summary>` |

So `true` *is* a statement about the output — exactly one space each side, and the author's two
collapse to one, which is what Skala already did. `false` is a statement about what the run may
insert: nothing is added and the author's own run survives, **per side and verbatim**. The gap is
recorded on the element (`XmlDocElement.InnerLead` / `InnerTrail`) and read only by `Flat`, because
an element the run *opens up* has its content re-flowed and loses the spaces — measured, the oracle
drops them there too.

⚠ Single-line content only, and the reason is a defect this found: Roslyn's `XmlElementSyntax.Content`
carries each continuation line's `///` with it, so the whitespace run at the end of a multi-line
content is the **marker's** space and not a gap anybody wrote inside a tag. Taking it as one reflowed
`<summary>One. Two.</summary>` with a space before its end tag.

- options: `skala_xmldoc_spaces_inside_tags`
- ⚠ status: **closed**, pinned by
  `constructs/xmldoc/skala_xmldoc_spaces_inside_tags.xmldoc.expected.cs` and by
  `SpacesInsideTagsFalse_DoesNotAddOne_AndDoesNotRemoveTheAuthors`, which carries the table above.

## SK-DIV-0023 — the blank line after a processing instruction carries no trailing space

Two shapes on one construct, both visible in one fixture. **The first is now fixed** and is kept here
because it is the record of what the marker exemption cost.

```
oracle   /// <?skala-probe mode="short"?>
         /// ␠                                    ← marker, space, nothing
         /// <summary>A summary that follows a processing instruction.</summary>

skala    /// <?skala-probe mode="short"?>         ← (1) fixed: the marker space is applied
         ///                                      ← (2) open: no trailing space
         /// <summary>A summary that follows a processing instruction.</summary>
```

1. ~~**The marker space is not applied to a processing-instruction line.**~~ **Fixed.** Skala emitted
   a processing instruction verbatim — that is the refusal reason `skala_xmldoc_pi_attribute_style`
   and its three siblings carry — and "verbatim" had swallowed the `///` marker along with the
   instruction, on a key that is Tier A on every other line of the same comment.

   ⚠ It was never only the processing instruction. The same exemption governs a `<code>` and a `<c>`
   body, and there it produced output no fixture reached: a `<c>` whose content starts on its start
   tag's line has one body line that never carried a marker, so opening the element up wrote it as
   `///Func&lt;int&gt;` — no space at all — in a comment whose every other line had one. That is what
   `skala format` did to two of this repository's own files, and it is why the fix is a change to how a
   verbatim line is *captured* rather than to how one is written: `XmlDocModel.SourceLines` removes the
   marker's space on the way in, all-or-nothing across the region, and `XmlDocFormatter` writes it back
   on the way out. A sample's own columns are what is left in between, the round trip still compares
   them to the byte, and a region that is not uniformly marker-spaced is shifted whole rather than
   flattened.

   ⚠ **The damage the old behaviour committed does not fully repair itself**, and that is worth
   knowing before the next `skala format` of this tree. On the text an author wrote the fix produces
   the right answer; on text the defect has already written — `1aad86f8` put `///if (…)` above
   `/// throw new …` in seventeen files — the column that told the two lines apart is gone, so the pair
   comes back as `/// if (…)` above `///  throw new …`. Faithful, idempotent, and one column wider than
   the author's. Repairing those properly means reverting those comments to their pre-`1aad86f8` form
   and re-formatting; guessing at it in the formatter would mean flattening a code block written under
   a marker-less convention, which is the one thing the verbatim rule exists to prevent.
2. ~~**The blank line the oracle writes after a processing instruction carries a trailing space.**~~
   **Fixed, and it was never about the processing instruction.** It was refused on this argument:
   "Skala's carries none, deliberately and for a reason that is stated at
   `XmlDocOptions.BlankLineAfterPi` and holds elsewhere: an empty line's trailing whitespace is the
   one thing every other pass in Skala strips. Unlike (1) this half is a decision."

   ⚠ That is a fact about Skala offered where a measurement was needed, and the measurement was one
   probe away. Asked at `skala_xmldoc_max_blank_lines_between_tags = 1`, the oracle writes `/// ` for **every**
   blank line it keeps — the ones between two tags as well as the one after a `<?…?>`, and whether or
   not the author's blank line had the space. So the space belongs to the marker, which is exactly
   what half (1) concluded and then was not carried across the empty case. The condition is now
   `Verbatim` rather than emptiness, so a blank line inside a `<code>` block still has none: those
   columns are the sample's and this space is the option's.

- options: `skala_xmldoc_blank_line_after_pi`, `skala_space_after_triple_slash`
- ⚠ status: **closed on both halves**, pinned by
  `constructs/xmldoc/skala_xmldoc_blank_line_after_pi.xmldoc.expected.cs`, which now agrees, by
  `MaxBlankLinesBetweenTags_IsHonoured`, which carries the generalisation, and by
  `ABlankLineInsideAVerbatimBlock_IsNeitherACrashNorATrailingSpace`, which carries the exception.
  ⚠ The entry names `skala_space_after_triple_slash`, which is **Tier A**: the key is reproduced
  everywhere its fixture exercises it, and this is a construct that fixture does not reach.
- ⚠ **What was missing was an assertion, not a fixture.** The fix for (1) was reported as still
  failing on a multi-line verbatim body, on the grounds that no `constructs/xmldoc/` fixture reaches
  one — and that is true of the construct corpus. It was never true of `real/`: five files under
  `real/vixen/` carry the shape, and `XmlDocPropertyTests.TheMarkerSpace_IsOnEveryLineTheSubFormatterWrote`
  fails on all five plus the processing-instruction fixture when run against the pre-fix formatter.
  The property is a comparison rather than a scan — a comment the sub-formatter *refused* may carry
  anything its author wrote; what it may not do is introduce a marker without the option's space —
  and it is the check that found the defect in the first place (`git diff | grep '^+\s*///[^ /]'`
  over this repository's own sources), moved out of a review and into the suite. ⚠ A re-report of the
  same shape against a **daemon** started before the fix would still reproduce it: nothing in
  `DaemonProtocol.Version` encoded the formatter build, so a daemon served the code it was launched
  with until it was stopped. ⚠ Historical — the daemon is deleted and this hazard with it.
## SK-DIV-0024 — a type parameter list wraps when the list overflows, not when the declaration does

T5a gave a type parameter list its first break points: at `skala_wrap_before_type_parameter_langle = false`
— the export's value — the oracle wraps the list itself, as a fill, and Skala now does the same. What
it does not reproduce is *which* of a generic declaration's two lists ReSharper chooses to wrap when
only the declaration as a whole is over the margin.

```csharp
// the oracle moves T5 down; Skala chops `(int a)` instead
public void ManyParams<T1, T2, T3, T4, T5>(int a) { }        // list ends at 116, line at 131

// the oracle chops the parameter list; arming the fill by the declaration makes Skala wrap <T0,T1,T2>
public void Information<T0, T1, T2>(string messageTemplate, T0 v0, T1 v1, T2 v2) { }
```

Both are the same question — two constructs on one line, one break needed, which one gives — and the
ordering rule (`GroupFacts.PrefersOuterBreak`) answers it for `=` and `=>` and has no fact that
answers it here. The two available readings were measured on `corpus/real/` rather than argued:
arming the fill by the *list's* own width costs **0.00** points of line fidelity, arming it by the
*declaration's* head costs **0.14** (99.53 % → 99.39 %), and adding `PrefersOuterBreak` to the second
recovers only half of that (99.50 %). Skala takes the first: a type parameter list wraps when the
list itself runs past the margin.

⚠ A second, smaller shape is left with it. Under `align_multiline_type_parameter_list = true` a
*single* type parameter wider than the margin is not wrapped at all, because the alignment column is
read after the anchor's gap has been written and here that gap is the break — so the group the break
belongs to is not open when the break is emitted, and the writer renders it flat.
`constructs/breaks/type-parameter-single.cs` is that shape, kept out of the aligned fixture for that
reason.

⚠ **Triage at `425b01d0`: the two halves get different verdicts. First half `deliberate`; second
half `debt`, size S.**

⚠ **The recorded example is right and its stated reason is wrong, and the correction matters.**
Re-measured with a generated sweep — the declaration's total width held fixed while width is moved
between the method *name* and the *type parameter list* — the oracle's choice flips on a sharp
threshold in the type parameter list's own width. At a total of 124 columns: list widths 20 through
27 chop the parameter list, 28 through 40 wrap the type parameter list. The flip is one column wide.

But the threshold is not a rule anyone would want:

| declaration width | 122 | 124 | 128 | 140 | 160 |
|---|---:|---:|---:|---:|---:|
| first list width that wraps the list | 29 | 28 | none in 20…40 | 20 | 33 |

Non-monotonic, with a total width at which the oracle never wraps the list at all. That is the same
curve SK-DIV-0005's margin sweep and SK-DIV-0050's arrow sweep produce, at a third construct — see
SK-DIV-0050 § "The two facts this family is made of", where this half is recorded as a member of the
preference fact.

⚠ **And the gap between the two rules is wider than the entry's title suggests.** "A type parameter
list wraps when the list itself runs past the margin" describes Skala accurately; what it does not do
is approximate the oracle even loosely. Measured directly:
`public void EtaOne<TFirst…, TSecond…, TThird…, TFourthTypeParam>(int a, int b) { }` at 122 columns
has its type parameter list ending at column **104**, sixteen columns inside the margin, and the
oracle wraps the list anyway. The oracle's rule is not "the list overflows" under any reading.

**First half: `deliberate`, and here is the argument that survives the oracle's retirement.** A type
parameter list wraps when the type parameter list is too long, and not otherwise. It is a rule a
person can state in one sentence, predict without running the formatter, and explain to someone
holding a diff: *the thing that overflowed is the thing that got wrapped.* The oracle's answer is a
threshold that varies non-monotonically with a width the reader cannot see, and reproducing it would
mean a user could not predict which of the two lists moves without measuring both. Choosing the
predictable rule over the faithful one is a position, not a shortfall — and it happens to be free,
at **0.00** points of line fidelity against **0.14** for arming the fill by the declaration's head.
⚠ It is deliberate on the *rule*, not on every line: the shapes above stay divergent and are
expected to, and that is the cost the position is paying.

**Second half: `debt`, size S.** Re-measured on `constructs/breaks/type-parameter-single.cs`. At the
export's `align_multiline_type_parameter_list = false` both engines break inside the `<`, byte
identical. At `true` the oracle still breaks and **Skala emits the declaration flat at 125
columns**. That is not a style difference: Skala holds a break point for that gap, knows the line is
over the margin, and renders it flat anyway, because the alignment column is read after the anchor's
gap has been written and the group is not open yet. A formatter leaving a line over its own
configured maximum when it has a break point for it is wrong under any house style. Size **S** — the
ordering inside `CSharpDocumentBuilder`'s alignment scope, not a new break point or a new scope kind
— and it is confined to one value of one Tier key, which is why it has stayed cheap and invisible.

⚠ **Re-swept at one-column resolution, and the first half's premise does not survive.**
[sk-div-preference-sweep.md](sk-div-preference-sweep.md) sweeps
`public abstract void <name><T…>(int a, int b);` over totals 124–180 against the type parameter
list's width 10–100. There is **no threshold in the list's own width anywhere in the grid**: the
answer turns with the *total* and not within a row, and it is reproduced exactly by *break the
parameter list exactly when breaking it brings the head line within the margin, and reach further
out when it does not*. The declaration chops its parameter list up to the total at which that stops
being enough, and never after.

⚠ **The exactness survives a corrected denominator; the count that was published with it does not.**
Version 1 of the sweep read "4 977 of 4 977 cells, all three filler profiles", and those 4 977 per
profile included the 2 522 cells of the grid where the oracle takes neither list — it breaks between
the return type and the method name — which were being scored as agreements because the model
happened to say "not the parameter list" there. Version 2 holds them out: the law is **100.00 % of
the 12 409 cells where the oracle took one of the two lists** (4 154, 4 075 and 4 180 under
`uniform-5`, `varied-long` and `varied-short`), and the 2 522 third-break cells are reported beside
that number rather than inside it. The finding is unchanged and now says what it measured.

⚠ Two things the earlier reading got wrong, and both were the probe's. The recorded flip "at a total
of 124, list widths 20 through 27 chop the parameter list, 28 through 40 wrap the type parameter
list" does not reproduce; and the oracle's third answer — **breaking between the return type and
the method name, declining both lists** — was not in the earlier model at all, though it takes 2 522
cells of this grid. It is what "none in 20…40" was.

⚠ This does not overturn the `deliberate` verdict, and it improves the argument for it. "The thing
that overflowed is the thing that got wrapped" is close to the oracle's own rule rather than
unrelated to it; where Skala and the oracle part is that the oracle asks whether the break would be
*enough* and Skala asks whether the construct is *over*. That is a difference two sentences can
hold, which is what a `deliberate` verdict needs.

- options: `skala_wrap_before_type_parameter_langle`, `skala_align_multiline_type_parameter_list`, `skala_wrap_parameters_style`
- ⚠ status: first half **deliberate**, argued above and re-measured in
  [sk-div-preference-sweep.md](sk-div-preference-sweep.md); second half **open**, measured, `debt`
  size S.

## SK-DIV-0050 — a lambda's `=>` is a break point of the oracle's and not of Skala's

`BreakPlan` gives an *expression-bodied member*'s arrow a group (`PlanExpressionBody`,
`ArrowExpressionClauseSyntax`) and gives a **lambda**'s arrow nothing. Roslyn spells the two
differently — a member gets an `ArrowExpressionClauseSyntax` with the token on it, a lambda carries
`ArrowToken` directly on `LambdaExpressionSyntax` — so the gap after `=>` is not a break point of any
group, and a long lambda breaks at whatever point it does have: its parameter list, its body's
argument list, its body's binary chain, or the `=` above it. The oracle prefers the arrow to all
four. Measured at the margins named:

```csharp
// 100 columns — the oracle takes the arrow; Skala chops the lambda's parameter list
C2((SomeVeryLongParameterTypeName firstParameterName, AnotherLongTypeName secondName) =>
    firstParameterName
);

// 100 columns — over the body's own argument list, which Skala chops instead
Action g = () =>
    DoSomethingWithAVeryLongMethodNameHere(firstArgument, secondArgument, third);

// 80 columns — over the body's binary chain, which Skala breaks at the `+` instead
Func<int, string> f = value =>
    value.ToString() + "a suffix long enough to force a wrap ok";

// 50 columns — over the `=`, which Skala takes instead: `f =\n    value => value.ToString()`
Func<int, string> f = value =>
    value.ToString()
    + "a suffix long enough to force a wrap ok";
```

The last of those is the shape reported as a pre-existing divergence — "breaks after the `=` where
the oracle breaks after the `=>`" — and it is confirmed here as one instance of the missing point
rather than a fault of `PlanAroundEquals`.

Two facts about the gap are settled and are what a fix starts from.
`skala_wrap_before_arrow_with_expressions` governs which side of the arrow the break lands on for a lambda
exactly as it does for a member — at `true` the oracle writes `B((first, second)\n    => first.…` —
and **preservation is `keep_user_linebreaks`'s, not `skala_keep_existing_expr_member_arrangement`'s**,
which is the guess `PlanExpressionBody` invites. Four lambdas the author had already broken after the
arrow come back broken at the export's values, come back broken with the expression-member key
flipped, and re-join under `keep_user_linebreaks = false` and `skala_keep_existing_linebreaks = false`
alike. Skala already preserves those, through the ordinary source-break gap, so no preservation is
lost by the missing group — only the width-driven break is.

⚠ **What is not settled is when the oracle takes it, and that is why the point is not implemented.**
The obvious plan — a `Preserve` group on the body owning the leading gap, with
`GroupFacts.PrefersOuterBreak` — was written, measured and withdrawn. It reproduces every row above
and it costs `corpus/real/` a file (file fidelity 85.78 % → 85.53 %, five files better and five
worse, line fidelity flat). Restricting it to lambdas that are not the sole argument of a call
removes every regression *and* every improvement: the corpus does not move at all, 380 files
byte-identical either way. So `corpus/real/` cannot arbitrate this and the deciding evidence has to
be direct measurement — which produces a contradiction no ordering rule in `Fitter` can hold:

```csharp
// 120 columns, both 12 columns over, both `() =>` with a chop-able call body.
// The oracle breaks the arrow on one and chops the argument list on the other.
Action a2 = value => DoSomethingWithARatherLongNam(firstArgument, secondArgument, thirdArgument, fourthArg);
Action a4 = () => DoSomethingWithARatherLongName(firstArgument, secondArgument, thirdArgument, fourthArgumentNameIsLonger);
```

`Worth`'s first question ("does this break alone finish the job?") fires for both; its second ("does
the line end here anyway?") declines both. `Fitter.OuterBreakMargin` cannot separate them either:
the first needs a slack of at most 13 to break and the second needs more than 17 to decline, and a
third shape at 120 needs the arrow taken where any slack above 3 refuses it. That is the same
finding the margin's own remarks already record for the `=` — "no affine function of the numbers
this fitter has reproduces that curve" — arriving at a second construct.

This is SK-DIV-0024's sibling: two constructs on one line, one break needed, which one gives, and no
fact in `GroupFacts` that answers it. Both belong with SK-DIV-0002.

`constructs/wrapping/lambda-arrow.cs` is the fixture, and it deliberately holds the shapes where the
two agree — the call-bodied lambda whose arrow the oracle declines, the author's own break, the
sole-argument lambda, the parameter list laid out by the declaration keys — so that the agreement is
pinned while the disagreement is described here. The divergent shapes are kept out of it for the
reason SK-DIV-0024 keeps `type-parameter-single.cs` out of the aligned fixture: a fixture that
diverges takes the `constructs` file ratchet down with it, and one non-exact file cannot be diluted
by any reasonable number of exact ones.

⚠ **Triage at `425b01d0`: `debt`. Two sizes, because the entry is two things.** Re-measured with a
generated sweep rather than the recorded rows: `Action a = () => Call…(firstA, se…);` held at a
fixed total width while the width is moved between the call's *name* and the call's *argument list*.

- **The missing break point is unconditional and it is size M.** Over totals 122–166 the oracle takes
  the lambda's arrow on **every** row of the sweep, and Skala takes it on none — it chops the body's
  argument list every time. There is no width at which the two agree. **No one chose this**: Skala
  breaks after an expression-bodied *member*'s arrow and cannot break after a *lambda*'s, and the
  only thing separating the two cases is that Roslyn hangs the token on a different node. A
  difference in output that traces to a difference in a parser's node shapes is a defect by
  definition — there is no sentence about C# style that produces it. Fixing *that* needs a group on
  the lambda's arrow gap — machinery
  `PlanExpressionBody` already has — and it can be built and tested with no oracle at all.
- **The preference — when the arrow wins over the body's list — is size L and is the shared fact.**
  This is why the `Preserve`-with-`PrefersOuterBreak` attempt cost a `corpus/real/` file: it armed
  the point unconditionally.

⚠ **The contradiction this entry records as unexplainable now has a shape, and it is a threshold on
the *inner* list's own width.** At a fixed total of 131 columns, moving width from the call's name
into its argument list: at argument-list widths 20, 30, 40 and 50 the oracle **takes the arrow**; at
60, 70, 80 and 90 it **declines the arrow and chops the argument list**. Narrowed to one column the
flip is at 54. The two shapes the entry calls a contradiction are simply two sides of that
threshold.

⚠ **But the threshold is not a function anything in `Fitter` can hold, which is the finding that
matters.** The same sweep run at five totals puts the flip at:

| declaration width | 125 | 131 | 141 | 151 | 171 |
|---|---:|---:|---:|---:|---:|
| first argument-list width that declines the arrow | 58 | 54 | 50 | 52 | 54 |

Non-monotonic, and it falls and rises again — the same curve SK-DIV-0005's margin sweep reports for
the `=` and SK-DIV-0024's for the type parameter list, arriving now at a third construct. Three
independent constructs, one curve: the margin is not modelling ReSharper, it is modelling the error.

### ⚠ Closed at `acf28b23`: the arrow is an `=` whose floor is nine columns higher

[`sk-div-preference-sweep.md`](sk-div-preference-sweep.md) version 3 settles the preference half of
this entry, and the answer is that **there was never an arrow-shaped fact here.** Version 2 of that
sweep reported the arrow's floor as 58 against `eq`'s 0 and called it the one shape whose floor moves
with both its shape and its contents. It reached that by subtracting two shapes that differ by
*two* things: the arrow, and nine columns of head. The second turns out to be worth forty-nine
columns of floor.

Measured against an `=` built at the **same** head width, over the same inner construct, under the
same filler — six matched pairs, 24 measurements — the arrow's floor is higher by a constant that
runs 6 to 11, and forcing every one of them to **9** costs at most 0.26 percentage points of
agreement on any shape under any filler. Everything else the arrow needs, it shares with
SK-DIV-0005: the law (*break the inner construct when breaking it brings the head line within the
margin*), and the floor's dependence on the head width and on the content.

⚠ **One body shape is the exception, and it is a sentence rather than a number: over an operand
chain the arrow always wins.** `arrow-binary` breaks its body's chain in **0 of 5 082 cells**, where
an `=` at the same head width of 24 breaks it in 3 664. That is the law switched off rather than a
large floor, and — this is the part that matters for the fix — it needs no oracle to state, to
implement or to test.

So the two halves of this entry are now both buildable without ReSharper:

- The missing break point (size M) was always oracle-free, and still is.
- The preference (size L) is `PlanAroundEquals`'s own floor at the lambda's head width, plus 9,
  and unconditional over a binary operand chain. The `Preserve`-with-`PrefersOuterBreak` attempt
  cost a `corpus/real/` file because it armed the point unconditionally; the grid says
  unconditional is right for exactly one body shape and wrong for the rest.

⚠ **And it costs SK-DIV-0005 a correction, which is recorded there and repeated here because this
entry quotes it.** "Nothing to the left of the inner construct moves `F`" was established on four
shapes that all break at column 12. What sits *between* the outer break and the inner construct is
inert — a qualifier, a cast, a type argument list, and now a lambda's parameter list, which is why
`arrow-generic` reproduces `arrow` column for column across all 57 totals. Where the outer break
itself sits is not inert: the same argument list floors at 0, 49, 52 and 53 at head widths 12, 21,
29 and 50. The five-total table above reads as an unmodellable curve partly because every row in it
holds the head width fixed at one value.

### The two facts this family is made of

⚠ **This entry has been the family's anchor and the family is not one fact, it is two.** They are
recorded here once so the other members can point at them instead of restating them:

1. **The containment fact — "a group must be able to ask whether anything inside it will break."**
   No widths are involved: an inner construct is *certain* to span lines (the author broke it, or it
   is too long to do otherwise) and the container has to see that and break too. `Fitter` resolves
   the outer group first and carries one flat width per node, so the question cannot be asked.
   Members: **SK-DIV-0007** (an argument list around an author-broken binary chain),
   **SK-DIV-0077** (a call around a broken anonymous-method parameter list), **SK-DIV-0078** (an
   expression body's arrow over a broken binary-pattern chain). One mechanism closes all three, and
   — this is the important half — **it is statable and testable without the oracle**: "a construct
   that spans lines makes its container span lines" is a principle, not a fitted number. Size **M**
   once, not three times.
2. **The preference fact — "two constructs on one line, one break needed, which one gives."**
   Members: **SK-DIV-0005** (`=` versus the argument list), **SK-DIV-0024**'s first half (`<…>`
   versus `(…)`), and this entry (`=>` versus the body's list). In all three the oracle's answer is a
   sharp threshold on the *inner* construct's own width, and in all three that threshold moves
   non-monotonically with the outer width. Size **L**: it is a subsystem `Fitter` does not have, and
   unlike the containment fact **it cannot be settled after the oracle is uninstalled** — there is no
   principle to appeal to, only measurement, and the instrument goes away.

   ⚠ **That last sentence was the reason to sweep it, and the sweep has largely refuted it.**
   [sk-div-preference-sweep.md](sk-div-preference-sweep.md) measured all three members over a
   total-width × inner-width grid at one-column resolution, and two of the three turn out to have a
   principle after all: *break the inner construct exactly when breaking it brings the head line
   within the margin, and reach further out when it does not.* It is 100.00 % of SK-DIV-0024's grid
   and 99.96 % of SK-DIV-0005's, it carries no fitted number, and anyone can state it with nothing
   installed. **The three constructs are not one curve** — the "same curve at three constructs"
   reading came from three probes that each measured one construct at coarse resolution and read
   the shared shape of a *floor* as a shared law.

   What survives is narrower and much cheaper to keep: each construct has a minimum width `F` below
   which the oracle will not break the inner construct at all, and `F` is genuinely unmeasurable
   later — 0 for a type parameter list, 0 for a multi-argument call after `=`, 17 for a
   single-argument one, 29 for an array initialiser, 58 for a call under a lambda arrow.
   **This entry's own member is the one the principle does not carry**, at 70.69 % without `F`
   against 97.82 % with it, so `=>` is where the remaining irreducible measurement lives. Five
   numbers and a grid, rather than a subsystem's worth of unknowable surface.

   ⚠ **Version 2 of the sweep keeps all five numbers and withdraws the words "per shape".** Ten more
   shapes were measured, and `F` does not vary with the shape at all: it varies with what the inner
   construct is *made of*. A parenthesised argument list floors at 0, 17 or 29 according to whether
   the profile fits several arguments, one, or a lone string literal, and it does so identically
   behind a bare callee, a qualifier, a cast and a type argument list. A braced initialiser floors
   at 29 under every profile. A binary chain and a conditional need no floor: the margin law alone
   is 100.00 % of both. So the irreducible content is smaller than five numbers — two for argument
   lists, one for initialisers, none for operator chains — plus this entry's arrow, which is the
   only place a shape's own identity moves `F`.

   ⚠ **Two version-1 numbers were wrong and are corrected in the artefact.** `{ "…" }` was built two
   columns narrow, so `eq-array` × `single-literal`'s floor read 31 where it is 29 — which is the
   whole reason that construct's four content profiles appeared to disagree; they agree now. And
   cells where the oracle declined *both* constructs were counted as agreements, which inflated
   every construct that has a third break. Percentages in version 2 are **not comparable** with
   version 1's for `type-parameters`, `lambda-argument` or `member-chain`.

⚠ **SK-DIV-0011 has been folded into this entry as `moot`** — it is the same fact measured on a
sole-argument lambda instead of an assigned one, and its corpus count is carried below.

⚠ **The corpus count moves here from SK-DIV-0011: 12 hunks over 40 lines across 7 files, measured at
`8cbd66d` and _not_ re-measured at `425b01d0`.** It is carried, not confirmed, and it is labelled
that way deliberately — the signature it counts (a hunk in which a line the oracle wrote ends in
`=>` and Skala's does not) is this entry's missing break point, so the number belongs here, but the
next reader should re-derive it rather than quote it. On the `8cbd66d` basis it is the second
largest single class after SK-DIV-0005.

⚠ **The preference half is now captured as data, and it is one number wide.**
[sk-div-preference-sweep.md](sk-div-preference-sweep.md) sweeps this entry's shape over totals
124–180 against the body's argument-list width 10–100, one column at a time, under three filler
profiles that differ only in how long the arguments' identifiers are. **All three agree on the flip
column at 57 of 57 totals**, so the boundary is a fact about the oracle and the width and not about
the probe — the question that refuted two models here this week is answered for this one.

The curve is the recorded one, at full resolution: the flip falls 65 → 55 over totals 124–142 and
rises 55 → 61 over 142–180, with the minimum sitting immediately past total 137, which is where
taking the arrow stops fitting on its own. Non-monotone, as recorded, and now to the column.

⚠ **What it reduces to.** Against the two-term model the sweep fits at every construct — *break the
inner construct when breaking it brings the head within the margin **and** the inner construct is at
least `F` columns wide* — this entry scores 97.82 % at `F = 58` for a multi-argument body, against
70.69 % for the margin term alone. Unlike SK-DIV-0005 and SK-DIV-0024, the margin term does not
carry it: at a total of 124 chopping the body would have been enough from width 10 and the oracle
keeps the arrow to 64, fifty-five columns past sufficiency, and the gap closes only as the total
approaches 180. **So `F = 58` is this family's irreducible measurement — one constant, not a
surface** — and the 55…65 band the flip actually wanders through as the total moves is in the
committed grid and nowhere else.

⚠ **"One constant" is one constant per *content shape*, and this entry's two disagree by 24
columns.** 58 is the floor under the three profiles that differ only in identifier length; under
`single-literal` — a body whose argument list is one string, the shape that has no comma to break
at — it is **82**, and the law alone falls to 42.58 % against 70.69 %. Fitted over all four at once
it is 60 at 91.43 %. The sentence above is true of a multi-argument body and was graded on the
three profiles that share one; the fourth is in the same table and says something else. That spread
is the reason the sweep carries four content profiles and grades on the worst, and it is why this
entry, alone among the family, still needs its grid rather than its constant.

- options: `skala_wrap_before_arrow_with_expressions`, `skala_keep_user_linebreaks`, `skala_keep_existing_linebreaks`, `skala_place_single_method_argument_lambda_on_same_line`, `skala_wrap_parameters_style`
- ⚠ status: **open**, measured; the break point is missing. The rule that would arm it is now known
  to one constant and a three-column wander, both in
  [sk-div-preference-sweep.md](sk-div-preference-sweep.md).
## SK-DIV-0060 — the nine `disable_*` switches, measured; five of them are not divergences at all

ReSharper ships nine keys that **suppress a class of edit** rather than choosing between two
renderings, and they are the only options in this registry with that shape. It makes them unusually
testable — a suppressed class must come back byte-identical to the input in that respect — and it
makes the one-key-at-a-time sweep unusually bad at reaching them, because a switch over a family the
export has already switched off is inert until a *second* key moves. Three of the nine were on
record as "inert: the oracle returns the file unchanged at both values"; one of those three is
refuted below, and the fixture, not the key, was the reason.

Every row was measured against `jb cleanupcode` under `OracleProfile.FormatOnly` with this
repository's own `.editorconfig` and the key appended, on a subject wrong in spacing, indentation,
blank lines and wrapping **at once** — the negative control is the same file at the export's value,
which comes back reformatted in all four.

```csharp
public int Alpha ;                //   spacing
                                  //   three blank lines, past the cap
public void Method( int one,int two ) {
        var sum=one+two;          //   indentation
    if(sum>0){                    //   a break the rules introduce
    Alpha = sum;   // a trailing comment the author padded
    }
Call(one,                         //   a wrap the rules would rewrite
    two);
```

| key | what the oracle does with it at `true` | state |
|---|---|---|
| `disable_formatter` | returns the file **byte for byte** — not "formats less" | **implemented** |
| `disable_blank_line_changes` | every blank run survives; other line breaks still move | **implemented** |
| `disable_indenter` | spacing, blanks and wrapping still apply; line-start whitespace does not move | SK-DIV-0061 |
| `disable_space_changes` | every inter-token run survives; indentation and wrapping still apply | SK-DIV-0062 |
| `disable_line_break_changes` | no break added, none removed, blank runs included | SK-DIV-0063 |
| `disable_line_break_removal` | none removed; additions still happen | SK-DIV-0064 |
| `disable_int_align` | nothing — until `int_align = true` is supplied alongside | masked |
| `disable_space_changes_before_trailing_comment` | nothing, at either value of the rule it could gate | unreachable |
| `ignore_space_preservation` | nothing, on four shapes and three pairings | unreachable |

**The two implemented ones are not divergences and are recorded here for the method.** Both are
`Conformant` on `sweep verify` — two distinct outputs from each engine, agreeing at both values — on
`constructs/file/skala_disable_formatter.cs` and
`constructs/blank-lines/skala_disable_blank_line_changes.cs`. ⚠ They are left at Tier D
deliberately: a fixture pins one configuration and Tier A is a claim about the option, so the
promotion belongs to the key-flip sweep on master and not to the commit that added the fixture.

**`disable_int_align` is masked, and the recorded probe holds.** Re-measured rather than inherited:
on three adjacent declarations `int_align = true` alone pads them to a column, and `int_align = true`
plus `disable_int_align = true` produces output byte-identical to the export's own configuration. It
is decisive one key away from the export and inert at it — which is the shape the whole family has,
and the reason the rest were probed with a second key rather than alone.

**Two are unreachable, which is a different finding from "not done yet".**
`disable_space_changes_before_trailing_comment` has exactly one rule it could gate:
`skala_space_before_trailing_comment` normalises the gap to one space at `true` and to none at `false`, and
it does both **identically with this key on**. Its broad sibling `disable_space_changes` preserves
that same gap, so the gap is governed and it is the narrow key the C# formatter does not consult.
`ignore_space_preservation` moved nothing on four subjects and three pairings, including the three
places the formatter demonstrably *does* preserve spaces — a disabled `#if` branch, an
`@formatter:off` region, and an int-aligned run under `int_align = true`.

⚠ **The interaction hole is the finding, not an aside.** Six of these nine cannot be reached by any
one-key flip from the export's corner, and the committed sweep's "inert" verdict on three of them was
therefore true and useless in the same breath. This is the case `./build.sh Pairwise` was built for;
`disable_*` × `int_align`, `disable_*` × `skala_space_before_trailing_comment` and
`disable_*` × `keep_blank_lines_*` are the pairs that pay.

- options: `skala_disable_formatter`, `skala_disable_blank_line_changes`, `skala_disable_int_align`, `resharper_disable_space_changes_before_trailing_comment`, `resharper_ignore_space_preservation`
- ⚠ status: **not a divergence** on any of the five — two implemented and conformant, one masked, two
  unreachable. The entry is the measurement and the method; the four keys that *are* divergences have
  entries of their own below.

## SK-DIV-0061 — `disable_indenter`: the oracle stops reindenting — resolved

At `true` the oracle applies spacing, blank-line and wrapping rules exactly as it would otherwise and
leaves **line-start whitespace alone**: a line that existed in the input keeps the indentation it was
written with.

⚠ **"A line the wrapping created starts at column zero" is refuted, and by the shape rather than by
the key.** The original probe wrapped only at binary operators — where the created line does *not*
start at column zero, it starts with one space — and the entry recorded the wrong half of the rule
because one shape cannot separate the two. Re-measured on a fixture that wraps both ways, a created
line begins with **the break point's own flat rendering**: nothing after `(` and before `)`, one
space after `,` and before a binary operator.

```
var chopped = Compute(
alpha + beta,           ← nothing
 epsilon + zeta         ← one space
);                      ← nothing

var value = Compute(a)
 + Compute(b)           ← one space
```

That is `LineFlags.FlatSpace`, which the writer already carries for the flat case: the indenter being
off does not delete the gap the break replaced, it deletes the indentation in front of it.

```csharp
// oracle, disable_indenter = true
public void Method(int one, int two) {
        var sum = one + two;          // kept its eight
    if (sum > 0) {
    Alpha = sum;                      // kept its four
    }
    Call(
one,                                  // a line that did not exist: column zero
            two
);                                    // likewise
```

`LayoutWriter` now takes the input — only when the key is on, so the ordinary path does not
materialise a whole-file string — and `WriteSuppressedIndent` answers the two cases. ⚠ Only the
*emission* is suppressed: `Effective()` keeps returning the indentation the rules would have written,
so groups are still fitted against it. Which of the two columns `jb cleanupcode` measures its margin
against under this key is unmeasured, and the alternative would be a second rule invented from the
same probe.

- options: `skala_disable_indenter`
- ⚠ status: **resolved**. `verify skala_disable_indenter` on
  `constructs/suppression/skala_disable_indenter.cs` — Conformant, 2 of 2 values. It stays Tier D
  until the committed sweep reaches it; a `verify` run is not the sweep.

## SK-DIV-0062 — `disable_space_changes`: the oracle preserves every inter-token run; Skala collapses

⚠ **This key was on record as "inert: the oracle returns the file unchanged at both values", and that
is refuted.** The fixture was the reason and not the key: asked on a file whose spacing is actually
wrong, the oracle preserves every horizontal run between two tokens **byte for byte** while still
reindenting and rewrapping.

```csharp
// oracle, disable_space_changes = true
public int Alpha ;                        // the space before `;` survives
public void Method( int one,int two ) {   // so does every gap in the header
    var sum=one+two;                      // and the missing ones
        Alpha = sum;   // a padded trailing comment, on a line that was reindented
```

The *n*-bit half is what this cost. `CSharpDocumentBuilder.PreservedRun` hands the run to a
`DocKind.Space` node carrying its own text — a space and not a text node, because a preserved run may
sit immediately before a break point and a space is the only node the writer discards when the break
is taken. Written as text it would be trailing whitespace, which nothing else in this formatter can
emit. At a break point the run is emitted *before* the point with `flatSpace` off, so it appears when
the point stays flat and vanishes when it breaks.

⚠ The gap before a trailing comment is covered too, which its narrow sibling
`disable_space_changes_before_trailing_comment` cannot move at either of `skala_space_before_trailing_comment`'s
values.

- options: `skala_disable_space_changes`
- ⚠ status: **resolved**; supersedes the recorded inert claim, which was measured on a fixture with no
  wrong spacing in it. `verify skala_disable_space_changes` — Conformant, 2 of 2 values, with the
  two-space runs around `+` and the four-space run before the trailing comment reproduced byte for
  byte. Tier D until the sweep reaches it.

## SK-DIV-0063 — `disable_line_break_changes`: no break is added and none removed

The broadest of the break switches, and a strict superset of both SK-DIV-0064 and
`disable_blank_line_changes`: blank runs survive, breaks the wrapping rules would introduce are not
introduced, and breaks the author wrote are not joined. Spacing and indentation still apply.

"A different builder" turned out to be one branch. `CSharpDocumentBuilder.EmitGap` is the single
funnel through which a break is added or removed, so the key is answered there and nowhere else: a
gap the author left flat stays flat, a gap the author broke becomes a hard break with the author's own
blank count. The break plan is never consulted, which is what stops a wrapping rule adding anything;
`ResolveBlankLines` short-circuits to the source count, which is where the "blank runs included" half
lives and why the key is the union of `disable_blank_line_changes` and `disable_line_break_removal`
rather than a third rule.

- options: `skala_disable_line_break_changes`
- ⚠ status: **resolved**. `verify skala_disable_line_break_changes` — Conformant, 2 of 2 values.
  Tier D until the sweep reaches it.

## SK-DIV-0064 — `disable_line_break_removal`: one direction only

Measured apart from SK-DIV-0063 on the same file, and the two came out different, which is the whole
reason both entries exist: with `disable_line_break_removal = true` the three-blank run survived
**and** `skala_blank_lines_around_invocable` still inserted its blank after the closing brace, while
`disable_blank_line_changes` on the same file suppressed the insertion too. So this key is removals
only — the author's breaks are never joined and the cap never truncates a run — and additions are
untouched.

Implemented in the same funnel as SK-DIV-0063 and sharing exactly one of its two branches, which is
what "one-directional half" means in code: a gap the author broke becomes a hard break, and a gap the
author left flat falls through to every rule below it, so the wrapping still adds breaks the author
never wrote. `BreakPlan` is untouched — the near-miss route through `KeepsUserBreaksBetweenItems`
would have changed a value the wrapping functions read, and does not need to be taken.

The blank-line half is a clamp rather than a short-circuit: `ResolveBlankLines` resolves normally and
the result is raised to the author's own count. Two of the three blank-line systems only ever *reduce*
a run (the cap, the near-brace removal) and one only ever raises it, so `Math.Max` is exactly "the
reductions are off and the requirement is not" — which is what the oracle does, and what separates
this key from `disable_blank_line_changes` on the same file.

- options: `skala_disable_line_break_removal`
- ⚠ status: **resolved**. `verify skala_disable_line_break_removal` — Conformant, 2 of 2 values.
  Tier D until the sweep reaches it. ⚠ The nearest *other* implemented shape is `keep_user_linebreaks`,
  which is still not the same key: it governs the gaps between items of a list, and this governs every
  gap.
## SK-DIV-0030 — a chain whose receiver ends in `?.` is not chopped at all

`PlanChainedCalls` collects a chain's dots by walking the receiver side of each link. For a
`ConditionalAccessExpressionSyntax` it walks `conditional.Expression` and stops, on the argument —
correct where the conditional access is reached *from* an enclosing invocation — that "the `?.` is
the binding's dot, already added by the invocation above". When the conditional access is itself the
chain root that is not true: the whole chain hangs off `WhenNotNull`, which is never walked, so
`dots` comes back empty, no group is planned, and the chain has no break points.

```csharp
// the oracle, at 120 columns
var result = someCollectionOfThingsHere?.Where(item => item.IsEnabled)
    .Select(item => item.Name)
    .ToList();

// Skala: no chain group, so the argument list of the last call takes the break instead
var result = someCollectionOfThingsHere?.Where(item => item.IsEnabled).Select(item => item.Name).OrderBy(n => n
).ToList();
```

Found while building `constructs/alignment/outdent.cs`, which wanted a mixed-width chain — `?.` is
two columns and the dots after it are one — as the shape that tests whether one chain-wide outdent
amount is enough. The shape is out of that fixture and out of `align-declaration.cs`, because a
fixture carrying it would pin this defect rather than the options those files exist for.

It is a break-point defect and not an option's: no key in the `align_multiline_*` or `outdent_*`
family changes it at either value, and the outdent family's three implemented keys are conformant on
every chain that *is* chopped.

**The recorded model was confirmed, and the recorded *reason* was half wrong.** Dumping the tree
settles it: `a?.B().C()` splits into a `?` — the `ConditionalAccessExpressionSyntax`'s own operator —
and a `.` that belongs to the `MemberBindingExpression` under `WhenNotNull`, where *every* dot of the
chain lives. So walking only `Expression` collected the receiver's dots and none of the chain's, and
for the common shape (a bare identifier receiver) `dots` came back empty exactly as recorded. But the
comment's justification — "already added by the invocation above" — describes a path that does not
exist: the invocation arm's `MemberBindingExpressionSyntax` branch adds the dot and **returns without
recursing**, so `Collect` never reaches a conditional access from an enclosing invocation at all. It
is reached only from the chain root, from a `!` on the way out, or from an enclosing conditional's
`WhenNotNull`. The fix is `Collect(conditional.WhenNotNull)` before `Collect(conditional.Expression)`
— `WhenNotNull` first, because the list is outermost-first and it holds the dots right of the `?`.

⚠ **Fidelity did not move by a single line** — `constructs`, `real` and `pathological` are
byte-identical before and after — and that is a fact about the corpus, not about the fix.
`corpus/real/` has 102 files containing `?.method(` and **zero** in which a `?.` call is followed by
another `.` call, so `dots.Count < 2` returned early on every one of them and the defect could not
fire. The measured pin is `constructs/wrapping/chained-calls.cs`, which is where the five shapes and
their non-conditional control now live; with the fix reverted it takes `constructs` line fidelity
from 98.34 % to 98.29 % and turns `Fidelity_DoesNotDecrease(set: "constructs")` red.

⚠ **`outdent.cs` still does not get the shape, and the entry's description of it was wrong in a way
that mattered.** The chain it named — `a?.B().C()` — outdents 12 → 11 on every wrapped line, measured
at both values of `skala_outdent_dots`, which is the same answer to the column as the plain chain
that fixture already carried: under `skala_wrap_before_first_method_call = false` the leading `?.` is the
first invoked dot, so it is never a break point and never starts a wrapped line. There is no mixed
width in it. The shape that carries one is a *nested* conditional access, `a?.B()?.C().D()`, whose
second `?` is the only two-column operator that reaches the start of a line at this export's values,
and fixing SK-DIV-0030 and SK-DIV-0065 is what made it reachable. It settles what `outdent.cs` was
asking — the outdent is **per line, by that line's own leading operator** and not one chain-wide
amount — and it settles it *against Skala*, which spends one amount for the whole chain. That is
SK-DIV-0069, and it is the new reason the shape stays out: a fixture carrying it would demote a
Tier A key on the strength of an unrelated defect. The fixture now records the measurement instead.

⚠ `align-declaration.cs` does **not** want the shape, and never depended on this defect for that:
`align_multiline_calls_chain` anchors on a column that moves with the margin. Only the
cross-reference in its comment was stale.

- options: none — `skala_wrap_chained_method_calls` and `skala_wrap_before_first_method_call`
  are read correctly and have no chain to apply to
- ⚠ status: **resolved**, in two parts. `Collect` walks `WhenNotNull`, and `ChainDot` puts the break
  point on the `?` rather than on the binding's `.` (SK-DIV-0065 below, which the first part
  uncovered and which had to be fixed with it). Pinned by `constructs/wrapping/chained-calls.cs`
  (`RootedAtAConditionalAccess` and four sibling contexts, with the oracle's own answer regenerated
  under `SkalaFormatOnly`), and sabotage-tested against the pre-change formatter.
  ⚠ Fixing the root arm exposed three *further* chain-planner divergences that it had been masking,
  none of which is this one: SK-DIV-0066 through SK-DIV-0068 below.

## SK-DIV-0065 — the break before a conditional link lands on the `.`, not the `?`

Uncovered by SK-DIV-0030's fix, which is what made the shape reachable at all, and fixed with it
because leaving it would have made Skala's output on real code *worse* than the unchopped chain it
replaced. `Collect` registered a `MemberBindingExpression`'s `.` as the chain's dot; the `?` is the
enclosing `ConditionalAccessExpressionSyntax`'s own operator and a separate token, so a break on the
`.` stranded the `?` at the end of the line above.

```csharp
// the oracle
return model.GetDeclaredSymbol(current, cancellation)
        ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
    ?? current.Kind().ToString();

// Skala, with SK-DIV-0030 fixed and this one not
return model.GetDeclaredSymbol(current, cancellation)?
    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
    ?? current.Kind().ToString();
```

⚠ **That is Skala's own `ArrangementSafety.ContainerOf`**, and it is how the defect was found: the
`?.` fix made `skala format --check .` want to move three of the repository's own files, and the
change it wanted was wrong. The measured corpus could not have caught it — `corpus/real/` has no
multi-link `?.` chain at all — and neither could the first round of probes, because the token choice
is unobservable until the chain's *receiver* contributes a dot of its own. With a bare identifier
receiver the binding's dot is the last entry in `dots`, `skala_wrap_before_first_method_call = false` holds
the last entry back, and the `?` therefore never begins a wrapped line.

⚠ It is also what made the mixed-width chain *measurable* — the nested `?.` is the only shape at this
export's values where a two-column operator starts a wrapped line — and measuring it is what turned
up SK-DIV-0069. `constructs/alignment/outdent.cs` still does not carry the shape, because that
divergence would demote a Tier A key from a file that is not about it; it records the measurement.

- options: none — `skala_outdent_dots` is read correctly and was conformant on the `.` lines of
  the same chain throughout
- ⚠ status: **resolved**. `BreakPlan.ChainDot`. ⚠ Measured at
  `skala_wrap_before_first_method_call = false`, the export's value. What the *other* value does with the
  chain's leading `?.` is still unmeasured — at `true` the first dot becomes a point and this change
  decides whether the break lands before `a?` or before `?.`.

## SK-DIV-0066 — a chain whose last link is a property is not a chain root, so it is not chopped

⚠ **This one has no `?.` in it**, and that is how it was found: it is what remained when the same
eight shapes were re-measured with every `?.` replaced by `.`, as the control for SK-DIV-0030.

`IsChainRoot` requires the outermost node to be an `InvocationExpressionSyntax` or a
`ConditionalAccessExpressionSyntax`. A chain that ends in a property — `source.Where(…).ToList().Count`
— is outermost a `MemberAccessExpressionSyntax`, matches neither, and gets no group at all, which
reproduces SK-DIV-0030's exact symptom on a chain with no conditional access anywhere in it:

```csharp
// the oracle
var result = someCollectionOfThingsHere.Where(c => c.IsEnabled)
    .Select(c => c.Name)
    .OrderBy(n => n)
    .ToList()
    .Count;

// Skala: the argument list of the last call takes the break
var result = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).OrderBy(n => n
).ToList().Count;
```

⚠ The oracle breaks *before* the trailing `.Count`, which is not what
`skala_wrap_after_property_in_chained_method_calls = false` predicts on the reading in `PlanChainedCalls`'s
remarks ("the property travels with the call it feeds") — there is no call after it to travel with.
Whatever rule the oracle is applying to a chain-final property has not been identified.

⚠ **Triage at `425b01d0`: `debt`, size M.** Re-measured after this week's conditional-access
chain-root fix, which did not reach it:
`someCollectionOfThingsHere.Where(…).Select(…).OrderBy(…).ToList().Count` still comes back chopped
from the oracle and still comes back from Skala as

```csharp
var result = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).OrderBy(n => n
).ToList().Count;
```

**That output is the whole verdict.** It is a 106-column line followed by an orphaned `)` at column
zero of its statement, produced because the chain got no group at all and the last argument list
that could take a break took it. It is not a different opinion about where chains should break; it
is what a formatter emits when it has no opinion. No user would accept it and no argument
reconstructs it.

⚠ **SK-DIV-0068's item 2 belongs here and is now measured to.** `source?[0].Children.Where(…)…`
reproduces with the `?` removed as well — `source[0].Children…` — so an element access in a chain is
a break point of the oracle's and not of Skala's for exactly the reason this entry names: the
outermost node is not one `IsChainRoot` matches. Same predicate, same absence, one fix.

⚠ **So is item 1's `!`, and its untested reading is now tested.** `a.SelfLink()!.SelfLink()…`
reproduces with no `?.` anywhere in it. And at `skala_wrap_before_first_method_call = true` — the value
SK-DIV-0068 records as never having been asked — the oracle writes `receiver?.SelfLink()!\n
.SelfLink()` where Skala writes `receiver\n    ?.SelfLink()!`. Both values now agree with the
reading SK-DIV-0068 could only guess at: **the oracle treats everything up to and including the `!`
as the chain's receiver, and the call after the `!` as the chain's first method call.** That half is
a rule change inside the boundary computation, size S.

Size **M** for the entry: widening `IsChainRoot` past
`InvocationExpressionSyntax or ConditionalAccessExpressionSyntax` to reach a chain-final
`MemberAccessExpressionSyntax` and a chain-internal `ElementAccessExpressionSyntax` puts a group on
every `a.B().C().Prop` in the tree, and the group has to agree with the two indent scopes
`CSharpDocumentBuilder` opens from the same predicate. It is a new scope population, not a new
subsystem, and — unlike SK-DIV-0050's preference fact — **the target shape is not in doubt**: the
oracle's layout here is also the only sane one, so this can be finished after the oracle is gone if
it has to be. It should not have to be.

⚠ One thing genuinely stays unknown and is smaller than the entry implies: the oracle breaks
*before* a chain-final `.Count`, which `skala_wrap_after_property_in_chained_method_calls = false` does
not predict. That is one break's placement inside a fix whose shape is otherwise settled.

- options: none identified — `skala_wrap_after_property_in_chained_method_calls` is implicated
  but the shape is broken at both of its values
- ⚠ status: **open**, measured, unfixed. Deliberately not fixed alongside SK-DIV-0030: widening
  `IsChainRoot` puts a group on every `a.B().C().Prop` in the tree, which is a far larger wrapping
  change than the `?.` arm and wants its own measurement.

## SK-DIV-0067 — a property run that *straddles* the `?` is still cut in half

`skala_wrap_after_property_in_chained_method_calls = false` is a loop in `Collect` that walks back over a
run of `MemberAccessExpressionSyntax` receivers so the break lands before the run rather than after
it. It used to stop at the `MemberBindingExpressionSyntax`, which was wrong for every run touching a
`?`; it now ends on the `?` itself, which is right for a run that *begins* there and still one link
short for a run that begins to its left. Measured, at the export's values:

```csharp
// A — the run begins at the `?`. Conformant.
var a = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).FirstOrDefault()
    ?.Trim().ToUpperInvariant();

// B — the run begins at the `?` and carries two properties. Conformant.
var b = someParticularThingWithALongName.Self().Self().Self()
    ?.Inner.Children.Where(c => c.IsEnabled).ToList();

// C — the run straddles the `?`: `.Inner` is left of it, `.Children` right of it. Open.
//   the oracle
var c = someParticularThingWithALongName.Self()
    .Inner?.Children.Where(item => item.IsEnabled)
    .Select(item => item.Name)
    .ToList();
//   Skala: the run ends at the `?` and `.Inner` is left behind
var c = someParticularThingWithALongName.Self().Inner
    ?.Children.Where(item => item.IsEnabled)
    .Select(item => item.Name)
    .ToList();
```

⚠ The same chain with `.` for `?.` is conformant, measured, which is what makes the remainder a fact
about the conditional access and not about the property rule.

⚠ Half of this was fixed with SK-DIV-0030 rather than deferred, because `format --check` on Skala's
own repository would not pass otherwise: `VersionSources.Value` and `VersionSourcesTests` both carry
shape A, and until the run ended on the `?` the formatter wanted to write them the wrong way round.
Both files moved *toward* the oracle in the same commit — the committed indent was four columns
deeper than `jb cleanupcode` produces.

⚠ **Triage at `425b01d0`: `debt`, size M, and it is still its own fact.** Re-measured all three
shapes plus the `.`-for-`?.` control. A and B are still conformant. C still diverges to the column,
in exactly the recorded direction — the oracle writes `.Self()\n    .Inner?.Children…` and Skala
writes `.Self().Inner\n    ?.Children…`. The control with every `?.` replaced by `.` is still
byte-identical, so the remainder is still a fact about the conditional access and not about the
property rule.

⚠ **Checked specifically against this week's conditional-access chain-root fix: it does not close
this.** That fix moved where a chain's *root* is; shape C is about how far back the property-run
walk in `Collect` may travel from the break, which is a different loop. This entry is **not** a
duplicate of SK-DIV-0066 either: 0066 is a chain that gets no group at all, this is a chain that
gets the right group and the wrong break column.

**Why `debt` and not `deliberate`.** The two shapes are the same expression with one character
changed. `a.Self().Inner.Children.Where(…)` and `a.Self().Inner?.Children.Where(…)` break at
different places in Skala, and nothing about a null-conditional access is a reason for `.Inner` to
change sides. A rule that is right for a property run and silently wrong for the same run when a `?`
appears in the middle of it is not a position; it is a walk that stops one link short. There is no
sentence describing Skala's behaviour here that a reader would accept as intentional.

Size **M**. The remaining fix has to let the run continue past the `?` into the receiver's own
property chain, and the walk that would do it is the one the conditional-access arm already performs
on `Expression` — so it is a restructure that makes the two walks share a traversal rather than an
extra arm bolted onto `Collect`. ⚠ It is not urgent in the oracle's-retirement sense: the target
shape is unambiguous once stated, so this is repayable afterwards if it has to be.

⚠ The half that was fixed rather than deferred stays fixed, and the reason it had to be is worth
keeping: `format --check` on Skala's own repository fails otherwise.

- options: `skala_wrap_after_property_in_chained_method_calls` — read correctly, and conformant on
  every property run that does not straddle a `?`
- ⚠ status: **open** for shape C only, measured. The remaining fix has to let the run continue past
  the `?` into the receiver's own property chain, and the walk that would do it is the one the
  conditional-access arm already performs on `Expression` — so it needs the two not to collect the
  same dots twice, which is a restructure rather than another arm.

## SK-DIV-0068 — three smaller chain-planner divergences, measured together

All three surfaced from the same eight-shape probe and none is large enough to carry its own entry.
⚠ The third was re-confirmed on Skala's own `VersionSources.Value`, so none of them is exotic.

1. **A `!` between two links resets the oracle's "first method call" and does not reset Skala's.**
   On `a?.Self()!.Self().Self()…` the oracle keeps `a?.Self()!.Self()` on the opening line and Skala
   keeps only `a?.Self()!`, one link fewer. The reading that fits is that the oracle treats
   `a?.Self()!` as the receiver and the call after the `!` as the chain's first, which
   `skala_wrap_before_first_method_call = false` then holds back — but that reading has not been tested at
   the key's other value.
2. **An element access in the chain is a break point for the oracle and not for Skala.** On
   `source?[0].Children.Where(…)…` the oracle breaks after `[0]`; Skala keeps `.Children` attached.
   ⚠ Also reproduces without the `?.`, so it belongs with SK-DIV-0066 rather than with SK-DIV-0030.
3. **A chopped chain that is the left operand of `??` takes one continuation level from Skala and two
   from the oracle.** ⚠ Only observable when the chain chops *and* the `??` breaks; the obvious
   control — the same chain right of the `??`, and the same chain left of a `+` — is conformant, so
   this is not simply "a chain inside a binary operand". ⚠ **The `+` half of that control is wrong;
   see the triage below.**

⚠ **Triage at `425b01d0`: `debt`, but the entry is no longer three of anything.** All three still
reproduce; two of them turn out to belong to SK-DIV-0066 and the third's recorded shape is wrong.

**Item 1 — the `!`. Re-measured, re-attributed, and its untested reading is now tested.** It
reproduces with no `?.` in the expression at all (`a.SelfLink()!.SelfLink()…` diverges the same
way), so it is SK-DIV-0066's family and not SK-DIV-0030's. And at `skala_wrap_before_first_method_call =
true` — the value this entry records as never having been asked — the oracle writes
`receiver?.SelfLink()!\n    .SelfLink()` against Skala's `receiver\n    ?.SelfLink()!`. **Both
values now confirm the reading the entry could only propose:** the oracle treats everything through
the `!` as the chain's receiver and the call after it as the chain's first method call. Size **S**,
a rule change in the first-call boundary; recorded in SK-DIV-0066, which is where the fix lands.

**Item 2 — the element access. Re-measured, and it is SK-DIV-0066's, exactly as the entry suspects.**
`source[0].Children.Where(…)…` without the `?` diverges the same way as `source?[0]…`. The cause is
`IsChainRoot` not matching a chain-internal `ElementAccessExpressionSyntax`, which is the same
predicate 0066 has to widen. It costs nothing extra there.

**Item 3 — the `??` operand. ⚠ Re-measured and the recorded shape is wrong, in the direction that
makes it bigger.** The entry says the control "the same chain left of a `+`" is conformant and
concludes "this is not simply a chain inside a binary operand". Measured today, at the export's
values, on one file holding all three:

```csharp
// the oracle — and it is the same for `??` and for `+`
var w = someParticularThingWithALongName.SelfLink()
        .SelfLink()                                   // two continuation levels: 8 columns
        .SelectName(n => n.Name)
    + otherFallbackValueName.SomeFallbackProperty;    // the operator's own level: 4

// Skala — the operator line agrees, the chain's lines do not
var w = someParticularThingWithALongName.SelfLink()
    .SelfLink()                                       // one level: 4 columns
    .SelectName(n => n.Name)
    + otherFallbackValueName.SomeFallbackProperty;
```

The `+` control diverges too, and identically to `??` — the chain's continuation lines take two
levels from the oracle and one from Skala, while the operator's own line agrees at one. The
right-hand-operand control **is** conformant, and that is what names the
fact: a binary expression opens a continuation scope, and its **left** operand's own continuation
lines have to be inside it. Skala only enters that scope after the first break, so a chain that
begins on the statement's own line never gets the operand's level. It is not about `??`, and it is
not about coalescing — it is one indent scope entered one break too late. Size **S**, and it is
larger in reach than "only observable when the chain chops and the `??` breaks" implied.

**So this entry is now one item, not three**, and it should be read as the record of where the other
two went. It stays open because item 3 has no home elsewhere.

- options: none
- ⚠ status: items 1 and 2 **re-attributed to SK-DIV-0066**, which is where their fix belongs — still
  unfixed, in both places; item 3 **open**, re-measured, and its recorded control was wrong — the
  fact is every left binary operand, not `??`

## SK-DIV-0069 — `skala_outdent_dots` spends one amount for the whole chain; the oracle spends one per line

`constructs/alignment/outdent.cs`'s header asks whether one chain-wide outdent amount is enough, and
until now nothing could answer it: every wrapped line of every chain in the corpus begins with a
one-column `.`, so "the chain's amount" and "this line's amount" are the same number. The one shape
where they differ is a *nested* conditional access, whose second `?` is the only two-column operator
that reaches the start of a wrapped line at this export's values — and it was unreachable in both
engines' agreement until SK-DIV-0030 and SK-DIV-0065 were fixed. Asked at both values:

```csharp
// skala_outdent_dots = false, both engines
var result = someCollectionOfThingsHere?.WhereEnabled()
    ?.SelectName(item => item.Name)
    .OrderByName(name => name);

// skala_outdent_dots = true, the oracle: per line, by that line's own leading operator
var result = someCollectionOfThingsHere?.WhereEnabled()
  ?.SelectName(item => item.Name)     // 12 → 10, two columns
   .OrderByName(name => name);        // 12 → 11, one column

// skala_outdent_dots = true, Skala: one amount for the chain, and it is the `.`'s
var result = someCollectionOfThingsHere?.WhereEnabled()
   ?.SelectName(item => item.Name)    // 12 → 11
   .OrderByName(name => name);        // 12 → 11
```

⚠ **Inert at the export**, which sets `skala_outdent_dots = false`, and that is why this is a
divergence rather than a fidelity defect: at `false` nothing outdents and the two engines agree to
the column.

⚠ **The shape is deliberately out of `outdent.cs`.** `skala_outdent_dots` is Tier A and
the committed sweep records it Conformant on that fixture; adding the shape takes it to Divergent 1
of 2 — a demotion earned by the outdent arithmetic, not by anything the alignment fixture exists to
pin. The same trap caught `skala_wrap_before_first_method_call`, also Tier A, when the
first draft of `constructs/wrapping/chained-calls.cs` used a property receiver and so carried
SK-DIV-0067 shape C — and a third, `skala_wrap_after_dot_in_method_calls`, was not a
fixture problem at all but a real bug in SK-DIV-0065's fix: `ChainDot` registers the `?`, and the
token after a `?` is the `.` rather than the name, so "break after the dot" broke after the `?` and
wrote `…(more)?\n.Where(…)` where the oracle writes `…(more)?.\n Where(…)`. ⚠ **All three were found
by running `verify` on every key pinned to a fixture this branch changed**, which neither the
differential nor the ratchets can do: a fixture only ever measures the export's value, and all three
faults are at the *other* one. Ten keys are pinned to the five fixtures touched here; nine had to come
back unchanged and one had to improve.

⚠ **Triage at `425b01d0`: `debt`, size S, and it is a distinct fact from the other three chain
entries.** Re-measured at both values on the nested conditional access. At the export's
`skala_outdent_dots = false` the two engines are still byte-identical. At `true` the divergence is still
exactly one column on exactly the `?.` line — the oracle writes it at column 10, Skala at 11 — and
the `.` line agrees at 11 in both.

**`deliberate` is not available here, however small the difference is.** `skala_outdent_dots` means "pull
the wrapped line back by the width of the operator that starts it", and there is only one reading of
that sentence: an operator two characters wide pulls back two. Skala computes one amount for the
whole chain and spends the `.`'s, so a `?.` line is outdented by one — which is neither the option's
meaning nor any other coherent rule. Nobody decided this; the arithmetic was written where a chain
has one operator and the shape with two was unreachable until SK-DIV-0030 and SK-DIV-0065 were
fixed.

Size **S**: the amount is computed once for the group in the outdent scope and has to be computed
per break point from that break's own leading operator. No new group, no new scope kind, no new
break point. ⚠ It is not urgent against the oracle's retirement — the option's own wording settles
the target — but it is cheap, and being cheap is the argument for doing it rather than for leaving
it.

⚠ **The reason this stays a divergence rather than a fidelity defect is unchanged and is the
strongest part of the original entry**: the export sets `skala_outdent_dots = false`, nothing
outdents, and the two engines agree to the column on every chain in the corpus. The fixture
exclusion stands for the reason it was made — one non-exact file cannot be diluted — and so does the
finding beside it, that all three faults were found by running `verify` on every key pinned to a
changed fixture. ⚠ That practice is the reusable part of this entry and it should outlive the entry.

- options: `skala_outdent_dots` — read correctly, and conformant on every chain whose wrapped
  lines all begin with the same operator, which is every chain the corpus contains
- ⚠ status: **open**, measured, unfixed. It is an arithmetic in the outdent scope rather than in the
  chain planner: the amount is computed once for the group and has to be computed per break point.

## SK-DIV-0031 — a field with several declarators wraps after the type; Skala wraps at the commas

The oracle breaks a too-long multi-declarator *field* between its type and its first declarator and
then leaves the declarators alone; Skala keeps the first declarator on the type's line and chops at
every comma.

```csharp
// the oracle
System.Collections.Generic.List<int>?
    alphaFieldNameHere = null, betaFieldNameHere = null, gammaFieldNameHere = null;

// Skala
System.Collections.Generic.List<int>? alphaFieldNameHere = null,
    betaFieldNameHere = null,
    gammaFieldNameHere = null;
```

⚠ **A local declaration of the same shape agrees**, which is what makes this a field rule rather than
a declarator rule: `System.Int32 a = 1, b = 2, …` comes back identical from both engines, and it is
the fixture `constructs/alignment/align-declaration.cs` pins. The asymmetry is also why
`CSharpDocumentBuilder.AlignsFromOwnColumn` excludes a `FieldDeclarationSyntax`'s declaration from
`skala_align_multiple_declaration` — the oracle does not move a field's declarators at either value of that
key, so the exclusion is measured and not a consequence of this divergence.

Found while building that fixture, and kept out of it for the same reason as SK-DIV-0030.

⚠ **Triage at `425b01d0`: `deliberate`.** Re-measured, both halves: the field still comes back from
the oracle broken between its type and its first declarator, Skala still chops at the commas, and
the local declaration of the same shape is still byte-identical from both engines. Nothing moved.

**The argument, without the oracle.** A declaration with several declarators wraps at its commas, and
a field and a local wrap the same way. Both halves of that are defensible on their own terms:

- **Wrapping at the commas puts the break where the list is.** `List<int>? a = null,\n    b = null,\n    c = null;`
  gives one declarator per line, each aligned under the first, and the type stays where the eye
  looks for it. The oracle's layout — `List<int>?\n    a = null, b = null, c = null;` — breaks
  between the type and its declarators and then leaves all three crowded on one continuation line,
  which is the one break that does *not* separate the things being listed. If the line is too long
  because there are three declarators, the declarators are what should move.
- **A field and a local are the same construct and should not format differently.** This is the half
  the entry already proves and does not claim credit for: the oracle formats
  `System.Int32 a = 1, b = 2, …` one way inside a method and `List<int>? a = null, …` another way at
  class scope, for no reason a reader could name. Skala is the consistent one, and consistency
  between two spellings of the same declaration is exactly the sort of thing a formatter is for.

⚠ **The honest caveat, so this is not an acceptance wearing an argument.** This is a divergence
Skala is choosing to keep, not one that costs nothing: `CSharpDocumentBuilder.AlignsFromOwnColumn`'s
exclusion of `FieldDeclarationSyntax` from `skala_align_multiple_declaration` is downstream of it, and the
exclusion is measured — the oracle does not move a field's declarators at either value of that key —
so the exclusion stands on its own and does not have to be revisited if this position is ever
reversed. The shape does not appear in the harness's ranked classes, so nothing in the work queue is
waiting on it.

- options: `skala_align_multiple_declaration`, `skala_wrap_multiple_declaration_style`
- ⚠ status: **deliberate** — argued above; the difference is kept and the fixture keeps pinning the
  half where the two engines agree

## SK-DIV-0032 — `alignment_tab_fill_style` has three layouts and Skala wrote one of them, under the wrong name

⚠ **Fixed 2026-08-30; what is left is the fixture. See the section at the end of this entry**, which
also refutes two clauses of the model recorded below. The text down to it is kept as written, because
the refutation is only legible beside the claim it corrects.

`LayoutWriter.WriteIndentTo` wrote whole indent units and then spaces for the remainder, and its
remarks said that is "what `alignment_tab_fill_style = use_spaces` asks for". It is not. Asked under
`indent_style = tab`, the oracle gives three distinct layouts of the same alignment column, and the
one Skala writes is `optimal_fill`:

| value | column 12, block at 8 | chain aligned at 21 |
|---|---|---|
| `use_spaces` (the export) | 2 tabs + 4 spaces | 2 tabs + 13 spaces |
| `use_tabs_only` | 3 tabs | 5 tabs — column 20, short of the anchor |
| `optimal_fill` | 3 tabs | 5 tabs + 1 space |
| Skala, at every value | 3 tabs | 5 tabs + 1 space |

`use_spaces` indents in tabs only as far as the *enclosing block's* level and spells the alignment
remainder in spaces, which is what makes it "look aligned on any tab size"; `optimal_fill` divides
the whole column by the tab width. The two coincide whenever the block level and the alignment column
fall on the same side of a tab stop, which is why nothing caught it.

⚠ **Not reachable under this repository's configuration**, which sets `indent_style = space`; all
three values then produce identical output and the key reads as inert. That is why it sat on the
"never read by the C# formatter" list — every probe that asked it was indented with spaces. It is a
real divergence on a tab-indented configuration and it is Skala's to fix, not an option to implement:
the export's own value is the one Skala gets wrong.

⚠ **Re-measured 2026-08-30 under `indent_style = tab`, and the table above reproduces to the
character.** One probe — a statement condition aligned at column 15 inside a block at column 8 —
formatted by `jb cleanupcode` at each of the three values and by Skala:

| value | the continuation's indent |
|---|---|
| `use_spaces` — **the export's own value** | 2 tabs + 7 spaces |
| `use_tabs_only` | 4 tabs |
| `optimal_fill` | 3 tabs + 3 spaces |
| Skala, at all three | 3 tabs + 3 spaces |

`LayoutWriter.WriteIndentTo` is unconditional: `column / _indentWidth` whole units and spaces for the
remainder, with no reference to the key anywhere in the formatter. So Skala writes `optimal_fill`
under every configuration, and under the export's `use_spaces` it is wrong on every aligned
continuation line of every tab-indented file.

### ⚠ Fixed 2026-08-30 — and the model above was wrong in two places

`LayoutWriter.WriteIndentTo` now takes the line's **level column** beside its target column and writes
all three layouts; `PhaseOneOptions.TabFill` reads the key and `CSharpFormatter` passes it. Measured
against `jb cleanupcode` 2025.2.6 under `OracleProfile.FormatOnly` with this repository's
`.editorconfig` plus `indent_style = tab`, `tab_width = 4`, one value per invocation, on a probe
carrying a plain continuation and three statement conditions aligned at columns 12, 14 and 19. Tab
portion `»`, space portion `·`:

| column | block | `use_spaces` (the export) | `use_tabs_only` | `optimal_fill` |
|---|---|---|---|---|
| 12 (a *continuation*, not an alignment) | 8 | `»»»` | `»»»` | `»»»` |
| 12 (an alignment) | 8 | `»»····` | `»»»` | `»»»` |
| 14 | 8 | `»»······` | `»»»` | `»»»··` |
| 19 | 12 | `»»»·······` | `»»»»»` | `»»»»···` |

Skala reproduced the oracle byte for byte at all three values after the change, and at none of them
before it. Two clauses of the model this entry carried are **refuted by that run**:

- **`use_spaces` tabs to the line's own *level* column, not to the enclosing block's.** Rows 1 and 2
  are the refutation and they were never separated before: both land on column 12 inside a block at 8,
  and the oracle writes the plain continuation as three whole tabs while writing the aligned line as
  two tabs and four spaces. A continuation level is a level and stays tabs; only what *alignment* adds
  becomes spaces. Every probe in this entry's history used alignment alone, where the two readings
  coincide, so the "enclosing block" phrasing survived unchallenged — and it is wrong on every
  continuation line of every tab-indented file, which is far more lines than the aligned ones.
- **`use_tabs_only` rounds to the *nearest* tab stop, ties downwards — not down.** Column 14 goes down
  to 12 and column 19 goes *up* to 20. The "rounded down" claim rested on the single column-21 datum in
  the first table above, which rounds to 20 under either rule and so could not tell them apart.

- options: `skala_alignment_tab_fill_style`, `indent_style`
- ⚠ status: **narrowed**. The three layouts are implemented and pinned by
  `Formatting/Rikarin.Skala.Formatting.CSharp.Tests/TabFillStyleTests.cs`, which carries the oracle's
  own bytes at each value plus an idempotence round-trip and a space-indented control. What remains is
  **the fixture, and only the fixture**: the corpus has no per-directory `.editorconfig`, so no
  committed fixture can be tab-indented, so the key can carry no `oracle` glob, so the key-flip sweep
  cannot reach it — `verify skala_alignment_tab_fill_style` answers "not swept: no `oracle`
  fixture in the registry" after this change exactly as it did before. The key is therefore registered
  `OfInert` in `PhaseOneOptions`, beside `tab_width`, which is inert for the identical reason: read,
  honoured, and unobservable because every fixture there is is indented with spaces. It becomes `Of`
  and Tier A when that mechanism lands, and the promotion needs no further measurement — the table
  above is the specification.
- ⚠ **triage 2026-08-30: `debt`, size S.** Nothing was decided here — Skala wrote one of three
  layouts because `WriteIndentTo` was written before the question was asked, and the layout it
  wrote was not the one the export requests. There is no version of that to defend to a user: "you
  asked for tabs to the level and spaces for the remainder, and you got neither" is a defect. Paying
  it was three cases in one method plus the value on `LayoutWriter`; the *fixture* was and remains the
  larger half.

## SK-DIV-0033 — the oracle realigns a block comment's asterisks; Skala left the comment as written

⚠ **Fixed at the export's value 2026-08-30, and narrowed. See the two sections at the end of this
entry** — one corrects the shape of comment the key governs, the other records what is left. The text
down to them is kept as written, because the corrections are only legible beside the claims.

`skala_align_multiline_comments` is **`true`** in the export — one of very few keys in the
`AlignMultilineConstructs` family that is — and it moves each continuation line of a `/* … */`
comment whose lines begin with `*` onto the opening delimiter's column plus one.

```csharp
/*
 * A starred block comment.
   * A line whose asterisk is out of place.      // the oracle pulls both of these to ` * `
* Another one.
 */
```

At `false` the oracle returns the comment exactly as written, which is what Skala does at every
value. A block comment with no leading asterisks is untouched at either value, by both engines.

Recorded in `PhaseOneOptions` until now as "no probe found a shape where it changes the oracle's
output"; the probes were passing comments whose asterisks were already aligned.

Unimplemented rather than wrong-by-choice: it rewrites the *interior* of a comment token, which no
other key in this family does and which the formatter has no trivia rewriter for. It is the only
member of the family whose export value is the one Skala fails to honour, so it is a divergence at
the export's own values and not merely an unimplemented option.

⚠ **Re-measured 2026-08-30 and it reproduces at the export's value.** On a probe carrying a starred
block comment at file scope, a second one indented inside a type, and a third with no asterisks at
all, `jb cleanupcode` under `SkalaFormatOnly` pulls every starred continuation line to the opening
delimiter's column plus one — including the member-level comment, whose lines move from columns 8
and 2 to column 5 — and leaves the unstarred comment exactly as written. Skala returns all three
byte for byte as written.

### ⚠ Fixed at the export's value 2026-08-30, and the shape is narrower than this entry said

`LayoutWriter.AlignStarred` puts every continuation line on the opener's column plus one;
`CSharpDocumentBuilder.StarredFlag` decides which comments qualify; `PhaseOneOptions.AlignMultilineComments`
reads the key. On the probe below Skala's output is byte-identical to `jb cleanupcode` 2025.2.6's at
`true` — the export's own value — where before the change it matched at neither value.

⚠ **"A block comment whose lines begin with `*`" is too loose, and three shapes refute it.** Asked at
`true`, the oracle returns each of these **exactly as written**:

| probe | why it does not qualify |
|---|---|
| first continuation starred, second not | not *every* continuation line begins with `*` |
| first continuation unstarred, second starred | the same, from the other side |
| ⚠ every line starred, with one **empty** line among them | an empty line does not begin with `*` either |

The last is the one a "does it look like a javadoc block" heuristic gets wrong, and it is why the
implementation has no exemption for blank lines. The closing `*/`'s line counts as a starred line and
moves with the rest — which is what makes a comment with an unstarred *body* come out unqualified
rather than half-moved.

⚠ **The anchor is the opening `/*`'s own column and not the code's indent.** A block comment that
begins on a code line — `public int Trailing; /*` with the `/*` at column 25 — has its asterisks put
at 26, not at the member's 5. Measured.

### ⚠ Two adjacent facts this entry did not have, both measured on the same run

1. **At `false` the oracle freezes a starred comment *entire*, its opening `/*` included.** On a
   comment whose opener is written at column 8 where the code indent is 4, `false` returns the opener
   at 8. Skala re-indents it to 4 — at both values, and it did so before this key was read at all. So
   this entry's "at `false` the oracle returns the comment exactly as written, which is what Skala
   does at every value" is wrong in its second half: Skala was never doing that, because it moves the
   opener. **Not implemented**, because it is a separable behaviour — about where the comment token
   starts rather than about its asterisks — and honouring it means emitting the comment self-indented
   from its written column, which touches anchoring and `skala_stick_comment`. It is why the key is
   registered `OfUnoracled` rather than promoted, and `AlignMultilineCommentTests` asserts the gap
   explicitly so that closing it has to come back and delete that test.
2. ⚠ **An *unstarred* multi-line comment's body is uniformly shifted with its opener, at both values,
   and that is not this key.** Written at opener 8 / body 6 / `*/` 5 where the code indent is 4, the
   oracle returns 4 / 2 / 1 — every line moved by −4. Skala returns 4 / 6 / 5: it moves the opener and
   leaves the body at its written columns. Recorded as **SK-DIV-0094**, because it is reachable at the
   export's values on a comment this key declines to touch.

- options: `skala_align_multiline_comments`
- ⚠ status: **narrowed**. Conformant at the export's `true`, pinned by
  `Formatting/Rikarin.Skala.Formatting.CSharp.Tests/AlignMultilineCommentTests.cs` against the
  oracle's own bytes plus a fixed-point round trip and four disqualified shapes. Open at `false`, for
  fact 1 above. ⚠ `verify skala_align_multiline_comments` still answers "not swept" — the
  key has no `oracle` glob, and it may not have one while it is non-conformant at a value, because a
  glob on a Tier D entry the sweep has not demoted is a promotion nobody made
  (`OptionCoverageTests.TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt`). The glob and the fixture
  are the natural next step once fact 1 is paid.
- ⚠ **`corpus/real` fidelity is unmoved (99.55 line / 86.05 file), and the reason is measured**: a
  scan of all 380 files finds **zero** multi-line block comments whose every continuation line begins
  with `*`. The construct does not occur there, so no fix to it could move the number.
- ⚠ **triage 2026-08-30: `debt`, size M.** It looks like SK-DIV-0001's principle — do not rewrite
  the inside of something you did not parse — and it is not, because Skala already rewrites the
  inside of a `///` comment by default (SK-DIV-0006). Once the interior of one comment kind is the
  formatter's, "we do not touch the interior of a block comment" is not a position, it is an
  omission, and the export asks for the realignment at the value it actually sets. Paying it:
  `SourcePieces.AddTrivia` keeps a `MultiLineCommentTrivia` as one piece emitted verbatim; the work
  is to split it per line the way `AddDocumentationLines` already splits a `///` run, indent each
  continuation whose first non-space character is `*` to the opener's column plus one, and leave a
  comment with no leading asterisks alone. The size is M rather than S because a new per-line piece
  kind has to answer to `@formatter:off`, to the inactive-branch opacity above, and to idempotence,
  and it wants a fixture at both values. ⚠ Needs the oracle, for the fixture.
## SK-DIV-0070 — the oracle reads the four Roslyn qualification keys for `this.`, and not `resharper_remove_this_qualifier`

`resharper_remove_this_qualifier = true` is in the export and the key is **Tier A**, pinned by
`constructs/arrangement/redundancy/qualifiers-and-parentheses.arranged.expected.cs`. That fixture is
a file in which every `this.` is removed — and it is satisfied by an implementation that reads the
key and by one that ignores it, because `dotnet_style_qualification_for_field`, `…_property`,
`…_method` and `…_event` are all `false` in the same export and each of them removes `this.` on its
own.

Probed against `jb cleanupcode` 2025.2.6 under `OracleProfile.Cleanup`, on a file carrying a bare and
a `this.`-qualified reference to each of the four member kinds:

| Override | Result |
|---|---|
| none (the export) | every `this.` removed |
| `resharper_remove_this_qualifier = false` | **byte-identical** — still removed |
| `dotnet_style_qualification_for_field = true` | `this._field` written, other three kinds untouched |
| `…_for_property` / `…_for_method` / `…_for_event = true` | the same, one kind each |

So the ReSharper key is dominated on this repository's configuration: it is read, if at all, only
where the four Roslyn keys leave the question open, and the export leaves it open nowhere.

**Skala keeps reading it, as a gate on the removing direction only.** At `remove_this_qualifier =
false` with the four Roslyn keys at `false`, Skala keeps an existing `this.` and the oracle removes
it — that is the divergence, and it is deliberate. Dropping the key instead would make a Tier A
option unobservable and leave its committed fixture proving nothing, which is a bigger claim to make
than this one. The tier is the maintainer's call; the measurement is here so it can be made.

⚠ **Superseded on 2026-08-29 and re-checked 2026-08-30.** `resharper_remove_this_qualifier` was
deleted from the template, the generated `.editorconfig`, the registry (520 → 510), the free-form
exemption list and the arranger, as one of ten keys ReSharper does not support — and the removal was
proved inert by regenerating 1 336 fixtures and re-stamping 760 with **not one non-header line
changed** (`302338e6`). This entry was the first of three readings of that key, and the last one is
the one that stuck: dominated (here), then `SPURIOUS` in the first cleanup-profile sweep, then gone.

`ThisQualifierRule.IsEnabled` is now `true` unconditionally — deliberately not the disjunction of the
four Roslyn keys, which the export sets all `false` and which would therefore have switched the rule
off entirely. The four `dotnet_style_qualification_for_*` keys are the whole story, they drive the
*adding* direction as well as the removing one, per member kind, and all four read `Conformant`
Tier A in the committed sweep against
`constructs/arrangement/redundancy/instance-qualifier.cs`.

- options: `resharper_remove_this_qualifier`, `dotnet_style_qualification_for_field`, `dotnet_style_qualification_for_property`, `dotnet_style_qualification_for_method`, `dotnet_style_qualification_for_event`
- ⚠ status: **open**, measured; the divergence is only reachable by flipping `resharper_remove_this_qualifier` away from the export's value.
- ⚠ **triage 2026-08-30: `moot`.** The divergence this entry records — Skala gating the removing
  direction on a fifth key and so keeping a `this.` the oracle removes — is unreachable, because the
  key no longer exists to flip. Kept for the lesson rather than the measurement: it was recorded
  from a probe that could only see that the key was *dominated*, and "dominated on this
  configuration" was read as "read, but never decisive" when the truth was "not read at all". The
  probe that settled it was deleting the key and regenerating the corpus.

## SK-DIV-0071 — `resharper_remove_unused_only_aliases` is a Visual Basic option, not a second spelling of the C# one

The export carries both `skala_remove_only_unused_aliases = true` and
`resharper_remove_unused_only_aliases = false`, four characters apart, and the registry classified
both `csharp` "by vocabulary". The obvious reading is one option under two spellings — the shape
`SK9004` exists for — and it is wrong.

The setting names are recoverable the way `docs/oracle-cleanup-profile.md` recovers task names:

```
grep -rl RemoveUnusedOnlyAliases $JB --include=*.dll   # JetBrains.ReSharper.Psi.VB.dll, Features.Altering.dll
grep -rl RemoveOnlyUnusedAliases $JB --include=*.dll   # JetBrains.ReSharper.Psi.CSharp.dll, Feature.Services.dll, Features.Altering.dll
```

`RemoveUnusedOnlyAliases` is declared in the **VB** language module; `RemoveOnlyUnusedAliases` is the
C# one. Confirmed by measurement rather than left at the name: probed at `true` under the cleanup
profile over a file carrying a used and an unused instance of a trivial and a non-trivial alias, the
output is byte-identical to the export's value, while flipping the C# key on the same file removes or
keeps two directives.

It is therefore **inert for C#** with a recorded reason, and the C# key is Tier A.

- options: `resharper_remove_unused_only_aliases`, `skala_remove_only_unused_aliases`
- ⚠ status: **closed**; recorded as `inert` in the registry.

## SK-DIV-0072 — ⚠ RETRACTED. `skala_keep_nontrivial_alias` was recorded inert on a probe that could not distinguish it

The registry carried:

> The oracle returns `using Map = System.Collections.Generic.Dictionary<string, int>;` beside a
> trivial `using Simple = System.String;` unchanged at both values, under the format-only profile and
> under the cleanup profile.

That is true and it is not evidence. Both aliases in that probe were **in use**, and a using that is
in use is not removable at any value of any key — the observation could not have come out otherwise.
It is the same failure as SK-DIV-0006's: a fixture that agrees for a reason unrelated to the option.

With the aliases unused the key separates cleanly, measured over all four combinations with
`skala_remove_only_unused_aliases`:

| `keep_nontrivial_alias` | `remove_only_unused_aliases` | unused **trivial** alias | unused **non-trivial** alias |
|---|---|---|---|
| `false` | `true` — the export | removed | **removed** |
| `true` | `true` | removed | kept |
| `false` | `false` | removed | kept |
| `true` | `false` | removed | kept |

"Trivial" is the alias identifier equalling the aliased type's own name — `using Regex =
System.Text.RegularExpressions.Regex;` is trivial and `using Trivial = System.String;` is not — which
is measured, not read off the word. The two keys AND, so each moves the output on its own from the
export's pair, and both are now implemented and Tier A.

- options: `skala_keep_nontrivial_alias`, `skala_remove_only_unused_aliases`
- ⚠ status: **closed**; the `inert` mark is removed and `constructs/arrangement/usings/aliases.cs` pins both.

## SK-DIV-0073 — the oracle shortens a qualified reference; Skala has no rule that does

`resharper_csharp_prefer_qualified_reference = false` is in the export, and it is **not** inert. Under
the cleanup profile, on a file writing `new System.Text.StringBuilder()` and
`global::System.Console.WriteLine(…)` beside their short forms:

- at the export's `false` the oracle **shortens** both to `StringBuilder` and `Console`, keeping the
  usings that make the short forms legal;
- at `true` it goes the other way and fully qualifies every simple name, dropping the usings that
  become redundant.

Skala does neither. It has no reference-shortening rule at all, so on this key it is unreachable
rather than unread, and it stays Tier D with no `oracle` glob. The cost is not zero: the corpus
fixture `constructs/arrangement/usings/placement.cs` was written with a `using Collections =
System.Collections.Generic;` alias at nested scope and had to lose it, because the oracle replaced
`Collections.List<int>` with `List<int>` — `System.Collections.Generic` is an implicit using — and
removed the alias, which is the shortening rewrite arriving by another door.

The three neighbouring keys of the same family are a different matter and are inert rather than
unimplemented: `resharper_csharp_allow_alias`, `resharper_csharp_can_use_global_alias` and
`resharper_csharp_qualified_using_at_nested_scope` govern what ReSharper **writes** when it imports a
name — a completion, a paste, a quick-fix — and cleanup imports nothing. Each was probed at its
flipped value under the cleanup profile *and* under a profile with `CSShortenReferences` added, on a
file built to need it (a `global::` prefix disambiguating a locally-shadowed `Console`; a using at
nested scope), and each came back byte-identical. Same shape as SK-DIV-0013's three code-generation
settings.

⚠ **Re-measured 2026-08-30 under `OracleProfile.Cleanup`, and it reproduces in full — with one shape
this entry did not record.** On a probe carrying `new System.Text.StringBuilder()`,
`global::System.Console.WriteLine(…)` and `System.Collections.Generic.List<int>` beside their short
forms, the oracle shortens all three and removes the `using` that becomes redundant; Skala changes
none of them. The new shape: on a second probe carrying `using Abe = System.Text.StringBuilder;`,
the oracle rewrote `new StringBuilder()` to **`new Abe()`** — it prefers a user-declared alias to the
imported name, and then removed `using System.Text;` as unused. So the rule is not "shorten to the
simple name"; it is "shorten to the shortest name that binds here, alias included", which is a
design question of its own and not a detail of the implementation.

- options: `resharper_csharp_prefer_qualified_reference`, `resharper_csharp_allow_alias`, `resharper_csharp_can_use_global_alias`, `resharper_csharp_qualified_using_at_nested_scope`
- ⚠ status: **open** on the first, **closed** (inert, recorded) on the other three.
- ⚠ **triage 2026-08-30: `debt`, size L.** The export sets `prefer_qualified_reference = false`,
  which is a request Skala does not answer — not a considered refusal, an absent rule. It already
  costs the corpus a construct: `constructs/arrangement/usings/placement.cs` had to lose its
  nested-scope alias because the oracle shortened through it. Paying it is a new semantic rule with
  the full weight of one: resolve the qualified name, re-look-up the candidate short form at exactly
  that position, require the same symbol (`LookupSymbols`, the way `ThisQualifierRule` already
  does), decide the alias-versus-import preference above, and then feed the using removal that
  follows. Layer 3's re-bind catches a wrong shortening that fails to compile; it does not catch one
  that binds to a *different* symbol of the same name, so that check has to be explicit. ⚠ Needs the
  oracle — the alias preference is exactly the kind of behaviour no specification states.

## SK-DIV-0074 — `dotnet_separate_import_directive_groups` was a formatting key in the oracle and an arrangement key in Skala

⚠ **Fixed 2026-08-30. See the section at the end**, which also corrects this entry's grouping model
and its claim that only one of the key's two directions belongs to the format-only profile. The text
down to it is kept as written, because the correction is only legible beside the claim.

At `true` the oracle writes one blank line between using directives whose first namespace segment
differs, and at the export's `false` it takes every blank line inside the using block back out. Both
directions were measured under the cleanup profile, and the second one **also happens under
`CSReformatCode` alone** — the format-only profile strips the blank line too.

Skala's formatter does not read the key. Only the arranger does, so `skala format` on a file with a
blank line inside its using block keeps it where the oracle removes it, and `skala arrange` removes
it. Moving the key into the formatter is the right end state and it is not this change: the formatter
is a different component with its own fixture set, and a key that two components both act on is worse
than a key one of them acts on late.

The consequence for the corpus is recorded in the fixture itself:
`constructs/arrangement/usings/import-groups.cs` deliberately has **no** blank line in its source,
because one would make its format-only fixture measure this gap rather than the option. The removal
direction is pinned by `ArrangementOptionTests` instead.

⚠ **Re-measured 2026-08-30 and it reproduces, in both directions.** On a probe whose using block
carries two blank lines, under `SkalaFormatOnly` — the *format-only* profile, with no arrangement
task in it at all — the oracle removes both; `skala format` returns the file unchanged; `skala
arrange` removes them. So the divergence is precisely the seam this entry names, and it is visible
on the profile milestones 1–3.1 used as their whole oracle.

### ⚠ Fixed 2026-08-30 — and *both* directions turn out to be the format-only profile's

The key is `PhaseOneOptions.SeparateImportDirectiveGroups` now, resolved in
`CSharpDocumentBuilder.ImportGroupSeparation`; `UsingsRule.Separate` and the arranger's copy of the
option are deleted rather than moved, so exactly one component reads it. The arranger still decides
the *order* and the formatter separates whatever order it is handed, which is why the cleanup
profile's answer (sort, then separate the sorted order) and the format-only profile's (separate the
written order) now fall out of the pipeline instead of each being arranged for.

⚠ **This entry understated the seam.** It says the *removal* direction "also happens under
`CSReformatCode` alone". Measured on a probe carrying every kind of directive and blank runs of 0, 1
and 2, under `SkalaFormatOnly` — no arrangement task in the profile at all — **both** directions do:
at `true` the oracle *inserts* the separating blank lines and removes the ones inside a group. So it
is not "a formatting key in one direction and an arrangement key in the other"; it is a formatting
key, entire.

⚠ **And the grouping model was wrong, in two ways.** Both `UsingsRule.Separate` and this entry said
"by first segment and nothing finer". Measured on the same probe:

| gap | oracle at `true` | why |
|---|---|---|
| `System.Text` ▸ `System.Globalization` | 0 | same kind, same segment |
| `System.Globalization` ▸ `Zeta.Support` | 1 | same kind, segment differs |
| `Zeta.Support` ▸ `Zeta.Support.Deep` | 0 | the segment and nothing finer — this half held |
| ⚠ `Zeta.Support.Deep` ▸ `using static System.Math` | **1** | a change of **kind** separates on its own |
| `static System.Math` ▸ `static Zeta.Support.Helper` | 1 | segment differs within the static kind |
| ⚠ `AliasOne = System…` ▸ `AliasTwo = Zeta…` | **0** | aliases are one group **whatever they alias** |
| ⚠ `AliasTwo = Zeta…` ▸ `using System.Threading` | **1** | kind again, alias back to plain |

So the group is **(kind, first segment)**, where kind is plain / `using static` / alias, and the
segment is not consulted for an alias at all. `using System.Text;` and `using static System.Math;`
share a first segment and are separated — which the old model could not produce.

⚠ **And "it takes every blank line inside the using block back out" is too strong.** A gap holding a
comment is not this key's gap at either value: at `true` the oracle inserts nothing across a comment
even between two different groups, and at `false` it *keeps* a blank line the author wrote above one.
That needs no special case in the implementation — the formatter attributes a gap to the piece below
it, and a comment piece is not a using directive.

⚠ An `extern alias` is left alone at both values, and that is recorded as **unmeasured** rather than
inert: the directive needs an aliased assembly reference the probe project has no way to carry.

- options: `dotnet_separate_import_directive_groups`
- ⚠ status: **fixed**, `verify dotnet_separate_import_directive_groups` reports **Conformant** — and
  now on a fixture that actually asks the question. `constructs/arrangement/usings/import-groups.cs`
  carries a blank line inside its using block for the first time, both its `.expected.cs` and its
  `.arranged.expected.cs` are regenerated at 2025.2.6, and the removal direction is pinned by the
  corpus rather than by a unit test standing in for it. `ImportDirectiveGroupTests` carries the
  oracle's own bytes at both values, and the two `ArrangementOptionTests` that pinned the arranger's
  copy are replaced by one that asserts the arranger no longer acts on the key at all.
- ⚠ **`corpus/real` fidelity is unmoved, and the reason is measured rather than assumed.** Not one of
  the 380 files has a blank line between two using directives — the two candidates a scan turns up
  are `using var` statements inside method bodies. The construct does not occur in that corpus, so no
  fix to it could move the number.
- ⚠ **triage 2026-08-30: `debt`, size S.** Narrow is not the same as decided, and nothing here was
  defensible on its own terms: a user who ran `skala format` and a user who ran `skala arrange` got
  different blank lines from the same file and the same key, which is a seam rather than a choice.

## SK-DIV-0075 — `T x = default(T)` for a reference `T` is a `var` candidate the oracle takes and Skala refuses

Asked directly, unbatched, under the cleanup profile at the export's values, on a scratch project
carrying this repository's `.editorconfig`:

```csharp
string a = default(string);        →  var a = default(string);
List<int> b = default(List<int>);  →  var b = default(List<int>);
int c = default(int);              →  var c = default(int);
string d = "x";                    →  var d = "x";
```

Skala converts the third and the fourth and refuses the first two. The refusal is `VarRule`'s
nullable-flow precondition and it is not an oversight: `default(string)` has type `string` and flow
state maybe-null, so `var` retypes the local as nullable, and the next place it is passed to something
expecting `string` is a new CS8600. That precondition's own remarks record what it is worth —
**567 of Vixen's 618 re-bind reverts were this one**, the largest single cause and invisible to every
check that compares types rather than states. `default(T)` for a reference `T` is the one shape where
the declared type and the initializer's type are *identical* and the flow state still differs, so it is
the one shape where the precondition and the oracle can disagree.

⚠ Lifting the precondition to close this entry is refused for now: it would buy one line of one
construct fixture and re-open a class the corpus has already paid for once.

`constructs/arrangement/type-inference/var-and-maybe-null.cs` is the fixture, with the value-type row
beside it as the control, and **no option is globbed to it**. The construct was moved there out of
`default-literal.cs`, where it had been making both `skala_default_value_when_type_evident`
and `skala_default_value_when_type_not_evident` unattributable in the key-flip sweep — two
keys with nothing wrong with them, paying for a third rule's divergence.

⚠ **Re-measured 2026-08-30 under `OracleProfile.Cleanup`, and the shape is wider than the title
says.** Six rows, arranged and formatted by Skala and by `jb cleanupcode` at the export's values:

| written | oracle | Skala |
|---|---|---|
| `string a = default(string);` | `var a = default(string);` | `string a = default;` |
| `List<int> b = default(List<int>);` | `var b = default(List<int>);` | `List<int> b = default;` |
| `object e = default(object);` | `var e = default(object);` | `object e = default;` |
| `int? f = default(int?);` | `var f = default(int?);` | `int? f = default;` |
| `int c = default(int);` | `var c = default(int);` | the same |
| `string d = "x";` | `var d = "x";` | the same |

Two corrections. **The shape is not "a reference `T`"** — `int?` is a value type and is refused for
the same reason, so it is *any `T` whose flow state after `default(T)` is maybe-null.* And **the
refusal is not the end of it**: `VarRule` declining leaves the declaration standing, `DefaultValueRule`
then fires on it, and Skala emits a third form — `string a = default;` — that neither engine's
`var` rule produces. That is a better outcome than the one this entry implies, because it keeps the
declared non-nullable type where the oracle's `var` discards it.

- options: `csharp_style_var_for_built_in_types`, `csharp_style_var_when_type_is_apparent`, `csharp_style_var_elsewhere`
- ⚠ status: **open**, deliberate, and narrow.
- ⚠ **triage 2026-08-30: `deliberate`.** The argument stands without the oracle in it: *`var` is
  only a shorthand when it retypes nothing, and `var x = default(string)` retypes `x` from `string`
  to `string?`.* The next thing that passes `x` to a parameter expecting `string` is a new CS8600
  the author did not write, and the precondition that prevents it is the largest single cause of
  re-bind reverts this repository has measured — 567 of Vixen's 618, invisible to every check that
  compares types instead of flow states. Skala's answer, `string a = default;`, is the shortening
  that costs the user nothing. Lifting the precondition to match the oracle would buy one line of
  one construct fixture and re-open a class the corpus has already paid for once.

## SK-DIV-0076 — an argument is a target-typed position the oracle converts and Skala has no case for

`Take(new object())` comes back from the oracle as `Take(new())`, and a *named* argument the same way:
`Take(number: 1, other: new object())` → `other: new()`. `ObjectCreationRule.TargetTypeOf` enumerates
the positions C# target-types — a declarator's initializer, a property initializer, a simple
assignment, an arrow body, a `return`, an explicitly-typed collection or array initializer element —
and an argument is not among them.

The list is deliberately explicit rather than `GetTypeInfo(node).ConvertedType`, for the reason its
remarks give: the converted type of `new Foo()` in a context with no target is `Foo` itself, so
trusting it converts expressions that have no target at all. Adding the argument case means resolving
the call, finding the parameter at the argument's index, and being right about `params`, `ref`/`out`
and named arguments — and then about **overload resolution**, because `new()` in an argument can rebind
the call the way `DefaultValueRule`'s remarks already record for `M(default)` versus `M(default(int))`.
That is a capability with its own fixtures and its own safety argument, not a precondition to relax.

`constructs/arrangement/type-inference/target-typed-new-argument.cs` is the fixture, with the
declaration and assignment rows beside it as the control, and **no option is globbed to it**. The
construct was moved there out of `lists/argument-style.cs`, where a single `new object()` argument was
making all four `resharper_csharp_arguments_*` keys unattributable in the key-flip sweep.

⚠ **Re-measured 2026-08-30 under `OracleProfile.Cleanup`, and it reproduces at the export's values.**
`Take(new object())` comes back from the oracle as `Take(new())`; Skala leaves it. The controls in
the same file — a declarator's initializer, a property initializer, a simple assignment, a `return`
— are converted by both engines identically, so the divergence is the argument position and nothing
else. `TypeInferenceRules`'s `TargetTypeOf` still falls through `default: return null` for an
`ArgumentSyntax` parent, which is the whole of it.

- options: `skala_object_creation_when_type_evident`, `skala_object_creation_when_type_not_evident`
- ⚠ status: **open**; a missing capability rather than a decision, and it needs its own task.
- ⚠ **triage 2026-08-30: `debt`, size M.** ⚠ **The difficulty is not the argument.** Nobody would
  defend "Skala target-types every position C# does, except an argument" to a user; it is a hole in
  a rule, and the entry's own words — "a missing capability rather than a decision" — say so. The
  size is real and it is where the care goes: mapping an argument to its parameter needs `params`,
  `ref`/`out` and named arguments handled, and then the hazard `DefaultValueRule` records for
  `M(default)` versus `M(default(int))` applies here in its sharpest form — **`M(new())` can bind a
  different overload than `M(new Foo())`, and the result compiles.** Safety layer 3 re-binds and
  reverts on a compile error; it cannot see a silently different overload, so the precondition has
  to be explicit: resolve the call before and after, and require the same method symbol. With that,
  the rest is one case in `TargetTypeOf` and a fixture beside
  `constructs/arrangement/type-inference/target-typed-new-argument.cs`. ⚠ Needs the oracle for the
  fixture; the overload-stability check is Skala's own and does not.

## SK-DIV-0077 — an anonymous method whose parentheses the author broke leaves the call's line, and its block body breaks with it

Measured, unbatched, at the export's values, with the control beside each row:

```csharp
Use(delegate(int first) { return first; });     // kept whole — the key applies to anonymous methods
Use(delegate(int first) {                       // body re-joined: → Use(delegate(int first) { return first; });
    return first;
});
Use(delegate(                                   // →  Use(
    int first) { return first; });              //        delegate(
                                                //            int first
                                                //        ) {
                                                //            return first;
                                                //        }
                                                //    );
Use((                                           // →  Use((
    int first) => { return first; });           //        int first
                                                //    ) => {
                                                //        return first;
                                                //    }
                                                //    );
```

Two facts, and neither is the one the option names suggest. `place_single_method_argument_lambda_on_same_line`
**does** apply to an anonymous method — row 1 keeps it whole — but row 3 moves it off the call's line
anyway while row 4 keeps a *lambda*'s `(` joined to `Use(`. And a block body the author wrote on one
line is re-joined when everything fits (row 2) and broken when the parameter list is broken (rows 3
and 4).

The second fact is the harder one, and it is the same shape as SK-DIV-0050 and SK-DIV-0024: an outer
construct has to break because an inner one did. Skala's `Fitter` resolves the outer group first, so
the argument list's group is decided before the anonymous function's parameter list is, and
`GroupFacts.BreaksWithOwner` reads an owner that must already be resolved. SK-DIV-0050 records the one
attempt at this family — a `Preserve` group with `PrefersOuterBreak` — as written, measured and
withdrawn.

`constructs/wrapping/anonymous-method-parens.cs` is the fixture, with the lambda beside it as the
control, and **no option is globbed to it**. The construct was moved there out of
`preservation/lambda-parens.cs`, where it was making
`skala_wrap_after_declaration_lpar`, `skala_wrap_before_declaration_rpar` and
`skala_keep_existing_lambda_and_anonymous_function_parens_arrangement` unattributable in the
key-flip sweep. All three keys were re-checked on the reduced fixture and the first two still move the
oracle at both values; the third moved neither engine's oracle side before the change either.

⚠ **Triage at `425b01d0`: `debt`, size M shared — the containment fact** (SK-DIV-0050 § "The two
facts this family is made of"), paid once with SK-DIV-0007 and SK-DIV-0078. Re-measured, all four
rows, unbatched: row 1 still comes back whole, and rows 2, 3 and 4 all still diverge. Nothing this
week touched it.

⚠ **This is the entry that settles the family's verdict, because its output is the one that cannot
be defended.** Skala's answers to rows 3 and 4 are

```csharp
Use(delegate(          // Skala
        int first
    ) { return first; }
);
```

— a parameter list broken across three lines with the block body then joined onto the closing
parenthesis's line. Ask the test the triage runs on: would you defend that to someone who has never
heard of ReSharper? There is no reading under which a formatter *meant* to write it. It is what
falls out when the outer group is resolved before the inner one and cannot be told the inner one
broke, and row 2 is the same absence from the other side — a one-line block body re-joined when
everything fits, which Skala also gets wrong.

⚠ Note what is **not** debt here. `place_single_method_argument_lambda_on_same_line` applying to an
anonymous method (row 1) is measured and conformant, and the entry's care over the fixture — moving
the construct out of `preservation/lambda-parens.cs` so three keys stop being unattributable — is
work that stands. The debt is one mechanism, not four keys.

- options: `skala_place_single_method_argument_lambda_on_same_line`, `skala_keep_existing_lambda_and_anonymous_function_parens_arrangement`, `skala_wrap_after_declaration_lpar`, `skala_wrap_before_declaration_rpar`
- ⚠ status: **open**; SK-DIV-0050's family, and it needs the same missing fact.

## SK-DIV-0078 — an expression body's `=>` breaks when its body breaks, and Skala's fitter decides the arrow first

```csharp
bool M(object o) => o is int      →     bool M(object o) =>
    or string                                 o is int
    or bool;                                      or string
                                                  or bool;
```

The first line is 33 columns wide, so no width test on the arrow can produce this break.
`skala_place_expr_method_on_single_line = if_owner_is_single_line` is what does: the *owner* is the
declaration, and a declaration whose body spans lines is not on a single line — which is exactly the
reading `PlanExpressionBody`'s remarks already record ("a body that spans lines makes it not
single-line however short its first line is"). What Skala cannot do is *apply* it here. The body's
breaks are the binary pattern chain's, that group is nested inside the arrow's, and `Fitter` resolves
the outer group first: when the arrow's group is decided the chain has not yet said it will break.

⚠ This is not the same as SK-DIV-0050. There the arrow has **no group at all**, because Roslyn spells a
lambda's arrow differently from a member's; here the group exists and is resolved in the wrong order.
It is SK-DIV-0024's and SK-DIV-0077's shape instead — two constructs, one break, and no fact in
`GroupFacts` that lets the outer one read the inner.

`constructs/wrapping/binary-pattern-arrow.cs` is the fixture, with a one-line member beside it as the
control, and **no option is globbed to it**. The construct was moved there out of
`breaks/binary-patterns.cs`, which was rewritten to put its chains in `return` statements — where
there is no arrow to break — so that `skala_wrap_before_binary_pattern_op` is measured on
its own. Re-checked after the rewrite: the oracle keeps the before-the-operator breaks and re-joins
the after-the-operator ones at `true`, and does the reverse at `false`, so both values still move it.

⚠ **Triage at `425b01d0`: `debt`, size M shared — the containment fact** (SK-DIV-0050 § "The two
facts this family is made of"), paid once with SK-DIV-0007 and SK-DIV-0077. Re-measured on the
entry's own shape and it reproduces to the column, including the second-order consequence the entry
does not spell out: because Skala keeps `o is int` on the arrow's line, its continuation lines land
four columns to the left of the oracle's, so the divergence is three lines wide, not one.

⚠ **This entry is the family's cleanest argument for `debt` over `deliberate`, because the rule is
already written down and already agreed.** `skala_place_expr_method_on_single_line =
if_owner_is_single_line` is the export's value, `PlanExpressionBody`'s own remarks state the reading
correctly — "a body that spans lines makes it not single-line however short its first line is" — and
Skala's output contradicts its own documented rule. That is not a considered difference from
ReSharper; it is a rule the formatter holds and cannot apply. It stays debt even if ReSharper never
existed.

- options: `skala_wrap_before_binary_pattern_op`, `skala_place_expr_method_on_single_line`, `skala_wrap_chained_binary_patterns`
- ⚠ status: **open**; the ordering fact is missing, and it is shared with SK-DIV-0077.

## SK-DIV-0079 — `skala_xmldoc_wrap_tags_and_pi` wraps a tag's *attributes*, and Skala cannot re-read a header it broke

⚠ **The sweep called this one `SPURIOUS` and the verdict was about the fixture, not the key.**
`constructs/xmldoc/skala_xmldoc_wrap_tags_and_pi.cs` is prose carrying a `<see cref="…" />` that
cannot stay on its line, and the oracle returns **the same bytes at `true` and at `false`** — it moves
the `<see/>` to the next line either way. Skala read the key as "whether a tag may be moved to a new
line to fit", so only Skala's output varied, which is exactly what `SPURIOUS` means.

### What it really governs, measured

Under `OracleProfile.DocComments`, on a tag whose own header is 170 columns wide:

```csharp
/// <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}" href="https://example.invalid/a/very/long/documentation/link/that/will/not/fit" />
```

| `skala_xmldoc_wrap_tags_and_pi` | the oracle |
|---|---|
| `true` (the export's own value) | `href="…"` moves to a continuation line at one indent, inside the tag |
| `false` | the header is returned whole, past the margin |

A `<?pi-name a="1" … p="16" ?>` beside it is untouched at both values, so on this build the key is
about element headers.

⚠ **And the other half of the old reading is measured false in its own right.** Asked at
`skala_xmldoc_wrap_text = false` — with `skala_xmldoc_wrap_tags_and_pi` left alone — the oracle *still* moves a
`<see/>` off the end of a line of prose while leaving the words around it exactly where they were, and
returns a `<summary>` of 170 columns of plain prose whole on one line. So permission for a tag to move
is not this key's to give: `skala_xmldoc_wrap_text` is permission for a **word** to move, and an element may always
move. `XmlDocRenderer.Flush` now says that, and the change is what turns the committed fixture from
"only Skala varies" into "neither varies".

### Why Skala does not implement it

`XmlDocModel` refuses a comment whose input carries a **multi-line tag header** — `Unmodelled`, one of
the five recorded refusal reasons. So a Skala that wrapped a header would emit a comment it could not
re-read on the next run, and `format(format(x))` would leave the header wrapped and every other rule
unapplied. That is an idempotence violation traded for one non-export value of one key, and
idempotence is the property this product exists to provide.

It joins the four tag-header keys already in `XmlDocIds.Refused` — `skala_xmldoc_attribute_indent`,
`skala_xmldoc_attribute_style`, `skala_xmldoc_alignment_tab_fill_style`, `skala_xmldoc_allow_far_alignment` — all
pending on the same prerequisite, which is a model that can read a header back. Whoever lifts that
lifts all five at once; four of them have nothing to say until this one works.

⚠ The registry entry keeps `oracle: null`, on the `wrap_verbatim_interpolated_strings` precedent: a
glob pointing at a fixture the oracle wraps and Skala does not would make the sweep report `INERT`
with a baseline that disagrees, and would red `XmlDocOracleTests` on a committed fixture Skala cannot
reproduce. The construct is written down here instead, in the shape a future fixture wants.

⚠ **Re-measured 2026-08-30 under `OracleProfile.DocComments`, and it reproduces — at the export's
own value.** The probe: a `<see cref="…" href="…" />` header 170 columns wide inside a `<remarks>`.

| | oracle | Skala |
|---|---|---|
| `skala_xmldoc_wrap_tags_and_pi = true` — **the export's value** | `href="…"` moves to a continuation line at one indent, inside the tag | header returned whole, past the margin |
| `skala_xmldoc_wrap_tags_and_pi = false` | header returned whole | the same |

⚠ **This corrects the entry's own last sentence.** It says the idempotence violation would be
"traded for one non-export value of one key"; it is the reverse — Skala matches the oracle only at
the value the export does *not* set, and diverges at the value it does.

The prerequisite is confirmed by the converse test: handed the oracle's wrapped output, Skala's
formatter returns it unchanged and applies **no** doc-comment rule to that comment, because
`XmlDocModel.Build` returns null and `XmlDocFormatter.Replacement` refuses the whole comment as
`Unmodelled`. So a Skala that emitted a wrapped header would emit a comment it could not read back,
and `format(format(x))` would leave every other doc rule unapplied to it.

### ⚠ 2026-08-30 — still open, and the prerequisite this entry names is necessary but **not sufficient**

The wrap reproduces exactly as recorded above. What is new is the shape of the fix, and it is larger
than "teach `XmlDocModel` to read a wrapped header".

⚠ **The oracle does not unwrap. An author's break inside a header is preserved at BOTH values.**
Handed a header that is already wrapped, `jb cleanupcode` 2025.2.6 under `OracleProfile.DocComments`
returns it wrapped at `true` **and at `false`** — and it does so even for a *short* header that would
fit comfortably on one line:

```csharp
/// <see cref="System.String"
///     href="https://short.invalid/" />        ← kept wrapped at both values
/// <see
///     cref="System.String" />                 ← kept wrapped; only the continuation's indent is normalised
```

So `false` means "do not *introduce* a break", not "join what is broken", and `true` adds only the
introduction. The wrapping of a header is otherwise **preserved**, with continuation lines re-indented
to the tag's column plus one level.

⚠ **That refutes the reader-only fix, which is the obvious first move and would make things worse.**
`XmlDocModel.Attributes` refuses on `attribute.ToFullString().Contains('\n')`, and the one-line change
— test the attribute's own span text instead, so a newline lying in the *separator* between two
attributes is tolerated — does lift the `Unmodelled` refusal. But `XmlDocRenderer.Tag` rebuilds the
header from the name and the attributes with single spaces between them, so the comment would come
back with the header **joined**. Against an oracle that preserves the break at both values, that is a
new divergence at both values in place of the old one at one. The model must record *where* the
author's breaks fall inside a header, not merely tolerate them — and `XmlDocRenderer` must be able to
emit a tag across several lines, which its line model has no notion of today.

⚠ **The converse test reproduces, with a control this entry did not have.** A `<remarks>` whose header
is wrapped comes back byte-for-byte, prose lines and all; the *same* `<remarks>` with a single-line
header has its prose re-indented under the element. So the refusal is the header's and not the
comment's, and the five inert keys are inert for that reason exactly.

### ⚠ Three of the four dependent keys are measured, for the first time

The entry says they "have nothing to say until this one works" and that their behaviour "has never
been measured because nothing could ask". Nothing could ask *Skala*; the oracle can be asked directly,
on a header it wraps of its own accord. Measured under `OracleProfile.DocComments` on a three-attribute
`<see …/>` past the margin, and a second one that fits, one key at a time over the export:

**`skala_xmldoc_attribute_indent` — all three values separate.** The tag opens at column 12:

| value | the continuation lines |
|---|---|
| `single_indent` (the export) | column 16 — one indent past the tag |
| `double_indent` | column 20 |
| `align_by_first_attribute` | column 17 — under the first attribute, which starts after `<see ` |

**`skala_xmldoc_attribute_style` — and its recorded reason is refuted.** The reason on file is "it
arranges the attributes of a header Skala does not yet wrap". It does not wait for a wrap: two of its
four values restructure a header that fits perfectly well.

| value | header past the margin | header that fits |
|---|---|---|
| `do_not_touch` (the export) | broken at the margin | left on one line |
| `on_single_line` | broken at the margin — indistinguishable from `do_not_touch` on this probe | left on one line |
| `on_different_lines` | tag name alone, then every attribute on its own line | ⚠ **the same** — restructured though it fits |
| `first_attribute_on_single_line` | first attribute on the tag's line, the rest one per line | ⚠ **the same** — restructured though it fits |

⚠ `on_single_line` and `do_not_touch` are **not** distinguished by this probe, and the shape that would
distinguish them is the already-wrapped short header above: `do_not_touch` demonstrably keeps it
wrapped, so `on_single_line` joining it is the obvious hypothesis and is **not measured**. Recorded as
an open question rather than an answer.

**`skala_xmldoc_allow_far_alignment = true` returns the probe byte-identical**, and that is not a
finding about the key: it governs when an `align_by_first_attribute` alignment is "too far", so it
needs that key flipped with it and a tag name long enough to push the alignment out. A one-key probe
cannot reach it, exactly as `skala_alignment_tab_fill_style` cannot be reached without
`indent_style = tab` (SK-DIV-0032). **Still unmeasured**, and now for a precise reason.

**`skala_xmldoc_alignment_tab_fill_style` is untouched here**, for the same pairwise reason: it
fills a continuation line's alignment and needs `indent_style = tab` beside it.

### ⚠ 2026-08-31 — both of those open shapes are closed, and one of them the other way

The two paragraphs above name two things this entry could not settle. Both were asked, with the
prerequisites they specified actually supplied, and one of them turns out to have been a fact about
the probe rather than about the key.

**The `on_single_line` open question is closed, and the hypothesis it recorded was right.** The shape
the entry predicted would distinguish it does: an already-wrapped *short* header, inside a
`<remarks>` under `OracleProfile.DocComments`.

```csharp
///     <see cref="System.String"
///         href="https://short.invalid/" />
```

| `skala_xmldoc_attribute_style` | that header |
|---|---|
| `do_not_touch` (the export) | kept wrapped |
| `on_single_line` | ⚠ **joined onto one line** |

So all **four** values separate, not three, and the missing distinction was never the key's.

**`skala_xmldoc_allow_far_alignment` is measured, and "still unmeasured" was the probe's fault
rather than the key's.** The paragraph above names two prerequisites — `skala_xmldoc_attribute_indent` flipped
beside it *and* a tag name long enough to push the alignment out. The earlier run supplied only the
first, which is why it reported one output; the option that decides whether a far alignment is
allowed had no far alignment to decide about. Supplied both — a 90-character element whose first
attribute begins at column 105, tag opening at column 12:

| `skala_xmldoc_allow_far_alignment` | the oracle |
|---|---|
| `false` — **the export's own value** | falls back to a **double indent**, column 16 |
| `true` | aligns at column 100; the continuation line runs to 129, past the 120 margin |

A shorter element in the same comment, aligning at column 39, aligns at **both** values — so the key
is a threshold between 39 and 105 rather than a switch, and the export's `false` is doing real work
on wide tag names. ⚠ It stays out of the sweep: two flips, so no one-key row can reach it, and it is
marked `inert` in the registry with that as the reason.

**`skala_xmldoc_alignment_tab_fill_style` is measured too, and it is genuinely flat.** Given the
full prerequisite — `indent_style = tab`, `skala_xmldoc_indent_style = tab`, `tab_width = 4`,
`skala_xmldoc_attribute_indent = align_by_first_attribute` and `allow_far_alignment = true`, so a continuation line
carries 96 columns of alignment fill — `use_spaces`, `use_tabs_only` and `optimal_fill` are
byte-identical and the fill is **spaces** at all three. The control is in the same output: the file's
own *code* lines did take tabs. The inside of a `///` comment is always spaces, which is the finding
`skala_xmldoc_indent_style` already carries, and this key inherits it.

⚠ **So the count in this entry's triage note is now three, not five.** Of the five keys named there as
doing nothing, `alignment_tab_fill_style` is inert in the oracle and `allow_far_alignment` is
masked by the export's own `skala_xmldoc_attribute_indent = single_indent`; neither is waiting on the header
renderer. What the renderer would actually unlock is `skala_xmldoc_attribute_indent`, `skala_xmldoc_attribute_style` and
`skala_xmldoc_wrap_tags_and_pi` — and `allow_far_alignment` only for someone who also changes `skala_xmldoc_attribute_indent`.

- options: `skala_xmldoc_wrap_tags_and_pi`
- ⚠ status: **open, and better specified**. Not a wrapping bug and not merely a missing half of the
  model: the model must learn to *record* a header's own line breaks, and the renderer must learn to
  emit a tag across lines. The reader-only change is a one-liner and is refuted above — it would trade
  one divergence for two.
- ⚠ **triage 2026-08-30: `debt`, size M.** ⚠ The idempotence argument is a reason not to ship the
  wrap *yet*; it is not a reason the current behaviour is right, and the two are easy to confuse.
  What Skala does is not a choice anyone made — it is what falls out of a model that cannot
  represent a multi-line tag header, and the cost is five declared keys that do nothing:
  `skala_xmldoc_wrap_tags_and_pi` at the export's own value, plus `skala_xmldoc_attribute_indent`,
  `skala_xmldoc_attribute_style`, `skala_xmldoc_alignment_tab_fill_style` and `skala_xmldoc_allow_far_alignment` in
  `XmlDocIds.Refused`, four of which have nothing to say until this one works. Paying it: teach
  `XmlDocModel` to *parse* a header spread over several `///` lines back into one element node —
  after which the emitter side is the easy half and all five keys become answerable at once. ⚠ Needs
  the oracle for the four dependent keys' behaviour, which has never been measured because nothing
  could ask.

## SK-DIV-0080 — an aligned list pattern's continuation lines are not filled greedily, and no one rule fills both it and a collection expression

⚠ **The columns were the visible half and they are fixed.** `skala_align_multiline_list_pattern = true`
anchors a list pattern and a collection expression on their own `[`, and the oracle puts the elements
one level *past* that anchor while bringing the `]` back to it — the same relationship a braced
initializer already had, and the reason `skala_align_multiline_array_and_object_initializer` and
`skala_align_multiline_switch_expression` were conformant while this key was not. `PlanDelimited` read
"aligned" as "no level at all" and put the elements on the bracket's own column. That is repaired.

What is left is the *packing* of every continuation line after the first, and it is not reproducible
by a width rule. Measured against `jb cleanupcode` 2025.2.6 at the export's 120-column margin, this
key alone flipped, on `constructs/wrapping/alignment.cs` and on four purpose-built probes:

```
                                       firstElementPatternName, secondElementPatternName, thirdElementPatternName,   ← 114
                                       fourthElementPatternName,                                                     ← 64
                                       fifthElementPatternName                                                       ← 62
                                   ];
```

Line 1 is greedy: adding the fourth element would reach 139. Line 2 is not — `fifthElementPatternName`
would put it at 88, well inside the margin, and the oracle still moves it down. Skala fills greedily
and writes the two together.

⚠ **It is not "fill the first line, then chop", and it is not a narrower margin either.** Both
readings are refuted by the same run:

| probe | the oracle's lines |
|---|---|
| six elements, the last one two columns wide | 3 / 1 / **2** — so the tail is not chopped |
| fifth element 10, 16 or 20 columns wide | 3 / 1 / 1 at every width — so it is not a threshold on the item |
| collection expression, the same five elements, contents at 46 | 2 / **2** / 1 |
| list pattern, the same five elements, contents at 45 | **3** / 1 / 1 |
| either construct, `skala_align_multiline_list_pattern = false` | 4 / 1 — greedy, and both engines agree |

The last three rows are what closes the door. Both constructs' FIRST lines are greedy and the one
column between their anchors is exactly what explains 3 against 2: at 45 the third element ends at
120 and at 46 it would end at 121. Neither construct's second line is greedy, and their budgets
contradict each other — the list pattern moves a fifth element down that would have ended at 82,
while the collection expression writes a second line of 96 and only then moves one down that would
have ended at 108. One key, `skala_wrap_list_pattern`, governs both. And the whole effect
disappears when the construct is not aligned, where both engines are greedy and agree.

⚠ Skala's behaviour is therefore deliberate: `wrap_if_long` is a greedy fill everywhere in this
formatter — that is what the value means, what `LayoutWriter`'s fill point implements, and what the
oracle itself does for the same key on the same construct when alignment is off. Reproducing the
aligned answer would mean fitting a second, construct-specific packing rule to numbers that
contradict each other across two constructs of one key. This is SK-DIV-0005's class: a ReSharper wrap
threshold that no affine function of the numbers this fitter has will reproduce, recorded rather than
fitted.

- options: `skala_align_multiline_list_pattern`
- ⚠ status: **accepted**. The column relationship is fixed; the packing is measured, modelled twice,
  refuted twice, and left greedy. `constructs/wrapping/alignment.cs` pins the columns at both values.

## SK-DIV-0081 — an aligned property pattern's subpattern has no break point between its `:` and its value

The other half of `constructs/wrapping/alignment.cs`'s pattern pair, and a different defect from
SK-DIV-0080's. At `skala_align_multiline_property_pattern = true` the clause is anchored on its `{`, its
contents land four columns past that (39) and its `}` on it (35) — which Skala now writes. The
divergence is one line inside:

```
        var matched = candidate is {
                                       OnlySubpatternPropertyName:          ← the oracle breaks here
                                       "a string long enough that the pattern cannot stay on its line"
                                   };
```

At column 39 the subpattern is 130 columns wide, so it has to break, and the oracle breaks after the
subpattern's colon and lands the value on the *same* column — no continuation level at all. Skala has
no break point on a `SubpatternSyntax` at all: `BreakPlan` plans the property-pattern *clause* and its
commas, and a subpattern's own colon is not a gap anybody planned. The line comes back at 130.

⚠ The break itself is not the hard part — a point at `FirstToken(subpattern.Pattern)` is one line. What
was not established is the level it lands on. Every other undelimited continuation in this formatter
spends a level; this one spends none, and that was the only measurement available. A group whose
`spendsIndent` is false everywhere would have been a rule fitted to one line of one fixture at one
value of one Tier D key, which is the kind of fact this register exists to refuse until a second
measurement agrees with it.

### ⚠ Closed 2026-08-30. The second measurement arrived, and it brought a third

Two claims above are corrected by it.

**The level.** The value lands on the subpattern's own column in three configurations that do not
share a cause — aligned at the export's 120-column margin (the original), un-aligned at a 60-column
margin, and nested inside an outer property pattern:

```
var matched = candidate is {          var matched = candidate is {
                               OnlySubpatternPropertyName:      OnlySubpatternPropertyName:
                               "a string long enough …"         "a string long enough …"
                           };         };
align = true, margin 120              align = false, margin 60
```

**"Reachable only under alignment" is false.** The paragraph that said so reasoned from the export's
margin alone: at 120 the un-aligned subpattern is 101 columns and nothing has to break. Narrow the
margin and it breaks, un-aligned, onto the same column — which is how the third and cleanest
measurement was obtained, and it is why the rule is not an artefact of the alignment column.

`BreakPlan.PlanSubpattern` adds the point and `StartsAUnit` gives it its column;
`skala_align_multiline_property_pattern` now reads `Conformant` at both values.
`corpus/real` did not move — 99.55 / 86.05 bare and 99.64 / 86.58 with symbols, before and after.

- options: `skala_align_multiline_property_pattern`
- ⚠ status: **closed — fixed**. `constructs/wrapping/alignment.cs` pins the columns at both values and
  the sweep row is `Conformant`.

## SK-DIV-0082 — `max_line_length = 1` explodes a file at break points Skala does not have, and cannot reasonably acquire

⚠ **The other end of this key is a real defect and it is fixed.** `max_line_length = 0` means *no
limit* — measured, the oracle returns `constructs/wrapping/initializers.cs` with a 141-column line
untouched — and `PhaseOneOptions` read it as 120. The registry asserted the opposite in
`boundsBecause` and is corrected. What follows is only about the degenerate value at the other end.

At `max_line_length = 1` the oracle wraps at every gap it has, and its gaps are not this formatter's:

```
using                         var
    System                        e =
    .                                 new
    Collections                           [] {
    .                                         1,
    Generic;                                  …
```

It breaks between `System` and the `.` of a namespace name, between `new` and the `[]` of an implicit
array creation, and between `List` and its `<`. Skala has no break point in any of those places, and
adding them would mean giving a qualified name, an array-creation keyword and a generic name break
points that no margin above about 20 columns ever spends — points that every fitter measurement in
this repository would then have to be re-established against.

⚠ **This is a fact about the probe's domain and not about the key.** The int probe offers the
declared minimum, and for a width that is 1. The key is measured Conformant at its export value and at
`0` after the fix; the disagreement exists only at a margin narrower than the shortest C# statement,
and the shape it exercises is "what does ReSharper do when nothing can fit", not "where does ReSharper
wrap".

- options: `skala_max_line_length`
- ⚠ status: **accepted**. Conformant at `120` and at `0`; the residue is the probe's floor, and closing
  it would cost break points that no usable configuration reaches.

## SK-DIV-0083 — two placement keys the export masks outright, and the semantics measured behind the mask

Both were `SPURIOUS` in the key-flip sweep and both now read `UNEXERCISED`, which is the honest
verdict and not a pass. Skala was moving where the oracle could not; Skala has stopped, and the reason
the oracle cannot move is a *second* key in the export rather than a weak fixture. Neither is
reachable by a one-key flip, so neither can be resolved to `Conformant` from this base — that is the
pairwise phase's, and this entry exists so the model it should check against is written down rather
than living only in a code comment.

### `skala_place_simple_embedded_statement_on_same_line`, masked by `skala_keep_existing_embedded_arrangement = true`

Asked in both directions, one key flipped from the export at a time, the oracle returns every probe
byte-identical at all three values. What survives under the mask is the width rule alone:

```
// never, keep = true
if (c) M(c, d);                    ← left joined; `never` does not get to break it
if (depth < 0)                     ← broken, and only because the `if` overflows the margin
    throw new ArgumentOutOfRangeException(…);
```

⚠ The previous note in `PlanEmbeddedStatement` read that second line as "`never` is not gated on the
keep key" and it is not: the break is the margin's. Under keep, the placement key is inert in **both**
directions.

With the mask lifted (`skala_keep_existing_embedded_arrangement = false`) the key is real, and "simple" has
two halves, both measured:

```
// always, keep = false
if (c) M(c, d);                    joined       — a plain statement
while (c) M(c, d);                 joined
if (c) / if (d) / M(c, d);         unmoved      — the embedded statement carries one of its own
if (c) / using (…) / M(c, d);      unmoved
if (c) { if (d) M(c, d); }         JOINED       — the same nesting, inside a block
if (c) M(c, d); else M(d, c);      joined       — an `else` clause joins
```

So a statement carrying an embedded statement is not simple, **and** an owner that is itself somebody
else's embedded statement does not get to join. Skala reproduces both probes byte for byte; the sweep
simply cannot ask.

### `skala_place_simple_switch_expression_on_single_line`, masked by `skala_wrap_switch_expression = chop_always`

The precedence was recorded backwards — `PlanSwitchExpression` said this key outranks `chop_always`.
Measured, one key flipped at a time:

```
chop_always  + place = true     every arm on its own line — the placement key does nothing
wrap_if_long + place = true     `value switch { 1 => 1, _ => 0 }` on one line
wrap_if_long + place = false    the braces open; the arms fill `1 => 1, _ => 0`
```

Under the export's own `chop_always` Skala flattened a chopped switch expression onto one line at
`true` and the oracle left it chopped; that was the whole of the row. The wrap style outranks the
placement key, and only with the mask lifted does the key decide anything.

- options: `skala_place_simple_embedded_statement_on_same_line`,
  `skala_place_simple_switch_expression_on_single_line`
- ⚠ status: **accepted as unreachable from the one-at-a-time sweep**, not as a defect. Both keys agree
  with the oracle at every value both under the mask and with it lifted. They are two of the pairs
  `pairwise` exists for: `(skala_place_simple_embedded_statement_on_same_line,
  skala_keep_existing_embedded_arrangement)` and `(skala_place_simple_switch_expression_on_single_line,
  skala_wrap_switch_expression)`.
- Both are registered the way `align_multiline_argument` already is for exactly this situation —
  `OfInert` plus an `inert` note naming the masking key and the measurement behind it. Here "inert"
  means "no input distinguishes its values *under this configuration*", which is the sense that entry
  established; it does not mean the formatter ignores them.
- ⚠ Their `oracle` globs are deliberately KEPT rather than nulled, which is where they differ from
  `align_multiline_argument`. The committed sweep carries a demoted row for each, and
  `OptionCoverageTests.TierD_CarriesAFixtureOnlyWhereTheSweepDemotedIt` is right to refuse a glob
  stripped out from under it: the next sweep has to be able to re-measure the claim made here. The
  rows will read `UNEXERCISED`, and this entry is what that verdict points at.

## SK-DIV-0084 — `predefined_type_for_locals_parameters_members = false` asks for the framework name, and Skala's rule only contracts

⚠ **Still open, and NOT a defect at the export's value — see the section at the end**, which refutes a
framing this entry has now been handed twice and widens the specification from five positions to
roughly fifteen.

`PredefinedTypeRule` rewrites `Int32` ⇒ `int` and reads
`dotnet_style_predefined_type_for_locals_parameters_members` to decide whether to. It has no rewrite
in the other direction, and that key's `false` is a request for one. Asked one key at a time under
the cleanup profile on `constructs/arrangement/redundancy/qualifiers-and-parentheses.cs`, where this
key's `oracle` glob used to point:

```csharp
int _count;                            →  Int32 _count;
static int _shared;                    →  static Int32 _shared;
void Shadowed(int _count)              →  void Shadowed(Int32 _count)
void Parentheses(int a, int b, int c)  →  void Parentheses(Int32 a, Int32 b, Int32 c)
```

Skala leaves all four as written, so the key read `DIVERGENT` at `false` and agreed at the export's
`true`. ⚠ None of those four lines is what that fixture is about — it is the this-qualifier and
parenthesis file — so the row was measuring a construct the key does not name, which is the same
shape SK-DIV-0013's redundant brace had on the same file and for which the brace case was moved out.

⚠ **Implementing the expansion is a capability, not a relaxed precondition.** `int` ⇒ `Int32` is
legal only where `Int32` binds — `System` in scope, or the name written out — and being wrong about
it costs the whole document rather than the one rewrite: safety layer 3 re-binds, finds `CS0246`, and
reverts the file. Nothing in this repository's configuration asks for the direction; the export
writes `true`. The sibling key `dotnet_style_predefined_type_for_member_access` has the same
one-directional rule and reads `CONFORMANT`, because `redundancy/predefined-member-access.cs` writes
every governed position under its framework name and therefore only ever asks for the contraction.

`constructs/arrangement/redundancy/predefined-type-declarations.cs` is this key's fixture now and is
written the same way, which is what lets the row attribute. The expansion itself has **no fixture**,
and cannot have one: at the export's `true` there is nothing to expand and the two engines agree, so
the divergence exists only at the value the export does not use.

⚠ **Re-measured 2026-08-30 under `OracleProfile.Cleanup` on a fixture written for this key**, with
`dotnet_style_predefined_type_for_locals_parameters_members = false` and nothing else flipped:

```csharp
int _count;                            →  Int32 _count;
static int _shared;                    →  static Int32 _shared;
void Shadowed(int _count)              →  void Shadowed(Int32 _count)
void Parentheses(int a, int b, int c)  →  void Parentheses(Int32 a, Int32 b, Int32 c)
string Name()                          →  String Name()          ← and the return type, which this entry had not recorded
```

Skala leaves all five as written. The control holds: at the export's `true` the two engines agree
byte for byte on the same file. The fifth row is new — the expansion reaches a **return type** as
well as a field, a parameter and a local, so "locals, parameters, members" is to be read at its
widest.

### ⚠ 2026-08-30 — still open, and the "five rows" are nearer fifteen

⚠ **First, the framing this entry has to keep refusing.** A triage brief put this batch's five entries
under the heading "wrong at the configuration the repository actually ships — the export's own value".
That is true of the other four and **false of this one**, and the measurement says so plainly:
`.editorconfig` line 169 is `dotnet_style_predefined_type_for_locals_parameters_members = true`, at
`true` the two engines agree byte for byte on this key's own fixture, and
`verify dotnet_style_predefined_type_for_locals_parameters_members` reports **Conformant** both before
and after this batch's work. The divergence lives only at `false`, which this repository does not set.
Nothing here is wrong for a user running the shipped configuration. That is what the triage already
concluded — "the lowest-priority of this batch's debts because the export's own value agrees" — and
the brief inverted it.

⚠ **Second, the specification was too narrow.** Re-measured under `OracleProfile.Cleanup` at `false`
on a purpose-built probe, the expansion reaches far more positions than the five rows above, and it is
the whole predefined set rather than `int` and `string`:

| position | at `false` |
|---|---|
| field, static field | `int _count` ⇒ `Int32 _count` |
| ⚠ **property type** | `public bool Enabled` ⇒ `Boolean`, `public long Total` ⇒ `Int64` |
| return type | `string Name()` ⇒ `String`, `double Ratio(…)` ⇒ `Double` |
| parameter | `Double numerator, Double denominator`, `Int32 a, Int32 b, Int32 c` |
| ⚠ **type argument** | `List<int>` ⇒ `List<Int32>`, `Dictionary<string, int>` ⇒ `Dictionary<String, Int32>` |
| ⚠ **in an object creation** | `new List<int>()` ⇒ `new List<Int32>()` |
| ⚠ **cast expression** | `(int)value` ⇒ `(Int32)value` |
| ⚠ **`object`** | `Object value` — so it is every predefined keyword, not the numeric ones |

And what it leaves alone, each for its own reason:

- `int.MaxValue` — a **member access receiver**, which is the sibling key
  `dotnet_style_predefined_type_for_member_access`'s and stays `int` while that key is `true`. The two
  keys partition the positions and this probe shows the seam.
- `nameof(Int32)` — the spelling is the value.
- `IntPtr Native` — `builtin_type_apply_to_native_integer = false`, as recorded.
- ⚠ **a local variable, and it is masked rather than exempt.** `int local = count;` comes back
  `var local = count;` at *both* values, because the cleanup profile's `ArrangeVarStyle` claims it
  first. So the "locals" in the key's own name cannot be observed on any probe that lets `var` run, and
  whether the expansion reaches a local whose type `var` declines is **not measured**.

- options: `dotnet_style_predefined_type_for_locals_parameters_members`, `dotnet_style_predefined_type_for_member_access`
- ⚠ status: **open**; a missing capability rather than a decision, and it needs its own task.
  `PredefinedTypeRule.Rewriter` visits `IdentifierNameSyntax` and `QualifiedNameSyntax` only — the
  framework-named spellings — so there is no visitor for `PredefinedTypeSyntax` and the reverse
  direction does not exist even in outline. ⚠ **And the fixture is reachable after all**, which this
  entry had wrong: `constructs/arrangement/redundancy/predefined-type-declarations.cs` is written
  framework-named *so that* it measures only the contraction, which is the same deliberate dodge
  `usings/import-groups.cs` used for SK-DIV-0074 and which was removed there. Written the other way
  round — every governed position in its predefined spelling — the file measures the **expansion** on
  the existing corpus with no per-directory `.editorconfig` at all: at `true` both engines leave a
  file that is already contracted alone, and at `false` both should expand it. Rewriting it is what
  makes the fix provable; it must not be rewritten before the fix, or the key goes DIVERGENT on a
  capability nobody has built yet.
- ⚠ **triage 2026-08-30: `debt`, size M.** A user who writes
  `dotnet_style_predefined_type_for_locals_parameters_members = false` and gets no framework names is
  not looking at a considered refusal, and this repository never made one — `PredefinedTypeRule` was
  written in the contracting direction because that is the direction the export asks for. Paying it:
  the expansion is only legal where `Int32` binds to `System.Int32` at the position, so the rule
  needs a `LookupSymbols` check per site and a decision about what to do when it does not bind
  (write `System.Int32`, or decline) — being wrong costs the whole document, because layer 3
  re-binds, sees CS0246 and reverts the file. The other half is the fixture: at the export's `true`
  there is nothing to expand, so the divergence exists only at the value the export does not use and
  cannot be pinned without the per-directory `.editorconfig` mechanism SK-DIV-0032 also wants. ⚠ The
  five rows above are the specification; recording them is what lets this be paid after the oracle
  is gone, and it is the lowest-priority of this batch's debts because the export's own value agrees.
  ⚠ **The "five rows" are superseded by the table in the section above** — the expansion reaches
  property types, type arguments, object creations and cast expressions as well, and covers every
  predefined keyword rather than `int` and `string`. The triage's *ordering* stands and was re-confirmed
  by the same run: the export sets `true`, the two engines agree at `true`, and `verify` reports
  Conformant.

## SK-DIV-0085 — `sort_usings = false` still reorders, and the oracle's unsorted order is not the written one

Skala reads `skala_sort_usings = false` as "leave the block in the order it was written" and
sorts nothing. The oracle reorders it anyway. Measured on two probes under the cleanup profile, with
the removals the same on both sides:

```
written                          oracle, sort_usings = false
System.Text                      Alias = System.Collections.Generic.List<int>
Alpha.Things                     static System.Math
System.Globalization             System.Text
Alias = …List<int>               System.Globalization
static System.Math               Alpha.Things

System.Text                      Zebra = System.Globalization.CultureInfo
System.Collections.Concurrent    Alpha = System.Text.StringBuilder
Zebra = …CultureInfo             static System.Math
Alpha = …StringBuilder           System.Text
static System.Math               System.Collections.Concurrent
```

Two things are legible and one is not. The **groups invert**: sorted, the block is
plain → alias → `static`; unsorted, it is alias → `static` → plain. Within the alias group the
written order holds, which is the same fact `UsingsRule.SortKey` records for the sorted case. Within
the plain group two hypotheses fit both probes and neither is refuted by them — "`System` first, then
the rest, written order within each" and "descending ordinal" — so what the oracle is doing there is
**not established**, and reproducing a guess would be worse than reproducing nothing.

⚠ The key is not inert on Skala's side and this is not the `UNEXERCISED` case: at `true` Skala sorts
and agrees with the oracle byte for byte, and at `false` it emits a different order from the oracle's
rather than the same one. The disagreement is confined to the value the export does not use.

### ⚠ The probe this entry asked for was run on 2026-08-30, and it refutes both hypotheses

Nine directives, all resolvable and all used, interleaving alias, `static` and plain, and with three
non-`System` plain namespaces (`Zulu.Alpha`, `Alpha.Zulu`, `Mango.Beta`) placed among three `System`
ones so that `System`-ness and ordinal order disagree in both directions. Under the cleanup profile
at `skala_sort_usings = false`:

```
written                              oracle
1  Zulu.Alpha            (plain)     Zed = …CultureInfo        (3)
2  System.Text           (plain)     static Zulu.Alpha.ZHelp   (5)
3  Zed = …CultureInfo    (alias)     Abe = …StringBuilder      (7)
4  Alpha.Zulu            (plain)     static System.Math        (9)
5  static …ZHelp         (static)    Zulu.Alpha                (1)
6  System.Collections.Concurrent     Alpha.Zulu                (4)
7  Abe = …StringBuilder  (alias)     System.Collections.Conc.  (6)
8  Mango.Beta            (plain)     Mango.Beta                (8)
9  static System.Math    (static)
```

**The rule is: a directive that binds a name — an alias or a `static` — is hoisted above the plain
ones, and both partitions keep their written order.** Not two groups but one: aliases and `static`s
interleave with each other exactly as written (3, 5, 7, 9), which the "plain → alias → `static`"
reading above gets wrong. Within the plain partition the written order is preserved with a
non-`System` namespace *first* (1 before 6), which refutes "`System` first"; and `Zulu.Alpha` staying
ahead of `Alpha.Zulu` refutes "descending ordinal". A control run with no aliases and no `static`s at
all came back in written order, unchanged, both with and without an unused directive to remove — so
the hoist is the whole effect and a removal does not trigger a re-sort.

⚠ That leaves one row of this entry's first probe unexplained: `Alpha.Things` moving to the end of
the plain group. It could not be reproduced — the re-run's `Alpha.Things` was unresolvable and got
removed instead — and the nine-directive probe above contradicts the only rule that would explain
it. Treat the first table's fifth row as unreliable, not the model.

- options: `skala_sort_usings`
- ⚠ status: **open**, and no longer a guess: the oracle's rule at `false` is stated above.
- ⚠ **triage 2026-08-30: `deliberate`**, and now for a reason rather than for want of a measurement.
  The argument does not mention ReSharper: **`sort_usings = false` says do not sort, and Skala does
  not sort.** A user who switches sorting off and finds their using block reordered anyway has been
  told one thing and given another; that the reordering is small and consistent does not make it
  asked for. Reproducing it would mean implementing a hoist nobody requested in order to agree with
  a tool that will not be installed. `UsingsRule` keeps the written order at `false` and sorts at
  `true`, where it agrees with the oracle byte for byte, and the sweep's `Divergent` row for this key
  is the price of the position rather than evidence against it. The oracle's rule is written down
  above so that the choice stays re-examinable once the oracle is gone.

## SK-DIV-0086 — with the body-style heuristic off, a body carrying a comment still stays a block

At `skala_use_heuristics_for_body_style = false` the oracle converts *every*
single-statement body, and a body carrying a comment is converted with the comment on its own line
between the `=>` and the expression:

```csharp
public void Commented() =>
    // the author said something here
    Console.WriteLine("x");
```

Skala leaves it a block. The other two halves of that key's heuristic do lift: `throw` in statement
position already did, and `async void` now does — `async void AsyncVoid() => await Task.Delay(1);`
is what both engines write at `false`. The `#if` case is not a heuristic at all and holds at both
values, because a `#if DEBUG` around the only statement means the body has no active statement to
lift.

⚠ Carrying the comment through *arrangement* is three lines, and it was written and then thrown
away rather than shipped. The comment survives the rewrite; the **formatter** then leaves it at
column 0:

```csharp
public void Commented() =>
// the author said something here
    Console.WriteLine("x");
```

which is worse than not converting. Skala's formatter has no indentation rule for a comment between
an arrow and its expression, and acquiring one is a formatter change with its own fixtures — not
something to bolt onto an arrangement rule. At the export's `true` the heuristic holds and
`constructs/arrangement/body-style/heuristics.cs` agrees with the oracle byte for byte.

### ⚠ Re-measured 2026-08-30, and the entry names one shape where there are two

At `skala_use_heuristics_for_body_style = false`, under the cleanup profile, with the export's values
otherwise:

| body | oracle | Skala |
|---|---|---|
| a comment **above** the only statement | `=>` with the comment on its own line between arrow and expression | stays a block |
| `return 1; // trailing` | **`=> 1; // trailing`** | stays a block |
| `throw new …;` | stays a block | the same |
| `async void` + `await` | `=>` | the same |
| `#if DEBUG` around the only statement | stays a block | the same |

The second row is new and it is not covered by the entry's argument. `HasTriviaThatBlocksConversion`
walks `DescendantTrivia` and refuses on **any** comment anywhere in the body, so a comment that
merely trails the statement blocks the conversion too — and there is nothing to place: the comment
stays on the same line as the expression, where the formatter already handles it.
`BodyStyleRule.Semicolon` even carries a closing brace's trailing trivia through for exactly this
reason; it is the statement's own trailing trivia that is being dropped on the floor.

- options: `skala_use_heuristics_for_body_style`
- ⚠ status: **open**; the blocker is the formatter's comment placement and not the body-style rule.
- ⚠ **triage 2026-08-30: `debt`, size S** — for the second row. The two halves are different things
  and only one of them was decided:
  - **the leading-comment half is `deliberate`.** "A body with a comment inside it stays a block" is
    defensible on its own terms, and the alternative was measured and thrown away: carrying the
    comment through arrangement is three lines, and the formatter then leaves it at column 0, which
    is worse than not converting. Acquiring an indentation rule for a comment between an arrow and
    its expression is a formatter change with its own fixtures, and it is a fair thing to decline.
  - **the trailing-comment half is `debt`.** No blocker applies to it — the output is one line with
    the comment where the author left it — so the refusal is the precondition being wider than its
    reason. Paying it: narrow `HasTriviaThatBlocksConversion` to comments and directives that are
    *not* trailing trivia on the body's last statement, and carry that trivia onto the new
    semicolon the way `Semicolon(closeBrace)` already does. A fixture row beside
    `constructs/arrangement/body-style/heuristics.cs`. ⚠ Needs the oracle for the fixture only;
    reachable only at `skala_use_heuristics_for_body_style = false`, which the export does not set.

## SK-DIV-0089 — the four formatter-tag keys, and why no one-key flip can ask about any of them

All four were `SPURIOUS` in the key-flip sweep and all four now read `UNEXERCISED`. **Three real
defects were found on the way and are fixed**; what is left is a statement about the probe, and it is
provable rather than suspected.

### `cleanupcode` honours formatter tags, and it reads all four keys

The first question was whether the CLI oracle honours `@formatter:off` at all, because if it did not
then Skala honouring it would be a deliberate divergence and the row would be evidence rather than a
defect. It does. Measured on `constructs/trivia/skala_formatter_tags_enabled.cs`, one appended
`[*.cs]` section, `SkalaFormatOnly`:

```
// the export's own configuration
class C {
    // @formatter:off
    void   M( )   {          ← preserved
    }
    // @formatter:on
    void N() { }             ← formatted
}

// the negative control: the same file with the tags rewritten to `@fmt`
    // @fmt:off
    void M() { }             ← formatted; the tag is not recognised, so the mechanism is live
```

And it reads the keys: with `skala_formatter_off_tag = @fmt:off` and
`skala_formatter_on_tag = @fmt:on`, that same `@fmt` file comes back preserved.

### What the four keys actually mean — three findings, all fixed

The built-in `@formatter:off` / `@formatter:on` are recognised **whatever the four keys say**, and the
configured pair is **additional to** them rather than a replacement. `tags_enabled` and
`accept_regexp` govern only the configured pair. Every line below is a separate `cleanupcode` run:

| configuration | source's tag | oracle | Skala, before | after |
|---|---|---|---|---|
| `off_tag = @zzz:off`, `on_tag = @zzz:on` | `@formatter:off` | preserved | formatted | preserved |
| `off_tag = @fmt:off`, `on_tag = @fmt:on` | `@fmt:off` | preserved | preserved | preserved |
| `tags_enabled = false` | `@formatter:off` | preserved | formatted | preserved |
| `tags_enabled = false`, `off_tag = @fmt:off` | `@fmt:off` | formatted | formatted | formatted |
| `accept_regexp = true`, `off_tag = @f.*:off` | `@fmt:off` | preserved | formatted | preserved |
| `accept_regexp = false`, `off_tag = @f.*:off` | `@fmt:off` | formatted | formatted | formatted |

So Skala was wrong in three ways, and every one of them was **less protective than the oracle** on the
one feature whose entire contract is "nothing touches this":

1. a configured `off_tag` *replaced* the built-in, so `skala_formatter_off_tag = @fmt:off` silently
   stopped honouring every `// @formatter:off` already written in the tree;
2. `skala_formatter_tags_enabled = false` opened the guard outright, so one line of configuration
   disabled the escape hatch for every file — where the oracle keeps honouring the built-in pair;
3. `skala_formatter_tags_accept_regexp = true` also opened the guard, under a comment saying the
   regexp reading was "not implemented, in any pass". The one key a person sets to make their tags
   *more* expressive turned the hatch off.

All three are fixed in `FormatterTagGuard`: `FormatterTags.BuiltinOff` / `BuiltinOn` are matched
unconditionally, the configured pair is matched beside them under `Enabled`, and `AcceptRegexp`
compiles the configured tag as a pattern anchored at the start of the comment's body — anchored so
that SK-DIV-0017's narrowing survives the regexp reading and a pattern cannot re-open the
match-anywhere footgun the literal reading was narrowed to close. An unparsable pattern matches
nothing rather than falling back to a literal, because a typo that silently becomes a different rule
is the failure mode this feature cannot have.

### Why the sweep still cannot ask, and why that is the probe

Every row now agrees at every value the sweep offers, which is `UNEXERCISED` and not a pass. The
reason is not a weak fixture and it is not a masking key — it is arithmetic on the probe's own value
generator, and it holds for **every** fixture:

- `OptionDomain.Probes` offers a free-form string exactly two values: the key's default, and the
  default with `x` appended.
- The tag test is "the comment's body starts with the tag" — in both engines; the oracle's is even
  wider, a plain substring.
- So if a comment matches `@formatter:offx` it also matches `@formatter:off`, and `@formatter:off` is
  the built-in, which fires whatever the key says. Both probe values therefore produce the same
  output on any input whatsoever. The key is unobservable **by construction of the probe**, not by
  choice of fixture.

`tags_enabled` and `accept_regexp` are masked one step further out, and that half *is* SK-DIV-0083's
shape: both govern the configured pair only, and the export leaves the configured pair sitting on the
built-in values, where neither key can change anything. They are reachable, but only in pairs —
`(tags_enabled, off_tag)` and `(accept_regexp, off_tag)` — which is what `pairwise` exists for.

- options: `skala_formatter_off_tag`, `skala_formatter_on_tag`,
  `skala_formatter_tags_enabled`, `skala_formatter_tags_accept_regexp`
- ⚠ status: **accepted as unreachable from the one-at-a-time sweep**, not as a defect. Skala agrees
  with the oracle at every value the sweep can offer *and* at every configuration measured by hand
  above.
- All four are registered `OfInert`, in the sense `align_multiline_argument` established and
  SK-DIV-0083 restated: "no input distinguishes its values *under this configuration*", not "the
  formatter ignores them". ⚠ **This entry first said the opposite** — that the mark was wrong for keys
  whose behaviour is pinned by six tests — and the harness refuted it in the same run:
  `OptionCoverageTests.EveryImplementedOption_ChangesTheOutputOfItsCorpusFile` and
  `OptionObservabilityTests.EveryValue_IsDistinguishableOnTheKeysOwnFixture` both failed on all four,
  and both were right to. An implemented key that no input can distinguish is exactly what the mark is
  for; the tests were the argument and the prose was the opinion.
- ⚠ Their `oracle` globs are kept, the way SK-DIV-0083's are, so the next sweep re-measures the claim
  and `UNEXERCISED` points here.
- ⚠ The model above is pinned by `FormatterTagTests` — one test per row of the table, each with the
  unrecognised-tag negative control beside it — because the sweep cannot pin it and a measurement that
  only lives in a document is one nobody will notice going stale.

## SK-DIV-0090 — `skala_use_continuous_indent_inside_parens` / `_initializer_braces`, masked by `skala_continuous_indent_multiplier = 1`

Both were `SPURIOUS` in the key-flip sweep and both now read `UNEXERCISED`. **A real defect was found
and is fixed**: `false` does not mean "no indent", it means "one indent width", and the two readings
are the same number under the export's own multiplier — which is why the oracle could not move and
Skala could.

⚠ **This settles the note the registry and `docs/tier-d-split.md` disagreed about.** The registry said
these keys are "implemented and observable"; a pass measured `SPURIOUS` and could not reconcile the
two. Both were right. The key *is* implemented and observable in `jb cleanupcode` — the mask is
`skala_continuous_indent_multiplier`, which the export sets to `1`.

### The measurement

`skala_continuous_indent_multiplier = 2` is the only configuration that can ask, and at it the
oracle is decisive at both values and in both spellings, prefixed and unprefixed:

```
// skala_use_continuous_indent_inside_parens            // …_inside_initializer_braces
M(                    M(                          new List<int> {      new List<int> {
        a,                a,                              1,               1,
        b                 b                               2                2
);                    );                          };                   };
true                  false                       true                 false
8 + 2×4               8 + 1×4                     12 + 2×4             12 + 1×4
```

So `false` is **one indent width of continuation**, not the absence of one. Skala suppressed the
scope outright, putting the contents on the owning construct's own column — a level short of the
oracle at every multiplier, and visible in the sweep only because at multiplier 1 the oracle's two
answers coincide and Skala's do not.

`IndentKind.OneLevel` is what the IR was missing: `Continuous` with the multiplier forced to 1. It is
deliberately not `Block` — a block is absolute and replaces whatever continuation is open, and the
contents of a parenthesis do not reset the continuation context.

⚠ **One half of the `true` arm is still short, and it is a different key's row.** A braced
initializer's contents come from an `IndentKind.Block` scope, which is one indent width whatever the
multiplier says, so at any multiplier above 1 Skala's `true` is a level short of the oracle. That is
`skala_continuous_indent_multiplier`'s defect on braced initializers, not this key's; it is recorded at the
key in `options.json` and is not fixed here, because turning an absolute scope into a relative one
under every initializer in `corpus/real` is not a change to make on the strength of a row that does
not ask about it.

- options: `skala_use_continuous_indent_inside_parens`,
  `skala_use_continuous_indent_inside_initializer_braces`
- ⚠ status: **accepted as unreachable from the one-at-a-time sweep**, not as a defect. Skala now
  agrees with the oracle at both values under the export's multiplier *and* at the multiplier that
  unmasks them. Two more pairs for `pairwise`: `(skala_use_continuous_indent_inside_parens,
  skala_continuous_indent_multiplier)` and `(skala_use_continuous_indent_inside_initializer_braces,
  skala_continuous_indent_multiplier)`.
- ⚠ Both `oracle` globs are kept, the way SK-DIV-0083's are, so the next sweep re-measures the claim
  and `UNEXERCISED` points here.

## SK-DIV-0091 — `csharp_indent_braces`, and the brace-split direction the placement family does not have

Four brace rows were resolved together and three of them are **fixed**; this entry is the residue, and
it is two separate things that share a mechanism.

### What was fixed, because it is the larger half

`csharp_new_line_before_open_brace` is a **flags** option with twelve members and Skala read it as a
two-valued switch — `NewLineBeforeOpenBrace is "none"`, so every other value behaved as `all`. **2 of
its 15 values agreed with the oracle; 15 of 15 do now.** The seven groups the C# formatter actually
has were measured one value per run, on a probe whose every body holds two statements and every
initializer six elements — the first attempt had one-statement bodies and read `accessors`,
`lambdas` and `object_collection_array_initializers` as inert when all three are live, because the
export had already joined the constructs they govern:

| value | what moves |
|---|---|
| `types` | class, struct, interface, enum, record — **and a block namespace** |
| `methods` | method, constructor and operator bodies — **and local functions** |
| `properties` | the accessor **list** of a property, an indexer or an event alike |
| `accessors` | the accessor **body**: `get`, `set`, `add`, `remove` |
| `control_blocks` | `if`, `while`, `for`, `foreach`, `do`, `lock`, `using`, `try`/`catch`/`finally`, `switch` |
| `lambdas` | a lambda body — **and an anonymous method's `delegate(…) { }`** |
| `object_collection_array_initializers` | initializer and anonymous-type braces — **and a switch *expression*'s and a property pattern's** |
| `anonymous_methods`, `anonymous_types`, `events`, `indexers`, `local_functions` | **nothing** — each is covered by a neighbour |

Three of those groupings are the ones reading the names would get wrong, and each was checked against
the neighbour that would have explained it away: `local_functions` moves nothing while `methods` moves
the local function; `events` and `indexers` move nothing while `properties` moves all three accessor
lists; `anonymous_methods` moves nothing while `lambdas` moves the `delegate`.

`skala_empty_block_style`'s `together_same_line` (read as `multiline`, so the pair was split),
`skala_new_line_before_while`'s indentation and `skala_special_else_if_treatment`'s split direction were fixed in
the same pass and all three now read `Conformant`.

### The first thing that is not fixed: the split direction

Brace placement in this formatter is a **join** decision. `ShouldJoin` is "the one place phase 1
removes a line break the author wrote", and nothing inserts one before a brace. So a K&R input under
`csharp_new_line_before_open_brace = all` comes back K&R, where the oracle splits every brace onto its
own line; and `skala_empty_block_style = together_same_line`'s second half — pulling `{ }` back onto the
declaration's line *against* the placement key — cannot be honoured either.

⚠ **This is invisible to all four rows, and not by luck.** Every one of their fixtures is written with
the break already there, so all fifteen values of the placement key only ever ask the join question.
`FormatterTests.BraceSplitIsNotImplemented` asserts the gap so that it is a recorded absence rather
than a discovery. Closing it means a break point before a brace in every construct in the language,
which is not a change to make from a row that is already `Conformant`.

`skala_special_else_if_treatment` is the one member of the family that *did* get its split direction, in
`MustBreak`, because its row needed it and its shape is a keyword rather than a brace.

### The second: `csharp_indent_braces`, and the probe that moved the question

The key governs a brace **on a line of its own**. Measured with both Roslyn brace keys supplied
together, the oracle is flat at both values under the export's `csharp_new_line_before_open_brace =
none`, and decisive with the braces split — where the answer is Whitesmiths, the brace taking one
level and the body taking the *same* level:

```
class C                     class C
    {                       {
    void M()                    void M()
        {                       {
        if (true)                   if (true)
            {                       {
            M();                        M();
            }                       }
        }                       }
    }                       }
indent_braces = true        indent_braces = false
```

Skala applied the key whatever the brace placement, so under `none` the joined opening brace could not
move and the closing one did — a shape no configuration of either key writes. That is fixed; the key
is now inert under the export, exactly as the oracle is.

⚠ **And that is why its sweep row reads `INERT` — the first in the table — rather than `UNEXERCISED`.
The verdict is about the probe.** The sweep appends the key in an `[*.cs]` section of its own, and
ReSharper re-derives the Roslyn brace pair from *that section*, defaulting the absent
`csharp_new_line_before_open_brace` to Roslyn's `all`. So the oracle switches to Allman at **both**
values and answers a question about brace *placement* that was never asked:

```
base only                          → K&R          (both engines; BaselineAgrees)
+ [*.cs] csharp_indent_braces=false → ALLMAN      ← the coupling
+ [*.cs] with both keys, either order → K&R       ← ask properly and it is flat
```

The export itself sets both keys, `csharp_indent_braces` first, and is unaffected; it is only the
appended single-key section that trips it.

Skala cannot follow, and deliberately does not try. Reproducing it means teaching the option resolver
that assigning one key in a section silently re-derives another from that section's *absent* members —
a rule whose only justification is that ReSharper does it, on a configuration nobody writes. Doc 00's
non-negotiable 9 is exactly this case: the reference tool is a test subject, not a specification.

- options: `csharp_indent_braces`, and the split-direction gap in `csharp_new_line_before_open_brace`
  and `skala_empty_block_style`
- ⚠ status: **accepted**. `csharp_indent_braces` agrees with the oracle at both values whenever the
  question is put to both engines the same way; the row's disagreement exists only under a
  single-key section that changes what the oracle was asked. Registered `OfInert` with the mask named,
  and its `oracle` glob is kept so the next sweep re-measures it.
- ⚠ `INERT` in the sweep's own table means "the oracle moved and Skala did not — the key is ignored",
  and for this row that reading is wrong in both halves. Whoever runs the next sweep should read this
  entry before acting on the count.

## SK-DIV-0092 — `skala_blank_lines_around_single_line_property` governs a shape this export never produces, and the fixture said otherwise

`SPURIOUS` in the key-flip sweep, and the defect was real: Skala applied the key to a shape ReSharper
does not, so Skala moved where the oracle could not. **That is fixed.** What is left is a mask.

### What "a single-line property" is, and the claim it refutes

Measured 2026-08-30, one key at a time, at `0`, `1`, `2` and `3`, on input written both tight and with
blank runs already in it so that both directions were asked:

| shape | the key that governs it |
|---|---|
| `public int X { get => 1; }` — an **accessor-list** property on one line | **this key**, decisive at 2 |
| `public int X { get; set; }` | `skala_blank_lines_around_single_line_auto_property`, confirmed on the same run |
| `public int X => 1;` — expression-bodied | **neither**: not this key in either spelling, and not `skala_blank_lines_around_property` |

⚠ **The key's own corpus fixture asserted the opposite**, in a comment that reads as a measurement:
"`public int X => 1;` is the single-line property this key governs — at 2 the oracle puts two blank
lines around each of these two". The oracle puts none, at every value and in both directions. The
comment is corrected in place rather than deleted, because the file had already been changed *once*
for this key — away from the accessor-list form, which was the right shape — and a note saying only
"this is the shape" would invite the same move a third time.

`RequirementFor` now reads the key for an accessor-list property alone; an expression-bodied one
states no blank-line requirement, which is what the oracle does. ⚠ Only the *single-line* arm is
narrowed. A multi-line expression-bodied property still takes `skala_blank_lines_around_property`, because
that is not a shape this pass measured and the register does not extend a measurement past what was
asked.

### Why the row cannot reach `Conformant`

`skala_keep_existing_declaration_block_arrangement = false` in the export expands
`public int X { get => 1; }` onto three lines, and a property that is not on one line is not this
key's. So the one shape the key governs cannot exist under this configuration, and no fixture makes it
observable from a single flip — SK-DIV-0083's shape exactly. With the mask lifted
(`skala_keep_existing_declaration_block_arrangement = true`) the key is decisive at 2 in both engines.

The fixture keeps the expression-bodied shape rather than moving back to the accessor-list one,
because the accessor-list form would be expanded and the row would be `UNEXERCISED` either way — and
this way the file carries the measurement that was wrong twice.

- options: `skala_blank_lines_around_single_line_property`
- ⚠ status: **accepted as unreachable from the one-at-a-time sweep**, not as a defect. Registered
  `OfInert` with the mask named; the `oracle` glob is kept so the next sweep re-measures it. One more
  pair for `pairwise`: `(skala_blank_lines_around_single_line_property,
  skala_keep_existing_declaration_block_arrangement)`.

## SK-DIV-0093 — `keep_existing_lambda_and_anonymous_function_parens_arrangement` is a key the C# formatter does not read

`SPURIOUS` in the key-flip sweep, and the finding is the one this batch's brief warned is the more
valuable verdict: not a mask, not a weak fixture — **a documented editorconfig property the C#
formatter is not wired to.** Skala answered to it, so Skala moved where the oracle could not.

Measured on `constructs/preservation/lambda-parens.cs`, whose lambda has the author's break inside its
parentheses, one configuration per `cleanupcode` run:

| configuration | the single-parameter lambda |
|---|---|
| the export | `Use((\n int first\n ) => first\n);` — the break is kept |
| `keep_existing_lambda_… = false` | **unchanged** |
| `keep_existing_lambda_… = true` | unchanged |
| `skala_keep_existing_declaration_parens_arrangement = false` | **`Use((int first) => first);` — rejoined** |
| both `false` | rejoined, and no further |
| `declaration = false`, `lambda = true` | rejoined |

The last row is the control that settles it: with the declaration key doing the work, setting the
lambda key *back* to `true` does not restore the break. It is inert in both directions, under both
values of its neighbour, and in both spellings.

`skala_keep_existing_declaration_parens_arrangement` governs a lambda's and an anonymous method's
parameter list along with every other one. `BreakPlan.DeclarationKeeps` had a special case routing the
first two to the lambda key; it is gone, and one key now governs every parameter list.

⚠ The two-parameter lambda in the same file stays broken under every configuration above and rejoins
only at `keep_user_linebreaks = false`. That is a different key doing a different job — the break it
keeps is between *items*, not at the parenthesis — and it is named here because it is the obvious
wrong conclusion to draw from the table.

- options: `skala_keep_existing_lambda_and_anonymous_function_parens_arrangement`
- ⚠ status: **accepted**. The row goes `SPURIOUS` → `UNEXERCISED`, which is the honest verdict for a
  key nothing reads: both engines are flat at both values because there is nothing to be flat about.
  Registered `OfInert` with "unread" rather than "masked" spelled out, since the two are recorded the
  same way and mean different things. Still resolved and reported by `skala config explain` — a key
  the registry knows and the tool silently drops is worse than one it reports as having no effect.
- ⚠ Its `oracle` glob is kept. If a future ReSharper wires the key up, this is the row that notices.

## SK-DIV-0094 — an unstarred block comment's body moves with its opener, and Skala moves only the opener

⚠ **Found while measuring SK-DIV-0033 and separated from it deliberately**, because it is reachable at
the export's values on exactly the comments that entry's key declines to touch.

A multi-line `/* … */` comment whose continuation lines do **not** all begin with `*` is re-indented by
the oracle as a **unit**: the opening `/*` goes to the code's own column and every other line moves by
the same delta. Measured against `jb cleanupcode` 2025.2.6 under `OracleProfile.FormatOnly`, on a
comment written at opener 8 / body 6 / `*/` 5 inside a type whose member indent is 4:

| | opener | body | `*/` |
|---|---|---|---|
| written | 8 | 6 | 5 |
| oracle — at **both** values of `skala_align_multiline_comments` | 4 | 2 | 1 |
| Skala | 4 | 6 | 5 |

Skala moves the opener and leaves the interior at its written columns, so the comment's shape is
destroyed rather than preserved: the body ends up further right than its own delimiter by a margin that
depends on how far the opener moved.

⚠ **Not `skala_align_multiline_comments`.** The uniform shift happens identically at `true` and at `false`,
and it applies precisely to the comments that key does not govern — a comment whose every continuation
line begins with `*` is realigned instead (SK-DIV-0033), and one that qualifies for neither, because it
is a single line, has no interior to move.

⚠ **The mechanism is already in the formatter and pointed at something else.**
`VerbatimFlags.Realign` is exactly a uniform shift of a multi-line token's interior, written for
`skala_indent_raw_literal_string` and carrying the safety argument that a uniform shift leaves a raw string's
stripped value character for character identical. A comment has no value to preserve, so the argument
is only easier here. What is missing is the caller: `CSharpDocumentBuilder` emits a `BlockComment`
piece with `CommentFlags(piece)` and never with `Realign`.

- options: none. It is reachable at every configuration, and no key turns it off — `skala_align_multiline_comments`
  governs the starred case only.
- ⚠ status: **open**, measured, unfixed. Nothing in `corpus/real` reaches it — a scan of all 380 files
  finds no multi-line block comment at all whose opener the formatter moves — which is why nine
  milestones of fidelity measurement never saw it.
- ⚠ **Untriaged.** Recorded at the size the measurement supports and no further: the probe above is one
  comment at one depth, and what has *not* been asked is what happens when the opener moves right
  rather than left, when the body's own lines are ragged in the other direction, or when a line of the
  body would be pushed to a negative column. A uniform shift that clamps at zero is not the same rule
  as one that does not, and this probe cannot tell them apart.

## SK-DIV-0095 — `__makeref`, `__reftype` and `__refvalue` take a space before their parenthesis

⚠ **Found by the syntax-coverage audit** (`fidelity coverage`), which is the only reason it was found
at all: `MakeRefExpression`, `RefTypeExpression`, `RefValueExpression` and `ArgListExpression` were
four of the thirty-seven `SyntaxKind`s that occurred **nowhere** in `Testing/corpus/`, so no fidelity
number, no fixture and no divergence class had ever been a statement about them.

Measured against `jb cleanupcode` 2025.2.6 under `OracleProfile.FormatOnly`, at the export's values:

| written | oracle | Skala |
|---|---|---|
| `__makeref(origin)` | `__makeref(origin)` | `__makeref (origin)` |
| `__reftype(reference)` | `__reftype(reference)` | `__reftype (reference)` |
| `__refvalue(reference, int)` | `__refvalue(reference, int)` | `__refvalue (reference, int)` |
| `__arglist(alpha, bravo)` | `__arglist(alpha, bravo)` | `__arglist(alpha, bravo)` — **agrees** |

⚠ **The mechanism, and why `__arglist` is the one that agrees.** `SpaceRules.BeforeOpenParen`
switches on `next.Parent`. `ArgListExpression` carries a real `ArgumentListSyntax`, so it reaches the
`ArgumentListSyntax` arm and takes `skala_space_before_method_call_parentheses`. The other three carry no
argument list at all — the `(` belongs to `MakeRefExpressionSyntax` / `RefTypeExpressionSyntax` /
`RefValueExpressionSyntax` directly — so they fall through to the `default` arm, where
`SyntaxFacts.IsKeywordKind(prev.Kind())` is true of `__makeref` and the rule returns
`skala_space_between_keyword_and_expression`, which the export sets to `true`. That fallback is written for
`return (value)` and `await (task)`; three keywords whose parenthesis is a call site in everything but
the parse tree reach it by accident.

- options: `skala_space_between_keyword_and_expression` (wrongly), `skala_space_before_method_call_parentheses` (correctly)
- ⚠ status: **open**, measured, unfixed. Pinned by `constructs/syntax/varargs-and-typed-references.cs`,
  whose fixture is the oracle's answer.
- ⚠ **Low severity, deliberately recorded anyway.** Nobody writes `__makeref` on purpose. But this is
  precisely the class of defect the audit exists to find: it survived ten milestones because the
  construct was absent from the corpus, and after `jb` is uninstalled no `.expected.cs` for it could
  ever be authored.

## SK-DIV-0096 — an accessor list written on one line is expanded by the oracle and left alone by Skala

⚠ **Found by the syntax-coverage audit.** `EventDeclaration`, `AddAccessorDeclaration` and
`RemoveAccessorDeclaration` occurred nowhere in the corpus, and no corpus file anywhere carried a
non-auto accessor list written on a single line — so the shape had never been measured.

`skala_keep_existing_declaration_block_arrangement = false`, which the export sets, expands an accessor
list one accessor per line. Measured on eight shapes in one file, 2026-08-31:

| written | oracle | Skala |
|---|---|---|
| `int P { get => 1; }` | three lines | unchanged |
| `int P { get => 1; set { } }` | four lines | unchanged |
| `int P { get { return 1; } set { } }` | four lines | unchanged |
| `int P { get; set; }` | unchanged | unchanged |
| `int P { get; set { } }` | four lines | unchanged |
| `int P => 1;` | unchanged | unchanged |
| `event EventHandler E { add { } remove { } }` | four lines | unchanged |
| `int this[int i] { get => i; set { } }` | four lines | unchanged |

So the oracle's rule is: **an accessor list is broken one accessor per line unless every accessor is
semicolon-only.** An auto-property is the sole exception; an indexer's and an event's accessor lists
are governed identically, and an expression-bodied member has no accessor list to break. Skala
reformats none of them — `skala format` reports `0 files reformatted, 1 left alone` on the whole file.

⚠ **The mechanism.** `BreakPlan.PlanOnePerLine` applies the key to `TypeDeclarationSyntax` and
`NamespaceDeclarationSyntax` members and to a `BlockSyntax`'s statements, and to nothing else. An
`AccessorListSyntax` is a declaration block by the same key's own definition and has no arm.

⚠ **This narrows SK-DIV-0092's reasoning.** That entry argues the key
`skala_blank_lines_around_single_line_property` governs a shape "this export never produces", because the
oracle expands `public int X { get => 1; }` onto three lines. True of the oracle; **not true of
Skala**, which leaves the shape on one line and therefore does apply the key to it. The two engines
diverge on the blank-line question there as a consequence of this entry, not independently of it.

- options: `skala_keep_existing_declaration_block_arrangement`
- ⚠ status: **open**, measured, unfixed. Pinned by `constructs/syntax/field-keyword.cs`, whose last
  property is the one-line accessor-list form. ⚠ `constructs/syntax/event-accessors.cs` does **not**
  pin it — its accessor lists were written expanded and both engines agree on them, which is worth
  saying because a reader would otherwise expect the event fixture to be the one that fails.

## SK-DIV-0097 — a labelled statement's statement always starts a new line, and Skala writes `label: {`

⚠ **Found by the syntax-coverage audit.** `LabeledStatement` occurred twice in the whole corpus and
`GotoDefaultStatement` nowhere; neither instance was a label on a block.

Measured 2026-08-31. The oracle puts the labelled statement on its own line whatever it is:

| written | oracle | Skala |
|---|---|---|
| `block: { Consume(1); }` | `block:` / `{` / `Consume(1);` / `}` | `block: {` / `Consume(1);` / `}` |
| `empty: { }` | `empty:` / `{ }` | `empty: { }` |
| `plain: Consume(2);` | `plain:` / `Consume(2);` | agrees |

⚠ **No key turns it off, and three candidates were eliminated rather than assumed.** Re-measured with
`skala_outdent_statement_labels = false`, `skala_keep_existing_embedded_arrangement = true` and
`skala_keep_existing_embedded_block_arrangement = true` all set at once: the oracle still breaks. It is
unconditional. The divergence is only ever visible when the label's statement is a **block** — Skala
agrees on `plain:`, because a non-block statement's own break comes from elsewhere.

- options: none. Not `skala_outdent_statement_labels`, not either `keep_existing_embedded_*`.
- ⚠ status: **open**, measured, unfixed. Pinned by `constructs/syntax/goto-and-labels.cs`.

## SK-DIV-0098 — an expression body after a wrapped constraint clause goes to its own line

⚠ **Found by the syntax-coverage audit.** `AllowsConstraintClause` and `DefaultConstraint` occurred
nowhere and `TypeConstraint` was thin, so no fixture had a member with its constraint clauses on
their own lines *and* an expression body.

Measured 2026-08-31, and the width hypothesis is the one the measurement refutes:

| written | oracle |
|---|---|
| `static T Short<T>(T s) where T : class => s;` (one line, 46 columns) | unchanged |
| `static T Two<T>(T s) where T : class, new() => s;` (one line) | unchanged |
| `static T W<T>(T s)` / `    where T : class => s;` (35 columns on the constraint line) | `where T : class =>` / `s;` |

So it is **not a width rule**: at 35 columns, well inside the 120-column margin, the oracle still
breaks. The rule is that when a member's declaration head is already wrapped — a constraint clause on
its own line — the expression body's operand goes to a line of its own, with the `=>` left on the
constraint line. Skala keeps `=> s;` on the constraint line at any width.

⚠ **"Is that a fact about the probe, or about the key?"** The first reading of this was "a constraint
clause forces the break", which the one-line rows above refute: a constraint that stays on the
declaration's line keeps the body with it. What forces the break is the *head already being wrapped*,
not the constraint's presence.

- options: none identified. `skala_keep_existing_expr_member_arrangement = false` is set in the export and
  is a plausible owner, but it was not flipped in this measurement and the entry does not claim it.
- ⚠ status: **open**, measured, unfixed. Pinned by `constructs/syntax/generic-constraints.cs`.

## SK-DIV-0099 — a declaration head that overflows is not broken, and a break already in it is indented one level short

⚠ **Found by the syntax-coverage audit**, on two constructs at once — a `using` alias to a
non-name type (C# 12, which occurred **nowhere** in the corpus, while ordinary name aliases were
common) and a field declaration whose type alone exceeds the margin.

Measured 2026-08-31 in two directions, because the two halves are different claims. **Given a flat
input**, the oracle breaks and Skala does not:

| written flat | oracle | Skala |
|---|---|---|
| `using Overflows = (…IReadOnlyList<string> Names, …IReadOnlyDictionary<string, int> Counts);` at 140 columns | break after `=`, continuation at column 4, then a chop inside the tuple | unchanged, 140 columns |
| `…IReadOnlyDictionary<string, …IReadOnlyList<string>> Overflowing;` at 121 columns | break before the declarator, continuation at column 8 | unchanged, 121 columns |
| the same field at 117 columns | unchanged | unchanged — **the oracle's rule is a width rule here** |
| `delegate* unmanaged[Stdcall, MemberFunction, SuppressGCTransition]<…> Overflowing;` at 121 columns | break before the declarator | unchanged |

**Given the oracle's own output as input**, Skala keeps the break and puts the continuation one
indent level to the left of where the oracle put it:

| construct | oracle's continuation column | Skala's |
|---|---|---|
| `using` alias | 4 | **0** |
| field declarator (member indent 4) | 8 | **4** |

In both, the continuation lands at the construct's own start column rather than one level in. A
`using` continuation at column 0 is the worse of the two, because nothing else in a compilation unit
sits there.

⚠ **One entry for two constructs, and the arithmetic is not the argument.** Both are exactly one
indent level short, which is suggestive and is *not* evidence that one mechanism owns both — the two
go through different builder paths and nothing here has measured whether the same code is at fault.
Recorded together because it is one measurement made twice; it splits into two entries the moment
somebody measures a mechanism.

⚠ **Scoped to alias directives.** A plain `using System.…;` has no `=` to break at and was not asked
about; nothing here says what the oracle does with one.

- options: none identified.
- ⚠ status: **open**, measured, unfixed. Pinned by `constructs/syntax/alias-any-type.cs` and
  `constructs/syntax/unsafe-and-function-pointers.cs`.
