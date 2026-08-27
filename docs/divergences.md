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
| 0006 | the oracle does not format doc comments | open, deliberate | four keys confirmed Tier D; no sub-formatter in the tree |
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

⚠ **`resharper_space_after_triple_slash` stays demoted, and its reason is gone.** The 79 lines it
cost were `jb cleanupcode` under a profile that declines to insert the space, charged to Skala. The
space is inserted again. The key cannot return to Tier A, but not for the old reason: Tier A means
"pinned by an oracle fixture" and the fixtures were all generated under the profile that does not
move. That is a fact about the fixtures, and it expires when they are regenerated.

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

⚠ **Registry state.** `resharper_space_after_triple_slash` is **Tier D**,
`resharper_xmldoc_wrap_lines` is **Tier D**, `trim_trailing_whitespace` is **Tier D** with
`defaultSource: oracle-probe` — the probe that established it is recorded in the registry entry
itself — and `resharper_remove_spaces_on_blank_lines` is **Tier D**, inert as this entry says.

### The sub-formatter is the default

`XmlDocFormatter` re-wraps documentation comments on every run of `skala format`, `skala arrange`,
the daemon and the MCP server. `--no-xmldoc` is the only thing that turns it off, and the only thing
that still reproduces the pinned oracle profile's answer.

⚠ **These keys are pinned differently from every other formatter option in the project, and the
difference is stated rather than hidden.** Tier A means "Skala reproduces Rider's behaviour, pinned
by at least one oracle fixture", and no committed fixture shows Rider doing any of this — because
every one of them was generated under a profile that switches it off. So every id the sub-formatter
reads is registered through `Ids.OfUnoracled`: read, honoured, never entering
`PhaseOneOptions.Implemented`, never claiming Tier A.

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

**Seventeen of the twenty-seven `resharper_xmldoc_*` keys are honoured** and ten are refused. Each
of the seventeen is asserted observable by `AnUnoracledKey_IsObservable`, against a hand-written
probe rather than against `constructs/` — nine of them cannot be seen there, because the constructs
fixtures carry short, already-tidy doc comments written when nothing read them. The refusals are
reasons, not a backlog, and each one is in `XmlDocIds.Refused`:

- `attribute_indent`, `attribute_style`, `space_after_last_attribute`, `spaces_around_eq_in_attribute`,
  `alignment_tab_fill_style`, `allow_far_alignment` — **Skala emits a tag header byte-for-byte and
  never breaks inside one.** One rule settles all six. A `cref=` or `name=` is read by the compiler
  and by the doc build, and Skala will not edit inside one for a whitespace preference; nothing is
  ever wrapped inside a header, so nothing is ever aligned or indented there either. ⚠ These six
  are unaffected by the profile finding: they were never refused for want of an oracle.
- `linebreaks_inside_tags_for_elements_longer_than` — the export sets `int.MaxValue`, "never", and
  **JetBrains' own reference page does not say what is measured against it.** The UI label is "when
  element is longer than" and the value is documented only as "an integer"; nothing states whether
  the threshold counts characters, columns or lines, or whether the element's tags are included.
  ⚠ The reason has *changed*: it used to be "a threshold never crossed cannot be pinned by a
  fixture", which is no longer a reason for anything here. What refuses it now is that the semantics
  are undocumented, which no amount of oracle access fixes.
- `wrap_around_elements` — ⚠ **the old reason was wrong and is withdrawn.** It said the key is
  "indistinguishable from `wrap_tags_and_pi` without an oracle". JetBrains documents them
  distinctly and in different sections: `wrap_tags_and_pi` is "Wrap tags and processing
  instructions" under *Line wrapping*, `wrap_around_elements` is "Wrap before and after elements"
  under *Tag content*. The refusal now rests on something narrower and true — the docs describe each
  separately and never describe how the two interact, so Skala honours the one whose scope it can
  state and refuses the one whose scope only exists relative to it. **This is the one refusal that
  is now a backlog item rather than a reason.**
- `tab_width` — it only changes how wide a tab is when measuring, and the only tab a re-wrap can
  meet is inside a `<code>` block, which is verbatim and never measured.
- `insert_final_newline` — a `///` comment has no file end to put a newline at, and JetBrains' key
  index does not list XMLDOC among the languages that accept the key at all.

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

- options: `resharper_csharp_keep_existing_embedded_block_arrangement` (Tier A), `resharper_csharp_align_multiline_for_stmt` (Tier D)
- ⚠ status: **open**, all three measured

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

## SK-DIV-0014 — parenthesis removal is gated behind `--aggressive`, and the gate costs 4.02 points

The oracle's cleanup profile removes redundant arithmetic parentheses
(`dotnet_style_parentheses_in_arithmetic_binary_operators = never_if_unnecessary`), and Skala's
default does not. [06](plan/06-arrangement-and-syntax-styles.md) § "Qualification and redundancy"
asks for exactly this: "Parenthesis removal is the highest-risk rewrite in the whole tool […] Skala
gates parenthesis removal behind `arrange --aggressive` for the first release regardless, and
revisits when the corpus differential shows zero divergences."

Measured rather than assumed, over `corpus/real/` plus `constructs/arrangement/`, 391 files:

| | changed spans agreed |
|---|---|
| default (gated) | **77.61 %** (2 506 / 3 229) |
| `--aggressive` | **81.63 %** (2 635 / 3 228) |

So the gate is worth 4.02 points of agreement, and that is the price of the caution rather than a
hidden disagreement. The condition for revisiting is in the doc and is not yet met: `--aggressive`
is not at zero divergences either.

- options: `dotnet_style_parentheses_in_arithmetic_binary_operators`, `dotnet_style_parentheses_in_other_binary_operators`

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
