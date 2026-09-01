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

⚠ **`probe:` is the field that decides whether the nightly goes red when this defect is
rediscovered**, and it is a claim about the *cause*, not a label. It names one entry of the closed
vocabulary in `Testing/Rikarin.Skala.Testing/OpenDefectProbes.cs`, each of which is an edit that
removes that trigger from an arbitrary input and nothing else. When the expedition hits a violation
it applies the probe of every entry sharing the property, re-runs the property oracle on the result,
and accepts the entry **only if the property then holds** — so a rediscovery is reported and does not
fail the job, while an input that carries a *second*, unregistered defect still fails the re-run and
is reported as new. `OpenDefectTests` requires each probe to fire on its own entry's fixture and to
be the reason that fixture fails.

⚠ The objection this has to answer was written down here before the mechanism existed, by the entry
that prompted it: *"a suppression list keyed on the defect the fuzzer is for would hide the next
variant too."* It would, and that is why a `probe:` is not a suppression key. The check is a
measurement — the oracle is re-run on the neutralised input — so the next variant, being a different
defect, still fails it.

⚠ **An entry with no `probe:` still reds the nightly, and that is deliberate.** An entry whose status
line says "cause not established" has no trigger to name, and the expedition genuinely cannot tell
its rediscovery from a new defect — guessing there would be exactly the suppression list SK-FUZZ-0016
argues against below. The cost is therefore visible and attributable to a named entry rather than
showing up as "the nightly is flaky", and
`OpenDefectTests.EntriesWithoutAProbe_AreTheOnesThatStillRedTheNightly` bounds how many there may be.

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

⚠ **SK-FUZZ-0016 and SK-FUZZ-0018 were retired without their reproductions being promoted into
`Testing/corpus/pathological/`, and that is a deliberate omission rather than the usual retirement.**
The prescribed move regenerates a fixture with `./build.sh Oracle --only <name>` and adding a file to
a measured set moves that set's fidelity number — SK-FUZZ-0003's retirement is recorded above as
having lowered `pathological`'s ratchet to 0.9589 — and the ratchets were out of scope for the commit
that fixed these two. Neither case is lost: both are pinned by named regression tests (in the "fixed
by" rows below), and SK-FUZZ-0018's reproduction could not have become a corpus fixture in any case,
because its own entry recorded that it does not reproduce on a loose file at all — it needs the
semantic half, so a fixture in a set analysed with no compilation would pin nothing. Promoting
SK-FUZZ-0016's four-line file is a real, small piece of remaining work; it is worth one commit that
also moves the ratchet.


⚠ **SK-FUZZ-0015 was retired the same way and for the same reason**, and its entry had itself asked
for this: "the fix is small — derive the sub-formatter's newline from the output the way
`FinalNewLine` already does — and its blast radius is not […] it wants its own measured commit."
That commit is the doc-comment sweep batch, which re-measured the sub-formatter against
`OracleProfile.DocComments` end to end, so the blast radius is now covered by the thing the entry
said was missing. Its reproduction is pinned by
`XmlDocSubFormatterTests.ADocCommentRunUnderALeadingCrlf_KeepsItsOwnEnding` rather than promoted into
the measured set, because promoting it moves `pathological`'s ratchet and the ratchets are out of
scope for that commit.

| | property | fixed by |
|---|---|---|
| `SK-FUZZ-0015` | idempotency — a `///` run took its line ending from the first newline in the *input* | ⚠ **the entry's diagnosis was right and its prescription is not what closed it.** The diagnosis stands: `CSharpFormatter.DefaultNewLine` answers with the first newline in the input, hands that to `XmlDocFormatter.Rewrite`, and the first pass deletes the text above that newline — SK-FUZZ-0003's defect in the place its fix did not reach. What removed the violation is a different change in the same commit, and it is the *oracle's* rule rather than a repair: a comment the run would otherwise leave alone now keeps its own `///` markers, because `jb cleanupcode` does the same — measured on one file holding two comments with identical blank `///` lines, where the one whose summary had to be rewrapped came back with `/// ` and the one that needed nothing came back bare. A comment already in the renderer's own shape is therefore not rewritten at all, and one that is rewritten reaches its fixed point on pass 1, so the unstable value is spent at most once and no second pass can disagree with a first. ⚠ **That is an argument, so the value was made stable as well**: the sub-formatter now reads the ending from the comment's own first gap, which the next pass reads back out of what this one wrote. ⚠ **No test distinguishes that second change** — with the old read put back, both the minimised bytes and a run that must be rebuilt still converge — and it is recorded here rather than presented as the fix. Pinned by `XmlDocSubFormatterTests.ADocCommentRunUnderALeadingCrlf_KeepsItsOwnEnding`, which carries both shapes |
| `SK-FUZZ-0016` | token equivalence — a `#region` inside disabled text stopped being a directive (SK9099, the file unformattable) | ⚠ **the entry's recorded diagnosis was wrong on both halves, and re-measurement is what found the fix.** It said the piece splitter folded the inactive branch into one `DisabledTextTrivia` run and re-emitted it as text, and that the defect was `#region` specifically because Roslyn does not keep a `#pragma` structured inside disabled text. Measured on Roslyn 5.9.0, Roslyn keeps **every** directive structured inside a skipped branch — `#pragma`, `#nullable`, `#line`, `#define`, a nested `#if` — and `SourcePieces.Split` was already producing four directive pieces for the fixture. Nothing was ever folded. The real cause was the converse: `blank_lines_around_region` fires on the gaps around the pair (`RequiredBlankLines` exempts regions from the `TouchesDirective` early return on purpose), and inside an inactive branch a blank line is not spacing — re-parsed it is a `DisabledTextTrivia` of `\n` that was not in the input, which is the `'P:#region fuzz' became 'D:\n'` the entry recorded. `#pragma` was fine only because no rule adds blank lines around one. So the rule needed was not "disabled text is opaque except for the directives Roslyn reports inside it" but **"the inactive branch is opaque even where Roslyn kept it structured"**. `Piece.Inactive`, set from `DirectiveTriviaSyntax.IsActive`, and `EmitGap` copies the gap byte for byte on either side of it. Pinned by `SafetyTests.ARegionInsideAnInactiveBranch_TakesNoBlankLines` and `ARegionInTheInactiveArm_KeepsItsGapsWhileALiveRegionKeepsItsBlankLines`, the second carrying the control that a **live** `#region` still gets its blank lines |
| `SK-FUZZ-0018` | arrangement idempotency — a `using` inside a wrapped file-scoped namespace was hoisted by one pass and deleted by the next | the entry was right, including that this is SK-FUZZ-0013's *timing* half — but its two candidate fixes were not equal and the second is refuted. "`UsingsRule` refuses to move a directive across the namespace boundary" cannot reach `using System;` / `public class Foo { public string M() => String.Empty; }`, which has no namespace anywhere in it: `EmptyStringRule` rewrites the body, `System` becomes unused, and pipeline #2 deletes a using pipeline #1 kept. Same defect, different rule, no boundary involved — so the stale set is the defect and the boundary was a coincidence of the first reproduction. The removable set is therefore **recomputed on every pass whose arrangement rewrote the tree**, after the rebind so the model answers about the text that now exists. ⚠ The obstacle was never cost (11.49 ms against a 107.53 ms pipeline pass) but the **contract**: `ArrangeCommand` hands the pipeline one compilation and a removable set that is the *intersection* over every owning compilation, so recomputing inside the pipeline would answer for one target framework and could delete a using another needs. The pipeline takes a recomputation delegate and ownership of the intersection stays with the caller. Measured A/B over 200 files: identical output, identical pass counts, identical safety reverts, **+17.9 % cost**. Pinned by `ArrangementRuleTests.AUsingAnEarlierPassMadeRedundant_GoesInThatSameRun`, which asserts the arrangement actually happened as well as that it converged — declining to arrange is also a fixed point |
| `SK-FUZZ-0009` | token equivalence — a `#endif` after a lone `\r` stopped being a directive (SK9099, the file unformattable) | ⚠ **the entry's own guess, and it was right for once.** `CSharpDocumentBuilder.CountNewLines` counted `'\n'` and nothing else, so the gap `}   <CR>#endif` reported zero newlines and `EmitGap` reasoned about the brace and the directive as though they shared a line — it joined them, a `#` that is not first on its line is not a directive to Roslyn, and the `#endif` became a skipped token. `FirstNewLine` beside it had always read a lone `\r` correctly, which is what made the disagreement invisible: the *style* of the break was right, there just was not one. It now counts the terminators C# recognises, `\r\n` as one |
| `SK-FUZZ-0010` | idempotency — a wrapped signature and a trailing comment needed two passes for one blank line | SK-FUZZ-0007's mistake one step further out, exactly where the entry pointed. `OutputWidth` measures the gaps *between* a member's tokens and there is no gap after the last one, so the trailing comment that will share the member's line was not in the width `IsSingleLine` compares to the margin: 116 columns without `// fuzz`, 124 with it. The member was called single-line, `blank_lines_around_single_line_invocable = 0` declined the blank line — and then the fitter, which does count the comment, chopped the member onto three lines, so pass 2 read a multi-line member, asked `blank_lines_around_invocable = 1` instead, and inserted the blank line pass 1 had refused. ⚠ The register said the trigger "needs the *wide* method signature that the fitter chops"; the width it was missing was the comment's |
| `SK-FUZZ-0012` | crash — a target-typed `new` whose target is a **delegate** type, carrying a LINQ query in its object initializer, threw `IndexOutOfRangeException` out of the *arrangement pipeline* and took the process with it | ⚠ **the throw is Roslyn's and the defect is ours.** `Func<int> v = new () { P = (from item in items select null) };` makes `MemberSemanticModel.GetLowerBoundNode` index an empty bound-node list, out of a plain `SemanticModel.GetSymbolInfo` on a node of the model's own tree — `PredefinedTypeRule` calls it, and there is no version of that call that can know in advance which node will do it. Two guards, because the first was not enough: a rule that throws is now skipped and reported as `SK9095` (the sibling of `SK9030` "analyzer threw"), and `ArrangementSafety.Check` — the layer whose whole job is to stop a bad rewrite reaching disk, and which was itself the thing taking the process down — now answers **revert** when its re-bind throws, because an unanswered safety question is not a safe one |
| `SK-FUZZ-0013` | arrangement idempotency — which rules fire depended on how the author had spaced a dotted `using` name | the removable-usings set is Roslyn's `CS8019` keyed by `Name.ToString()`, which carries the trivia *between* a qualified name's tokens: `using  System .Threading. Tasks;` keyed as `"System .Threading. Tasks"`, the set is computed once before the pipeline, and the formatter rewrites exactly that spacing on its first pass. So pass 1 offered the removal, pass 2 could no longer match its own key, and the *next* pipeline run — which recomputes the set — removed a using the first had left. Both sides now key on the name with whitespace dropped. ⚠ **What that was masking is worth more than the bug.** With the key fixed, `NamespaceBodyRule` (SK0213) and the removal (SK0210) fire together on this file and the re-bind reports `CS1027: #endif directive expected`, so safety layer 2 reverts the arrangement whole — while either rule *alone* is safe on it. The file is now stable and arranged **less than it should be**, which is layer 2 behaving as designed and is also why "the file converged" is not the same as "the file was arranged". Not diagnosed further; the reproduction is `pathological/unused-using-whose-name-carries-spaces.cs` |
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
