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
folder, in a git hook, on an agent's scratch directory, and at 4 691 files in under twenty seconds.

## The document IR

Language-agnostic, in `Rikarin.Skala.Formatting`, deliberately small:

```csharp
abstract record Doc;

record Text(string Value, int Width) : Doc;              // a token's text; Width ≠ Length for tabs/wide chars
record Concat(ImmutableArray<Doc> Parts) : Doc;
record Space(SpaceKind Kind) : Doc;                      // Required | Forbidden | Preserve
record Line(LineKind Kind) : Doc;                        // Hard | Soft | Blank(n) | Preserve
record Group(Doc Body, GroupMode Mode, GroupId Id) : Doc;
record Fill(ImmutableArray<Doc> Items) : Doc;            // wrap_if_long
record Indent(Doc Body, IndentKind Kind) : Doc;          // Block | Continuous | None | Outdent
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
the oracle's. *Which* of a line's candidate points to wrap at is `prefer_wrap_around_eq`'s job and
belongs to milestone 3.

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
that is how it was written. That is correct. `chop_always` and `--reflow` are how you ask for the
other thing, and `--reflow` is a deliberate, occasional, reviewed operation, never the default and
never in a hook.

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
| Delimited group (parens, brackets, initializer braces) | Yes — one per *opening line*, so they nest |
| Undelimited continuation | No — however deep the expression, they collapse to one |
| Chained method call | Yes, its own |
| Binary **expression** chain | No |
| Binary **pattern** chain | Yes |

The distinction in the last two rows looks arbitrary and is not: it is what `jb cleanupcode`
produces, it is pinned by fixtures in `constructs/indentation/`, and a formatter that "rationalises"
it diverges on real code immediately.

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
| XML doc comment | Its own sub-formatter: `resharper_xmldoc_wrap_lines = true`, `xmldoc_max_line_length = 120`, `xmldoc_linebreak_before_elements = summary,remarks,…`. Parsed as XML with Roslyn's `DocumentationCommentTrivia`, re-wrapped, re-prefixed with `/// `. ⚠ Text inside `<code>` is `Verbatim`. |
| `#region` / `#endregion` | `resharper_indent_preprocessor_region = usual_indent` — indented like code. `blank_lines_inside_region`, `blank_lines_around_region` apply. Regions do not affect grouping. |
| `#if` / `#else` and **disabled text** | ⚠ The dangerous one. Roslyn parses the inactive branch as `DisabledTextTrivia` — an unstructured string. Skala emits it `Verbatim`, byte-for-byte, and *never* reindents it. `resharper_indent_preprocessor_if = no_indent` puts the directives at column 0. A construct whose braces are split across a `#if` (`#if X` … `{` … `#else` … `{` …) is detected and the whole member is emitted `Verbatim` with `SK9011` (info): "not formatted, unbalanced preprocessor structure". Silently doing something clever here is how formatters destroy code. |
| `#pragma`, `#nullable`, `#line` | Own line, no indent change, no grouping effect. Between attributes and a member they suppress attribute-placement rules for that member. |
| Formatter tags | `resharper_formatter_tags_enabled = true`, `off_tag = @formatter:off`, `on_tag = @formatter:on`. A comment containing the off tag starts a `Verbatim` span that ends at the on tag or at end of file. `formatter_tags_accept_regexp = false` ⇒ literal match. This is the escape hatch, and it must work on the first attempt or people stop trusting the tool. |
| Raw string literals (`"""`) | `Verbatim`, with the one exception of `resharper_indent_raw_literal_string`, which re-indents the *closing delimiter and the common prefix* — a transformation that changes the string's value if done wrong. Tier B until the fixtures cover interpolated raw strings with nested braces. |
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
build(tree):    per node, accumulate flatWidth and headWidth as the arena is filled
                — ⚠ the measure pass is FUSED into the build (doc 13), not a traversal of its own
fit+emit(doc, width):
  walk depth-first, maintaining (column, indentStack), and on entering each Group:
            Group(Flat)     -> flat
            Group(Break)    -> broken                        (and its flatWidth is ∞, so nothing
                                                              containing it can be flat either)
            Group(Auto)     -> flat if column + headWidth <= width else broken
            Group(Preserve) -> sourceBroken ? (joinsIfFits && whole group fits ? flat : broken)
                                            : (breaksIfTooLong && !fits ? broken : flat)
            Group(Owner)    -> the owner's resolved mode; the owner is an ancestor, so it is known
            Fill(items)     -> emit items, breaking before the first item that would overflow,
                               then continue on the new line (classic fill)
```

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

Consequences worth stating: `--check` is "did we produce any edits", exit code 1, no writes;
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

A failure is a Skala bug by definition. It aborts the file, writes nothing, emits `SK9099` (error),
and drops `.skala/crash/<hash>/{input.cs,output.cs,config.snapshot}` — a ready-made regression test.
Cost measured on the corpus: one extra parse, ≈ 15 % of format time. ⚠ There is no flag to turn it
off. A user who discovers `--no-verify` in a hurry is a user who ships a corrupted file.

The parallel property, enforced in CI rather than at runtime: **idempotency**. `format(format(x))`
must produce zero edits, for every file in the corpus, every commit. Any oscillation — the classic
being a blank-line rule fighting a brace rule — is a build break.

## What the engine does not do

- **It does not reflow prose in ordinary comments.** Only xmldoc, and only because
  `xmldoc_wrap_lines = true` asks for it.
- **It does not sort or move members.** Member ordering is arrangement, is a semantic change, is
  never part of `format`, and is not in the export's option set anyway.
- **It does not touch generated files.** `*.g.cs`, `<auto-generated>` headers, and anything matched
  by `skala.jsonc`'s `generated` block are skipped by default, reported as skipped in `--verbose`.
- **It does not format code it could not parse.** Ever. (ADR-003)
