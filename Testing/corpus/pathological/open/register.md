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
  `pathological/`, run `./build.sh Oracle --only …`, and delete its entry";
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

⚠ **Retiring such a pair moves both halves**, and the pair survives the move without needing a
bespoke test: two corpus files that differ only in whitespace acquire two `.expected.cs` fixtures,
and those fixtures being **byte-identical** is the absorption statement, now asserted by the
ordinary differential instead of by an entry here. SK-FUZZ-0007 was retired that way.

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

### ⚠ The diagnosis above is wrong, and here is what four probes say instead

⚠ **It is not `indent`, and it is not the content line.** Running every absorbed mutation over this
one file across 5 000 seeds: **2 466 of 3 462 applied mutations change a token**, and they are
spread across `indent` (1 260 applied), `widen-gap` (1 230) and `trailing-space` (972) — not one
mutation, three. The by-name exclusion is therefore masking three mutations rather than the one it
names.

Four targeted probes, each a single edit compared with `TokenEquivalence.Compare`:

| edit | result |
|---|---|
| append five spaces at **end of file** | ⚠ **CHANGED** at token 20: `T8517:\n` → `T8517:\n·····` |
| indent **line 0** (`class C {`) only | token-equivalent |
| add one space to the **string's content line** | CHANGED at token 11: `T8517:········{` → `T8517:·········{` |
| append five spaces at end of a **plain file** with no raw string | token-equivalent |

Read the first and the fourth together. Appending whitespace *after the file's final newline* —
touching nothing else, nowhere near the string — changes a token, and the identical edit on an
ordinary file does not. Token 8517 is `InterpolatedStringTextToken`, and in the unmutated file its
text is exactly `\n`.

⚠ So the region that must not be written to is not the string's content lines: it is **everything
from the unclosed `{{` to the end of the file**. `{{ brace` in a `$$"""` string opens an
interpolation that is never closed, and the parser's recovery extends a text token over the rest of
the file — the trailing empty line included. That is why all three whitespace mutations trip it and
why the file's *last* line is as unsafe as its middle.

⚠ **And it is why the three `SourceMap` attempts failed.** All three looked for a bounded region to
protect — a node's span, an intersection, the lines a token spans — and computed it from a tree
whose recovery token has no meaningful end. A line-level safe-lines map cannot be right about a
token that runs to EOF. The next attempt should start from "does this file parse cleanly?" rather
than from the shape of the region: a file with parse errors has no reliable notion of a safe line
for an absorbed mutation, and the honest answer may be that this fixture has *no* safe lines at all.

⚠ The third probe is a real hazard and is **not** this defect: a space added to the content line
genuinely changes what the program prints, and `TokenEquivalence` catches it correctly. That one is
what the original entry describes, and it is the rarer of the two.

- property: `whitespace-absorption` (fuzzer-oracle)
- ⚠ status: **open**, reproduced and minimised; cause now characterised but **not fixed**
- ⚠ ⚠ This is the *only* entry here whose defect is in the test harness rather than the tool. When it
  is fixed the exclusion goes, not the fixture.

## SK-FUZZ-0009 — a `#endif` after a lone `\r` stops being a directive

- file: `preprocessor-directive-after-a-lone-cr.cs`
- property: `token-equivalence`
- seed: `16325283595831279955`
- found: mutating `pathological/mixed-line-endings-after-a-trailing-comment.cs` with `trailing-space`,
  `if-true`, `blank-lines`, `widen-identifier`; minimised from 71 characters to 42.

```
#if true
class C_ww { // fuzz<CR><LF>
}   <CR>#endif
```

`skala format` reports **SK9099** and refuses to write:

```
error SK9099: not written, the formatted output has a different token stream
(at token 6: 'P:#endif' became 'S:#endif')
```

⚠ A **P**reprocessor directive became a **S**kipped token. C# ends a line at a lone `\r` as well as
at `\n`, so `}   <CR>#endif` puts the `#endif` at the start of its own line and it is a directive;
the formatter joins it onto the line above, where a `#` is no longer the first thing on the line and
Roslyn stops treating it as one. The safety net does its job and nothing is written, but the file
cannot be formatted at all.

⚠ Suspect `CountNewLines` and `FirstNewLine` in `CSharpDocumentBuilder`: a gap whose only line
terminator is a lone `\r` looks like a gap with no newline in it, and the brace and directive rules
then reason about it as though the two tokens shared a line. That is a guess and is written here as
one — the cause is not established.

⚠ **Found on the file SK-FUZZ-0003 had just retired into the measured corpus**, which is the
"corpus only grows" argument paying for itself inside one session: retiring a reproduction hands the
mutator a seed file with a shape nothing else in the corpus has, and it found a second defect in it
within twelve minutes.

## SK-FUZZ-0010 — a wrapped signature and a trailing comment need two passes for one blank line

- file: `blank-line-after-a-trailing-comment.cs`
- property: `idempotency`
- seed: `15479240576151154023`
- found: a generated unit mutated with `widen-gap`, `trailing-comment`, `trailing-comment`; minimised
  from 4 779 characters to 330.

`format(format(x)) ≠ format(x)`: the second pass inserts one blank line and the third is stable.
Reproduced through the CLI byte for byte — pass 1 and pass 2 differ by a single added line, pass 2
and pass 3 are identical.

⚠ **Hand-narrowing failed, and that is the useful part of this entry.** The obvious reduction —
a method whose body ends in `}`, a trailing comment on it, a field below with another — converges in
one pass, and so does either half of it alone. The trigger needs the *wide* method signature that
the fitter chops, which points at the same place SK-FUZZ-0007 did: a blank-line decision that reads
a width, taken before the pass that changes it. The 330-character delta-debugged case is therefore
the entry rather than a prettier four-line one, because the prettier one does not reproduce.

---

## Retired

Kept as a list rather than deleted, because "what the fuzzer has already caught" is the evidence
that it is worth running — and an empty register would read as a fuzzer that finds nothing.

| | property | fixed by |
|---|---|---|
| `SK-FUZZ-0001` | crash — `@formatter:off` running to a whitespace-only end of file threw out of `EditEmitter`, past the crash handler, out of the process | the formatter-tag pass. `EditEmitter` indexed past the output because the file-level rules shorten it *after* the writer ran; and the exit code was wrong until `EnableDefaultExceptionHandler = false`, because System.CommandLine was swallowing the exception before any handler saw it |
| `SK-FUZZ-0005` | token equivalence — an interpolated string inside a formatter-off span | the same pass: `EmitVerbatim` was writing a node a second time inside an already-written region |
| `SK-FUZZ-0003` | idempotency — mixed line endings converged in two passes, not one | `insert_final_newline` chose its ending with `DefaultNewLine`, which answers with the first newline in the **input** — and the first pass can move, rewrite or delete the text above that newline, so the second pass asks a different question. It now reads the ending of the last break in the **output**, which is stable by construction and still keeps a CRLF file ending CRLF. ⚠ Committing the reproduction lowered `pathological`'s ratchet to 0.9589; its three lines are SK-DIV-0018, the oracle normalising a mixed-ending file where Skala preserves each gap |
| `SK-FUZZ-0002` | token equivalence — a `///` run beginning on the `{` line lost its continuation lines (SK9099, the file unformattable) | nothing was ever lost: both `///` lines were emitted, and a **blank line was inserted between them**. Roslyn ends a documentation comment at a blank line, so that split one trivia into two and the token stream changed. `stick_comment`'s early return spends a member's requirement above its comment rather than below it, but asks `previous.StartsLine` first — and the first `///` of a run that starts on the brace line does not start a line, so `blank_lines_around_invocable` landed inside the run. `ResolveBlankLines` now treats the gap between two `///` lines as structure that none of the three systems votes on: 0 → 1 splits a trivia and 1 → 0 fuses two |
| `SK-FUZZ-0007` | whitespace absorption — a blank line appeared because the *input* line was wider than the margin | `IsSingleLine` measured the member with `TextWidth.Measure` over its source span, which counts the gaps the author wrote between its tokens — gaps the formatter is about to collapse. It now measures the token stream and the spaces `SpaceRules` will actually emit. The leading-whitespace half of this had already been fixed once (`OutputIndentColumns`); the interior half is the same mistake one step in, and only a mutation that changes a width could reach it. Both halves of the pair are now measured fixtures whose `.expected.cs` are byte-identical |
| `SK-FUZZ-0004` | idempotency — the closing `]` of a split array-rank specifier landed at eight columns, then four | `EmitToken` matched a piece by its start position alone. A zero-width token has no piece of its own (`SourcePieces.Split` skips it), so the omitted size of `byte[…]` arrived holding the *next* token's piece — and it shares that token's start whenever no trivia separates them. The `]` was emitted one caller early, from inside the bracket's continuation scope instead of after it closed. Matching on the piece's length as well as its start is the fix; a space before the `]` moved it off the collision, which is why the second pass was right |

Their reproductions now live in `Testing/corpus/pathological/` as ordinary measured fixtures, which
is where a case belongs once the tool can process it.
