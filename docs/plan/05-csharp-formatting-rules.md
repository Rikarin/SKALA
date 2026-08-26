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
| Line breaks — required/forbidden | ~20 | `Line(Hard)` / `Space` | no | 2 |
| Placement (`place_*`) | 20 | `Group(Auto)` keyed on owner | no | 2 |
| Wrapping (`wrap_*`) | 47 | `Group`/`Fill`, the fitting pass | no | 2–3 |
| Alignment (`align_*`, `int_align_*`) | 28 | `Align` | no | 3 (mostly off — see below) |
| `keep_existing_*` | 18 | `Group(Preserve)` | no | 2 |
| Attributes | ~8 | `Group`, `Line` | no | 2 |
| Comments & xmldoc | ~16 | sub-formatter | no | 3 |
| Arrangement (`arrange_*`, body styles, `var`, qualifiers) | ~40 | tree rewrite | **yes** | doc [06](06-arrangement-and-syntax-styles.md) |

## Phase 1 — spaces, blanks, braces, indent

Nothing here needs the fitting pass. A Phase-1 Skala reformats a file correctly as long as no line
needs to move, which is already most of a `.cs` file, and it is the fastest route to a differential
harness that produces meaningful numbers.

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

## Phase 2 — line breaks, placement, `keep_existing`, attributes

Everything that decides *whether* a break exists at a point, before the fitting pass decides whether
to take an optional one.

### Required and forbidden breaks

`resharper_place_simple_embedded_statement_on_same_line = if_owner_is_single_line`,
`place_simple_case_statement_on_same_line = if_owner_is_single_line`,
`place_type_constraints_on_same_line = true`, `place_constructor_initializer_on_same_line = true`,
`place_primary_constructor_initializer_on_same_line = true`, `place_linq_into_on_new_line = true`,
`csharp_new_line_between_query_expression_clauses = true`, `new_line_before_enumerators = true`
(one enum member per line), `blank_lines_after_file_scoped_namespace_directive = 1`.

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

`place_attribute_on_same_line = false` / `place_property_attribute_on_same_line = false` /
`place_event_attribute_on_same_line = false`: attributes always on their own line.
`max_attribute_length_for_same_line` therefore never applies here, but is Tier A anyway because
`options.json` requires every key to be either implemented or explicitly tiered.

### `keep_existing_*`

Eighteen keys, all `false` in the export except the ones ReSharper defaults to `true`. They select
`Group(Preserve)` per construct family: declaration blocks, embedded blocks, invocation parens,
declaration parens, lambda parens, expression members, switch expressions, list patterns, property
patterns, attribute arrangement, primary-constructor parens, enum arrangement.

⚠ `keep_existing_* = false` does **not** mean "reflow everything". It means "this construct family
does not get the per-construct preservation exemption" — the *global*
`keep_user_linebreaks`/`keep_user_wrapping` still apply, and they are `true`. The interaction is:

| `keep_user_linebreaks` | `keep_existing_X` | Behaviour for X |
|---|---|---|
| true | true | never re-arranged; source layout is law |
| true | false | source breaks kept, but the wrap style may **add** breaks when too wide |
| false | true | X preserved, everything else reflowed |
| false | false | fully reflowed from the wrap style (`--reflow`) |

Getting this table wrong in either direction is a catastrophic first-run diff. It gets its own
fixture set, `constructs/preservation/`, run under all four combinations.

## Phase 3 — wrapping

The 47 `wrap_*` keys, plus the `max_*_on_line` counters, plus `csharp_max_line_length = 120`. This
is where the fitting engine earns its existence, and it is the phase that is allowed to take a
month.

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

Paired with the *break-position* keys, which decide which side of a token the break lands on:
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

✅ This is a significant simplification and it is verified, not assumed: with column alignment off,
laying out line *n* never requires knowing the contents of line *n−1*. The `Align` IR node exists
(the handful of `true` keys need it, and other people's configs will use it), but the hot path does
not, and the quadratic worst case that alignment brings does not exist for this configuration.

## Phase 4 — comments and xmldoc

`resharper_xmldoc_*` — 12 keys — is a small formatter in its own right: parse the doc comment as
XML, re-wrap text to `xmldoc_max_line_length = 120`, break before
`summary,remarks,example,returns,param,typeparam,value,para`, `xmldoc_max_blank_lines_between_tags = 0`,
`xmldoc_indent_child_elements`/`attribute_indent = single_indent`,
`xmldoc_space_before_self_closing = true`, `space_after_triple_slash = true`.

⚠ Two hazards. Text inside `<code>` and `<c>` is verbatim and must never be re-wrapped. A doc
comment that is not well-formed XML — extremely common in real code — must be left exactly as it is
and reported at `hint` (`SK0003`), not "fixed".

## Ordering summary

The order above is a build order, not a priority order. It is chosen so that each phase is
independently shippable and independently measurable against the oracle:

| After phase | `skala format` can be run on | Expected corpus fidelity |
|---|---|---|
| 1 | Any file, safely; produces correct spacing/blanks/indent, never moves a line | ~85 % of lines |
| 2 | Same, plus correct break *presence* | ~93 % |
| 3 | Everything the export configures | ≥ 99.9 % (the bar from [00](00-vision-and-principles.md)) |
| 4 | Same, plus doc comments | ≥ 99.9 % including xmldoc |

Percentages are lines-identical-to-oracle over `Testing/corpus/real/`, reported by
`./build.sh Conformance` on every commit, and published in the README. A phase is done when its
number stops moving, not when its keys are all implemented.
