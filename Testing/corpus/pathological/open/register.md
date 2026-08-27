# Open defects

Fuzz findings that are **minimised, reproduced through the shipping CLI, and not yet fixed**.

docs/plan/12 § "Corpus expansion": *"Any crash, non-idempotent case or token-equivalence failure is
minimised (delta-debugging on the input) and committed to `corpus/pathological/` with the bug
reference. The corpus only grows."* This directory is where such a case lives **before** the defect
it pins is fixed.

⚠ **It is excluded from `Corpus.Files()`, and the exclusion is the point rather than a dodge.** A
file that makes `skala format` throw does not fail one assertion — it poisons every harness path that
formats the corpus, `fidelity` and the differential report included, and it takes the measurement
down with it. So these three are not in the measured sets. What they are instead is
`OpenDefectTests`, which asserts of every entry below that it **still fails, in the way recorded
here**. That is a stronger obligation than a comment in a bug tracker:

- a defect that is fixed makes this suite fail, with "SK-FUZZ-000n now passes; move its file into
  `pathological/`, run `./build.sh Oracle --only=…`, and delete its entry";
- a defect that changes shape makes it fail too, because the property recorded is the property
  asserted;
- and nothing here can be quietly forgotten, because the file is a test rather than a note.

⚠ Each entry's `.cs` file is **byte-significant**. Two of the three are about a trailing space, a
missing final newline or a lone `\r`, so an editor that tidies on save destroys the case. There are
no `.expected.cs` fixtures here: an oracle fixture is a measurement, and a file the tool cannot
process has nothing to measure.

## SK-FUZZ-0001 — `skala format` throws on `@formatter:off` running to a whitespace-only end of file

- file: `formatter-off-to-end-of-file-with-trailing-space.cs`
- property: `crash`
- seed: `17252315466773767716`
- found: mutating `pathological/formatter-off-to-end-of-file.cs` with `trailing-space`, `widen-gap`,
  `trailing-space`; minimised from 102 characters to 32.

`IndexOutOfRangeException` out of `EditEmitter.AddIfDifferent`
(`Formatting/Rikarin.Skala.Formatting/TextEdit.cs`), **unhandled** — it escapes `FormatCommand`, the
`.skala/crash/` snapshot handler and the CLI's top level, so the process dies with a stack trace
rather than reporting SK9098 and leaving the file alone.

The three ingredients, established by hand from the minimised case:

| input | outcome |
|---|---|
| `class C {\n// @formatter:off\n}   ` (no final newline) | **throws** |
| `class C {\n// @formatter:off\n}` | fine |
| `class C {\n}   ` | fine |

So it needs a verbatim region that is still open at the end of file *and* trailing whitespace after
the last token *and* no final newline. The anchor list that `EditEmitter.Emit` walks ends with an
output offset past the end of the string it indexes.

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
