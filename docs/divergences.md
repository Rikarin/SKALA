# Divergences from the oracle

`jb cleanupcode` is the conformance oracle (ADR-011), not a master. Where Skala deliberately
differs, the difference gets an `SK-DIV-` number and the argument for it lives here. The count is
published alongside the fidelity number, because **a divergence is a decision and an unexplained
difference is a bug, and the harness cannot tell them apart without this file**
([12](plan/12-conformance-and-testing.md) § "Where the oracle is wrong").

Format: `## SK-DIV-nnnn — one line`, then the argument, then the option keys it touches.

At milestone 3.1, `corpus/real/` is **99.70 %** of lines and **85.79 %** of files identical to the
oracle over 380 files and 76 375 lines, with the oracle's own preprocessor symbols supplied —
**99.63 % / 85.26 %** without them. ⚠ Both numbers are reported because both are true of a real
invocation: `skala format` on a loose file has no symbols and `skala format --load=binlog` has them,
and `./build.sh Fidelity` prints the pair.

| Files | Line fidelity | File fidelity | What the residue is |
|---|---:|---:|---|
| containing a `#if` (91) | 99.36 % | 72.53 % | SK-DIV-0001, SK-DIV-0004 and ordinary tail |
| containing a raw literal (11) | 99.68 % | 90.91 % | SK-DIV-0003's interpolated half |
| neither (289) | **99.79 %** | 89.97 % | SK-DIV-0005 and SK-DIV-0011, mostly |

⚠ **The revised milestone-3 bar of ≥ 99.5 % on files with no `#if` is met at 99.79 %. The ≥ 99.9 %
overall bar is not met at 99.70 %,** and the entries below are what stands between the two: about
230 divergent line slots across 51 files, of which roughly a tenth are inside a conditional branch
neither tool compiles and the rest are the wrapping tail.

The trajectory, so that "asymptotic" is a measurement rather than an adjective:

| | line | file | corpus |
|---|---:|---:|---|
| M1 | 85 % bar | — | 380 files |
| M2 | 97.47 % | 49.47 % | 380 files |
| M3 | 98.86 % | 71.05 % | 380 files |
| M5 | 98.93 % | 71.58 % | 380 files, symbols supplied |
| M3.1 | **99.70 %** | **85.79 %** | 380 files, Vixen sample re-based |

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

Measured on `corpus/real/`: 141 lines across 73 files at M2; **18 lines across 17 files** at M3.1 —
11 where Skala keeps a blank line the oracle removed and 7 where the oracle inserts one Skala does
not. The class shrank because the rest of the tail shrank around it, not because it changed.

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
**99.68 %** of lines and **90.91 %** of files at M3.1, over the re-based sample's 11 such files.

⚠ **C# 11 made this reachable from ordinary code and it broke a property test rather than the
formatter.** A newline is legal inside an interpolation hole, so a multi-line interpolated string is
now something people write; `PropertyTests.MutateIndentationOnly` walked into one — it is a run of
tokens rather than one token, so the per-token guard missed it — and added whitespace that neither
Skala nor the oracle absorbs. The mutation now leaves the whole expression alone, the same way it
already left raw strings and disabled text alone.

- options: `resharper_csharp_indent_raw_literal_string`

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

- options: none
- commands: `skala format --define`, `skala format --load=`, `fidelity preprocessor`

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
break and chops the call instead. That shape and its siblings are **64 lines across 38 files** of
the residue, which is still the largest single class.

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

- options: `resharper_space_after_triple_slash`, `resharper_xmldoc_wrap_lines`, `resharper_xmldoc_max_line_length`, `resharper_xmldoc_linebreak_before_elements`, `trim_trailing_whitespace`

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

- options: `resharper_csharp_wrap_arguments_style`, `resharper_keep_user_linebreaks`

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

The consequence [05](plan/05-csharp-formatting-rules.md) § "Alignment" claims — "with column
alignment off, laying out line *n* never requires knowing the contents of line *n−1*" — **survives
the change**, and that is worth stating: an alignment scope's column is the column the writer is
already at when the scope opens, which is on the current line. The fitting pass is still linear.

- options: `resharper_csharp_align_multiline_statement_conditions` (now Tier A), `resharper_csharp_align_multiline_for_stmt`, `resharper_align_multiline_array_initializer`, `resharper_align_multiline_type_argument`, `resharper_align_multiline_ctor_init`

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

- options: `resharper_space_within_spread_pattern`

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
these are `ClassDeclaration` and `IdentifierName`, which are not rare. Measured on `corpus/real/`:
**12 lines across 4 files**, of which 8 are one generated `DataSet` partial class and one is a
`class` keyword left alone at the end of a line.

- options: `resharper_wrap_before_first_method_call`, `resharper_csharp_wrap_multiple_declaration_style`

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
one. Worth **45 lines across 21 files**, which makes it the second largest class after SK-DIV-0005.

- options: `resharper_place_single_method_argument_lambda_on_same_line`

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
   Worth 4 lines across 2 files, both of them the same generated `DataSet`.

- options: `resharper_csharp_keep_existing_embedded_block_arrangement`, `resharper_csharp_align_multiline_for_stmt`

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
