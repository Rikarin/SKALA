# 05 — C# Formatting Rules

The catalogue: which `resharper_*` families exist, what each governs, what the export sets them to,
and the order they get implemented in. This is the working document for Milestones 1–3 — every entry
here becomes rows in `options.json` and files in `Testing/corpus/constructs/`.

Counts below are C#-relevant keys after excluding the C++/VB/XAML/HTML/Razor namespaces.

| Family | Keys | Doc IR mechanism | Needs semantics | Phase |
|---|---:|---|---|---|
| Spaces | ~90 | `Space(Required\|Forbidden)` | no | 1 |
| Blank lines | ~30 | `Line(Blank(n))` + the three-system resolution | no | 1 |
| Braces & single-line blocks | ~10 | `Line(Hard)` placement, `Group(Flat)` | no | 1 |
| Indentation | ~25 | `Indent(Block\|Continuous\|Outdent)` | no | 1 |
| Line breaks — required/forbidden | ~20 | `Line(Hard)` / `Space` / `Flat` gaps | no | 2 |
| Placement (`place_*`) | 20 | `Group(Auto)` keyed on owner | no | 2 |
| Wrapping (`wrap_*`) | 47 | `Group`/`Fill`, the fitting pass | no | 2–3 |
| Alignment (`align_*`, `int_align_*`) | 28 | `Align` | no | 3 (mostly off — see below) |
| `keep_existing_*` | 18 | `Group(Preserve)` — delimiters only; the gaps between items are `keep_user_linebreaks`'s | no | 2 |
| Attributes | ~8 | `Group`, `Line` | no | 2 |
| Comments & xmldoc | ~16 | sub-formatter, opt-in | no | 3 |
| Arrangement (`arrange_*`, body styles, `var`, qualifiers) | ~40 | tree rewrite | **yes** | doc [06](06-arrangement-and-syntax-styles.md) |

## Phase 1 — spaces, blanks, braces, indent

Nothing here needs the fitting pass. A Phase-1 Skala reformats a file correctly as long as no line
needs to move, which is already most of a `.cs` file, and it is the fastest route to a differential
harness that produces meaningful numbers. ✅ Measured: 94.4 % line fidelity, and a run over Vixen
that touched 0.58 % of the tree.

⚠ **Some keys listed under this phase can never be observed at this phase, and are Tier D with the
reason recorded rather than Tier A.** M1 found five: `blank_lines_inside_type` and
`blank_lines_inside_namespace` (`remove_blank_lines_near_braces` wins over both by the documented
ordering, so no input can distinguish them), `max_line_length` and `tab_width` (nothing wraps yet;
the writer emits spaces), `end_of_line` while `enforce_line_ending_style = false`, and
`remove_spaces_on_blank_lines` (the writer cannot emit trailing whitespace at all, so the option has
no off state). ⚠ `max_line_length` is Tier A from M3, because M3 is the phase where the column limit
is the whole point; the other four are still inert. M3 adds two of its own —
`space_in_singleline_method`, whose shape no longer exists in any output, and
`place_simple_list_pattern_on_single_line`, which `keep_existing_list_patterns_arrangement` outranks
on ReSharper's own defaults. An option that cannot change behaviour must not claim a tier that says it was
verified — that is the difference between the tier matrix being a progress bar and it being
decoration.

### Spaces

Ninety keys, each of which resolves one inter-token gap to `Required` or `Forbidden`. They are
mechanical, they are 90 unit tests, and they are where a differential run finds the silly bugs. The
export's C# answers, condensed:

```csharp
// after keywords in control flow, around binary/assignment/lambda/ternary operators,
// after commas and after `:` in inheritance and case labels — a space.
if (a > b) { }                 x = y + z;      a => a.B      c ? d : e      class A : B
// before `(` of a call or declaration, after a cast, around `.`, before `;`, before `,`,
// inside any parentheses, before `<`, before `?` of a nullable — nothing.
Foo(a, b);   (int)x   a.B.C   new List<int>()   int? x
// exceptions worth remembering because they read oddly:
[Serializable] class A          // space_after_attributes = true
[A] [B] class C                 // space_between_attribute_sections = true
where T : struct                // space_before_type_parameter_constraint_colon = true
: base(x)                       // space_before_colon_in_ctor_initializer = true
public int X { get; set; }      // space_before_singleline_accessorholder + between_accessors
public void M() { }             // space_within_empty_method_parentheses = false, empty_block_style = together
new[] { 1, 2, 3 }               // space_within_single_line_array_initializer_braces = true
```

⚠ **Two gaps are governed by no rule at all, and `SpaceKind.Preserve` is what says so.** The IR has
carried a third state since milestone 1 and nothing produced one until 3.1, which made this family
total in the wrong way: every gap got an answer and two of them were answers the oracle does not
give. Asked directly, `[1, ..a]` comes back `..a`, `[1, ..   a]` comes back `.. a`, `a[1..3]` stays
closed up and `a[1  ..  3]` comes back `a[1 .. 3]`. That is not a rule with a value — it is
`extra_spaces` collapsing a run in a gap nobody legislated. The two are the operand side of a
collection expression's spread element and both sides of a range expression's `..`.

⚠ A slice pattern is **not** one of them and looks as though it should be: `a is [1, ..var r]` comes
back `.. var r`, a space the oracle inserts, because `space_within_slice_pattern = true` really does
govern its own construct. `space_within_spread_pattern` is inert at both values and is demoted to
Tier D — SK-DIV-0009.

⚠ **One key per construct, and "the family answers with one key" was thirty keys ignored.** Until
M9 a control-flow `(` was answered by `space_after_keywords_in_control_flow_statements` and the
inside of every parenthesis by `space_within_parentheses`, which was right for the export's values
and wrong about which key produced them. The oracle answers each construct separately —
`space_before_if_parentheses = false` gives `if(n > 0)` and leaves `while (…)` alone,
`space_within_if_parentheses = true` gives `if ( n > 0 )` and touches nothing else — so a rule
written against the family key silently ignores the other eight or fourteen. The same applies to
`space_between_method_{call,declaration}_[empty_]parameter_list_parentheses`,
`space_within_array_rank_brackets` and its empty twin, and `space_around_dot`. Fidelity cannot see
this: an ignored key whose configured value happens to agree costs nothing until someone changes it.

⚠ **A generalized key is honoured by the resolver expanding it, not by a rule reading it.** A key
whose registry entry carries `expands` — `space_around_ternary_operator`,
`space_before_open_square_brackets`, `indent_size` — is ReSharper's way of writing a group of
options on one line, and `OptionResolver.Expand` writes its value into each key it names. Later in
the file wins, which is what the oracle answers: `space_around_ternary_operator = false` appended
after the four ternary keys overrides them, and written before them does not. This is not
docs/plan/03 § "Precedence" step 3 — that orders *spellings of one option*, and a generalized key
and a key it names are two different options.

⚠ **Two of them expand to nothing on purpose.** `space_between_parentheses_of_control_flow_statements`
names the nine `space_within_<keyword>_parentheses` keys and the oracle ignores it at both values
while each of the nine answers, so its `expands` is empty: honouring it would add spaces Rider does
not. `csharp_space_between_parentheses` is the same measurement.

⚠ **An accessor body's braces are `space_in_singleline_accessorholder`'s, not
`space_in_singleline_method`'s.** `get { return _n; }` is the one single-line body any input
produces — `BreakPlan.PlanOnePerLine` gives every statement in a method a line of its own — and
Skala read its spacing out of the method key, which the oracle ignores. Measured both ways.

⚠ **Five more gaps the ninety keys do not describe, all found by ranking the divergence classes:**
an unbound generic's type argument list is commas and zero-width omitted arguments, so "a space
follows a comma" writes `ValueTuple<, >`; there is no key for the gap *after* a pointer's asterisk,
because behind it is an ordinary "a type is followed by a name" gap, and answering both sides from
`space_before_pointer_asterik_declaration` writes `int*p`; a function pointer's asterisk hangs from
`FunctionPointerTypeSyntax` rather than a `PointerTypeSyntax`, so `delegate* unmanaged<nint, nint>`
came back as a multiplication; an implicit element access has no operand in front of it, so
`space_before_array_access_brackets` has no gap to govern and `{ [a] = 1, [b] = 2 }` lost the space
after the comma; and `new (string Name, int Value)[]` opens a tuple *type*, which
`space_before_new_parentheses` has nothing to say about.

`resharper_extra_spaces = remove_all` is the global backstop: any run of spaces not required by a
rule collapses to one, or to none. ⚠ With one exception —
`disable_space_changes_before_trailing_comment = false`, so trailing-comment alignment that authors
built by hand *is* collapsed. This will produce a visible diff on first run in trees that align
trailing comments. It is correct, it is what Rider does, and it is worth calling out in the adoption
notes so it is not mistaken for a bug.

### Blank lines

The three-system resolution from [04](04-formatting-engine.md) § "Blank lines". Thirty keys, ~90
fixture files, and the highest bug density in the whole formatter because the systems interact:

```csharp
class C {                          // remove_blank_lines_near_braces_in_declarations: no blank after {
    int _a;
    int _b;                        // around_field = 1, but adjacent single-line fields: 0
                                   // (around_single_line_field), so these stay tight
                                   // ← around_invocable = 1
    /// <summary>Docs.</summary>   // stick_comment = true: the blank goes ABOVE the comment
    public void M() { }
                                   // around_single_line_invocable = 0 would drop this,
                                   // but M() has a doc comment and is not single-line
}
```

### Braces and single-line blocks

`csharp_new_line_before_open_brace = none` (K&R), `new_line_before_else/catch/finally = false`,
`csharp_new_line_before_members_in_object_initializers = true`,
`resharper_csharp_new_line_before_while = false` (`} while (x);`),
`csharp_preserve_single_line_blocks = true`, `resharper_csharp_empty_block_style = together` (`{ }`),
`resharper_special_else_if_treatment = true` (`else if` is one line, not a nested block),
`resharper_allow_comment_after_lbrace = false`.

`csharp_prefer_braces = true:none` and `resharper_braces_redundant = true` are **arrangement**, not
formatting — they add or remove braces, which changes the tree. Doc [06](06-arrangement-and-syntax-styles.md).

### Indentation

Block vs. continuous per [04](04-formatting-engine.md). The export's non-obvious choices:
`indent_nested_{for,foreach,while,using,lock,fixed}_stmt = false` (flush nesting),
`indent_preprocessor_if = no_indent` and `indent_preprocessor_other = no_indent` (directives at
column 0) but `indent_preprocessor_region = usual_indent` (regions indent with code),
`indent_case_from_switch` via `csharp_indent_switch_labels = true`, `csharp_indent_braces = false`,
`resharper_indent_wrapped_function_names = false`, `resharper_outdent_binary_ops = false`,
`resharper_outdent_commas = false`, `resharper_outdent_dots` — the outdent family is all off, which
again simplifies the layout engine.

⚠ **All off is not the same as implemented, and the outdent family was the second.** M9 asked the
oracle for each of the six at its other value and recorded what came back, because "the export sets
it to the value that costs nothing" is a fact about this export and not about the tool:

- `outdent_statement_labels` is implemented: it is one `Indent(Outdent)` scope around a label's two
  tokens. Finding that also fixed a divergence — `LabeledStatementSyntax` sits on the embedded-
  statement list beside `if` and `while`, where the body genuinely is a level down, and Skala put a
  labelled statement one level in where the oracle keeps the two flush.
- `outdent_binary_ops`, `outdent_dots` and `outdent_ternary_ops` are observable and **not**
  implemented. Each moves the wrapped operator left by its own width plus one — 12 → 10 for `+`,
  12 → 9 for `&&`, 12 → 11 for `.` — which is a column offset, and `Indent(Outdent)` is one level.
  They need a scope kind the IR does not have.
- `outdent_commas` is inert under this export and observable with `wrap_before_comma = true`, which
  the export sets false; with a trailing comma there is nothing at the head of a line to outdent.
- `outdent_binary_pattern_ops` is unverified: no input has yet been found that both wraps a binary
  pattern chain under this export and shows the outdent.

The seven `indent_*_pars` and `indent_*_angles` keys divide the same way: `indent_invocation_pars`
and `indent_method_decl_pars` are observable and place a closing delimiter, which is the wrapping
pass's; `indent_primary_constructor_decl_pars` needs
`wrap_before_primary_constructor_declaration_rpar = true` to have a delimiter on its own line at
all; and `indent_pars`, `indent_statement_pars`, `indent_typearg_angles` and `indent_typeparam_angles`
have no observed shape under this export. Every one of those sentences is in the key's registry
summary, so the next reader measures something else instead of re-measuring these.

## Phase 2 — line breaks, placement, `keep_existing`, attributes

Everything that decides *whether* a break exists at a point, and *which side of a token* it lands on,
before the fitting pass decides whether to take an optional one. ✅ Measured: 97.47 % line fidelity
and 49.47 % file fidelity on `corpus/real/`.

⚠ Deciding which side of a token a break lands on needs a model M1 did not have. M1's gap model
answers "does this gap hold a break"; the position keys ask "of the several gaps around this
operator, which one may". The answer is a pre-pass over the syntax tree that labels each gap
`Point` (a break point of some group), `Flat` (never a break, even if the author wrote one) or
`Mandatory` — a pre-pass rather than a decision taken during the walk, because a gap can be at the
structural level of two constructs at once. In `Foo(\n a + b, c)` the gap before `a` is the argument
list's first break point *and* the binary chain's first non-point, and only a pass that sees both can
let the point win.

### Required and forbidden breaks

`resharper_place_simple_embedded_statement_on_same_line = if_owner_is_single_line`,
`place_simple_case_statement_on_same_line = if_owner_is_single_line`,
`place_type_constraints_on_same_line = true`, `place_constructor_initializer_on_same_line = true`,
`place_primary_constructor_initializer_on_same_line = true`,
`csharp_wrap_enum_declaration = chop_always` with `max_enum_members_on_line = 1` (one enum member per
line), `blank_lines_after_file_scoped_namespace_directive = 1`.

⚠ Three corrections M2 established, all of them by running the oracle rather than by reading the
option names:

- **`resharper_new_line_before_enumerators` is not in `options.json`.** ⚠ It is, from M3: the
  importer's blind spot is repaired and 37 keys are registered, `resharper_prefer_wrap_around_eq`
  and `resharper_continuous_line_indent` among them. It was in the export template and M0's importer
  dropped it, along with about forty other C#-relevant keys that the export writes
  without a language prefix (`place_property_attribute_on_same_line`,
  `place_event_attribute_on_same_line`, `place_namespace_definitions_on_same_line`,
  `continuous_line_indent`, `indent_wrapped_function_names`, `wrap_base_clause_style`,
  `wrap_ctor_initializer_style`, `wrap_enumeration_style`, `simple_block_style`,
  `align_multiline_ctor_init`, `int_align_eq`, …). The mechanism is spelled with the two registered
  keys named above. ⚠ The importer registered an option only when one of the forms JetBrains
  *documents* for it appeared in the template, so a key the export writes in a spelling the tables do
  not list among that property's names was dropped whole — and surfaced as an SK9001 unknown key,
  which reads like a stray line in somebody's config rather than like a gap in the registry. The
  tier matrix is over 520 keys from M3.
- **`place_*_on_same_line = true` is permissive, not mandatory.** `place_type_constraints_on_same_line`
  and `place_constructor_initializer_on_same_line` at `true` do not *join* a `where` clause or a
  `: base(…)` the author put on its own line; they only decline to force a break. Their `false` value
  is what is observable, and that is what pins them.
- ⚠ **`csharp_new_line_between_query_expression_clauses` and `place_linq_into_on_new_line` were
  recorded here as inert, and the measurement behind it asked too little.** `from x in xs where p
  select x` on one line does come back on one line with both set to `true` — but that query *fits*,
  and neither key has anything to decide about a query that does not wrap. Asked with a query too
  wide for its line, `new_line_between_query_expression_clauses` is a **chop**: at `true` a query the
  author broke at one boundary comes back broken at *every* one, and a query too wide is chopped
  whole; at `false` the same two inputs keep exactly the author's breaks and gain one more only where
  the line runs out. `place_linq_into_on_new_line` decides the *continuation's* `into`
  (`group … by … into bucket`) and goes with that chop — it does not govern a `join … into matches`,
  which the oracle leaves on the join's line at `true` with the query chopped around it. Both are
  Tier A, pinned by `constructs/wrapping/linq-query.cs`; so are `align_linq_query` and
  `wrap_before_linq_expression`, which were Tier D behind the missing break point rather than behind
  anything to do with the keys. `BreakPlan.PlanQuery` is the break point.

### `place_*` and `if_owner_is_single_line`

Five keys — `place_expr_method_on_single_line`, `…_property_…`, `…_accessor_…`,
`place_simple_case_statement_on_same_line`, `place_simple_embedded_statement_on_same_line` — all set
to `if_owner_is_single_line`. This is the option that forces two-pass fitting: whether

```csharp
public int X => _x;
```

may stay on one line depends on whether its *owner* (the property declaration) resolved to a single
line, which depends on width, which depends on the child. The resolution rule is fixed-point-free by
construction: owners resolve first, children read the owner's resolved mode, children may only
become *more* broken. See [04](04-formatting-engine.md) § "The fitting algorithm", second pass.

Attributes always on their own line — through the six per-owner keys that are in the registry
(`csharp_place_{type,method,field,accessor,accessorholder,record_field}_attribute_on_same_line`, all
`never`) rather than through the unprefixed `place_property_…`/`place_event_…` pair, which the
importer dropped. ⚠ `resharper_place_attribute_on_same_line` (the language-agnostic one) *is* in the
registry and is Tier D: the six per-owner keys cover every C# attribute target, so it never gets to
decide. `max_attribute_length_for_same_line` is Tier D for the same kind of reason — a length
threshold for a placement that never happens cannot change an output. ⚠ An option that cannot change
behaviour must not claim a tier that says it was verified.

⚠ **`place_single_method_argument_lambda_on_same_line` governs the opening parenthesis only.**
`Assert.Throws(() => {` keeps the lambda on the call's line however long its body is — and the oracle
still moves the *closing* parenthesis to a line of its own, so the body gains a continuation level
and the call ends `}\n);`. Flattening both sides is the intuitive reading of the name and it is
wrong.

### `keep_existing_*`

Eighteen keys, all `false` in the export except the ones ReSharper defaults to `true`. They select
`Group(Preserve)` per construct family: declaration blocks, embedded blocks, invocation parens,
declaration parens, lambda parens, expression members, switch expressions, list patterns, property
patterns, attribute arrangement, primary-constructor parens, enum arrangement.

⚠ `keep_existing_* = false` does **not** mean "reflow everything". ⚠ And the table below is not the
one this document had: M2 measured all four corners against the oracle, and the two keys turn out to
govern **different gaps of the same construct** rather than the same gap with different strength.

| `keep_user_linebreaks` | `keep_existing_X` | break at X's **delimiters** | break **between X's items** |
|---|---|---|---|
| true | true | kept | kept |
| true | false | re-joined | kept |
| false | true | re-joined | re-joined |
| false | false | re-joined | re-joined |

Read off the fixtures: with `keep_existing_invocation_parens_arrangement = false`, `Foo(\n a)`
re-joins and `Foo(\n a,\n b)` does not. The first has only a delimiter break, which the
per-construct key governs; the second has a break between two items, which the global key governs.
The row this document had as "source breaks kept, but the wrap style may add breaks when too wide"
is half of that, and the row it had as "X preserved, everything else reflowed" is backwards — the
global switch turns the per-construct one off, and the per-construct one does not turn the global one
on.

The other half of the rule, which no option name suggests: **once a construct is broken at all, a
`chop_*` style breaks every one of its points**, the two at the delimiters included. `Foo(a,\n b)`
comes back as four lines, not two. That single rule is most of the distance M2 covered: the oracle's
output over `corpus/real/` has 1 006 lines that are nothing but a closing parenthesis and milestone
1's had 573.

Getting this table wrong in either direction is a catastrophic first-run diff. It gets its own
fixture set, `constructs/preservation/`, run under all four combinations — thirteen inputs × four
configurations × one committed `jb cleanupcode` fixture each, plus the repository's own.
`PreservationTests` asserts idempotency and token equivalence in every corner, not only the default
one: a formatter that corrupts a file only when `keep_user_linebreaks = false` is still a formatter
that corrupts files.

⚠ `resharper_csharp_keep_existing_linebreaks` reads like one of the family and is not: it is the
per-language form of the global `keep_user_linebreaks`, and putting it on the `keep_existing_*` axis
collapses the table — both "reflow" corners come out identical to their "keep" neighbours and the
2×2 stops measuring anything.

⚠ **And "outranks the placement key in both directions" is true of a delimited list and false of an
embedded statement**, which milestone 3.1 measured because the two readings disagree on the export's
own values:

| key | what it governs | may a break be *added*? |
|---|---|---|
| `keep_existing_invocation_parens_arrangement` and the rest of the delimited family | the construct's delimiters | ⚠ no — `place_simple_*_on_single_line` is inert while keep is on |
| `keep_existing_expr_member_arrangement` | the gap after a member's `=>` | ⚠ no — same |
| `keep_existing_embedded_arrangement` | the gap before an embedded statement | ✅ **yes** |

`if (depth < 0) throw new ArgumentOutOfRangeException(…);` written on one 168-column line comes back
from the oracle with the `throw` on a line of its own, under `keep_existing_embedded_arrangement =
true`. The key says the author's break is not *removed*; it does not say a break may not be added,
and `place_simple_embedded_statement_on_same_line = if_owner_is_single_line` then does exactly what
it says — the `if` does not occupy one line, so the statement leaves it. Reading the key the other
way made the placement key inert, which the option's own doc comment recorded as a fact about the
export for four milestones.

⚠ **`keep_existing_list_patterns_arrangement` preserves each *individual* item gap**, which a fill
cannot express — a collection expression the author wrote one element per line comes back one element
per line however well two of them would have shared, while the same shape written `new[] { … }` is
re-filled because an array initializer has no `keep_existing_*` key of its own. A per-group flag
cannot say it: the preserved gaps and the filled ones are siblings, so the preserved ones become
ordinary required breaks and the rest stay fill points.

## Phase 3 — wrapping

The 47 `wrap_*` keys, plus the `max_*_on_line` counters, plus `csharp_max_line_length = 120`. This
is where the fitting engine earns its existence, and it is the phase that is allowed to take a
month. ✅ Measured: **98.86 %** line fidelity and 71.05 % file fidelity on `corpus/real/`.

⚠ **Measured again at M3.1: 99.70 % line and 85.79 % file with the oracle's own preprocessor
symbols, 99.63 % / 85.26 % without.** The paragraph above is M3's number and is kept as the
trajectory.

⚠ Six rules M3 established against the oracle, none of which is readable off an option name:

- **`wrap_if_long` is a fill, and an object or collection initializer is not one.**
  `new[] { six, long, strings, here, again, again }` comes back with five on one line and one on the
  next; `new List<string> { four, long, strings, here }` comes back with one per line although two
  of them would have shared. An initializer therefore needs *two* groups — the braces decide whether
  it wraps, the elements decide whether they share a line — because it has three layouts and one
  group offers two of them.
- **The counters are not width tests.** `max_initializer_elements_on_line = 4` chops five elements
  onto five lines at 41 columns wide, and `max_array_initializer_elements_on_line = 10000` leaves the
  same five alone. Which counter applies is the syntax kind, not the option name.
- **`keep_existing_*` outranks `place_simple_*_on_single_line`, in both directions.** With keep on,
  neither the join at `true` nor the forced break at `false` happens. And `place_… = false` is not
  permission withheld: it forces the delimiters apart however short the construct is.
- **A chain's links break together**, which no per-operator group can decide, so the chain gets a
  group of its own holding no break points and the operator groups read it. ⚠ Same *precedence*, not
  merely "both are binary": `a > 0 && b > 0` is one chain of `&&` and the oracle chops it nowhere
  else.
- **A property in a call chain travels with the call it feeds**, not with the call before it —
  `.ToList()` then `.Count.ToString()`, which is the opposite of what
  `wrap_after_property_in_chained_method_calls = false` reads like.
- **`keep_existing_switch_expression_arrangement` outranks `chop_always`.** With it on,
  `value switch { 1 => 1, _ => 0 }` comes back on one line although the wrap style says every arm
  gets one of its own.

⚠ Five more that M3.1 established the same way, and every one of them is about *which* break is
taken rather than about a width:

- **The `=` break before a collection expression is not preserved, and it is the only right-hand side
  that behaves that way.** `int[] y =\n[\n 1,\n 2\n];` comes back `int[] y = [`, while
  `= \n new[] {`, `= \n new Thing {`, `= \n Make(` and `= \n @"…"` all keep the break the author
  wrote. The `=` break and the bracket's are alternatives rather than a pair, so the decision goes to
  the ordering rule — which then produces both halves, because a bracket that fits on a continuation
  line still gets the `=` break and one that has to chop does not. The arrow behaves the same way,
  and the two are measured separately because they need not have.
- **`if_owner_is_single_line` means the owner, and the owner is the declaration.** A chopped parameter
  list makes a declaration multi-line, so the arrow's body leaves its line — and the body's own width
  says nothing about it, because `SetBindGroup(pass, group, bindGroup, offsets)` fits on the `) =>`
  line with sixty columns to spare. `GroupFacts.BreaksWithOwner` is how the arrow reads the parameter
  list's resolved mode.
- **`blank_lines_after_block_statements` applies to a statement that *ends* with a brace**, which is
  not the same as a statement that *is* a block. An `if … else { }`, a `switch { }` and a
  `try … catch { }` all take the blank line, and none of their closing braces hangs from a
  `BlockSyntax` whose parent is a statement. ⚠ Not before a `case`, which is a label.
- **A chain of ternaries is a list rather than a staircase, and it is a different construct from a
  single ternary.** A conditional whose tail is not another conditional wraps at its own `?` and
  `:`, sized by `wrap_ternary_expr_style`; a chain of them wraps **after each `:`**, one member per
  line, and neither `wrap_ternary_expr_style` nor `wrap_before_ternary_opsigns` moves any of it —
  flipping either returns every chain in `constructs/wrapping/ternary-chains.cs` byte-identical
  while it moves the single conditional beside them. ⚠ Recorded through M10 as "the preserved
  position is the one that is measured", because every occurrence in `corpus/real/` is a chain the
  author had already broken. Now measured on a chain the formatter wraps itself, which is where the
  two keys turn out not to apply.
  - The chain runs through `WhenFalse` and does not see through parentheses: `a ? (b ? x : y) : z`
    and `a ? x : (b ? y : z)` are both single conditionals to the oracle.
  - The innermost member is not a break point. A chain the formatter re-wraps ends
    `cond ? "third" : "d";` however wide that line is — measured on a chain whose members are each
    wider than the margin.
  - A break the author put before the *final else* is kept, which is the one place a chain and a
    single ternary disagree about the same gap: `cond ? a :\n b` is re-joined for a single ternary
    and kept for a chain.
  - `nested_ternary_style` is what governs the layout, and all four of its values are distinct on a
    flat chain — `autodetect` chops if long, `compact` chops always, `expanded` writes the
    staircase, `simple_wrap` fills at the signs. Skala writes `autodetect`, which is the export's
    value; the other three are Tier D and measured rather than guessed.
  - `keep_user_linebreaks` and not the style key is what preserves a chain the author wrote at the
    signs: at `keep_user_linebreaks = false` the oracle rewrites the leading-`:` layout *and* the
    staircase into the one-member-per-line layout.
- **A named attribute argument's `=` is an `=`.** `[LoggerMessage(Message = "…" + "…")]` — it is
  neither an assignment nor an equals-value clause, and it had no plan at all.

⚠ **Every member and every statement gets a line of its own**, which this document lists under phase
2 and which M2 left as a deliberately-failing fixture. `csharp_preserve_single_line_blocks = true` is
in the export and ReSharper ignores it: `class B { int P => 1; int Q => 2; }` comes back as five
lines and `if (flag) { First(); }` as three, with no width test in it. Three exclusions, each from
the oracle: an empty body stays together, an accessor's body does not break
(`get { return _street; }` comes back exactly as written), and a lambda's block does not, because
the call it is an argument to keeps it on its line. Two more come from the preservation table —
`keep_existing_declaration_block_arrangement` and `keep_existing_embedded_block_arrangement` gate the
rule.

The export's wrap settings, which are the conformance target:

| Construct | Setting | Value |
|---|---|---|
| Invocation arguments | `csharp_wrap_arguments_style` | `chop_if_long` |
| Declaration parameters | `csharp_wrap_parameters_style` | `chop_if_long` |
| Primary-ctor parameters | `wrap_primary_constructor_parameters_style` | `chop_if_long` |
| Base list | `csharp_wrap_extends_list_style` | `chop_if_long` |
| Chained calls | `wrap_chained_method_calls` | `chop_if_long` |
| Ternary | `wrap_ternary_expr_style` | `chop_if_long` |
| Multiple declarators | `wrap_multiple_declaration_style` | `chop_if_long` |
| Enum members | `wrap_enumeration_style` | `chop_if_long` |
| **Switch expression** | `wrap_switch_expression` | `chop_always` |
| Array initializer | `wrap_array_initializer_style` | `wrap_if_long` (fill) |
| Ctor initializer | `wrap_ctor_initializer_style` | `wrap_if_long` |
| Base clause | `wrap_base_clause_style` | `wrap_if_long` |
| Initializer elements per line | `max_initializer_elements_on_line` | 4 |
| Everything else per line | `max_{invocation_arguments,formal_parameters,…}_on_line` | 10 000 (= no cap) |

Paired with the *break-position* keys, which decide which side of a token the break lands on. ⚠ M2
implements the position half of these (which gap of a construct may hold a break, and which may not),
because removing a break the author put on the wrong side is break *presence*; M3 owns the half that
chooses which of a long line's candidate points to wrap at. The keys:
`csharp_wrap_before_binary_opsign = true` (operator starts the new line),
`wrap_after_dot_in_method_calls = false` (the `.` starts the new line),
`wrap_before_first_method_call = false`, `csharp_wrap_after_invocation_lpar = true` and
`csharp_wrap_before_invocation_rpar = true` (so a chopped call puts `(` at the end of the first line
and `)` on its own), same for declarations, `wrap_before_comma = false`,
`wrap_before_arrow_with_expressions`, `wrap_before_extends_colon`, `wrap_before_ternary_opsigns`,
`prefer_line_break_after_multiline_lparen = true`.

Which yields, at 120 columns:

```csharp
// chop_if_long, wrap_after_invocation_lpar, wrap_before_invocation_rpar, wrap_before_comma = false
var result = repository.QuerySomethingRatherLong(
    firstArgumentName,
    secondArgumentName,
    thirdArgumentName
);

// chained calls, chop_if_long, wrap_after_dot = false, wrap_before_first_method_call = false
var q = source.Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => x.Id);

// switch expression, chop_always — every arm, always, regardless of width
var kind = token switch {
    Kind.A => 1,
    Kind.B => 2,
    _      => 0,   // ← no: int_align is off, so this is `_ => 0,`
};
```

### Alignment — the family that is mostly off

`int_align = false` and all eight `int_align_*` sub-keys `false`; `csharp_align_multiline_argument`,
`…_parameter`, `…_calls_chain`, `…_expression`, `align_multiline_binary_expressions_chain`,
`align_multiline_switch_expression` all `false`; `align_multiline_type_argument = true` and
`align_multiline_type_parameter*` and `align_multiline_ctor_init` are the survivors.

⚠ **The list above is wrong about which keys survive, and about one of them mattering.** Nine are
`true`, not three, and the one that matters most is missing from it:
**`align_multiline_statement_conditions`**. Measured at milestone 3.1, before implementing anything:
of 313 divergent line slots on `corpus/real/`, **40 across 11 files** were a line the oracle had put
at a column that is not a multiple of the indent width, and every one of the forty was that key.

```csharp
else if (ReflectionUtils.ImplementsGenericDefinition(
             NonNullableUnderlyingType,          // the `(`'s column plus one continuation level
             typeof(IEnumerable<>),
             out tempCollectionType
         )) {                                    // the `(`'s column
```

It is implemented, for `if`, `while`, `do`, `for`, `foreach`, `using`, `fixed`, `lock`, `switch` and
`catch … when`. `IndentKind.Align` is real and the writer's indent stack holds **columns rather than
levels** — see [04](04-formatting-engine.md) § "The document IR".

✅ **And the simplification survives, which is the part worth keeping:** with column alignment
*applied this way*, laying out line *n* still never requires knowing the contents of line *n−1*. An
alignment scope's column is the column the writer is already at when the scope opens, and that is on
the current line. The quadratic worst case alignment is supposed to bring does not exist here, and
the fitting pass is still linear.

⚠ What is still unimplemented is `align_multiline_for_stmt` and the four keys whose constructs never
occur broken in the corpus. SK-DIV-0008 has the table. ⚠ The `for` header's residue that used to be
listed against `align_multiline_for_stmt` — 4 lines and 2 files — was **not** that key's: it is
masked by `align_multiline_statement_conditions` and returns the same file at either value. The gap
was a missing break point at the header's `;`, which `wrap_for_stmt_header_style` governs and which
milestone 3.2 built.

## Phase 4 — comments and xmldoc

`resharper_xmldoc_*` — 32 keys in the registry, 21 honoured — reads like a small formatter in its own right: parse the doc comment
as XML, re-wrap text to `xmldoc_max_line_length = 120`, break before
`summary,remarks,example,returns,param,typeparam,value,para`, `xmldoc_max_blank_lines_between_tags = 0`,
`xmldoc_indent_child_elements`/`attribute_indent = single_indent`,
`xmldoc_space_before_self_closing = true`, `space_after_triple_slash = true`.

⚠ **It is on by default, and it was off for six milestones on a measurement that was read wrongly.**
`jb cleanupcode` under `OracleProfile.FormatOnly` does not touch documentation comments. Asked
directly, with the whole family in force, it returns `///<summary>…`, a 128-column summary, two
`<param>` tags on one line and a `<summary>` followed by a `<remarks>` on the same line — every one
of them exactly as written. That is the profile, not the tool: `CSharpFormatDocComments` is a
cleanup task, `Built-in: Reformat Code` sets it false, `Full Cleanup` sets it true, and
`FormatOnly` is the former. Rider formats doc comments, so Skala does. SK-DIV-0006 records the
correction.

⚠ **And then the correction's own tail was wrong for one more milestone.** This paragraph used to
end "the keys stay Tier D only because every committed fixture was generated under that profile, and
`resharper_space_after_triple_slash` stays demoted". That is a fact about which fixtures happened to
exist, stated as though it were permanent. `OracleProfile.DocComments` is `FormatOnly` plus the one
element, `constructs/xmldoc/` carries a fixture per key under it, and the family splits **13 Tier A /
9 measured-and-disagreeing** — `space_after_triple_slash` among the promoted. The nine are
SK-DIV-0019 through SK-DIV-0023, and five of them are one wrapping disagreement under five names.

⚠ The hazard half is implemented, because it needs no oracle. A doc comment that is not well-formed
XML — extremely common in real code — is left exactly as it is and reported at `hint` (`SK0003`),
never "fixed", under every setting. ⚠ Judged as a *fragment* and not a document: two sibling
`<param>` tags are ordinary, and document rules would report most of the corpus. DTD processing is
prohibited and there is no resolver, because the text comes from a source file anybody may have
written.

⚠ **The sub-formatter runs on every file and `skala format --no-xmldoc` turns it off.** **Seventeen
of the twenty-seven `resharper_xmldoc_*` keys are honoured and ten are refused with a reason each**
(`XmlDocIds.Refused`; six of them because a tag header is emitted byte-for-byte and never broken
open, which is one rule rather than six omissions). Each of the seventeen is asserted observable by
`AnUnoracledKey_IsObservable`, against a hand-written probe rather than against `constructs/` —
whose doc comments are short and already tidy, because nothing read them when it was written.

⚠ **These are the only options in the project not pinned against the oracle, and it is the fixtures
that stop them rather than the oracle.** Tier A means "pinned by an oracle fixture" and every
committed fixture was generated by a profile that leaves doc comments alone, so every id the
sub-formatter reads is registered `OfUnoracled` — read, honoured, never entering
`PhaseOneOptions.Implemented`, never Tier A *yet*. Regenerating the fixtures with
`CSharpFormatDocComments` enabled is what makes them promotable. What replaces the oracle: hand-written fixtures asserting the semantics JetBrains'
settings pages state; a **round trip** checked on every comment of every run, which reduces the
re-wrapped comment to a signature (prose whitespace-normalised, `<code>` and `<c>` byte-for-byte,
attributes exact) and puts the comment back exactly as written if it differs by one word; and four
corpus-wide properties over all 716 files, of which the load-bearing one is *the non-`///` lines of
the output are identical with and without the flag*.

⚠ Hazard 1 — text inside `<code>` and `<c>` — is no longer moot and is handled by never re-wrapping
it: those elements' bodies are emitted as their source lines, with only the `///` marker removed and
the marker space **not** re-applied. Measured over `corpus/real/`: 3 030 of 3 032 doc comments
re-wrap and round-trip clean, the two left are the two that are not well-formed XML, and the flag
costs 3.59 points of line fidelity against an oracle that never moves.

## Ordering summary

The order above is a build order, not a priority order. It is chosen so that each phase is
independently shippable and independently measurable against the oracle:

| After phase | `skala format` can be run on | Expected corpus fidelity |
|---|---|---|
| 1 | Any file, safely; produces correct spacing/blanks/indent, never moves a line | ~85 % of lines — ✅ measured 94.28 % |
| 2 | Same, plus correct break *presence* and *position* | ~93 % — ✅ measured 97.47 % |
| 3 | Everything the export configures | ≥ 99.9 % — ⚠ **measured 99.70 %**, see below |
| 4 | Same, plus doc comments | ✅ on by default; the pinned oracle profile does not format doc comments, so the comparison is 96.04 % over every line and 99.53 % outside them — the latter is the differential's basis. SK-DIV-0006 |

⚠ **Every number in the two paragraphs that used to stand here was M3 data presented in the present
tense**, and the corpus they were measured over no longer exists — it was re-based on a mainline
snapshot at M3.1. They said 98.86 % overall, 876 divergent lines of 76 660, and a three-way split of
274/91/15 files. Current, from M3.1 and unchanged through M7:

| | M3, as this document read | Current |
|---|---|---|
| Overall line fidelity | 98.86 % | **99.70 %** with symbols, 99.63 % without |
| Divergent lines | 876 of 76 660 | ~230 of **76 375**, across 51 files |
| Files with no `#if` | 274 @ 99.02 % | **289 @ 99.79 %** |
| Files with a `#if` | 91 @ 98.60 % | 98.92 % — and ⚠ **SK-DIV-0004 is closed**, which this document still cited as open |
| Files with a raw literal | 15 @ 97.81 % | **11 @ 99.68 %**, 12 divergent lines of 3 744 |
| Constructs at 100 % ([16](16-risks-and-open-questions.md) § R1) | 27 of 54 | **37 of 56** |

⚠ **Reproducing these is three commands, not one, and that is why they went stale.** `./build.sh
Fidelity` gives the aggregate and the per-origin breakdown; `dotnet run --project
Testing/Rikarin.Skala.Testing -- preprocessor` gives the `#if` split; and the raw-literal bucket has
**no command at all** — `docs/divergences.md` records that it was computed by hand from `dump real …
defined` output. A number nothing regenerates is a number that is wrong as soon as anything moves,
which is what happened here. `fidelity constructs` answers § R1's sharper question and is the number
to move; it is not the same number as the percentage.

Percentages are lines-identical-to-oracle over `Testing/corpus/real/`, reported by
`./build.sh Conformance` on every commit, and published in the README. A phase is done when its
number stops moving, not when its keys are all implemented.
