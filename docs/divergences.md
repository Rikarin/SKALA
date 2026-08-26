# Divergences from the oracle

`jb cleanupcode` is the conformance oracle (ADR-011), not a master. Where Skala deliberately
differs, the difference gets an `SK-DIV-` number and the argument for it lives here. The count is
published alongside the fidelity number, because **a divergence is a decision and an unexplained
difference is a bug, and the harness cannot tell them apart without this file**
([12](plan/12-conformance-and-testing.md) § "Where the oracle is wrong").

Format: `## SK-DIV-nnnn — one line`, then the argument, then the option keys it touches.

---

## SK-DIV-0001 — the oracle rewrites whitespace inside disabled `#if` text; Skala never touches it

ReSharper edits blank lines inside an inactive preprocessor branch. On
`corpus/real/newtonsoft/…/DeserializeComparisonBenchmarks.cs` it deletes the blank line between
`#if HAVE_BENCHMARKS` and the first disabled `using`, and it will reindent disabled lines given the
chance.

Skala does not. Roslyn hands the inactive branch back as `DisabledTextTrivia`, an unstructured
string; Skala emits it byte-for-byte and copies every gap that touches it verbatim
([04](plan/04-formatting-engine.md) § "Trivia"). The reason is not fastidiousness — it is that the
disabled branch is the branch nobody compiled, so nobody would notice if it were mangled, which is
exactly the property that makes mangling it unacceptable. A formatter that edits code it cannot
parse is a formatter that will eventually edit it wrongly.

Measured cost on `corpus/real/`: 141 lines across 73 files, 0.18 % of the total. Almost all of it is
one blank line per `#if` region in Newtonsoft.Json, whose files are largely wrapped in
`#if HAVE_BENCHMARKS`.

- options: `resharper_csharp_keep_blank_lines_in_code`, `resharper_csharp_keep_blank_lines_in_declarations`

## SK-DIV-0002 — Skala leaves a line long rather than guessing at a wrap it has not implemented

Milestone 2 fits the constructs whose break *points* it knows — argument lists, parameter lists,
enum bodies, switch expressions, expression-bodied members — so a call that does not fit is now
chopped. What it does not do is choose *which* of a long line's several candidate points to wrap at,
which is `prefer_wrap_around_eq`, `wrap_chained_method_calls` and the rest of the ordering, and is
milestone 3's. Where Skala has no ordering it leaves the line whole rather than breaking at the first
point it can reach: measured, breaking at the first available point costs 0.24 points of line
fidelity against leaving it alone, because the oracle's break lands one line away.

It is listed here so that the differential report's largest classes are *known* ones and do not have
to be re-diagnosed on every run.

Measured cost on `corpus/real/`:

| Milestone | lines | files | % of corpus |
|---|---:|---:|---:|
| M1 | 3 180 | 205 | 4.1 % |
| M2 | 747 | 175 | 0.97 % |

- options: `resharper_csharp_wrap_arguments_style`, `resharper_csharp_wrap_parameters_style`, `resharper_csharp_wrap_chained_method_calls`

## SK-DIV-0003 — the oracle re-indents the inside of a raw string literal; Skala does not

`resharper_csharp_indent_raw_literal_string = align` asks the formatter to move the closing
delimiter and the common prefix of a `"""` literal. That transformation changes the string's value
if it is done wrong — the indentation of a raw literal is part of what it evaluates to — and
docs/plan/04 already puts it at Tier B "until the fixtures cover interpolated raw strings with
nested braces".

Milestones 1 and 2 emit raw string literals verbatim. The option is Tier D, not Tier B, because Tier
B means "implemented with a documented divergence" and this is not implemented at all.

Measured cost: `corpus/pathological/raw-string-containing-braces.cs` (50 % line fidelity),
`interpolated-raw-string-with-nested-braces.cs` (14 %), and 79 lines across 4 files in
`corpus/real/`. It is the largest single-construct gap left that is not wrapping.

- options: `resharper_csharp_indent_raw_literal_string`

## SK-DIV-0004 — `skala format` has no preprocessor symbols, so `#if DEBUG` bodies are frozen

The oracle runs `cleanupcode` against a project, so `DEBUG` and `TRACE` are defined and it formats
the inside of a `#if DEBUG` block. `skala format` parses a file with no project and no symbols, so
Roslyn hands that block back as `DisabledTextTrivia` and Skala leaves it byte-for-byte.

⚠ This is a real limitation and not only a measurement artefact: on a tree with much conditional
code, the conditional half is not formatted at all. It is safe — nothing is mangled — but it is not
what a user would expect, and it is the strongest argument for `skala check`'s project loading
([07](plan/07-analysis-host.md)) reaching `format` as well. Milestone 5 supplies the compilation;
until then a repository that cares can pass the symbols explicitly once `--define` exists.

- options: none
