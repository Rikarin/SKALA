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

⚠ **The nightly-33148756015 harvest emptied this queue, and it did not stay empty for one run.**
SK-FUZZ-0009 and SK-FUZZ-0010 were retired out of it; SK-FUZZ-0012 and SK-FUZZ-0013 were fixed in
the same pass and went straight to the table below without ever being entries. SK-FUZZ-0015 was then
found by the very next fuzz run after those fixes landed, which is the argument for the nightly in
one line.

⚠ **Nightly 33195187043 returned three findings and only two of them were new.** The third was
SK-FUZZ-0016 again, from a different seed on a different origin file — recorded in that entry rather
than as a fourth. A register that counted it twice would report the queue growing when what actually
happened is that one open defect is common enough for the fuzzer to keep walking into it.

⚠ SK-FUZZ-0008 carries no `file:` line, so it is **not** one of the entries `OpenDefectTests`
asserts over: its defect is in the fuzzer's own mutation catalogue rather than in the formatter and
its fixture lives in the measured corpus. That is a gap in this register's own accounting and is
written down rather than tidied away — an entry nothing tests is a note, and this directory exists
because a note is not enough.

## SK-FUZZ-0018 — a `using` inside a wrapped file-scoped namespace is hoisted, then removed

- file: `using-inside-a-wrapped-file-scoped-namespace.cs`
- property: `arrangement-idempotency`
- seed: `10305983846149543162`
- found: mutating `pathological/wrapped-file-scoped-namespace-name.cs` with `join-line`; minimised
  from 158 characters to 114.

```csharp
namespace Serilog
  .Configuration;
using System;
public class Foo {
  void M() {
  Console.WriteLine(Bar);
  }
}
```

The pipeline converges, and then a second pipeline pass over its own output still wants one edit:

```
arrangement-idempotency [no symbols]: the second pipeline pass still wants 1 edit(s);
rules applied on the first: SK2010
```

⚠ **The first pass moves the directive; the second pass deletes it.** `using System;` is written
*after* a file-scoped namespace declaration, which puts it inside the namespace.
`csharp_using_directive_placement = outside_namespace` hoists it above `namespace Serilog
.Configuration;`, and that is the SK2010 the message names. On the *hoisted* text the compiler
answers `CS8019` — the fuzzer's compilation carries the implicit `global using System;` from
`ArrangementDifferential.ImplicitUsings`, so an explicit `using System;` at compilation-unit level is
redundant — and the second pass removes what the first had only relocated. Two passes, two different
answers about the same directive, from two different questions asked of two different texts.

⚠ **This is SK-FUZZ-0013's shape in a place its fix did not reach.** That entry records the
removable-usings set being computed once, before the pipeline, against a text the pipeline is about
to rewrite; its fix made both sides key on the name with whitespace dropped, which is the *key* half
of the problem. This is the *timing* half: the key is now stable and the set is still an answer about
the input rather than about the output, so a rule that moves a directive across the boundary that
decides its own removability makes the set stale in one pass.

⚠ **Not reproducible through the CLI on a loose file**, and that is diagnostic rather than an
inconvenience: `skala arrange` on this file with no compilation runs the syntactic subset, where
`_removable` is empty by construction and the hoist is all that happens — `arrange` then reports a
fixed point on its own output. The defect needs the semantic half, which is why it took a fuzz run
with `--arrange-every` to find it. Reproduce it with `FuzzProperties.Check(…, arrangement: true)`, as
`OpenDefectTests` does.

- ⚠ status: **open**, reproduced and minimised, cause established, **not fixed**. The fix is not the
  one-line kind: either the removable set is recomputed per pass — which costs a compilation per
  pass and changes what `ArrangementPipeline` is allowed to cache — or `UsingsRule` refuses to move a
  directive across the namespace boundary in the same pass that could remove it. Both are decisions
  about the pipeline's contract rather than repairs, and the loader that feeds it is being worked on
  in parallel. It wants its own commit.

## SK-FUZZ-0017 — a generated nested switch loses 51 characters on the second pass

- file: `trailing-space-in-a-generated-nested-switch.cs`
- property: `idempotency`
- seed: `5423343295399047858`
- found: generative fuzzing, mutated with `trailing-space`; minimised from 7 402 characters to 825.

`format(format(x)) ≠ format(x)`. The second pass wants one edit and it is a pure deletion:

```
idempotency [no symbols]: the second pass still wants 1 edit(s): [1112..1163) ->
```

⚠ **The mutation is `trailing-space`, which is declared `MutationClass.Absorbed`** — whitespace-only,
and therefore under the strongest property the suite has. Absorption itself holds here; what fails is
idempotency, so the first pass is not absorbing the added whitespace so much as converting it into
something the second pass then removes.

⚠ **It does not reproduce under the CLI's default options**, only under `Fuzzer.OptionsFor`. Two
passes of `skala format` on this file with the shipped defaults converge byte for byte, so the
non-convergence belongs to a key the corpus sets rather than to the formatter's default behaviour.
Which key is **not yet established** — that is the next step and it is written down as undone rather
than guessed at, because the deleted range sits in a deeply nested `switch` whose arms carry a
collection expression, a `switch` expression and a raw string, and any of those could own it.

- ⚠ status: **open**, reproduced through the property harness and minimised; **cause not
  established**. Unlike SK-FUZZ-0015 and SK-FUZZ-0016 there is no probe here yet — the entry records
  a real, replayable non-convergence and stops short of claiming to know why.
- ⚠ Determinism is the property this product exists to provide, so this entry should not sit here
  long: an idempotence violation means the same file formats two ways depending on how many times the
  tool has already run.

## SK-FUZZ-0016 — a `#region` inside disabled text stops being a directive

- file: `region-directive-inside-disabled-text.cs`
- property: `token-equivalence`
- seed: `13502335049781382213`
- found: mutating `real/newtonsoft/Newtonsoft.Json/Utilities/Base64Encoder.cs` with `join-line`,
  `region`, `tabs`, `tabs`; minimised from 1 149 characters to 45.

```
#if HAVE_ASYNC
#region fuzz
#endregion
#endif
```

`skala format` reports **SK9099** and refuses to write:

```
error SK9099: not written, the formatted output has a different token stream
(at token 1: 'P:#region fuzz' became 'D:\n')
```

⚠ A **P**reprocessor directive became **D**isabled text — the mirror image of SK-FUZZ-0009, where a
directive became a *skipped token*. Under no symbols `HAVE_ASYNC` is undefined, so the branch is
inactive; Skala's piece splitter treats everything inside it as one run of `DisabledTextTrivia` and
re-emits it as text, while Roslyn keeps `#region` and `#endregion` as **directive** trivia even
there. The file therefore cannot be formatted at all under the empty symbol set, and formats
correctly under one that defines `HAVE_ASYNC`.

⚠ **Three probes say it is `#region` specifically and not "a directive inside disabled text".**
Replace the `#region`/`#endregion` pair with `#pragma warning disable 1` in the same inactive branch
and every property holds — because Roslyn does *not* keep a `#pragma` as a directive inside disabled
text, and it does keep a region. Put the same region in an **enabled** branch, or in no branch at
all, and both hold. So the rule the splitter needs is not "disabled text is opaque" but "disabled
text is opaque except for the directives Roslyn still reports inside it".

⚠ **Found again, by a different seed on a different origin, and this is what an open entry costs.**
Nightly 33195187043 reported `token-equivalence` on seed `13694834950078302995`, mutating
`constructs/blank-lines/a-preprocessor-else-between-members.cs` with `comment-line`, `region`,
`widen-gap`, `trailing-comment` — a `#region` the `region` mutation dropped into the inactive arm of
an `#if DEBUG`. Reduced to `class C { #if DEBUG / #region fuzz / #endregion / int _a; #else … }` it
gives the same message this entry already records, at token 4 instead of token 1, and the `#pragma`
control above still formats cleanly. Same defect, not a second one.

⚠ **The expedition has no notion of a known open defect, so this reds the nightly on its own.**
`OpenDefectTests` pins the *fixture*; it does not stop the fuzzer rediscovering the defect from a new
seed, and `nightly.yml` fails the job on any finding. So while this entry stands, a nightly can go
red for a defect that is already registered, and the only way to tell that from a genuine regression
is to replay the seed and compare it with this section. That is an argument for fixing it rather than
for teaching the fuzzer to skip it: a suppression list keyed on the defect the fuzzer is *for* would
hide the next variant too.

- ⚠ status: **open**, reproduced through the CLI, minimised, and the shape established. Not
  diagnosed to the line, and not fixed: `#region` inside `#if` is ordinary in real code —
  Newtonsoft.Json is where the fuzzer found it — so the fix is worth its own commit with the
  differential run to price it.

## SK-FUZZ-0015 — a `///` run takes its line ending from the first newline in the *input*

- file: `doc-comment-run-under-a-leading-crlf.cs`
- property: `idempotency`
- seed: `7489454592082333649`
- found: mutating `real/serilog/Serilog/Events/LogEventLevel.cs` with `blank-lines`, `blank-lines`,
  `comment-inline`, `line-endings`; minimised from 1 588 characters to 84.

```
<CR>
// Copyright 2013-2015 Serilog Contributors
{<CR>
  /// <summary><CR>
  /// </summary>
}
```

`format(format(x)) ≠ format(x)` by one line ending, inside the `///` run:

```
pass 1:   /// <summary><CR><LF>       /// </summary><LF>
pass 2:   /// <summary><LF>           /// </summary><LF>
```

⚠ **The cause is established rather than guessed, by three probes.** Delete the file's leading
`<CR><LF>` and it converges; make the gap between the two `///` lines an `<LF>` and it still fails,
so the gap's own ending is not what is read; replace `///` with `//` and it converges.

`CSharpFormatter.DefaultNewLine` answers with **the first newline in the input**, and that value is
handed to `XmlDocFormatter.Rewrite`, which uses it between the lines of a run it reflows. The first
pass deletes the leading blank line — so the first newline of pass 2's input is a different newline,
in a file whose endings are mixed, and the same run is re-emitted with the other one.

⚠ **This is SK-FUZZ-0003's defect in a place its fix did not reach.** That entry records exactly
this sentence about `insert_final_newline` — "`DefaultNewLine` […] answers with the first newline in
the *input*, and the first pass can move, rewrite or delete the text above that newline, so the
second pass asks a different question" — and fixed `FinalNewLine` to read the ending of the last
break in the **output**. The same unstable value is still passed to the doc-comment sub-formatter
one line below the call that was fixed.

⚠ **A second reproduction, on a different file, from the next run.** Seed `12780975215320227180`
mutates `pathological/doc-comment-starting-on-the-brace-line.cs` — SK-FUZZ-0002's own retired
fixture — with `line-endings`, `tabs`, `join-line`, and lands on the same instability:

```
pass 1:   /// <summary>x</summary><CR><LF>   /// <remarks>y</remarks><CR><LF>
pass 2:   /// <summary>x</summary><LF>       /// <remarks>y</remarks><CR><LF>
```

⚠ Note what pass 2 did *not* do: it rewrote the first gap of the run and left the second. Two passes
are the bound the property checks, so a case that needs three is a case this property reports as
"still wants one edit" without saying it would want another. Not investigated further.

- ⚠ status: **open**, reproduced through the CLI byte for byte, minimised, and **cause established**.
- ⚠ Not fixed here deliberately. The fix is small — derive the sub-formatter's newline from the
  output the way `FinalNewLine` already does — and its blast radius is not: it moves a line ending
  inside every reflowed doc comment in every CRLF file, and docs/plan/04's own note says the
  doc-comment area is "the one area of the formatter with no differential safety net at all". It
  wants its own measured commit rather than a rider on a fuzz-triage branch.

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

---

## Retired

Kept as a list rather than deleted, because "what the fuzzer has already caught" is the evidence
that it is worth running — and an empty register would read as a fuzzer that finds nothing.

| | property | fixed by |
|---|---|---|
| `SK-FUZZ-0009` | token equivalence — a `#endif` after a lone `\r` stopped being a directive (SK9099, the file unformattable) | ⚠ **the entry's own guess, and it was right for once.** `CSharpDocumentBuilder.CountNewLines` counted `'\n'` and nothing else, so the gap `}   <CR>#endif` reported zero newlines and `EmitGap` reasoned about the brace and the directive as though they shared a line — it joined them, a `#` that is not first on its line is not a directive to Roslyn, and the `#endif` became a skipped token. `FirstNewLine` beside it had always read a lone `\r` correctly, which is what made the disagreement invisible: the *style* of the break was right, there just was not one. It now counts the terminators C# recognises, `\r\n` as one |
| `SK-FUZZ-0010` | idempotency — a wrapped signature and a trailing comment needed two passes for one blank line | SK-FUZZ-0007's mistake one step further out, exactly where the entry pointed. `OutputWidth` measures the gaps *between* a member's tokens and there is no gap after the last one, so the trailing comment that will share the member's line was not in the width `IsSingleLine` compares to the margin: 116 columns without `// fuzz`, 124 with it. The member was called single-line, `blank_lines_around_single_line_invocable = 0` declined the blank line — and then the fitter, which does count the comment, chopped the member onto three lines, so pass 2 read a multi-line member, asked `blank_lines_around_invocable = 1` instead, and inserted the blank line pass 1 had refused. ⚠ The register said the trigger "needs the *wide* method signature that the fitter chops"; the width it was missing was the comment's |
| `SK-FUZZ-0012` | crash — a target-typed `new` whose target is a **delegate** type, carrying a LINQ query in its object initializer, threw `IndexOutOfRangeException` out of the *arrangement pipeline* and took the process with it | ⚠ **the throw is Roslyn's and the defect is ours.** `Func<int> v = new () { P = (from item in items select null) };` makes `MemberSemanticModel.GetLowerBoundNode` index an empty bound-node list, out of a plain `SemanticModel.GetSymbolInfo` on a node of the model's own tree — `PredefinedTypeRule` calls it, and there is no version of that call that can know in advance which node will do it. Two guards, because the first was not enough: a rule that throws is now skipped and reported as `SK9095` (the sibling of `SK9030` "analyzer threw"), and `ArrangementSafety.Check` — the layer whose whole job is to stop a bad rewrite reaching disk, and which was itself the thing taking the process down — now answers **revert** when its re-bind throws, because an unanswered safety question is not a safe one |
| `SK-FUZZ-0013` | arrangement idempotency — which rules fire depended on how the author had spaced a dotted `using` name | the removable-usings set is Roslyn's `CS8019` keyed by `Name.ToString()`, which carries the trivia *between* a qualified name's tokens: `using  System .Threading. Tasks;` keyed as `"System .Threading. Tasks"`, the set is computed once before the pipeline, and the formatter rewrites exactly that spacing on its first pass. So pass 1 offered the removal, pass 2 could no longer match its own key, and the *next* pipeline run — which recomputes the set — removed a using the first had left. Both sides now key on the name with whitespace dropped. ⚠ **What that was masking is worth more than the bug.** With the key fixed, `NamespaceBodyRule` (SK2013) and the removal (SK2010) fire together on this file and the re-bind reports `CS1027: #endif directive expected`, so safety layer 2 reverts the arrangement whole — while either rule *alone* is safe on it. The file is now stable and arranged **less than it should be**, which is layer 2 behaving as designed and is also why "the file converged" is not the same as "the file was arranged". Not diagnosed further; the reproduction is `pathological/unused-using-whose-name-carries-spaces.cs` |
| `SK-FUZZ-0001` | crash — `@formatter:off` running to a whitespace-only end of file threw out of `EditEmitter`, past the crash handler, out of the process | the formatter-tag pass. `EditEmitter` indexed past the output because the file-level rules shorten it *after* the writer ran; and the exit code was wrong until `EnableDefaultExceptionHandler = false`, because System.CommandLine was swallowing the exception before any handler saw it |
| `SK-FUZZ-0005` | token equivalence — an interpolated string inside a formatter-off span | the same pass: `EmitVerbatim` was writing a node a second time inside an already-written region |
| `SK-FUZZ-0003` | idempotency — mixed line endings converged in two passes, not one | `insert_final_newline` chose its ending with `DefaultNewLine`, which answers with the first newline in the **input** — and the first pass can move, rewrite or delete the text above that newline, so the second pass asks a different question. It now reads the ending of the last break in the **output**, which is stable by construction and still keeps a CRLF file ending CRLF. ⚠ Committing the reproduction lowered `pathological`'s ratchet to 0.9589; its three lines are SK-DIV-0018, the oracle normalising a mixed-ending file where Skala preserves each gap |
| `SK-FUZZ-0002` | token equivalence — a `///` run beginning on the `{` line lost its continuation lines (SK9099, the file unformattable) | nothing was ever lost: both `///` lines were emitted, and a **blank line was inserted between them**. Roslyn ends a documentation comment at a blank line, so that split one trivia into two and the token stream changed. `stick_comment`'s early return spends a member's requirement above its comment rather than below it, but asks `previous.StartsLine` first — and the first `///` of a run that starts on the brace line does not start a line, so `blank_lines_around_invocable` landed inside the run. `ResolveBlankLines` now treats the gap between two `///` lines as structure that none of the three systems votes on: 0 → 1 splits a trivia and 1 → 0 fuses two |
| `SK-FUZZ-0007` | whitespace absorption — a blank line appeared because the *input* line was wider than the margin | `IsSingleLine` measured the member with `TextWidth.Measure` over its source span, which counts the gaps the author wrote between its tokens — gaps the formatter is about to collapse. It now measures the token stream and the spaces `SpaceRules` will actually emit. The leading-whitespace half of this had already been fixed once (`OutputIndentColumns`); the interior half is the same mistake one step in, and only a mutation that changes a width could reach it. Both halves of the pair are now measured fixtures whose `.expected.cs` are byte-identical |
| `SK-FUZZ-0006` | arrangement idempotency — a comment between two usings, and arrangement stops being a fixed point | ⚠ **the entry's cause was the symptom, and the defect was silent code deletion.** `UsingsRule.Renormalise` re-pins the block's opening trivia to whatever sorts first — and blanked the leading trivia of *every other* directive to do it. So sorting `using System.Text;` / `// keep me` / `using System.Collections;` deleted the comment, with no removal in play, both usings bound, on a file nothing about this register would have flagged; with `#if` / `#endif` in that position it deleted a preprocessor directive, which changes what compiles. The idempotency violation was downstream of that: `HasNoComment` keeps a using that carries a comment or a directive, so blanking the trivia on pass 1 made the same directive *removable* on pass 2 — the pipeline deleted the comment, then the using, then converged on a file that had lost both. Each directive now keeps its own leading trivia and only `original[0]` surrenders its own to the front, so the header still cannot be emitted twice |
| `SK-FUZZ-0011` | token equivalence — `@formatter:off` in the leading trivia of a member an unbalanced `#if` made verbatim (SK9099, the file unformattable) | SK-FUZZ-0005's guard, evaluated one call too early. `EmitVerbatim` checks `_verbatimUntil` at the top, but the tag comment is in the node's *leading trivia*, so the piece that opens the region is emitted by the `EmitUpTo` on the line below — traced: `-1` on entry, end-of-file on return. The node was then written a second time over source the tag had already covered, exactly as in SK-FUZZ-0005. Re-checked after `EmitUpTo`. ⚠ Both halves are needed: the unbalanced `#if` is what makes `PreprocessorGuard` emit the member verbatim at all, and without the tag `_verbatimUntil` never moves |
| `SK-FUZZ-0004` | idempotency — the closing `]` of a split array-rank specifier landed at eight columns, then four | `EmitToken` matched a piece by its start position alone. A zero-width token has no piece of its own (`SourcePieces.Split` skips it), so the omitted size of `byte[…]` arrived holding the *next* token's piece — and it shares that token's start whenever no trivia separates them. The `]` was emitted one caller early, from inside the bracket's continuation scope instead of after it closed. Matching on the piece's length as well as its start is the fix; a space before the `]` moved it off the collision, which is why the second pass was right |

Their reproductions now live in `Testing/corpus/pathological/` as ordinary measured fixtures, which
is where a case belongs once the tool can process it.
