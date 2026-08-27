# Open defects

Fuzz findings that are **minimised, reproduced through the shipping CLI, and not yet fixed**.

docs/plan/12 § "Corpus expansion": *"Any crash, non-idempotent case or token-equivalence failure is
minimised (delta-debugging on the input) and committed to `corpus/pathological/` with the bug
reference. The corpus only grows."* This directory is where such a case lives **before** the defect
it pins is fixed.

⚠ **It is excluded from `Corpus.Files()`, and the exclusion is the point rather than a dodge.** A
file that makes `skala format` throw does not fail one assertion — it poisons every harness path that
formats the corpus, `fidelity` and the differential report included, and it takes the measurement
down with it. So none of these is in the measured sets. What they are instead is
`OpenDefectTests`, which asserts of every entry below that it **still fails, in the way recorded
here**. That is a stronger obligation than a comment in a bug tracker:

- a defect that is fixed makes this suite fail, with "SK-FUZZ-000n now passes; move its file into
  `pathological/`, run `./build.sh Oracle --only=…`, and delete its entry";
- a defect that changes shape makes it fail too, because the property recorded is the property
  asserted;
- and nothing here can be quietly forgotten, because the file is a test rather than a note.

⚠ Each entry's `.cs` file is **byte-significant**. Several are about a trailing space, a missing
final newline, a lone `\r` or the width of one gap, so an editor that tidies on save destroys the
case. `.gitattributes` marks the corpus `-text` for the same reason. There are no `.expected.cs`
fixtures here: an oracle fixture is a measurement, and a file the tool cannot process has nothing to
measure.

⚠ A `whitespace-absorption` entry carries **two** files, because the property is a statement about a
pair: `<name>.cs` is the mutated input and `<name>.baseline.cs` is what it was mutated from, and the
two must differ only in whitespace. The test formats both and asserts the outputs still differ.

## SK-FUZZ-0002 — a `///` comment that starts on the brace line loses its continuation lines

- file: `doc-comment-starting-on-the-brace-line.cs`
- property: `token-equivalence`
- seed: `517720466767192565`
- found: mutating `real/vixen/Core/Vixen.Platform/Windowing/IGlContext.cs` with `widen-identifier`,
  `join-line`, `widen-identifier`, `remove-blank-line`; minimised from 6 620 characters to 156, and
  narrowed by hand to 79.

```
interface I { /// <summary>x</summary>
  /// <remarks>y</remarks>
  int M();
}
```

`skala format` reports **SK9099** and refuses to write:

```
error SK9099: not written, the formatted output has a different token stream (at token 6:
'C:///<summary>x</summary>\n///<remarks>y</remarks>' became 'C:///<summary>x</summary>')
```

The second `///` line is dropped. ⚠ The safety net does its job — nothing is written and no comment
is lost on disk — but the file cannot be formatted at all, which for a formatter is a full outage on
that file. The trigger is only the position: a `///` run whose first line begins on the same line as
the `{`. Move the run onto its own line and the file formats correctly. The malformed XML in the
fuzzer's own reduction was incidental; the hand-narrowed case above is well-formed.

## SK-FUZZ-0003 — mixed line endings need two passes to converge

- file: `mixed-line-endings-after-a-trailing-comment.cs`
- property: `idempotency`
- seed: `1642622263352775298`
- found: mutating `constructs/file/resharper_enforce_line_ending_style.cs` with `comment-line`,
  `trailing-comment`, `trailing-space`; minimised from 149 characters to 22.

`class C { // fuzz\r\n} \r` — a trailing comment, a CRLF, and a final lone CR.
`format(format(x)) ≠ format(x)`: the second pass inserts one `\r`, the third is stable. Reproduced
through the CLI byte for byte, on a second, independently found 36-character case
(`"  \nusing System;\r\nusing System.Linq;\n"`, seed `8809199335211412045`, generative half):

```
input   using System;<CR><LF>using System.Linq;<LF>   (after the leading blank line is dropped)
pass 1  using System;<CR><LF>using System.Linq;<LF>
pass 2  using System;<CR><LF>using System.Linq;<CR><LF>
pass 3  unchanged
```

⚠ `enforce_line_ending_style = false` means an existing ending is kept per gap
(`CSharpDocumentBuilder.FirstNewLine`), which is correct — but the ending chosen for the *inserted
final newline* is not the one the rest of the file converged on, and it changes once the first pass
has rewritten the gap above it. `pathological/mixed-crlf-and-lf.cs` exists and does not catch this,
which is the whole argument for the fuzzer: the corpus has the construct and not the shape.

## SK-FUZZ-0004 — the closing `]` of a split array-rank specifier lands two levels out, then one

- file: `array-rank-specifier-split-across-lines.cs`
- property: `idempotency`
- seed: `1391645108652186791`
- found: mutating `real/vixen/Platform/Vixen.Vfx.Gpu.Tests/RavenKernels.cs` with `split-line`,
  `comment-line`; minimised from 3 325 characters to 118, and narrowed by hand to 33.

```
class R {
  byte[
] f;
}
```

Through the CLI, byte for byte:

```
pass 1      byte[            pass 2      byte[            pass 3  unchanged
                ] f;                     ] f;
```

Eight columns on the first pass, four on the second. ⚠ The *converged* answer is the right one, and
that is what makes this the shape a fixed corpus cannot see: every file in `corpus/` has already been
through a formatter, so its `]` is already at four, the first pass agrees with it, and the property
holds. It takes an input whose `]` starts at zero to make the first pass disagree with the second,
and `split-line` produced one in a run of nine thousand cases.

## SK-FUZZ-0006 — a comment between two usings, and arrangement stops being a fixed point

- file: `comment-between-usings-with-inner-whitespace.cs`
- property: `arrangement-idempotency`
- seed: `11809147520796568340`
- found: a generated unit printed with `widen-gap`, `comment-line`, `tabs`, `region`; minimised from
  388 characters to 67 in 27 evaluations, and narrowed by hand to 45.

```
using System;
// c
using   System.  Collections ;
```

`pipeline(pipeline(x)) ≠ pipeline(x)`: the first pass applies **SK2010** and the second still wants
one edit. M4's own bar, from doc 12 § "Properties" — *"Formatting is idempotent on its own and
arrangement is idempotent on its own, and neither fact implies the pair is."*

⚠ Both ingredients are needed and neither is exotic. Remove the interior whitespace from the
qualified name and it converges; remove the comment between the two usings and it converges. Both
appear in real code constantly, and 391 corpus files under `ArrangementPropertyTests` do not contain
the combination — which is the same sentence as SK-FUZZ-0004's, for the second time.

## SK-FUZZ-0007 — a blank line appears because the *input* line was too wide

- file: `blank-line-from-an-over-wide-input-line.cs`
- property: `whitespace-absorption`
- seed: `15123090416411387126`
- found: a generated unit mutated with `widen-gap`, `tabs`, `widen-gap`, `indent`, `widen-gap`,
  `widen-gap`; minimised from 460 characters to 176, and narrowed by hand to two files that differ
  in **one gap**.

```
interface I {                    interface I {
    int P { get; }                   int P { get; }
    void M(int a);                   void M(int<…108 spaces…>a);
}                                }
```

The left one formats to itself. The right one formats to the same four lines **plus a blank line
between `P` and `M`** — from an input that differs only in the width of one inter-token gap, on a
line the formatter is about to rewrite anyway.

⚠ This is the fitting engine reading a measurement it should not have. The blank-line decision is a
function of whether the member is "wide", and the width it uses is the *input's* rather than the
output's — so a gap the formatter is about to collapse changes a decision about a different line
entirely. docs/plan/16 § R2 argues the fitter is the only genuinely novel code in the project and
that the property suite is what contains its risk; this is that risk, in four lines, found by the
only mutation in the catalogue that changes a width.

## SK-FUZZ-0008 — the `indent` mutation is misclassified as absorbed on a raw interpolated string

⚠ **A defect in the fuzzer's own catalogue, not in the formatter.** `pathological/interpolated-raw-string-with-nested-braces.cs`:

```csharp
class C {
    string M(int a) => $$"""
        {{{a}}} and a literal {{ brace
        """;
}
```

`FuzzMutations.Indent` is declared `MutationClass.Absorbed` — whitespace-only, and therefore subject
to the strongest property the suite has. On this file it writes four spaces into the raw string's
text token, which is **data**: a raw string literal's content is measured against the indentation of
its closing delimiter, so indenting a content line changes what the program prints while changing no
token the parser reports *at that position*. `AnAbsorbedMutation_ChangesNoToken` fails, correctly,
and the formatter never runs.

⚠ **Three fixes were attempted in `SourceMap` and none of them was it.** Two are kept because they
are right in their own right and would have been needed anyway: multi-line raw string literals are
now registered as verbatim regions (the token loop never saw them, since a raw string is one token
and the node loop only matched interpolated strings), and the safe-line test is an *intersection*
rather than a containment, because a region may begin mid-line. The third — protecting every line a
multi-line token spans — is also kept and also did not fix it, which is the part that says the cause
is not yet understood.

**Excluded by name** from `AnAbsorbedMutation_ChangesNoToken`, and by name only: widening the
exclusion to raw strings in general would silence the class the fuzzer exists to find. Every other
property still runs over this file.

- property: `whitespace-absorption` (fuzzer-oracle)
- ⚠ status: **open**, reproduced and minimised, cause not established
- ⚠ ⚠ This is the *only* entry here whose defect is in the test harness rather than the tool. When it
  is fixed the exclusion goes, not the fixture.

---

## Retired

Kept as a list rather than deleted, because "what the fuzzer has already caught" is the evidence
that it is worth running — and an empty register would read as a fuzzer that finds nothing.

| | property | fixed by |
|---|---|---|
| `SK-FUZZ-0001` | crash — `@formatter:off` running to a whitespace-only end of file threw out of `EditEmitter`, past the crash handler, out of the process | the formatter-tag pass. `EditEmitter` indexed past the output because the file-level rules shorten it *after* the writer ran; and the exit code was wrong until `EnableDefaultExceptionHandler = false`, because System.CommandLine was swallowing the exception before any handler saw it |
| `SK-FUZZ-0005` | token equivalence — an interpolated string inside a formatter-off span | the same pass: `EmitVerbatim` was writing a node a second time inside an already-written region |

Their reproductions now live in `Testing/corpus/pathological/` as ordinary measured fixtures, which
is where a case belongs once the tool can process it.
