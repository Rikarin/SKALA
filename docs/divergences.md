# Divergences from the oracle

`jb cleanupcode` is the conformance oracle (ADR-011), not a master. Where Skala deliberately
differs, the difference gets an `SK-DIV-` number and the argument for it lives here. The count is
published alongside the fidelity number, because **a divergence is a decision and an unexplained
difference is a bug, and the harness cannot tell them apart without this file**
([12](plan/12-conformance-and-testing.md) § "Where the oracle is wrong").

Format: `## SK-DIV-nnnn — one line`, then the argument, then the option keys it touches.

At milestone 3, `corpus/real/` is **98.86 %** of lines identical to the oracle over 380 files and
76 660 lines — 876 lines that differ. Split by cause, which is the shape that says what is left:

| Files | Line fidelity | File fidelity | What the residue is |
|---|---:|---:|---|
| containing a `#if` (91) | 98.60 % | 62.64 % | SK-DIV-0001 and SK-DIV-0004 |
| containing a raw literal (15) | 97.81 % | 53.33 % | SK-DIV-0003's remaining half |
| neither (274) | 99.02 % | 74.09 % | SK-DIV-0005 and SK-DIV-0007, mostly |

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

Measured on `corpus/real/`: 141 lines across 73 files at M2, and of the same order at M3. Almost all
of it is one blank line per `#if` region in Newtonsoft.Json, whose files are largely wrapped in
`#if HAVE_BENCHMARKS`.

- options: `resharper_csharp_keep_blank_lines_in_code`, `resharper_csharp_keep_blank_lines_in_declarations`

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

- options: `resharper_csharp_wrap_arguments_style`, `resharper_csharp_wrap_parameters_style`, `resharper_csharp_wrap_chained_method_calls`

## SK-DIV-0003 — an interpolated raw string literal is still emitted verbatim

`resharper_csharp_indent_raw_literal_string = align` asks the formatter to move the closing
delimiter and the content of a `"""` literal, and milestones 1 and 2 declined on the grounds that
the transformation changes the string's value if it is done wrong.

Milestone 3 does it, for the uninterpolated case, because there is a form of it that *cannot* be
done wrong: C# strips the closing delimiter's own whitespace prefix from every line, so a **uniform
shift** — every interior line and the closing delimiter by the same number of columns — leaves the
stripped result identical, character for character. The token-equivalence check would abort the file
if that were untrue.

⚠ What remains is the **interpolated** literal. `$"""…{x}…"""` is not one token but a run of them
with expressions between, and it stays on the verbatim path [04](plan/04-formatting-engine.md) puts
it on — "where a moved space changes the value". The option is Tier A on the strength of
`constructs/trivia/resharper_csharp_indent_raw_literal_string.cs`, and this entry is what its Tier B
caveat in doc 04 was pointing at.

Measured on `corpus/real/`: files containing a raw literal went from 94.41 % to 97.81 % of lines,
and the 102 that remain are the interpolated ones and the alignment of what surrounds them.

- options: `resharper_csharp_indent_raw_literal_string`

## SK-DIV-0004 — `skala format` has no preprocessor symbols, so `#if DEBUG` bodies are frozen

The oracle runs `cleanupcode` against a project, so `DEBUG`, `TRACE` and the target framework's
symbols are defined and it formats the inside of a `#if DEBUG` block. `skala format` parses a file
with no project and no symbols, so Roslyn hands that block back as `DisabledTextTrivia` and Skala
leaves it byte-for-byte.

⚠ This is a real limitation and not only a measurement artefact: on a tree with much conditional
code, the conditional half is not formatted at all. It is safe — nothing is mangled — but it is not
what a user would expect, and it is the strongest argument for `skala check`'s project loading
([07](plan/07-analysis-host.md)) reaching `format` as well. Milestone 5 supplies the compilation;
until then a repository that cares can pass the symbols explicitly once `--define` exists.

Measured at M3: the 91 files of `corpus/real/` that contain a `#if` are at 98.60 % of lines against
99.02 % for the 274 that do not, and 62.64 % of files against 74.09 %. Whole files are affected
rather than lines: `Issue2504.cs` is wrapped in `#if (NET45 || NET5_0_OR_GREATER)`, so for Skala the
entire body is disabled text and the file is reproduced unchanged.

- options: none

## SK-DIV-0005 — the ordering rule's margin is an empirical constant, not a derivation

Milestone 3's ordering rule (`GroupFacts.PrefersOuterBreak`) decides which of a long line's
candidate points is wrapped at. Its first question is "does this break alone finish the job", and
the budget that question is asked against is **not** `max_line_length`.

Sweeping one shape a character at a time through the oracle at three nesting depths gives a clean
threshold each time, and in every case the oracle stops taking the `=` break well before the
continuation line reaches 120:

| block depth | continuation column | longest continuation line the oracle still writes |
|---:|---:|---:|
| 2 | 12 | 109 |
| 3 | 16 | 108 |
| 4 | 20 | 107 |

So the budget for this one decision is `120 − (8 + column / indent)`. What ReSharper is really
computing there is not known — it is not a width test on the result, because the result fits with
eleven columns to spare — and the formula above reproduces its answer exactly at all three depths.

⚠ It is an approximation and it has a known counter-example. `byte[] data =
Convert.FromBase64String("…");` at 121 columns comes back from the oracle broken after the `=`, with
the call whole on a 110-column continuation line; the margin declines that break and chops the call
instead. Gating the margin on whether the right-hand side opens with an *expression brace* fixes
that shape and costs 0.15 points of line fidelity and 3 points of file fidelity elsewhere, so it is
not what ships. Measured alternatives, on `corpus/real/`:

| Rule | line | file |
|---|---:|---:|
| margin everywhere (ships) | 98.86 % | 70.53 % |
| margin only where the RHS opens with a brace | 98.71 % | 67.37 % |
| no margin at all | 98.42 % | — |

- options: `resharper_prefer_wrap_around_eq`, `resharper_csharp_wrap_before_eq`

## SK-DIV-0006 — `jb cleanupcode` does not format documentation comments, so neither does Skala

[05](plan/05-csharp-formatting-rules.md) § "Phase 4" describes an xmldoc sub-formatter: parse the
comment as XML, re-wrap text to `xmldoc_max_line_length = 120`, break before
`summary,remarks,example,returns,param,typeparam,value,para`. It is not implemented, and the reason
is a measurement.

Asked directly, with the export's whole `resharper_xmldoc_*` family in force, the oracle returns
every one of these exactly as written:

```csharp
///<summary>No space after the marker.</summary>
/// <summary>A summary line 128 columns wide …</summary>
/// <param name="x">…</param><param name="y">…</param>
/// <summary>Text</summary><remarks>…</remarks>
```

A Skala that re-wrapped them would diverge from the oracle on every doc comment in the corpus, and
would have no oracle to check itself against while doing it, which is how a formatter acquires
behaviour nobody asked for. The twelve `resharper_xmldoc_*` keys stay Tier D with this as the
reason, and `resharper_space_after_triple_slash` was **demoted** from Tier A: milestone 1 inserted
the space, the oracle does not, and it was worth 79 lines across 15 files of `corpus/real/`.

What is implemented is the half [05](plan/05-csharp-formatting-rules.md) calls the hazard and that
needs no oracle: a doc comment that is not well-formed XML is left exactly as it is and reported at
`hint` (`SK0003`), never "fixed".

- options: `resharper_space_after_triple_slash`, `resharper_xmldoc_wrap_lines`, `resharper_xmldoc_max_line_length`, `resharper_xmldoc_linebreak_before_elements`

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

- options: `resharper_csharp_wrap_arguments_style`, `resharper_keep_user_linebreaks`

## SK-DIV-0008 — column alignment is not implemented, and four keys in the export ask for it

`int_align` and all eight `int_align_*` sub-keys are `false`, and so are `align_multiline_argument`,
`…_parameter`, `…_calls_chain`, `…_expression` and `align_multiline_binary_expressions_chain`. Four
survive: `align_multiline_type_argument`, `align_multiline_type_parameter`,
`align_multiline_ctor_init` and `align_multiline_array_initializer`, all `true`.

Skala implements none of them, and the `Align` IR node [04](plan/04-formatting-engine.md) reserves
is still unused. Two shapes in `corpus/real/` show it:

```csharp
for (int i = 0;
     i < n && i < 100;      ← aligned to the `(`, not indented one level
     i++) { }

var directions = new[] {
                     new Vector3(…), new Vector3(…),   ← aligned to `new[]`
                 };
```

The consequence [05](plan/05-csharp-formatting-rules.md) § "Alignment" claims — "with column
alignment off, laying out line *n* never requires knowing the contents of line *n−1*" — remains true
of the hot path and is why the fitting pass is linear. The four keys that are on are the exception,
they are rare in the corpus, and they are Tier D with this entry as the reason.

- options: `resharper_csharp_align_multiline_for_stmt`, `resharper_align_multiline_array_initializer`, `resharper_align_multiline_type_argument`, `resharper_align_multiline_ctor_init`
