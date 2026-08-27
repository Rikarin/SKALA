# 04 — Formatting Engine

The part that has to be right, because it runs on every file, changes every file, and is trusted.

## The pipeline

```
SourceText
   │
   ├─▶ 1. Parse            Roslyn, LanguageVersion.Preview. Errors ⇒ SK9010, abort, write nothing.
   │
   ├─▶ 2. Arrange          (optional, needs semantics — doc 06) tree ⇒ tree
   │
   ├─▶ 3. Build document   syntax tree + trivia ⇒ Document IR
   │
   ├─▶ 4+5. Fit & emit     resolve every Group's mode against max_line_length while writing the
   │                       layout ⇒ TextChange[] against the ORIGINAL SourceText
   │
   ├─▶ 6. Verify           token-stream equivalence, input vs. applied output. Mismatch ⇒ fatal.
   │
   └─▶ 7. Write / report   apply, or print a diff, or return edits over LSP/MCP
```

⚠ **Steps 4 and 5 are one pass, and this document had them as two.** Whether a group fits is
`column + flatWidth <= width`, and the column is a function of the indentation stack, of pending
spaces and of every break taken so far — that is, of exactly the state the writer maintains. A
standalone fitting pass has to reproduce that state, and two implementations of an indentation model
that must agree to the column is the kind of duplication that produces a wrap which moves when
nothing moved. M2 resolves each group on entry, at the column the writer is actually at.

Steps 1, 3–7 need no `Compilation` and no project file. That is what makes `skala format` work on a
folder, in a git hook, on an agent's scratch directory, and at 4 708 files in eleven seconds — ⚠ a
number this document could not write until M3 made the loop parallel (doc 13 § "Parallelism"). It is
also why `#if DEBUG` bodies are frozen, which is SK-DIV-0004 and the strongest argument for the
project loading of doc 07 reaching `format`.

## The document IR

Language-agnostic, in `Rikarin.Skala.Formatting`, deliberately small:

```csharp
abstract record Doc;

record Text(string Value, int Width) : Doc;              // a token's text; Width ≠ Length for tabs/wide chars
record Concat(ImmutableArray<Doc> Parts) : Doc;
record Space(SpaceKind Kind) : Doc;                      // Required | Forbidden | Preserve
record Line(LineKind Kind) : Doc;                        // Hard | Soft | Blank(n) | Preserve
record Group(Doc Body, GroupMode Mode, GroupId Id) : Doc;
record Fill(ImmutableArray<Doc> Items) : Doc;            // wrap_if_long — ⚠ a flag on a break point
record Indent(Doc Body, IndentKind Kind) : Doc;          // Block | Continuous | None | Outdent | Align
record IfBroken(GroupId Of, Doc Then, Doc Else) : Doc;   // trailing commas, `=>` placement, …
record Verbatim(string Text) : Doc;                      // raw strings, disabled #if regions, off-tag spans
record Anchor(SourceSpan Source, int TokenId) : Doc;     // maps output back to input for edits + verify
```

⚠ `Anchor` carries a span and an opaque id, **not** a `SyntaxToken`. `Rikarin.Skala.Formatting` may
not reference Roslyn ([02](02-repository-layout.md)), because this IR and this fitting pass are what
the HTML and CSS front ends reuse ([14](14-web-languages.md)); the C# front end keeps its own token
table and looks ids up in it. ⚠ That also rules out `TextSpan`, which this document named until M1
pointed out it lives in `Microsoft.CodeAnalysis.Text` — hence `SourceSpan`, Skala's own, with the
conversion in the C# front end.

⚠ **The records above are a notation, not the representation.** [13](13-performance.md) § "The
fitting pass" requires `Doc` nodes to be structs in a per-file arena indexed by `int` — a 1 000-line
file produces ~40 000 nodes and the corpus ~110 M, which as class instances is several GB of garbage
per run. Read the shapes here for what the IR *means* and the arena for what it *is*.

`Anchor` is what makes step 5 a *minimal edit* rather than a rewrite: every `Text` is traceable to
the source span it came from, so a run of output that matches the original bytes exactly produces no
edit.

⚠ **`Fill` is not a node, and M3 established that it cannot be one.** A fill's delimiters and its
item separators do not behave alike: `wrap_array_initializer_style = wrap_if_long` puts the `{` at
the end of the opening line and the `}` on a line of its own *whenever the initializer wraps at
all*, and fills only the gaps between elements. A `Fill` node would have to either swallow the
braces — producing `new[] { "aaa",` — or exclude the elements from the group that decides whether it
wraps. It is a flag on a break point (`LineFlags.FillPoint`) instead: the group decides whether the
construct wraps, and a fill point decides on its own whether what follows it fits.

⚠ **`Align` is real from M3.1, and it made the writer simpler rather than more complicated.** The
node this document reserved for column alignment went unused through M1–M3 (SK-DIV-0008), and
implementing it needed one change: **the writer's indent stack holds columns rather than levels.**
After that an alignment scope is a *block* scope whose column happens not to be a multiple of the
indent width — "absolute, and nothing below it applies" is what a block already means — and no new
stack semantics were required at all. The conversion on its own is byte-for-byte neutral over the
whole corpus, which is how it was checked before anything was wired to it.

⚠ And the property this document's alignment section worried about **survives**: "with column
alignment off, laying out line *n* never requires knowing the contents of line *n−1*". An alignment
scope's column is the column the writer is *already at* when the scope opens, which is on the current
line. The fitting pass is still linear.

### Three-state groups

This is the concrete form of ADR-002 and the thing that distinguishes Skala from every
Prettier-lineage formatter.

```csharp
enum GroupMode {
    Flat,      // never break        — the group fits, or the option says one line
    Break,     // always break       — chop_always, or a rule demands it
    Auto,      // break iff too wide — chop_if_long: classic Prettier group
    Preserve,  // ← the third state: broken iff it was broken in the source, subject to width
    Owner,     // ← the fourth: broken iff the group it names resolved broken
}
```

⚠ **"Subject to width" is two facts, not one, and this document had it as one.** It runs in both
directions and the export wants a different direction per construct family:

| Fact | Means | Example |
|---|---|---|
| `BreaksIfTooLong` | the group may **add** a break the author did not write | `chop_if_long` chops a call that does not fit even though it was written on one line |
| `JoinsIfFits` | the group may **remove** one the author did write | `keep_existing_expr_member_arrangement = false` re-joins `int P =>\n 1;` |

Neither is the other's default. Giving an expression body the argument list's rule — break after
`=>` whenever the declaration is over 120 — costs 0.24 points of line fidelity on `corpus/real/`,
because the oracle wraps such a line at a different point and Skala's break lands one line away from
the oracle's. *Which* of a line's candidate points to wrap at is `prefer_wrap_around_eq`'s job; M3
implements it as `PrefersOuterBreak` and the rule is under "The fitting algorithm" below.

⚠ **A group certain to break has no flat form, and that is true of a `Preserve` group too.**
`GroupMode.Break` already reported ∞; a `Preserve` group whose source was broken at its own points
and which may not re-join is just as certain, and whatever contains it has to know — the oracle chops
`Report(Diagnostic.Create(` into two lines as soon as the inner call is broken, although the outer
call is 59 columns wide. ⚠ On **delimited lists only**, and that is measured rather than tidy: an
expression body's arrow is resolved against its whole flat width, so an unbreakable body would break
every such arrow and `bool Property(object o) => o is { … };` would lose its first line.

⚠ **The two directions are also measured against different widths.** Joining needs the whole flat
width, because the join puts all of it on one line; breaking needs only the width up to the group's
first unavoidable break, because the line was going to end there anyway. `P => new Thing {\n … };`
is not improved by a break after the `=>`, and `M() =>\n from x in y\n select x;` is not joinable
even though its first line fits.

`Preserve` is selected when `resharper_keep_user_linebreaks = true` (it is) and the construct has no
option forcing otherwise, or when a `resharper_keep_existing_*_arrangement` key is `true`.
Resolution of a `Preserve` group:

1. Look at the *original* source span of the group. Did it contain a line break at any of this
   group's break points? ⚠ At **its own** break points — not "somewhere inside it".
   `var n = aaa +\n bbb;` and `var n = aaa\n + bbb;` are both breaks inside the same binary chain,
   and the oracle removes the first and keeps the second, because `wrap_before_binary_opsign = true`
   makes only the gap before the operator a break point of that group. A containment test cannot
   tell them apart, and M1's `ContainsSourceBreak` was a containment test.
2. If **no**: try `Flat`. If it does not fit in `max_line_length`, fall through to the group's
   configured wrap style (`chop_if_long` ⇒ `Break`, `wrap_if_long` ⇒ `Fill`).
3. If **yes**: keep the author's break points, and additionally break any point where the resulting
   line still does not fit. ⚠ It does **not** re-flow the author's breaks away unless `JoinsIfFits`
   says it may — that is what `keep_user_linebreaks = true` means, and getting this wrong turns a
   formatting run on Vixen into a 1.35 M-line diff. ⚠ `keep_user_wrapping` is **not** the key that
   means it: measured against the oracle, setting `keep_user_wrapping = false` while
   `keep_user_linebreaks = true` changes nothing on any shape tried. It is Tier D with that reason.

The corollary that surprises people: Skala will leave a 40-column call broken across four lines if
that is how it was written. That is correct. `chop_always` is how you ask for the other thing.

⚠ **`--reflow` does not exist**, and this paragraph described it twice as though it did — "a
deliberate, occasional, reviewed operation, never the default and never in a hook". Nothing in the
CLI has ever accepted the flag; until M9 it was silently swallowed as a path glob, so
`skala format --reflow` reported "0 files would be reformatted" and exited 0, which reads as
success. It is now a configuration error that names the token.

The gap it names is real: `keep_user_linebreaks = true` means a badly wrapped file stays badly
wrapped, and there is no way to ask Skala to re-wrap it. `--reflow` would be `format` with
`keep_user_linebreaks` and `keep_user_wrapping` forced off for one run — `skala format --option
resharper_csharp_keep_user_linebreaks=false` is the spelling that works today. Whether that deserves
a flag of its own is undecided; what is decided is that this document stops claiming it has one.

### Mapping the ReSharper enums

| Option value | Group construction |
|---|---|
| `chop_always` | `Group(Break)`, one `Line(Hard)` per item |
| `chop_if_long` | `Group(Auto)` |
| `wrap_if_long` | `Fill` |
| `keep_existing_* = true` | `Group(Preserve)` whose *delimiter* break points survive; the gaps between items are `keep_user_linebreaks`'s (see [05](05-csharp-formatting-rules.md)) |
| `place_*_on_single_line = if_owner_is_single_line` | `Group(Owner)`, or — where owner and construct coincide, which is all five keys in the export — a `Preserve` group over the owner |
| `max_*_on_line = n` | `Group(Break)` when item count > n, else as configured |

`if_owner_is_single_line` (used by five `place_expr_*` keys in the export) is the reason `Group`
carries an `Id` and `IfBroken` references one: a child's layout depends on a parent's resolved mode.

⚠ **That does not make fitting two traversals, and this document said it did.** In all five keys the
owner is the child's syntactic *ancestor* — a declaration owning something inside that declaration —
so a depth-first walk already resolves owners before children. The walk order gives every property
the second pass was there to give: owners first, children read the owner's resolved mode, a child may
only move Flat → Broken, and termination follows from the shape rather than from a convergence
argument. The fitter counts the cases where the invariant does not hold
(`Fitter.OwnerUnresolved`) rather than guessing at them; it is zero for every document the C# front
end produces, and a test asserts it.

## Indentation

`resharper_continuous_line_indent = single`, `resharper_use_continuous_indent_inside_parens = true`,
`resharper_use_continuous_indent_inside_initializer_braces = true`,
`resharper_indent_wrapped_function_names = false`, and every `resharper_indent_nested_*_stmt = false`.

Two kinds of indent, and conflating them is the classic formatter bug:

- **Block indent** — one level per `{ }`, per `case`, per embedded statement. Governs statements.
- **Continuous indent** — applied to *continuation lines of one expression*: a wrapped argument list,
  a wrapped binary chain, a wrapped initializer.

⚠ **This document originally said `single` means one continuation level regardless of depth. That is
wrong**, and M1 established it against the oracle the expensive way — it was the single largest source
of divergence on the Vixen run. `= single` sets the *size of one level*; it does not mean there is
only ever one. The oracle's actual behaviour is five rules:

| Construct | Spends a level? |
|---|---|
| **Grouping** parenthesis (a `(expr)`, a statement's condition) | Yes — always, even beside another scope on the same line |
| Other delimited group (argument list, brackets, initializer braces) | Yes — one per *opening line*, so several on one line are one step |
| Undelimited continuation | No — one per line at most, and none where a delimited scope inside it already paid |
| Chained method call | Yes, its own |
| Binary **expression** chain | No |
| Binary **pattern** chain | Yes, its own — except as a statement's condition |
| A **ternary** | Yes, its own — ⚠ unless it is part of a *chain* of ternaries, which takes none |
| A lambda's expression body | ⚠ Its own continuation *context*, which is not the same as a level |

The distinction in the binary rows looks arbitrary and is not: it is what `jb cleanupcode` produces,
it is pinned by fixtures in `constructs/indentation/`, and a formatter that "rationalises" it
diverges on real code immediately.

⚠ **M3.1 added the last three rows and corrected one.**

- **A chained method call takes its level even inside another continuation.** M3 gated it on "is any
  other continuation open", which is right for an undelimited one and wrong here, so an
  expression-bodied member whose arrow had broken came out with its chain flush against its receiver.
- **A chain of ternaries takes none.** `align_ternary = align_not_nested` and `nested_ternary_style =
  autodetect` between them make `cond ? a : cond ? b : c` a flat list of lines rather than a
  staircase, and it is the shape people write. A ternary that is *not* part of a chain still takes
  its level.
- **A lambda body is a continuation context of its own**, which is a different thing from a level: it
  resets "is a continuation already open" so that a chain or a binary chain inside it may take one,
  and the level itself still obeys the one-per-opening-line rule. ⚠ The reset is deferred to the
  frame's first piece, because the break that lands just *before* the lambda belongs to whatever
  encloses it — and the deferral was being undone by the lambda's own parameter, which opens a frame
  of its own and put the enclosing depth back on the way out.

⚠ **The first row is M3's correction, and the second and third are its other half.** "One level per
opening line" was M1's rule and it is really three, which sweeping the oracle separates cleanly:

```csharp
if ((expr                  ← two levels, one per parenthesis, both opened on the same line
        == value))
[Attr(                     ← one. The bracket and the argument list are one step.
    argument
)]
var d = Drawn(             ← one. The `=` does not pay for what the parenthesis pays for.
    argument
);
```

Dropping either half costs about 1.9 points of line fidelity on `corpus/real/`, in opposite
directions: without the first, `if ((… == …))` puts the operand one level short — 176 lines across
26 files; with the first but without the third, `[Attr(` and `var d = Drawn(` both gain a level they
should not have.

⚠ **A sole lambda argument's parenthesis is a grouping one for this purpose.**
`place_single_method_argument_lambda_on_same_line = true` keeps the lambda on the call's line, so
that parenthesis never gets a line of its own and would otherwise be collapsed into whatever the
lambda's body opens:

```csharp
messages.Any(message => message.Contains(
        "…"          ← two levels, from `Any(` and from `Contains(`
    )                ← one, back to `Contains(`'s opener
);
```

⚠ **The level a scope nests *from* is not the level a line starting now takes**, and the two differ
by exactly the scope that opened on the current line. A closing delimiter and a block's own level
both ask the first question; every line start asks the second.

⚠ **A binary pattern chain's own level is not spent as a statement's condition**, where
`align_multiline_statement_conditions = true` puts the alignment and the continuation level at the
same column: `if (o is IDisposable
    or IAsyncDisposable)` is one step where the same chain as an
argument takes two.

ReSharper's `double` and `resharper_continuous_indent_multiplier` change the level size and are Tier
A too, but the export uses `single`, so that is the path with fixture coverage.

⚠ **A continuation scope belongs to the construct, not to the break.** M1 opened it lazily, at the
first break that needed it, and closed it at the enclosing statement. That is fine while the
document's stack holds nothing but indent scopes; once groups are on the same stack the two
interleave, and a group that closes before the statement does pops the indent instead of itself.
M2 opens the scope inside the group that owns the break points and closes it there — and only where
`_continuousDepth == 0` and no enclosing frame has already spent its level, which is what keeps
`M(\n a\n + b)` at the parenthesis's one level rather than two.

Nested-statement outdenting (`indent_nested_for_stmt = false`) means

```csharp
for (var i = 0; i < n; i++)
for (var j = 0; j < n; j++) {
    …
}
```

stays flush rather than stair-stepping — a real transformation, not a no-op, and one of the few
places where the formatter *removes* indentation the author wrote.

## Trivia — where formatters actually break

Roslyn attaches trivia to tokens (leading/trailing) with rules that are subtle. Skala re-associates
trivia into an explicit model before building the document, because "which token owns this comment"
must be a decision Skala makes, not one it inherits.

| Trivia | Handling |
|---|---|
| End-of-line comment | Attached to the *preceding* token. `resharper_space_before_trailing_comment = true` inserts exactly one space; `space_before_trailing_comment_text = false` leaves `//x` alone. ⚠ A trailing comment makes its line unbreakable after the comment — the fit algorithm must treat it as infinite-width tail, or it will "fix" a long line by moving code onto the comment's line. |
| Own-line comment | `resharper_stick_comment = true`: a comment immediately above a declaration binds to it and moves with it; blank-line rules see the comment as part of the member. `place_comments_at_first_column = false`: indent with the code. |
| XML doc comment | ⚠ **Not formatted by default, and that is the measurement rather than the schedule.** `jb cleanupcode` does not touch documentation comments — not the missing space after `///`, not a 128-column summary, not two `<param>` tags on one line — with the export's whole `resharper_xmldoc_*` family in force (SK-DIV-0006, re-verified at 2025.2.6 by `constructs/trivia/a-malformed-doc-comment-is-left-alone.cs`). A comment that is not well-formed XML is reported at `hint` (`SK0003`) and left exactly as written, under every setting. ⚠ **`skala format --xmldoc` turns on the sub-formatter**, which re-wraps them and honours 17 of the 27 `resharper_xmldoc_*` keys; it is off by default because on is a divergence from Rider on every doc comment in every repository, and it is worth 3.59 points of line fidelity when on. `<code>` and `<c>` are emitted verbatim, and no comment is written unless its content survives a round trip through `XmlDocSignature`. |
| `#region` / `#endregion` | `resharper_indent_preprocessor_region = usual_indent` — indented like code. `blank_lines_inside_region`, `blank_lines_around_region` apply. Regions do not affect grouping. |
| `#if` / `#else` and **disabled text** | ⚠ The dangerous one. Roslyn parses the inactive branch as `DisabledTextTrivia` — an unstructured string. Skala emits it `Verbatim`, byte-for-byte, and *never* reindents it. `resharper_indent_preprocessor_if = no_indent` puts the directives at column 0. A construct whose braces are split across a `#if` (`#if X` … `{` … `#else` … `{` …) is detected and the whole member is emitted `Verbatim` with `SK9011` (info): "not formatted, unbalanced preprocessor structure". Silently doing something clever here is how formatters destroy code. |
| `#pragma`, `#nullable`, `#line` | Own line, no indent change, no grouping effect. Between attributes and a member they suppress attribute-placement rules for that member. |
| Formatter tags | `resharper_formatter_tags_enabled = true`, `off_tag = @formatter:off`, `on_tag = @formatter:on`. A comment containing the off tag starts a `Verbatim` span that ends at the on tag or at end of file. `formatter_tags_accept_regexp = false` ⇒ literal match. This is the escape hatch, and it must work on the first attempt or people stop trusting the tool. |
| Raw string literals (`"""`) | ⚠ **Shifted, not re-indented.** `resharper_indent_raw_literal_string = align` moves the content to the opening quotes' column, and the transformation that cannot be got wrong is a *uniform shift*: C# strips the closing delimiter's own whitespace prefix from every line, so moving every interior line and the closing delimiter by the same number of columns leaves the stripped result identical, character for character. Re-indenting the lines independently, or moving the content without the delimiter, changes what the program prints. Tier A for the uninterpolated token; an interpolated raw string is a run of tokens with expressions between them and stays `Verbatim` (SK-DIV-0003). |
| Blank lines | Not trivia in the IR: `Line(Blank(n))`, computed from the blank-line option set (below). |

### Blank lines

53 `resharper_blank_lines_*` keys, of which ~30 apply to C#. They form two independent systems that
must be resolved in a fixed order:

1. **Caps.** `keep_blank_lines_in_code = 2`, `keep_blank_lines_in_declarations = 2` — the author's
   blank runs are truncated to n, never extended.
2. **Requirements.** `blank_lines_around_type = 1`, `around_invocable = 1`, `around_field = 1`,
   `around_property = 1`, `around_single_line_invocable = 0`, `after_using_list = 1`,
   `after_file_scoped_namespace_directive = 1` — a minimum inserted where absent.
3. **Removals.** `remove_blank_lines_near_braces_in_code = true`,
   `…_in_declarations = true` — blank lines immediately after `{` or before `}` are deleted, and
   this wins over (2).

Order: removals ∘ requirements ∘ caps, evaluated on the *gap between two members*, with the gap
attributed to the member below (so `stick_comment` moves the right blank lines with the comment).

⚠ **"Single-line" is a property of the output, and the requirements branch on it.** Half the
`blank_lines_around_*` family has an `_around_single_line_*` twin, and a 140-column field is single
line in the source and four lines after the fitter has had it — so reading the answer off the input
makes the first pass emit no blank and the second emit one. Milestone 1 never broke a line, so the
question never arose; milestone 2 answers it by predicting: a member is single-line iff its source
occupied one line, nothing inside it is certain to break (a `chop_always` group, an attribute the
placement rules will move), its width fits, and it does not share its line with the member before it.
⚠ The width must be measured from the *tree* — the member's own span plus the indentation its nesting
implies — and not from the source line, or an indentation-only mutation changes the answer and
`format(mutate_whitespace(x)) ≡ format(x)` stops holding.
✅ Verified against the oracle on `constructs/blank-lines/*`, which is 90 files, because this is the
area where hand-reasoning is least reliable.

## The fitting algorithm

Wadler-shaped, iterative rather than recursive (C# has no TCO and files nest 30 deep), two passes
per group tree because of `if_owner_is_single_line`.

```
build(tree):    per node, accumulate flatWidth, headWidth, pointWidth and afterPoint as the arena
                is filled — ⚠ the measure pass is FUSED into the build (doc 13), not a traversal of
                its own
fit+emit(doc, width):
  walk depth-first, maintaining (column, indentStack), and on entering each Group:
            Group(Flat)     -> flat
            Group(Break)    -> broken                        (and its flatWidth is ∞, so nothing
                                                              containing it can be flat either)
            Group(Auto)     -> flat if column + headWidth + trailing <= width else `worth`
            Group(Preserve) -> sourceBroken ? (joinsIfFits && whole group fits ? flat : broken)
                                            : (breaksIfTooLong && !fits ? `worth` : flat)
            Group(Owner)    -> the owner's resolved mode; the owner is an ancestor, so it is known
      a fill point         -> broken iff what follows it, up to the next fill point, does not fit
```

⚠ **Four widths, not two, and M3 needed every one of them.**

| Measure | Stops at | The question it answers |
|---|---|---|
| `flatWidth` | nothing | can the whole group go on one line — the test for *joining* |
| `headWidth` | the first **certain** break | how much lands on this line whatever happens |
| `pointWidth` | the first break **point**, optional ones included | how much lands on this line if the construct inside wraps |
| `afterPoint` | the point after this group's own first | what follows this group's own break, up to the next |

`headWidth` and `pointWidth` differ exactly where a construct's only breaks are optional: for
`= new Dictionary<…> { a, b }` the head is the whole thing and the point width is
`= new Dictionary<…> {`, which is what the ordering rule needs and what the head cannot say.

⚠ **A group is not the line it lands on.** Every fit is `column + width + trailing`, where `trailing`
is what remains of the line after the group ends, up to the next break. `var f = new Thing { A = 1,
B = 2, C = 3 };` at 121 columns is the shape that proves it: the initializer's group covers `{ … }`,
is entered at column 26, measures 94, concludes 120 and stays flat — and then the semicolon that is
not in it makes the line 121. Every construct that ends before its statement does has the same blind
spot, and closing parentheses, semicolons and commas are exactly what follows the constructs that
wrap. It is Prettier's `fits(next, restCommands)`; the walk's own stack already holds it.

⚠ **A break point's own flat rendering is not part of `trailing`.** The measure is "the rest of this
line assuming every break point is taken", and if the point is taken the line ends there and the
space it would have rendered as is never written. Counting it made this measure one column larger
than the one a fill point uses on the same gap, and the two disagreeing is a *non-idempotency*: the
fill keeps an item on the line, the item's own group finds itself one column over and breaks, and the
second pass sees a multi-line item and breaks before it. Two files out of Vixen's 4 708 did exactly
that, and no corpus file did.

### The ordering rule

⚠ `worth` above is milestone 3's substance, and the thing milestone 2 left out on purpose. A group
that does not fit does not therefore break: it breaks when its own break is the one worth taking.
Two questions, in order:

1. **Does this break alone finish the job?** If what follows the group's own first break point fits
   on a continuation line, take it — two lines beat the three that wrapping something inside would
   cost. `JsonObjectContract c =
    (JsonObjectContract)r.ResolveContract(typeof(T));` is that
   case; chopping the argument list instead produces a third line the oracle does not write.
2. **Otherwise, does the line end here anyway?** Something inside is going to wrap, so the current
   line runs to the first break point whether this group breaks or not. If that much fits, this
   group's break buys a line and gains nothing: `schema.Properties = new Dictionary<…> {` is the
   oracle's first line, not `schema.Properties =`. If it does *not* fit — the call's own name runs
   past the margin — then both breaks are needed and this one is taken.

⚠ The budget in question 1 is **not** `max_line_length`, and SK-DIV-0005 records the measurement and
the counter-example. It is a local rule and not a search; the paragraph below still holds.

⚠ **Question 2 had never actually run, from M3 until M3.1.** It is answered from `afterPoint`, and
`afterPoint` was zero for every group in the family the rule exists for. `MeasureSegments` looks for
a group's own break points among its **direct children**, and a group that spends a continuation
level opens the indent scope *inside itself* — so the `=` family's only break point is a grandchild
and the scan found none. Zero then makes question 2 answer "yes, the line ends here" unconditionally,
so the `=` break was taken when it finished the job and never otherwise, including when nothing
inside the right-hand side could wrap at all:

```csharp
const string CallableDirectiveRegex = @"^(?<directive>audit-to|…){0,1}$";   // 163 columns, left whole
```

The same scan sets `segment`, which is what a fill point asks, so a `wrap_if_long` list broke at its
delimiters and then ran off the right margin without ever breaking between items. Two of the fitter's
four measures were consistently zero, and **no property caught it**: the output was idempotent,
token-equivalent, deterministic and stable, and simply not what the oracle writes.

⚠ Two corrections the general scan then needed, both found by measurement: a break the *rules*
require ends a segment rather than making it infinite — a list pattern whose items the author pinned
one per line has hard lines between the fill's own points, and measuring one of those as infinitely
wide breaks the fill point in front of it — and `IfBroken` is not spliced, because its flat width is
one branch's rather than the sum of both.

Complexity is O(n) in document nodes, in one traversal.
No backtracking, no search. The optimal-layout algorithms (Yelland/Bernardy, "A Pretty Expressive
Printer") give better output on adversarial input at super-linear cost; they are rejected because
ReSharper is not optimal either, and matching ReSharper is the requirement.

⚠ **The column is the column, not the width.** `TextWidth.Measure` answers "how many columns does
this text occupy", and the writer needs "which column am I at afterwards" — the two differ for text
that spans lines, where the answer is the last line's width rather than the sum. M1 assigned the
width to its column and nothing read it back; M2's fitter reads it on every group, and the mistake
showed up as a 126-column line the formatter thought was 72. It was worth 1.4 points of line
fidelity on `corpus/real/`.

**Width is measured in columns, not chars.** `Text.Width` accounts for tab expansion
(`alignment_tab_fill_style = use_spaces`, so output has none, but input may) and for wide/combining
characters — a CJK identifier or an emoji in a string literal must not silently blow the budget.
Grapheme-cluster width via `System.Globalization`, computed once at `Text` construction.

**Unfittable lines are left long.** If a group is fully broken and a line still exceeds the width —
a 200-character string literal, a deeply-qualified generic type — Skala emits it and moves on. It
never breaks a token, never breaks inside a string, and never emits a diagnostic for it by default
(`SK0002` at `hint` for the audit).

## Emitting minimal edits

Walk the resolved layout and the original text in lockstep, using `Anchor` tokens as sync points.
For each maximal region where output bytes equal input bytes, emit nothing. Elsewhere, emit one
`TextChange` spanning the smallest range that differs, snapped outward to token boundaries so that
edits never land inside a token.

Consequences worth stating: `--check` is "did we produce any edits", **exit code 2**
([09](09-quality-gates-and-reporting.md) § "Exit codes"), no writes;
`--diff` is a unified diff over the edits; LSP `textDocument/formatting` is the edit list converted;
`--range a:b` filters to edits intersecting the range *after* full-file fitting, which is the only
way range formatting can be consistent with whole-file formatting.

File-level concerns are applied last, as edits like any other: final newline
(`resharper_csharp_insert_final_newline = true` wins over `[*] insert_final_newline = false`, see
[03](03-configuration-model.md)), trailing whitespace (`remove_spaces_on_blank_lines = true`),
line endings (`end_of_line = lf`, and `resharper_enforce_line_ending_style = false` means mixed
endings are *preserved* rather than normalised — ⚠ note the contradiction with `[*] end_of_line`,
resolved the same way as the others and reported once), BOM (preserved exactly; never added, never
removed).

## The safety net

Before any write, for every file:

```csharp
static bool IsEquivalent(SourceText before, SourceText after) =>
    Tokens(before).SequenceEqual(Tokens(after), TokenComparer.Significant);
// Significant: (RawKind, ValueText) for every non-trivia token, plus the ordered sequence of
// comment texts (normalised for the intentional xmldoc rewrap), plus every preprocessor directive
// in order, plus every disabled-text block verbatim.
```

⚠ **"Normalised for the intentional xmldoc rewrap" was not true until the sub-formatter shipped, and
it is now true in a narrower way than it sounds.** The normalisation is per-line trimming plus the
one space after a marker, which no re-wrap survives, because a re-wrap moves the line breaks. The
allowance exists only when `--xmldoc` actually re-wrapped something in this file, applies only to
`///` trivia, and is `XmlDocSignature` — the same boundary the sub-formatter itself refuses to
cross — rather than "comments are exempt" or "the words in order". Both of the weaker readings would
have to be widened again for `space_before_self_closing` and again for `spaces_inside_tags`, and
each widening is a class of damage the net stops seeing. The signature is *tighter* than the old
comparison where it counts: a `<code>` body is compared byte-for-byte, which it never was before.

A failure is a Skala bug by definition. It aborts the file, writes nothing, emits `SK9099` (error),
and drops `.skala/crash/<hash>/{input.cs,output.cs,config.snapshot}` — a ready-made regression test.
Cost measured on the corpus: one extra parse, ≈ 15 % of format time. ⚠ There is no flag to turn it
off. A user who discovers `--no-verify` in a hurry is a user who ships a corrupted file.

The parallel property, enforced in CI rather than at runtime: **idempotency**. `format(format(x))`
must produce zero edits, for every file in the corpus, every commit. Any oscillation — the classic
being a blank-line rule fighting a brace rule — is a build break.

## What the engine does not do

- **It does not reflow prose in ordinary comments.** Only xmldoc, only under `format --xmldoc`, and
  only because `xmldoc_wrap_lines = true` asks for it — the key alone is not enough, because the
  oracle sets it and ignores it (SK-DIV-0006).
- **It does not sort or move members.** Member ordering is arrangement, is a semantic change, is
  never part of `format`, and is not in the export's option set anyway.
- **It does not touch generated files.** `*.g.cs`, `<auto-generated>` headers, and anything matched
  by `skala.jsonc`'s `generated` block are skipped by default, reported as skipped in `--verbose`.
- **It does not format code it could not parse.** Ever. (ADR-003)
