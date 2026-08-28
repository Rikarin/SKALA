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

- options: `resharper_csharp_keep_blank_lines_in_code`, `resharper_csharp_keep_blank_lines_in_declarations`
- ⚠ status: **open**, measured

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

`resharper_csharp_indent_raw_literal_string = align` asks the formatter to move the closing
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
`constructs/trivia/resharper_csharp_indent_raw_literal_string.cs`, and this entry is what its Tier B
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

- options: `resharper_csharp_indent_raw_literal_string` (Tier A, `default = align`, from the template)
- ⚠ status: **open**, measured

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
right-hand-side shapes, five block depths, both values of `wrap_before_eq`, one character at a time.
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
   `wrap_before_eq = true` the whole table moves down by two to four columns.

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

- options: `resharper_prefer_wrap_around_eq` (Tier D), `resharper_csharp_wrap_before_eq` (Tier D)
- ⚠ status: **open**, measured. The sweep is [sk-div-0005-margin-sweep.md](sk-div-0005-margin-sweep.md)
  and it says no value of the constant closes this; it is not tail work

## SK-DIV-0006 — the pinned oracle profile does not format documentation comments; Rider does, and so does Skala

⚠ **This entry's title used to be "`jb cleanupcode` does not format documentation comments, so
neither does Skala", and both halves have now been measured false.** The measurement behind it was
real and is reproduced below; the conclusion drawn from it was not. Read this entry as the record of
a wrong inference being corrected, because the way it was wrong is more useful than the fact.

[05](plan/05-csharp-formatting-rules.md) § "Phase 4" describes an xmldoc sub-formatter: parse the
comment as XML, re-wrap text to `xmldoc_max_line_length = 120`, break before
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
`resharper_space_after_triple_slash` was **demoted** from Tier A — milestone 1 inserted the space,
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
`linebreak_before_elements` and `max_blank_lines_between_tags` from this repository's own
`.editorconfig`. The negative control — the same element under a name the tool does not know —
changes nothing, which is what makes the positive result mean something. The full probe, its
commands and two incidental ways to get a false "no change" out of `jb cleanupcode` are in
[oracle-cleanup-profile.md](oracle-cleanup-profile.md).

**Skala formats documentation comments by default.** Not formatting them was the divergence.
`skala format --no-xmldoc` is the escape hatch, and it is a flag rather than
`resharper_xmldoc_wrap_lines = false` for two reasons: that key means "do not wrap long lines" and
would still leave the comment re-indented and its marker respaced, and it is not a documented
ReSharper key at all — `wrap_lines` appears nowhere in JetBrains' `.editorconfig` index, for any
language, though the export writes it.

⚠ **`resharper_space_after_triple_slash` is Tier A again.** This paragraph used to end "the key
cannot return to Tier A … That is a fact about the fixtures, and it expires when they are
regenerated." It has expired. `constructs/xmldoc/resharper_space_after_triple_slash.cs` carries the
fixture, generated under `OracleProfile.DocComments`, and Skala reproduces it byte for byte.

⚠ The fixture is narrower than it looks, and the narrowing is a measured shape rather than a
caveat. **The oracle does not rewrite a `///` marker on a comment it is otherwise leaving alone.**
The first cut of that fixture was a single short `///<summary>Docs.</summary>`, which needs no
wrapping, no element split and no blank-line removal — and it came back from
`CSharpFormatDocComments` byte-identical, marker included. The fixture that measures the key is two
crammed elements on one line, which the oracle has to rebuild, and the marker space appears with
the rebuild. So the 79 lines M3 charged to Skala are *still* not fully re-explained: `corpus/real/`'s
fixtures are generated under a profile that rebuilds nothing, and on a comment that needs no other
change the oracle and Skala genuinely differ about the marker.

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

⚠ **Registry state.** `resharper_space_after_triple_slash` is **Tier A** (see above),
`resharper_xmldoc_wrap_lines` is **Tier D** for the measured reason in SK-DIV-0019 rather than for
want of a fixture, `trim_trailing_whitespace` is **Tier D** with
`defaultSource: oracle-probe` — the probe that established it is recorded in the registry entry
itself — and `resharper_remove_spaces_on_blank_lines` is **Tier D**, inert as this entry says.

### The sub-formatter is the default

`XmlDocFormatter` re-wraps documentation comments on every run of `skala format`, `skala arrange`,
the daemon and the MCP server. `--no-xmldoc` is the only thing that turns it off, and the only thing
that still reproduces the pinned oracle profile's answer.

⚠ **RETRACTED, and this is the second retraction in the same entry.** What follows in this section
was written when the corpus had no fixture that could show the oracle formatting a documentation
comment, and it concluded that no fixture ever could. That conclusion was the original mistake at
one level down: a limitation of the *profile*, read as a limitation of the corpus.
`OracleProfile.DocComments` is `OracleProfile.FormatOnly` plus
`<CSharpFormatDocComments>True</CSharpFormatDocComments>` and nothing else, `./build.sh Oracle`
regenerates `constructs/xmldoc/*.xmldoc.expected.cs` under it, and the family is measured. **13 of
the 22 keys are Tier A**, pinned exactly the way every other option in the registry is. The nine
that are not have measured shapes and entries of their own: SK-DIV-0019 through SK-DIV-0023.

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
latter would have to be widened again for `space_before_self_closing` and again for
`spaces_inside_tags`. The signature is *tighter* than a word sequence where it counts: a `<code>`
body is compared byte-for-byte, which it was not before.

**Twenty-one of the thirty-two `resharper_xmldoc_*` keys are honoured** and eleven are refused. Each
of the twenty-one is asserted observable by `AnUnoracledKey_IsObservable`, against a hand-written
probe rather than against `constructs/` — nine of them cannot be seen there, because the constructs
fixtures carry short, already-tidy doc comments written when nothing read them.

⚠ **The counts were 17 of 27 and 10, and the arithmetic moved for two reasons that are both worth
reading.** Four keys were promoted out of the refusal list after being measured; and the family
itself grew by five, because the five processing-instruction keys had been *excluded from the
count* on the grounds that "a processing instruction in a C# documentation comment is not a thing
that occurs". It is — Roslyn parses one, and `blank_line_after_pi` acts on one at its default
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

So `space_after_last_attribute` and `spaces_around_eq_in_attribute` were refused for a reason that
described Skala's implementation — "Skala emits a tag header byte-for-byte" — dressed as a property
of the key. Both are implemented now: the renderer re-emits the header from a name/value split taken
at Roslyn's `EqualsToken`, so an attribute's **value, quote character included, is still the source
bytes** and only the whitespace around the `=` is chosen.

`linebreaks_inside_tags_for_elements_longer_than` was refused because "JetBrains' own reference page
does not say what is measured against it". True, and it did not need to: **the tool says.** The
element's *flat inner content* — its text and its child markup, neither tag — is compared **strictly
greater** than the threshold. At 12, twelve characters stay inline and thirteen do not. ⚠ `0`
therefore means "always", not "never"; `options.json` asserted the opposite in its `boundsBecause`,
and that is corrected.

`blank_line_after_pi` was never refused, considered, or counted.

### The refusals that stand

- `attribute_indent`, `attribute_style`, `alignment_tab_fill_style`, `allow_far_alignment` —
  **pending, not refused.** All four describe a wrapped tag header's continuation line, and Skala
  never breaks inside a header. The oracle does, so the subject exists; the prerequisite is a
  renderer that can wrap a header, and until then the reason is Skala's shape and says nothing about
  the keys.
- `pi_attribute_style`, `pi_attributes_indent`, `space_after_last_pi_attribute`,
  `spaces_around_eq_in_pi_attribute` — pending on the same prerequisite one construct over: a
  processing instruction is emitted verbatim, so its header has no attributes to space out. The
  export leaves `pi_attribute_style` at `do_not_touch`, which is what the oracle was measured doing
  to `<?pi first = "1" second="2" ?>` — it left it alone.
- `wrap_around_elements` — ⚠ **refused, and now for a measured reason.** With the doc-comment task
  enabled, at both values, over prose containing inline `<see/>`, `<c>` and `<b>` elements both long
  enough to wrap and short enough not to, the oracle's output is **byte-identical**. Either it is
  subsumed by `wrap_tags_and_pi` in this build or its subject is a construct no C# doc comment
  produces. The previous two reasons for this key were both guesses about documentation; this one is
  a diff.
- `tab_width` — it only changes how wide a tab is when measuring, and the only tab a re-wrap can
  meet is inside a `<code>` block, which is verbatim and never measured.
- `insert_final_newline` — a `///` comment has no file end to put a newline at, and JetBrains' key
  index does not list XMLDOC among the languages that accept the key at all.

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
choices: `linebreak_before_elements` is read as "this element owns its own line", a break before it
*and* before what follows it, because the strict reading leaves `</param><param …>` sharing a line
with the text after them; and `indent_child_elements = do_not_touch` is mapped to "no indent"
rather than "keep the author's", because under a re-wrap the author's indentation no longer exists
to keep.

⚠ [14](plan/14-web-languages.md) § "Why they are later" describes the sub-formatter as already
existing and makes lifting it out the exercise that proves the `ISkalaLanguage` seam. It exists now
— in `Formatting.CSharp`, as four files that share no state with the document builder — but
`ISkalaLanguage` still does not, and doc 14 still has no correction note.

- options: `resharper_space_after_triple_slash`, `resharper_xmldoc_wrap_lines`, `resharper_xmldoc_max_line_length`, `resharper_xmldoc_linebreak_before_elements`, `trim_trailing_whitespace`
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

- options: `resharper_csharp_wrap_arguments_style` (Tier A, `chop_if_long`), `resharper_keep_user_linebreaks` (Tier A, `true`)
- ⚠ status: **half closed** (the chain, at M3.1), half **open**, measured

## SK-DIV-0008 — ⚠ half closed: statement conditions are aligned, four other keys are not

`int_align` and all eight `int_align_*` sub-keys are `false`, and so are `align_multiline_argument`,
`…_parameter`, `…_calls_chain`, `…_expression` and `align_multiline_binary_expressions_chain`.

⚠ **Milestone 3 said four keys survive and there are nine**, which is the first correction:
`align_multiline_statement_conditions`, `align_multiline_type_argument`,
`align_multiline_type_parameter`, `align_multiline_ctor_init`, `align_multiline_array_initializer`,
`align_multiline_implements_list`, `align_multiline_comments`, `align_first_arg_by_paren`'s
companion `align_ternary = align_not_nested`, and `int_align_fix_in_adjacent`.

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

⚠ **Current, at `8cbd66d`:** `resharper_csharp_align_multiline_statement_conditions` is **Tier A**,
so the half that was built stays built. `align_multiline_for_stmt`, `align_multiline_array_initializer`,
`align_multiline_type_argument` and `align_multiline_ctor_init` are all still **Tier D**. The `for`
header's residue is **5 hunks over 13 lines across 3 files** — signature: a hunk mentioning a
`for (` header — up from the 4 lines across 2 files recorded at M3.1, and the increase is the
signature being broader rather than the formatter regressing: the whole of `corpus/real`'s
`ForStatement` attribution is 2 lines ([16](plan/16-risks-and-open-questions.md) § R1's table), so
three of the three files are the same generated `DataSet` and its neighbours. The other three keys
are still worth 0 lines: the corpus contains no instance of their shapes.

⚠ **The `for` row's attribution was wrong, and milestone 3.2 measured it.** The residue was never
`align_multiline_for_stmt`'s: that key is **masked** by `align_multiline_statement_conditions = true`,
which the export sets, and both of its values return the same file byte for byte. What was missing
was a **break point at the header's `;`** — Skala broke inside the incrementor expression instead,
producing `i +=\n 1` where the oracle chops the clauses. `wrap_for_stmt_header_style` is the key that
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

- options: `resharper_csharp_align_multiline_statement_conditions` (now Tier A), `resharper_csharp_align_multiline_for_stmt`, `resharper_align_multiline_array_initializer`, `resharper_align_multiline_type_argument`, `resharper_align_multiline_ctor_init`
- ⚠ status: **half closed** (statement conditions, at M3.1), four keys still **open**, measured

## SK-DIV-0009 — `space_within_spread_pattern` is inert, and the gap it names is not governed at all

The export sets `resharper_space_within_spread_pattern = true` and Skala honoured it, putting a
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
back `.. var r`, a space the oracle inserts, because `space_within_slice_pattern = true` really does
govern its own construct and stays Tier A. Reading `space_within_spread_pattern` as the
collection-expression twin of it — which is what its name says — put a space Skala had no evidence
for into 58 lines of `corpus/real/`.

`resharper_space_within_spread_pattern` is demoted from Tier A to Tier D and its fixture withdrawn,
for the same reason `trim_trailing_whitespace` is Tier D under SK-DIV-0006: an option Skala honours
and Rider ignores is a divergence wearing a tier badge.

⚠ **Current, at `8cbd66d`: the demotion holds and the entry is resolved.**
`resharper_space_within_spread_pattern` is **Tier D**, `defaultSource: unknown`, with no fixture —
which is the withdrawal this entry describes, verified in the registry rather than remembered. Its
twin `resharper_csharp_space_within_slice_pattern` is **Tier A**, which is the other half of the
argument. `resharper_remove_spaces_on_blank_lines` remains Tier D and inert. The 58 lines this cost
`corpus/real/` are gone: sweeping all 380 files' divergent hunks for a `..` inside brackets returns
two lines in one file, and both are a *range* expression (`line[TerminatorPrefix.Length..]`) whose
divergence is SK-DIV-0008's indentation, not a spread element's spacing.

- options: `resharper_space_within_spread_pattern` (Tier D), `resharper_csharp_space_within_slice_pattern` (Tier A)
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
`wrap_before_first_method_call = false`, which is the key that says the first dot stays with its
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

- options: `resharper_csharp_wrap_before_first_method_call` (Tier A), `resharper_csharp_wrap_multiple_declaration_style`
- ⚠ status: **open and deliberate** — a decision not to implement, measured

## SK-DIV-0011 — a lambda's expression body may leave the arrow's line, and the discriminator is unknown

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

- options: `resharper_place_single_method_argument_lambda_on_same_line` (Tier A, `true`, `oracle-probe`)
- ⚠ status: **open**, measured. The discriminator is still unknown; this is not tail work either

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
   statements do not. `keep_existing_embedded_block_arrangement` governs it — flipped to `true`, the
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

`resharper_csharp_keep_existing_embedded_block_arrangement` is **Tier A** (`false`, `oracle-probe`),
so item 2's governing key is implemented; what is missing is the placement decision it interacts
with. `resharper_csharp_align_multiline_for_stmt` is **Tier D**.

⚠ **Item 3 is closed at milestone 3.2, and it was filed against the wrong key.** It is not
`align_multiline_for_stmt` — that key is masked by `align_multiline_statement_conditions = true` and
returns the same file at either value. Skala had **no break point at the header's `;`** and broke
inside the incrementor expression instead. `resharper_csharp_wrap_for_stmt_header_style` is the key
that governs it, it is now implemented and **Tier A**, and the signature is down from 5 hunks over 13
lines across 3 files to **2 hunks in 1 file** — neither of which is a `for` rule any more: the
initializer's own `=` continuation lands on the align column where the oracle puts it one level
further in, unchanged by that branch and owned by `PlanAroundEquals`.

- options: `resharper_csharp_keep_existing_embedded_block_arrangement` (Tier A), `resharper_csharp_wrap_for_stmt_header_style` (Tier A, item 3, at M3.2), `resharper_csharp_align_multiline_for_stmt` (Tier D, and never the cause of item 3)
- ⚠ status: **items 1 and 2 open**, item 3 **closed** at M3.2, all three measured

## SK-DIV-0013 — three rewrites the export configures and the oracle will not perform

`resharper_null_checking_pattern_style = not_null_pattern`, `resharper_empty_string = empty_literal`
and `resharper_braces_redundant = true` are all set in the export, all listed in
[06](plan/06-arrangement-and-syntax-styles.md), and `jb cleanupcode` 2025.2.6 performs **none** of
them. Swept as elements and as `CSCodeStyleAttributes` attributes, with the corresponding
`resharper_arrange_*_highlighting` keys left at their exported severities and raised to `warning`:
`if (p != null)` stays, `string.Empty` becomes `string.Empty` and stops, and `{ { x; } }` keeps both
pairs. The sweep is `docs/oracle-cleanup-profile.md`.

The reading that fits is that `null_checking_pattern_style` and `empty_string` govern the pattern
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

- options: `resharper_csharp_null_checking_pattern_style`, `resharper_empty_string`, `resharper_csharp_braces_redundant`

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

- options: `dotnet_style_parentheses_in_arithmetic_binary_operators`, `dotnet_style_parentheses_in_other_binary_operators`, `resharper_parentheses_redundancy_style`

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

The rule behind it is not established. `resharper_blank_lines_around_type = 1` would explain the
oracle's answer if the leading comment counts as the preceding *member*, but
`resharper_stick_comment = true` says a comment binds to what follows it, which would make the blank
line belong *above* the comment instead — and there is nothing above it. Whether the oracle special-cases
a file-scope comment block, or treats the compilation unit's start as a member boundary, wants a sweep
of its own before anything is implemented.

Measured: 1 line, 1 file of `constructs/` (324 files). Not observed in `corpus/real/`.

- options: `resharper_blank_lines_around_type`, `resharper_stick_comment`,
  `resharper_blank_lines_before_single_line_comment`
- ⚠ status: **open**, pre-existing, exposed at the M9 merge

## SK-DIV-0016 — the oracle's cleanup profile ignores `@formatter:off`; Skala honours it everywhere

`resharper_formatter_tags_enabled = true`, `resharper_formatter_off_tag = @formatter:off`,
`resharper_formatter_on_tag = @formatter:on` and `resharper_formatter_tags_accept_regexp = false` are
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

- options: `resharper_formatter_tags_enabled`, `resharper_formatter_off_tag`,
  `resharper_formatter_on_tag`, `resharper_formatter_tags_accept_regexp`
- ⚠ status: **permanent**. There is no configuration under which Skala should arrange inside the tags.

## SK-DIV-0017 — a comment that *mentions* the tag is prose; the oracle says it is a directive

`resharper_formatter_tags_accept_regexp = false` makes the match literal. The oracle reads "literal"
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

- options: `resharper_formatter_tags_enabled`, `resharper_formatter_off_tag`,
  `resharper_formatter_on_tag`, `resharper_formatter_tags_accept_regexp`
- ⚠ status: **permanent**, pinned by `Formatting.CSharp.Tests/FormatterTagTests` rather than by a
  corpus fixture — a fixture recording the oracle's answer here would lower the format-fidelity
  ratchet to pin a divergence that costs nothing on any real file.

## SK-DIV-0018 — on a file with mixed line endings the oracle normalises; Skala keeps each gap's own

`resharper_enforce_line_ending_style = false` means an existing line ending is kept rather than
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

- options: `resharper_enforce_line_ending_style`, `end_of_line`, `insert_final_newline`
- ⚠ status: **permanent**, pinned by the fixture above.

## SK-DIV-0019 — the oracle keeps the word that crosses `max_line_length`; Skala breaks before it

The first divergence the doc-comment oracle profile made visible, and the largest: **five** of the
nine keys that stayed Tier D are this one disagreement wearing five names, because every one of
them is measured on a comment that has to wrap.

Skala fills a documentation line while the *whole line* — code indent, `///`, marker space, content
indent and text — stays within `resharper_xmldoc_max_line_length`. The oracle fills while the line
is **strictly under** the limit and then keeps the word that crosses it, breaking after it. So the
oracle's lines routinely run one word past the margin and Skala's never do:

```
                                                                                          120 ↓
oracle   ///     A summary written … inside the configured column limit, so that the       (122)
skala    ///     A summary written … inside the configured column limit, so that           (118)
```

`constructs/xmldoc/resharper_xmldoc_max_line_length.xmldoc.expected.cs` carries 122 columns,
`…_wrap_lines` 122, `…_wrap_text` 122, `…_wrap_tags_and_pi` 121 and
`…_linebreaks_inside_tags_for_elements_longer_than` 122, all under `max_line_length = 120`. The
model was checked against every wrapping fixture in the subtree: greedy fill, break *after* the
crossing word, and the crossing word admitted only while the line before it was under 120.

⚠ **One part of the shape is measured and not explained.** Probed at three nesting depths with a
`<summary>` of sixty identical five-letter words, the oracle produced content lines of 125, 129 and
133 columns — a constant 113 columns of *content*, which is `120 − 3 − 4` — but the **first**
content line of each was one word shorter than the rest, at every depth. The greedy model fits every
later line and misses that one. The first-line reservation is unexplained; it is recorded here
rather than guessed at.

Skala's reading is not obviously the wrong one — a hard wrap exists to keep lines inside a margin,
and a wrap that overshoots by a word is a wrap that did not do its job — but it is a disagreement
either way, and until it is settled these five keys cannot claim to reproduce Rider.

- options: `resharper_xmldoc_max_line_length`, `resharper_xmldoc_wrap_lines`,
  `resharper_xmldoc_wrap_text`, `resharper_xmldoc_wrap_tags_and_pi`,
  `resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than`
- ⚠ status: **open**, pinned by the five fixtures above and by
  `Conformance.Tests/XmlDocOracleTests`, which asserts that each of them still fails.

## SK-DIV-0020 — the oracle opens an element that holds text *and* children; Skala opens one that holds only children

`resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements = true` puts an element's
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
elements". Fixing it is a change to `XmlDocRenderer`'s notion of mixed content and is not attempted
here.

- options: `resharper_xmldoc_linebreak_before_singleline_elements`
- ⚠ status: **open**, pinned by
  `constructs/xmldoc/resharper_xmldoc_linebreak_before_singleline_elements.xmldoc.expected.cs`.

## SK-DIV-0021 — the oracle leaves an unlisted element's content on one line however long; Skala wraps it

The mirror of SK-DIV-0020, on the same construct with a longer item, and it goes the other way.
`resharper_xmldoc_linebreak_before_elements` names eight elements — `summary`, `remarks`, `example`,
`returns`, `param`, `typeparam`, `value`, `para` — and `item` is not one of them. Asked to format a
`<list>` whose single `<item>` runs to 131 columns, the oracle **leaves it at 131 columns**:

```
oracle   ///         <item>An item written at enough length that … cannot fit on any single line of its own.</item>
skala    ///         <item>
         ///             An item written at enough length that … cannot fit on any single line of
         ///             its own.
         ///         </item>
```

⚠ The reading this suggests — that the oracle wraps *inside* an element only when
`linebreak_before_elements` names it — is consistent with every fixture in the subtree, `<summary>`
included, but it is a reading of five files rather than a probe of the rule, and it is written down
as such. Note that it also overshoots the margin, which is SK-DIV-0019 again; the two are separable
because this line is 11 columns over and no single word explains that.

- options: `resharper_xmldoc_linebreak_before_multiline_elements`
- ⚠ status: **open**, pinned by
  `constructs/xmldoc/resharper_xmldoc_linebreak_before_multiline_elements.xmldoc.expected.cs`.

## SK-DIV-0022 — `spaces_inside_tags = false` means "do not add one", not "remove the author's"

Skala reads `resharper_xmldoc_spaces_inside_tags` as a statement about the output: false means the
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

- options: `resharper_xmldoc_spaces_inside_tags`
- ⚠ status: **open**, pinned by
  `constructs/xmldoc/resharper_xmldoc_spaces_inside_tags.xmldoc.expected.cs`.

## SK-DIV-0023 — a processing instruction's line keeps its marker unspaced, and the blank line after it differs

Two shapes on one construct, both visible in one fixture.

```
oracle   /// <?skala-probe mode="short"?>
         /// ␠                                    ← marker, space, nothing
         /// <summary>A summary that follows a processing instruction.</summary>

skala    ///<?skala-probe mode="short"?>          ← no marker space
         ///                                      ← no trailing space
         /// <summary>A summary that follows a processing instruction.</summary>
```

1. **The marker space is not applied to a processing-instruction line.** Skala emits a processing
   instruction verbatim — that is the refusal reason `resharper_xmldoc_pi_attribute_style` and its
   three siblings carry — and "verbatim" has swallowed the `///` marker along with the instruction.
   That is a defect rather than a decision: `resharper_space_after_triple_slash` is Tier A on every
   other line of the same comment.
2. **The blank line the oracle writes after a processing instruction carries a trailing space.**
   Skala's carries none, deliberately and for a reason that is stated at `XmlDocOptions.BlankLineAfterPi`
   and holds elsewhere: an empty line's trailing whitespace is the one thing every other pass in
   Skala strips. Unlike (1) this half is a decision, and it would survive (1) being fixed.

- options: `resharper_xmldoc_blank_line_after_pi`, `resharper_space_after_triple_slash`
- ⚠ status: **open**, pinned by
  `constructs/xmldoc/resharper_xmldoc_blank_line_after_pi.xmldoc.expected.cs`. ⚠ The entry names
  `resharper_space_after_triple_slash`, which is **Tier A**: the key is reproduced everywhere its
  fixture exercises it, and this is a construct that fixture does not reach.
## SK-DIV-0024 — a type parameter list wraps when the list overflows, not when the declaration does

T5a gave a type parameter list its first break points: at `wrap_before_type_parameter_langle = false`
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

- options: `resharper_csharp_wrap_before_type_parameter_langle`, `resharper_align_multiline_type_parameter_list`, `resharper_csharp_wrap_parameters_style`
- ⚠ status: **open**, measured; the first half is the ordering rule's and belongs with SK-DIV-0002.
