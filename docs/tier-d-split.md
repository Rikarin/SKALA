# The Tier D split

`Core/Rikarin.Skala.Options/options.json` carries **520** options: **287 Tier A, 6 Tier C, 227 Tier D** (284/6/230 when this document was written; the `force_chop_compound_*` triple was implemented and promoted on 2026-08-28).
“Tier D” has only ever meant *not Tier A* — and Tier A is a narrow claim: the formatter reads the option
**and** a committed oracle fixture pins it. Quoting the 230 as remaining work turns that narrow claim into
a coverage number it was never making, which is where “45 % of the standard is unenforced” comes from.

This document splits the 230, one row per key, with the evidence beside it. **No key below is classified
on the strength of its name.**

⚠ **What it is pinned to.** Every number here is a join over the registry and the *committed* key-flip
sweep — `jb cleanupcode` 2025.2.6, base configuration sha256 `381a31a28c5ea94d`, 321 options swept. Both
files move. **Re-derive the counts before quoting them**; § “Re-deriving the 58 without trusting this
document” says how, and it is a few lines. A number in prose beside a number in a generated file is a
number that will drift, and this repository has been bitten by exactly that before
([03](plan/03-configuration-model.md) § “The tier matrix is published”).

## ⚠ Correction, 2026-08-28 — the “export value” column is wrong in 9 rows

**This document's per-row value column is the registry `default`, not the value the `.editorconfig`
export actually sets.** For most rows the two coincide; for **9 of the 63 implementable rows they do
not**, and where they differ the verdict can flip — because "implementable" here means *diverges at
the value the standard sets*, and a row measured at the wrong value answers a different question.

Five were confirmed three ways (`skala config explain` with source file:line, the committed sweep's
per-value hashes, and a live `verify` run) and **are not defects** — they agree at the export's value
and diverge only at values the export never uses:

| key | this document said | the export actually sets | Skala there |
|---|---|---|---|
| `resharper_csharp_wrap_after_invocation_lpar` | `false` | **`true`** (`.editorconfig:394`) | agrees |
| `resharper_csharp_wrap_before_invocation_rpar` | `false` | **`true`** (`.editorconfig:399`) | agrees |
| `resharper_csharp_wrap_chained_method_calls` | `wrap_if_long` | **`chop_if_long`** (`:830`, via alias) | agrees |
| `resharper_csharp_wrap_chained_binary_expressions` | `wrap_if_long` | **`chop_if_long`** (`:828`) | agrees |
| `resharper_csharp_wrap_chained_binary_patterns` | `wrap_if_long` | **`chop_if_long`** (`:829`) | agrees |

Four more carry the same column defect without a changed verdict — they disagree at *every* value and
have `BaselineAgrees = false` regardless: `resharper_csharp_keep_existing_property_patterns_arrangement`,
`resharper_csharp_wrap_after_declaration_lpar`, `resharper_csharp_wrap_before_declaration_rpar`,
`resharper_csharp_wrap_extends_list_style`.

⚠ **Read the export, not this column, before acting on any row.** The rest of the table has not been
re-checked key by key.

### Where the count stands after this correction

- **−5** rows move to *enforced at the export's own value* (58 → 63).
- **−3** implemented on 2026-08-28: the `force_chop_compound_{if,do,while}_expression` triple, now
  Tier A, Conformant at both values against `jb cleanupcode` 2025.2.6.
- **`resharper_csharp_wrap_lines` is blocked, and provably not its own defect.** At the export's
  values it and `resharper_csharp_wrap_extends_list_style` produce byte-identical hashes on both
  sides — one defect (a single-base-type base list that overflows and Skala will not wrap) surfacing
  under two keys.

**So the finish line is ~55, not 63.** The headline table below is left as first written, so the
correction is visible as a correction.

## Headline

| | keys | share of the 230 |
|---|---:|---:|
| Enforced at the export’s own value | 58 | 25 % |
| Implementable | 63 | 27 % |
| Masked | 12 | 5 % |
| Duplicate spelling | 24 | 10 % |
| Unreachable / inert | 49 | 21 % |
| Wrong subsystem | 10 | 4 % |
| Not a formatter option at all | 9 | 4 % |
| Unresolved | 5 | 2 % |
| **total** | **230** | |

**The finish line is 63 keys, not 230.** Everything else is already enforced at the value the standard
sets, is another key under a name the C# formatter does not answer to, is dominated by a key the export
already sets, cannot be moved by any input, or belongs to the arranger, the analyser or nobody.

### What “unenforced” actually is

The standard **is** the export. An option the export sets to `x` is enforced when Skala and
`jb cleanupcode` produce the same bytes at `x`. The committed key-flip sweep
(`Testing/Rikarin.Skala.Conformance.Sweep/conformance-sweep.json`) measures exactly that, and it reaches
81 of the 230 — the Tier D keys that have an `oracle` fixture.

| | keys |
|---|---:|
| swept Tier D keys | 81 |
| **agree with the oracle at the export’s own value** | **58** |
| disagree at the export’s own value | 23 |

⚠ Those 58 are Tier D because they **diverge at a value the export never uses**. Their baseline agreed
in all 58 cases (`BaselineAgrees` is true for every one), so the agreement is the key’s and not the
fixture’s. ⚠ **This is not a Tier A claim and must not be turned into one.** Tier A is a statement about
the option across its domain; this is a statement about the option at one value, on one fixture, under
one configuration. The six xmldoc keys promoted on fixture evidence and demoted the same afternoon were
demoted for exactly the shape this table describes — *agrees at the export’s value and diverges away from
it*. The difference is that here it is the conclusion rather than the mistake: agreement at the export’s
value is what “the standard is enforced” means, and it is **not** what Tier A means. **No tier is changed
by this document.**

So the defensible statement is:

> Of the 520 options the export sets, **342 are demonstrated to hold at the export’s own value** — the 284
> Tier A plus the 58 above. **110 cannot be enforced by this formatter at all** — 104 Tier D (duplicate
> spellings, masked keys, unreachable keys, other subsystems’ keys, non-options) plus the 6 Tier C, which
> are IDE toggles and old-engine switches. **63 are real, reachable, unimplemented behaviour.** 5 are
> unresolved and say so. 342 + 110 + 63 + 5 = 520.

That is **12 % of the export genuinely unenforced** (63 of 520), against the 45 % the raw Tier D count
implies — and the 45 % is not merely pessimistic, it is measuring a different thing.

## Size estimates for the 63

| band | meaning | keys |
|---|---|---:|
| S | a rule change inside machinery that exists | 26 |
| M | a new break point, scope kind or suppression mode | 33 |
| L | a subsystem that does not exist — a fitting pass, a preservation pass, a trivia rewriter, a doc-comment header wrap | 4 |

The 63 are not 63 independent pieces of work. They cluster:

- **23** are already wired and disagree at the export’s own value. ⚠ **They are not 23 defects.** For
  **17** of them the sweep records `BaselineAgrees = false` — the fixture already disagreed with the
  oracle before the key was touched — and those 17 sit on **8** fixtures, 12 of them on just three:
  `constructs/wrapping/patterns.cs` (5), `constructs/wrapping/base-list.cs` (4) and
  `constructs/preservation/lambda-parens.cs` (3). Until each fixture’s baseline is closed, nothing about
  those 17 rows is attributable to the key. The **6** with an agreeing baseline are the clean single-key
  fidelity fixes; 7 of the 23 agree at some other value, which localises the defect further.
- **9** are `xmldoc` keys the formatter reads and honours where the oracle answers differently
  (`OfUnoracled`); five of the nine are **one** disagreement, SK-DIV-0019’s wrap column.
- **8** more are `xmldoc` keys pending on two prerequisites — a renderer that can wrap a tag header (4) and
  one that can parse a processing instruction’s header (4).
- **4** are the `disable_*` family, which shares one suppression mechanism; two of the nine members of that
  family are already implemented, so the shape exists.
- **3** are the `force_chop_compound_*` triple, which is one break point.
- **3** are blocked on the polarity-aware `expands` the registry already records as missing.

## ⚠ Three recorded verdicts this pass overturns

All three are the same failure, and it is the one to watch for: **a flat probe was read as a fact about
the key when it was a fact about the fixture**, and a *distinct-output* measurement recorded elsewhere in
this repository contradicts it. A flat result cannot overturn one that produced two or more outputs — so
where two recorded measurements disagree, the one that saw the key move wins, and the question to ask of
the other is what its fixture could not reach.

All three sit in the same table: docs/plan/12 § “Unreachable — 34”. That table is otherwise sound — its
rows name their controls, which is why the conflict was findable at all — but it was produced in one pass
and three of its rows have been contradicted by later work that nobody went back to reconcile.

1. **`resharper_csharp_alignment_tab_fill_style` — `unreachable` → `masked`.** docs/plan/12 § “Unreachable”
   files it flat against an `int_align` control, on a family the export switches off entirely, so the probe
   produced no alignment column for the key to fill. `PhaseOneOptions.cs` ~1531 measures three *distinct*
   layouts of the same alignment column under `indent_style = tab`. The key is read; it is masked by the
   export’s `indent_style = space`; and `LayoutWriter.WriteIndentTo` implements `optimal_fill` under the
   name `use_spaces` — SK-DIV-0032.
2. **`resharper_align_multiline_type_parameter_constraints` — `unreachable` → `masked`.** Same table, same
   shape. `PhaseOneOptions.cs` ~1502 has the two-flip result: with
   `wrap_before_first_type_parameter_constraint = false` as well, a second `where` lands on the first
   `where`’s column. The export sets that key `true`, so the first `where` gets a line of its own and there
   is nothing left to align to.
3. **`resharper_outdent_ternary_ops` — `unreachable` → `implementable`.** ⚠ **This is the
   unprefixed-spelling trap, caught a third time.** The registry `summary` records the oracle moving a
   wrapped `?` and `:` left by two columns, re-measured at the ternary-chain work, with the reason the
   earlier probe missed it: it moves only the layout that wraps *at* the signs and leaves a nested chain —
   which wraps after each `:` — exactly where it was. The C# formatter reads this unprefixed spelling,
   unlike `align_ternary` beside it in the same list. And it is now **S**, not blocked:
   `IndentKind.OutdentColumns` was added for `outdent_binary_ops`, `outdent_binary_pattern_ops` and
   `outdent_dots`, and this is a fourth caller for it.

A fourth row of the same table, `resharper_alignment_tab_fill_style`, keeps its verdict but loses its
reason: the recorded argument was that its only C# target is itself unobservable, and row 1 shows that
target is observable under tabs. It stays unreachable because `OptionResolver` does not apply `expands`
to any generalized key — the finding already recorded against `resharper_int_align` — which is a
configuration-model gap, not a formatter one.

## ⚠ The unprefixed-spelling claim, key by key

A comment near `PhaseOneOptions.cs` ~1279 once said the unprefixed spellings belong to the C++ and VB
formatters and the C# formatter never reads them. **It is true for some keys and false for others**, and it
was refuted twice this year — for `align_multiline_type_parameter_list` and `outdent_ternary_ops`. Every
unprefixed key below carries its **own** measurement with a **named control** on the **same fixture**; none
inherits the blanket claim. Where the control was itself flat, the row says so instead of claiming a
verdict.

The claim holds, with a per-key control, for: `wrap_after_binary_opsign`, `wrap_after_dot`,
`wrap_arguments`, `wrap_base_clause_style`, `wrap_braced_init_list_style`, `wrap_ctor_initializer_style`,
`wrap_enumeration_style`, `wrap_before_colon`, `wrap_comments`, `align_multiline_array_initializer`,
`align_multiline_ctor_init`, `align_multiline_expression_braces`, `align_multiline_implements_list`,
`align_multiline_type_argument`, `align_multiline_type_parameter`, `align_ternary`, `expression_braces`,
`use_continuous_line_indent_in_expression_braces`, `int_align_eq`, `int_align_declaration_names`,
`int_align_enum_initializers` and the five lambda keys.

It **does not hold** for `alignment_tab_fill_style` (row 1 above), `align_multiline_type_parameter_list`,
`outdent_ternary_ops`, `place_simple_*_on_single_line` and the `keep_existing_*_patterns_arrangement` pair —
all of which the C# formatter demonstrably reads.

## How much of this is measurement and how much is inspection

| | keys |
|---|---:|
| measured against `jb cleanupcode`, with the probe or the control recorded | 226 |
| classified by inspection only, and flagged as such in the row | 4 |

The four are `resharper_csharp_allow_alias`, `resharper_csharp_can_use_global_alias`,
`resharper_csharp_qualified_using_at_nested_scope` and `resharper_remove_unused_only_aliases` — the
reference-qualification and `using`-directive family. Their measured family sibling
`resharper_csharp_prefer_qualified_reference` is decisive and belongs to `CSArrangeQualifiers` under the
cleanup profile, and these four are filed with it **on that resemblance and nothing else**. They are the
honest residue of this pass: 4 of 230.

⚠ Nothing in this document was measured by *this* pass. Every probe cited was run by an earlier one and is
recorded in `PhaseOneOptions.cs`, `XmlDocOptions.cs`, `docs/plan/12`, `docs/divergences.md`,
`Core/Rikarin.Skala.Options/options.json` or the committed sweep. What is new here is the join, the two
overturned verdicts, the export-value column, and the count.

## The rows

### Enforced at the export’s own value — 58

Skala and the oracle produce the same bytes at the value the export sets. Tier D because a value the
export never uses diverges. **Not a Tier A claim** — see the caveat above.

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_new_line_before_open_brace` | `none` | sweep `Divergent` on `constructs/braces/csharp_new_line_before_open_brace.cs`; agrees at the export's `none`, 2/15 values agree; diverges at `accessors`, `anonymous_methods`, `anonymous_types`, `control_blocks`, `events`, `indexers`, `lambdas`, `local_functions`, `methods`, `object_collection_array_initializers`, `properties`, `types`, `properties, types` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_align_multiline_list_pattern` | `false` | sweep `Divergent` on `constructs/wrapping/alignment.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_align_multiline_property_pattern` | `false` | sweep `Divergent` on `constructs/wrapping/alignment.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_align_multiline_statement_conditions` | `true` | sweep `Divergent` on `constructs/indentation/resharper_csharp_align_multiline_statement_conditions.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_empty_block_style` | `multiline` | sweep `Divergent` on `constructs/braces/resharper_csharp_empty_block_style.cs`; agrees at the export's `multiline`, 2/3 values agree; diverges at `together_same_line` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_pars` | `inside` | sweep `Divergent` on `constructs/indentation/delimiter-indent.cs`; agrees at the export's `inside`, 3/4 values agree; diverges at `none` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_preprocessor_if` | `no_indent` | sweep `Divergent` on `constructs/indentation/resharper_csharp_indent_preprocessor_if.cs`; agrees at the export's `no_indent`, 3/4 values agree; diverges at `outdent` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_preprocessor_other` | `no_indent` | sweep `Divergent` on `constructs/indentation/resharper_csharp_indent_preprocessor_other.cs`; agrees at the export's `no_indent`, 3/4 values agree; diverges at `outdent` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_primary_constructor_decl_pars` | `inside` | sweep `Divergent` on `constructs/indentation/delimiter-indent.cs`; agrees at the export's `inside`, 3/4 values agree; diverges at `outside_and_inside` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_raw_literal_string` | `align` | sweep `Divergent` on `constructs/trivia/resharper_csharp_indent_raw_literal_string.cs`; agrees at the export's `align`, 2/3 values agree; diverges at `indent` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_typearg_angles` | `inside` | sweep `Divergent` on `constructs/indentation/delimiter-indent.cs`; agrees at the export's `inside`, 3/4 values agree; diverges at `none` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_indent_typeparam_angles` | `inside` | sweep `Divergent` on `constructs/breaks/type-parameter-list.cs`; agrees at the export's `inside`, 3/4 values agree; diverges at `outside_and_inside` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_max_array_initializer_elements_on_line` | `10000` | sweep `Divergent` on `constructs/wrapping/initializers.cs`; agrees at the export's `10000`, 1/3 values agree; diverges at `0`, `1` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_max_enum_members_on_line` | `1` | sweep `Spurious` on `constructs/breaks/enum-members.cs`; agrees at the export's `1`, 1/2 values agree; diverges at `2` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_max_line_length` | `120` | sweep `Divergent` on `constructs/wrapping/initializers.cs`; agrees at the export's `120`, 1/3 values agree; diverges at `0`, `1` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_new_line_before_while` | `false` | sweep `Divergent` on `constructs/braces/resharper_csharp_new_line_before_while.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_accessor_attribute_on_same_line` | `false` | sweep `Divergent` on `constructs/placement/record-and-accessor-attributes.cs`; agrees at the export's `never`, 1/3 values agree; diverges at `if_owner_is_single_line`, `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_accessorholder_attribute_on_same_line` | `never` | sweep `Divergent` on `constructs/placement/attributes-on-own-line.cs`; agrees at the export's `never`, 1/3 values agree; diverges at `if_owner_is_single_line`, `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_comments_at_first_column` | `false` | sweep `Spurious` on `constructs/trivia/resharper_csharp_place_comments_at_first_column.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_method_attribute_on_same_line` | `false` | sweep `Divergent` on `constructs/preservation/attributes.cs`; agrees at the export's `never`, 1/3 values agree; diverges at `if_owner_is_single_line`, `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_simple_case_statement_on_same_line` | `if_owner_is_single_line` | sweep `Spurious` on `constructs/blank-lines/between-switch-sections.cs`; agrees at the export's `if_owner_is_single_line`, 2/3 values agree; diverges at `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_simple_embedded_statement_on_same_line` | `if_owner_is_single_line` | sweep `Spurious` on `constructs/indentation/resharper_csharp_indent_nested_for_stmt.cs`; agrees at the export's `if_owner_is_single_line`, 2/3 values agree; diverges at `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_simple_initializer_on_single_line` | `true` | sweep `Divergent` on `constructs/wrapping/initializers.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_simple_switch_expression_on_single_line` | `false` | sweep `Spurious` on `constructs/wrapping/switch-expression.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_place_type_constraints_on_same_line` | `true` | sweep `Divergent` on `constructs/breaks/type-constraints.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_after_unary_operator` | `false` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_after_unary_operator.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_around_relational_op` | `true` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_around_relational_op.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_around_shift_op` | `true` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_around_shift_op.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_before_array_rank_brackets` | `false` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_before_array_rank_brackets.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_before_new_parentheses` | `false` | sweep `Spurious` on `constructs/spaces/resharper_csharp_space_before_new_parentheses.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_before_singleline_accessorholder` | `true` | sweep `Spurious` on `constructs/spaces/resharper_csharp_space_before_singleline_accessorholder.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_between_accessors_in_singleline_property` | `true` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_between_accessors_in_singleline_property.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_space_in_singleline_accessorholder` | `true` | sweep `Divergent` on `constructs/spaces/resharper_csharp_space_in_singleline_accessorholder.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_special_else_if_treatment` | `true` | sweep `Divergent` on `constructs/braces/resharper_csharp_special_else_if_treatment.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_stick_comment` | `true` | sweep `Spurious` on `constructs/trivia/resharper_csharp_stick_comment.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_use_continuous_indent_inside_initializer_braces` | `true` | sweep `Spurious` on `constructs/indentation/resharper_csharp_use_continuous_indent_inside_initializer_braces.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_use_continuous_indent_inside_parens` | `true` | sweep `Spurious` on `constructs/indentation/resharper_csharp_use_continuous_indent_inside_parens.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_wrap_array_initializer_style` | `wrap_if_long` | sweep `Divergent` on `constructs/wrapping/initializers.cs`; agrees at the export's `wrap_if_long`, 2/3 values agree; diverges at `chop_always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_wrap_before_arrow_with_expressions` | `false` | sweep `Divergent` on `constructs/breaks/switch-expression-arms.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_wrap_enum_declaration` | `chop_always` | sweep `Spurious` on `constructs/breaks/enum-members.cs`; agrees at the export's `chop_always`, 1/3 values agree; diverges at `wrap_if_long`, `chop_if_long` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_wrap_for_stmt_header_style` | `chop_if_long` | sweep `Divergent` on `constructs/wrapping/for-header.cs`; agrees at the export's `chop_if_long`, 2/3 values agree; diverges at `wrap_if_long` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_csharp_wrap_switch_expression` | `chop_always` | sweep `Divergent` on `constructs/breaks/switch-expression-arms.cs`; agrees at the export's `chop_always`, 1/3 values agree; diverges at `wrap_if_long`, `chop_if_long` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_formatter_off_tag` | `@formatter:off` | sweep `Spurious` on `constructs/trivia/resharper_formatter_off_tag.cs`; agrees at the export's `@formatter:off`, 1/2 values agree; diverges at `@formatter:offx` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_formatter_on_tag` | `@formatter:on` | sweep `Spurious` on `constructs/trivia/resharper_formatter_on_tag.cs`; agrees at the export's `@formatter:on`, 1/2 values agree; diverges at `@formatter:onx` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_formatter_tags_accept_regexp` | `false` | sweep `Spurious` on `constructs/trivia/resharper_formatter_tags_accept_regexp.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_formatter_tags_enabled` | `true` | sweep `Spurious` on `constructs/trivia/resharper_formatter_tags_enabled.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_place_attribute_on_same_line` | `false` | sweep `Divergent` on `constructs/preservation/attributes.cs`; agrees at the export's `never`, 1/3 values agree; diverges at `if_owner_is_single_line`, `always` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_place_primary_constructor_initializer_on_same_line` | `true` | sweep `Divergent` on `constructs/breaks/constructor-initializer.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_space_before_colon_in_ctor_initializer` | `true` | sweep `Spurious` on `constructs/spaces/resharper_space_before_colon_in_ctor_initializer.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_space_before_trailing_comment_text` | `false` | sweep `Spurious` on `constructs/trivia/resharper_space_before_trailing_comment_text.cs`; agrees at the export's `false`, 1/2 values agree; diverges at `true` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_wrap_after_expression_lbrace` | `true` | sweep `Spurious` on `constructs/wrapping/initializers.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_wrap_before_expression_rbrace` | `true` | sweep `Spurious` on `constructs/wrapping/initializers.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_indent_size` | `4` | sweep `Spurious` on `constructs/xmldoc/resharper_xmldoc_indent_size.cs`; agrees at the export's `4`, 1/2 values agree; diverges at `1` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_indent_style` | `space` | sweep `Spurious` on `constructs/xmldoc/resharper_xmldoc_indent_style.cs`; agrees at the export's `space`, 1/2 values agree; diverges at `tab` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_linebreak_before_elements` | `summary,remarks,example,returns,param,typ…` | sweep `Spurious` on `constructs/xmldoc/resharper_xmldoc_linebreak_before_elements.cs`; agrees at the export's `summary,remarks,example,returns,param,typeparam,value,para`, 1/2 values agree; diverges at `summary,remarks,example,returns,param,typeparam,value,parax` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements` | `true` | sweep `Divergent` on `constructs/xmldoc/resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_linebreaks_inside_tags_for_multiline_elements` | `true` | sweep `Divergent` on `constructs/xmldoc/resharper_xmldoc_linebreaks_inside_tags_for_multiline_elements.cs`; agrees at the export's `true`, 1/2 values agree; diverges at `false` | n/a — enforced at the standard; only off-standard values differ |
| `resharper_xmldoc_max_blank_lines_between_tags` | `0` | sweep `Divergent` on `constructs/xmldoc/resharper_xmldoc_max_blank_lines_between_tags.cs`; agrees at the export's `0`, 2/3 values agree; diverges at `1` | n/a — enforced at the standard; only off-standard values differ |

### Implementable — 63

Real, reachable behaviour the oracle produces and Skala does not. This is the finish line.

⚠ **One pair of rows reads like a contradiction and is not.** `max_line_length` is here, banded `L`
on “needs a fitting pass”, while `resharper_csharp_max_line_length` is *enforced at the export’s
value* two sections up. Both are true: the margin is honoured at `120` — `BreakPlan` reads it and
the sweep agrees there — and the two engines part company at `0` and `1`, which are the degenerate
margins the int probe set offers and which no export sets. The `L` band is the cost of being right
across the option’s domain, not the cost of being right at the standard. Read the two rows together
or neither.

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_indent_braces` | `false` | sweep `Divergent` on `constructs/indentation/csharp_indent_braces.cs`; **disagrees at the export's `false`**, 0/2 values agree, baseline agreed | M — wired, wrong at every value; the rule needs re-deriving |
| `csharp_new_line_before_members_in_object_initializers` | `true` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable, blocked — same; and only observable on an initializer the single-line joiner does not put back | M — same, plus the single-line joiner has to leave the initializer alone |
| `csharp_preserve_single_line_blocks` | `true` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — forced expansion is a break point | M — forced expansion is a new break point |
| `csharp_space_around_binary_operators` | `before_and_after` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable, blocked — the polarity-aware `expands` the registry already records | M — blocked on the polarity-aware `expands` the registry records as missing |
| `csharp_space_between_parentheses` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable, blocked — same missing mechanism | M — same missing `expands` mechanism |
| `max_line_length` | *(unset)* | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — a fitting pass | L — a fitting pass |
| `resharper_csharp_align_first_arg_by_paren` | `false` | observable, shape recorded: arguments land on the `(`'s column plus one and the `)` on the `(`'s own column, one left of them. The recorded blocker was wrong on its own terms — `LayoutWriter.Scope` has carried `CloserLevel` separately since M3; what is missing is a caller (`PhaseOneOptions.cs` ~1544) | S — a second parameter on the `Align` path in `VisitDelimited`; no new model |
| `resharper_csharp_align_multiline_calls_chain` | `false` | observable and blocked: the anchor is the column the chain's first `.` lands on, which is a function of the *layout* — at 120 columns it is 26 past the receiver, at 70 it is the receiver's own column, and at 50 the oracle abandons the alignment. A one-key implementation on the 70-column reading was built, agreed there, disagreed by 26 columns at the repository margin, and was reverted (`PhaseOneOptions.cs` ~1556) | L — `AlignAnchor` is a source position resolved before the fitter; this needs the two interleaved |
| `resharper_csharp_align_multiline_comments` | `true` | measured, and ⚠ **`true` in the export** — the oracle pulls each ` * ` continuation onto the opening `/*`'s column plus one; Skala leaves it. A divergence at the export's own value, SK-DIV-0033 (`PhaseOneOptions.cs` ~1581) | M — it rewrites the interior of a comment token; the trivia rewriter that would own it does not exist |
| `resharper_csharp_align_multiline_expression` | `false` | measured, and the union is narrower than the name: on a binary chain it is byte-identical to `align_multiline_binary_expressions_chain` (Tier A); on a pattern chain it lands four columns left of `align_multiline_binary_patterns`; on a chained call or an argument list it changes nothing (`PhaseOneOptions.cs` ~1572) | M — an `Align` scope reads the column where it opens and cannot see the enclosing expression |
| `resharper_csharp_blank_lines_around_accessor` | `0` | sweep `Spurious` on `constructs/blank-lines/resharper_csharp_blank_lines_around_accessor.cs`; **disagrees at the export's `0`**, 0/3 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_blank_lines_around_auto_property` | `1` | sweep `Spurious` on `constructs/blank-lines/resharper_csharp_blank_lines_around_auto_property.cs`; **disagrees at the export's `1`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_blank_lines_around_single_line_local_method` | `0` | ⚠ **fixed** — `verify` now Conformant, 3/3 values, up from the recorded 2/3. The S banding was right that this was a fidelity fix in an existing rule, and wrong about which rule: the key was read correctly all along and `blank_lines_after_block_statements` was overwriting its answer, because that key tested "the previous token is a `}` that ends a statement in a list" rather than "the statement above is one *with* a child block". A single-line local function ends in a `}` and is not a block statement, so it took the other key's blank. Pinned by `constructs/blank-lines/after-a-block-statement.cs`. Tier D until the sweep re-runs | — |
| `resharper_csharp_blank_lines_around_single_line_property` | `0` | sweep `Spurious` on `constructs/blank-lines/resharper_csharp_blank_lines_around_single_line_property.cs`; **disagrees at the export's `0`**, 0/3 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_extra_spaces` | `remove_all` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable — **4 distinct outputs**, the widest in the set; needs a preservation pass that does not exist | L — needs a whitespace-preservation pass that does not exist |
| `resharper_csharp_force_chop_compound_do_expression` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — new break point | M — same break point |
| `resharper_csharp_force_chop_compound_if_expression` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — new break point | M — new break point in `BreakPlan.cs` (shared with the `do`/`while` pair) |
| `resharper_csharp_force_chop_compound_while_expression` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — new break point | M — same break point |
| `resharper_csharp_indent_anonymous_method_block` | `false` | measured and narrow: it aligns a lambda's braced body from the lambda's own parameter column instead of the call's continuation indent; `delegate(int v) { … }` does not move at either value (`PhaseOneOptions.cs` ~1786) | M — a new alignment scope shape (`Align` wrapping `Block`) |
| `resharper_csharp_int_align_binary_expressions` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable — `IntAlign.cs`, another agent's file | S — one more run kind in `CollectConditionalChains` |
| `resharper_csharp_keep_existing_list_patterns_arrangement` | `true` | sweep `Divergent` on `constructs/wrapping/patterns.cs`; **disagrees at the export's `true`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_keep_existing_property_patterns_arrangement` | `true` | sweep `Divergent` on `constructs/wrapping/patterns.cs`; **disagrees at the export's `true`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_nested_ternary_style` | `autodetect` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — **3 distinct outputs**, break point | M — new break point; 3 distinct oracle outputs |
| `resharper_csharp_place_simple_property_pattern_on_single_line` | `true` | sweep `Divergent` on `constructs/wrapping/patterns.cs`; **disagrees at the export's `true`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_after_declaration_lpar` | `false` | sweep `Divergent` on `constructs/preservation/lambda-parens.cs`; **disagrees at the export's `false`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_after_invocation_lpar` | `false` | sweep `Divergent` on `constructs/breaks/invocation-lpar-rpar.cs`; **disagrees at the export's `false`**, 1/2 values agree, baseline agreed | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_before_binary_pattern_op` | `true` | sweep `Divergent` on `constructs/breaks/binary-patterns.cs`; **disagrees at the export's `true`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_before_declaration_rpar` | `false` | sweep `Divergent` on `constructs/preservation/lambda-parens.cs`; **disagrees at the export's `false`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_before_extends_colon` | `false` | sweep `Divergent` on `constructs/wrapping/base-list.cs`; **disagrees at the export's `false`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_before_invocation_rpar` | `false` | sweep `Divergent` on `constructs/breaks/invocation-lpar-rpar.cs`; **disagrees at the export's `false`**, 1/2 values agree, baseline agreed | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_chained_binary_expressions` | `wrap_if_long` | sweep `Divergent` on `constructs/wrapping/binary-chains.cs`; **disagrees at the export's `wrap_if_long`**, 1/2 values agree, baseline agreed | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_chained_binary_patterns` | `wrap_if_long` | sweep `Divergent` on `constructs/wrapping/binary-chains.cs`; **disagrees at the export's `wrap_if_long`**, 1/2 values agree, baseline agreed | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_chained_method_calls` | `wrap_if_long` | sweep `Divergent` on `constructs/wrapping/chained-calls.cs`; **disagrees at the export's `wrap_if_long`**, 2/3 values agree, baseline agreed | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_extends_list_style` | `wrap_if_long` | sweep `Divergent` on `constructs/wrapping/base-list.cs`; **disagrees at the export's `wrap_if_long`**, 0/3 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_lines` | `true` | sweep `Divergent` on `constructs/wrapping/base-list.cs`; **disagrees at the export's `true`**, 1/2 values agree, baseline already diverged | S — wired already; a fidelity fix in an existing rule |
| `resharper_csharp_wrap_list_pattern` | `wrap_if_long` | sweep `Spurious` on `constructs/wrapping/patterns.cs`; **disagrees at the export's `wrap_if_long`**, 0/3 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_property_pattern` | `chop_if_long` | sweep `Divergent` on `constructs/wrapping/patterns.cs`; **disagrees at the export's `chop_if_long`**, 0/3 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_csharp_wrap_verbatim_interpolated_strings` | `no_wrap` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable-with-a-gap — **3 distinct outputs**, break point | M — a break point inside an interpolated string, which Skala emits as one piece |
| `resharper_disable_indenter` | `false` | measured and decisive on the oracle, and not read by Skala: spacing, blank lines and wrapping still apply, but a line that existed in the input keeps the leading whitespace it was written with and a line the wrap created starts at column zero. SK-DIV-0061 | M — a suppression mode in `LayoutWriter`; the family shares one mechanism with the three below |
| `resharper_disable_line_break_changes` | `false` | measured and decisive: no line break is added and none removed, blank runs included, while spaces and indentation still move. The union of `disable_blank_line_changes` and `disable_line_break_removal` plus the additions neither blocks. SK-DIV-0063 | M — same mechanism; `disable_formatter` and `disable_blank_line_changes` are already implemented, so the shape exists |
| `resharper_disable_line_break_removal` | `false` | measured and decisive, one direction only: a break the author wrote is never removed, one the wrapping rules want is still added. SK-DIV-0064 | S — the one-directional half of the mechanism above |
| `resharper_disable_space_changes` | `false` | ⚠ the recorded "inert at both values" is refuted and the *fixture* was the reason: asked on a file whose spacing is actually wrong, the oracle preserves every inter-token run byte for byte while still reindenting and rewrapping. Decisive, not read by Skala. SK-DIV-0062 | M — same suppression mechanism, applied to the gap layer |
| `resharper_int_align` | `false` | docs/plan/12 § "The third phase, part one": observable in the oracle, implementable — `IntAlign.cs`, another agent's file | M — the resolver has to apply `expands`; `IntAlign.cs` |
| `resharper_keep_existing_lambda_and_anonymous_function_parens_arrangement` | `true` | sweep `Spurious` on `constructs/preservation/lambda-parens.cs`; **disagrees at the export's `true`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_outdent_ternary_ops` | `false` | docs/plan/12 § "Unreachable" files it flat against `int_align_nested_ternary` / `max_line_length`, and the registry `summary` carries the distinct-output measurement that supersedes it: the oracle moves a wrapped `?` and `:` **left by two columns**. Re-measured at the ternary-chain work — it moves only the layout that wraps *at* the signs and leaves a nested chain, which wraps after each `:`, exactly where it was, which is why a probe cut on a nested chain reads flat. ⚠ **So the C# formatter does read this unprefixed spelling**, unlike `align_ternary` beside it | S — `IndentKind.OutdentColumns` is built and three sibling `outdent_*` keys already use it (`PhaseOneOptions.cs` ~1845); this needs a fourth caller at the ternary's wrap point |
| `resharper_wrap_before_comma_in_base_clause` | `false` | sweep `Spurious` on `constructs/wrapping/base-list.cs`; **disagrees at the export's `false`**, 0/2 values agree, baseline already diverged | M — wired, wrong at every value; the rule needs re-deriving |
| `resharper_xmldoc_alignment_tab_fill_style` | `use_spaces` | pending, not refused: it describes a wrapped tag header's continuation line and Skala never breaks inside a header. The oracle does, so the subject exists (docs/divergences.md § "The refusals that stand") | L for the group — a doc-comment renderer that can wrap a tag header; then S |
| `resharper_xmldoc_allow_far_alignment` | `false` | same group, same prerequisite | S once the header wrap exists |
| `resharper_xmldoc_attribute_indent` | `single_indent` | same group, same prerequisite | S once the header wrap exists |
| `resharper_xmldoc_attribute_style` | `do_not_touch` | same group, same prerequisite | S once the header wrap exists |
| `resharper_xmldoc_blank_line_after_pi` | `true` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0023 (a trailing space on the blank `///` line) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_linebreak_before_multiline_elements` | `true` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0021 (unnamed element content) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_linebreak_before_singleline_elements` | `false` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0020 (mixed content) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than` | `2147483647` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0019 (the wrap column) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_max_line_length` | `120` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0019 (the wrap column) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_pi_attribute_style` | `do_not_touch` | pending on the same prerequisite one construct over: a processing instruction is emitted verbatim, so its header has no attributes to space out. The export leaves this at `do_not_touch`, which is what the oracle was measured doing to `<?pi first = "1" second="2" ?>` — it left it alone (docs/divergences.md § "The refusals that stand") | M for the group — a PI renderer that parses its header; then S |
| `resharper_xmldoc_pi_attributes_indent` | `align_by_first_attribute` | same group, same prerequisite | S once the PI renderer exists |
| `resharper_xmldoc_space_after_last_pi_attribute` | `true` | same group, same prerequisite | S once the PI renderer exists |
| `resharper_xmldoc_spaces_around_eq_in_pi_attribute` | `false` | same group, same prerequisite | S once the PI renderer exists |
| `resharper_xmldoc_spaces_inside_tags` | `false` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0022 (`false` means "do not add", not "remove") | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_wrap_lines` | `true` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0019 (the wrap column) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_wrap_tags_and_pi` | `true` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0019 (the wrap column) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |
| `resharper_xmldoc_wrap_text` | `true` | read and honoured (`OfUnoracled`, `PhaseOneOptions.cs`); asked of `OracleProfile.DocComments` and answered differently — SK-DIV-0019 (the wrap column) | S — a renderer rule; four of the five SK-DIV-0019 keys are one fix |

### Masked — 12

Read, and invisible under this configuration because another key at the export’s own value decides
first. The per-option sweep flips one key and so cannot reach any of these.

| key | export value | evidence | size |
|---|---|---|---|
| `end_of_line` | `lf` | docs/plan/12 § "The third phase, part one": masked — `resharper_enforce_line_ending_style = false` in the export; the mark on it was already right | — |
| `resharper_align_multiline_type_parameter_constraints` | `false` | docs/plan/12 § "Unreachable" files it flat against the control `align_multiline_type_parameter_list`, but `PhaseOneOptions.cs` ~1502 carries the decisive two-flip measurement: with `wrap_before_first_type_parameter_constraint = false` as well, a second `where` lands on the first `where`'s column. The export sets that key `true`, which gives the first `where` a line of its own and leaves nothing to align to — a mask, not an unreachable key. A one-flip flat result cannot overturn a two-flip distinct one | — |
| `resharper_csharp_align_multiline_argument` | `false` | masked by `wrap_after_invocation_lpar = true` in the export, which gives the first argument a line of its own so there is nothing on the delimiter's line to align to. With the lpar key off as well it changes the output — two flips, so the per-option sweep cannot reach it (`PhaseOneOptions.cs` ~1512) | — |
| `resharper_csharp_align_multiline_for_stmt` | `false` | masked by `align_multiline_statement_conditions = true`, which the export sets. Re-measured three times, most recently in T5b: with only this key flipped the oracle returns `constructs/wrapping/for-header.cs` byte-identical; with the other key off too, the two values separate (`PhaseOneOptions.cs` ~1519; SK-DIV-0008) | — |
| `resharper_csharp_align_multiline_parameter` | `false` | masked by `wrap_after_declaration_lpar = true`, exactly as `align_multiline_argument` (`PhaseOneOptions.cs` ~1512) | — |
| `resharper_csharp_alignment_tab_fill_style` | `use_spaces` | ⚠ **contested, and the decisive measurement wins.** `PhaseOneOptions.cs` ~1531 measures three *distinct* layouts of the same alignment column under `indent_style = tab` — `use_spaces` = tabs to the block then spaces, `use_tabs_only` = the column rounded down, `optimal_fill` = floor(col/tab) tabs then the remainder — so the key is read and merely masked by the export's `indent_style = space`; `LayoutWriter.WriteIndentTo` implements `optimal_fill` under the name `use_spaces`, SK-DIV-0032. docs/plan/12 § "Unreachable" files it flat, but on an `int_align` fixture the export switches off, which produces no alignment column at all — a flat probe cannot overturn one that produced three outputs | S — rename and re-derive `WriteIndentTo`'s fill, once `indent_style = tab` is in scope |
| `resharper_csharp_indent_statement_pars` | `inside` | read by `ConditionLevels` and masked by `align_multiline_statement_conditions = true`, which the export sets: a level count has nothing to say about an absolute column. All four values return the same file while that key is on | — |
| `resharper_csharp_max_attribute_length_for_same_line` | `120` | docs/plan/12 § "The third phase, part one": masked by `place_*_attribute_on_same_line = false`; moves at `1` once they are `always` | — |
| `resharper_csharp_outdent_commas` | `false` | two measured facts, and both matter. (1) **Masked**: `wrap_before_comma = false` in the export puts the comma at the end of the line and a trailing comma is not something a line can be outdented by; with `wrap_before_comma = true` as well the oracle moves the leading comma from column 8 to 6 — the same width-plus-one rule the three implemented `outdent_*` keys use. (2) ⚠ Even unmasked it cannot use that mechanism: the outdent applies to the second and later items and not to the first, and `IndentKind.OutdentColumns` exempts the line the scope *opened* on — a point-level outdent, not a scope-level one (`PhaseOneOptions.cs` ~1873) | — |
| `resharper_csharp_place_simple_list_pattern_on_single_line` | `true` | docs/plan/12 § "The third phase, part one": masked by `keep_existing_list_patterns_arrangement = true` | — |
| `resharper_csharp_space_in_singleline_method` | `true` | ⚠ **masked, not inert, and the `OfInert` sentence in the source is wrong for the second time.** The recorded reason — "the shape it governs no longer exists" — is a statement about Skala at this export, and the oracle contradicts it: with `place_simple_method_on_single_line = true` and `keep_existing_declaration_block_arrangement = true` the key moves, and so does its Tier A sibling `space_in_singleline_anonymous_method` on the same fixture (docs/plan/12 § "Observable"). The mark is left in place because `AnInertKey_StillCannotBeObserved` asks about *Skala*, and that half still holds | — |
| `resharper_disable_int_align` | `false` | measured: `int_align = true` alone pads three adjacent declarations; `int_align = true` plus this key returns the export's own bytes. Decisive one key away from the export and inert at it — SK-DIV-0060 | — |

### Duplicate spelling — 24

Another key already does the job. Each row names the spelling that works and the control that
demonstrated it on the same fixture.

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_space_after_dot` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_space_around_dot` moved the same fixture under the same configuration; this spelling did not | — |
| `csharp_space_before_dot` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_space_around_dot` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_align_multiline_array_initializer` | `true` | unprefixed spelling; re-measured on an initializer that wraps at 60 columns — `false` returns it byte-identical while `resharper_csharp_align_multiline_array_and_object_initializer = true` (Tier A) beside it moves the elements to the brace's column (`PhaseOneOptions.cs` ~1483) | — |
| `resharper_align_multiline_implements_list` | `true` | docs/plan/12 § "The third phase, part one": `resharper_csharp_align_multiline_extends_list` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_align_multiline_type_parameter` | `true` | docs/plan/12 § "The third phase, part one": `resharper_align_multiline_type_parameter_list` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_csharp_space_within_new_parentheses` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_space_within_parentheses` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_int_align_declaration_names` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_int_align_fields` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_int_align_enum_initializers` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_int_align_fields` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_int_align_eq` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_int_align_variables` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_place_event_attribute_on_same_line` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_place_accessorholder_attribute_on_same_line` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_place_property_attribute_on_same_line` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_place_field_attribute_on_same_line` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_remove_blank_lines_near_braces` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_remove_blank_lines_near_braces_in_{code,declarations}` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_simple_block_style` | `do_not_change` | docs/plan/12 § "The third phase, part one": `resharper_csharp_place_simple_method_on_single_line` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_simple_case_statement_style` | `do_not_change` | docs/plan/12 § "The third phase, part one": `resharper_csharp_place_simple_case_statement_on_same_line` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_simple_embedded_statement_style` | `do_not_change` | docs/plan/12 § "The third phase, part one": `resharper_csharp_place_simple_embedded_statement_on_same_line` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_space_within_spread_pattern` | `true` | docs/plan/12 § "The third phase, part one": `resharper_csharp_space_within_slice_pattern` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_use_continuous_line_indent_in_expression_braces` | `false` | `resharper_csharp_use_continuous_indent_inside_initializer_braces` is the C# spelling of the same question, is implemented and is observable; measured at both values with the negative control recorded on `align_multiline_expression_braces` (registry `summary`) | — |
| `resharper_wrap_after_binary_opsign` | `true` | docs/plan/12 § "The third phase, part one": `resharper_csharp_wrap_before_binary_opsign` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_wrap_after_dot` | `false` | docs/plan/12 § "The third phase, part one": `resharper_csharp_wrap_after_dot_in_method_calls` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_wrap_arguments` | `wrap_if_long` | re-asked in T6 with its C# key in the same batch rather than inheriting the blanket claim: `wrap_arguments = chop_always` changes nothing while `csharp_wrap_arguments_style = chop_always` (Tier A) chops every argument list in the file, the ctor initializer included (`PhaseOneOptions.cs` ~2138) | — |
| `resharper_wrap_base_clause_style` | `wrap_if_long` | docs/plan/12 § "The third phase, part one": `resharper_csharp_wrap_extends_list_style` moved the same fixture under the same configuration; this spelling did not | — |
| `resharper_wrap_before_colon` | `false` | re-asked in T6 with its control: `wrap_before_colon = true` changes nothing in either spelling, while `wrap_before_extends_colon = true` in the same run moves the base list's `:` onto its own line (`PhaseOneOptions.cs` ~2143) | — |
| `resharper_wrap_braced_init_list_style` | `wrap_if_long` | measured at all three values: `chop_always` leaves a five-initializer file byte-identical, while `resharper_csharp_wrap_array_initializer_style = chop_always` and `..._wrap_object_and_collection_initializer_style = chop_always` both chop it one element per line (registry `summary`) | — |
| `resharper_wrap_ctor_initializer_style` | `wrap_if_long` | docs/plan/12 § "The third phase, part one": `resharper_csharp_wrap_arguments_style` moved the same fixture under the same configuration; this spelling did not | — |

### Unreachable / inert — 49

No input makes the key change the output. Each row names how that was established — which fixture,
which control, and where the control moved when the key did not.

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_style_prefer_utf8_string_literals` | `true:suggestion` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `the cleanup batch's own `indent_size` control` moved | — |
| `dotnet_style_prefer_collection_expression` | `when_types_loosely_match:suggestion` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_align_multiline_ctor_init` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_wrap_arguments_style` moved | — |
| `resharper_align_multiline_expression_braces` | `false` | measured at both values on a file with five wrapping initializers: byte-identical. Negative control on the same file — `wrap_object_and_collection_initializer_style = chop_always` and the array style — both rewrite it (registry `summary`) | — |
| `resharper_align_multiline_type_argument` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_align_ternary` | `align_not_nested` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_int_align_nested_ternary`, `max_line_length` moved | — |
| `resharper_alignment_tab_fill_style` | `use_spaces` | ⚠ the reason recorded in docs/plan/12 § "Unreachable" — "a generalized key whose only C# target is itself unobservable" — does not survive row 1 above, because that target **is** observable under `indent_style = tab`. The verdict stands on a different, separately recorded reason: `OptionResolver` does not apply `expands` at all, so setting any generalized key changes no value the formatter reads (the same finding recorded against `resharper_int_align`, `PhaseOneOptions.cs` ~1750). Unreachable in Skala, for a configuration-model reason rather than a formatter one | — |
| `resharper_blank_lines_around_global_attribute` | `0` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_blank_lines_after_using_list` moved | — |
| `resharper_continuous_line_indent` | `single` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_wrap_before_binary_opsign` moved | — |
| `resharper_csharp_allow_far_alignment` | `false` | re-measured in T5b with a run that unambiguously exists and is unambiguously far — `int_align_variables`/`int_align_fields` drag two runs' `=` to column 74 and flipping this key returns the file byte-identical, on locals and on fields (`PhaseOneOptions.cs` ~1687). ⚠ One lead recorded: at a 50-column margin with `align_multiline_calls_chain = true` the oracle abandons a chain alignment under a long receiver, which is where a future measurement should start | — |
| `resharper_csharp_instance_members_qualify_declared_in` | `this_class, base_class` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_csharp_int_align_fix_in_adjacent` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_int_align_fields` moved | — |
| `resharper_csharp_static_members_qualify_with` | `declared_type` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `..._qualify_members`, under the **cleanup** profile` moved` | — |
| `resharper_csharp_tab_width` | `4` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_csharp_use_indent_from_previous_element` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size` moved | — |
| `resharper_csharp_use_roslyn_logic_for_evident_types` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `csharp_style_var_for_built_in_types`, under **cleanup**` moved` | — |
| `resharper_declaration_body_on_the_same_line` | `if_owner_is_single_line` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_place_simple_method_on_single_line` moved | — |
| `resharper_disable_space_changes_before_trailing_comment` | `false` | the one rule it could gate is `space_before_trailing_comment`, and the oracle normalises the gap identically at both of that key's values with this one on; its broad sibling `disable_space_changes` does preserve the same gap. SK-DIV-0060 | — |
| `resharper_dont_remove_extra_blank_lines` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_keep_blank_lines_in_code` moved | — |
| `resharper_enable_wrapping` | `false` | named as though it were the master switch and measured not to be one: the export sets it `false` and wrapping visibly happens. Asked on a file that wraps four ways at 120 columns, at 60, and with `wrap_lines = false` — byte-identical at all three (registry `summary`; `PhaseOneOptions.cs` ~2173) | — |
| `resharper_expression_braces` | `inside` | measured at all four values on a file whose initializers wrap and whose `}` therefore sits on a line of its own — the shape a `ParenthesesIndentStyle` key would move — and the oracle returns it byte-identical each time, while the C# initializer wrap keys rewrite the same file (registry `summary`) | — |
| `resharper_expression_pars` | `inside` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_wrap_before_binary_opsign` moved | — |
| `resharper_ignore_space_preservation` | `false` | inert on every shape and pairing tried, including the three places the C# formatter demonstrably does preserve spaces — a disabled `#if` branch, an `@formatter:off` region and an int-aligned run. SK-DIV-0060 | — |
| `resharper_indent_aligned_ternary` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_indent_comment` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size`, `resharper_csharp_allow_comment_after_lbrace` moved | — |
| `resharper_indent_wrapped_function_names` | `false` | hunted rather than sampled: a wrapped call chain, a wrapped qualified name and a split declaration, then the same three under `wrap_after_dot_in_method_calls`, `align_multiline_calls_chain`, `outdent_dots` and `continuous_line_indent = double`. Every pairing returns what the control alone returns. ⚠ the C#-prefixed spelling was asked too and is equally inert (`PhaseOneOptions.cs` ~2195) | — |
| `resharper_keep_existing_line_break_before_declaration_body` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_keep_user_wrapping` | `true` | the whole four-corner table measured: `keep_user_linebreaks` decides and this key does not appear in the answer at either of its values, at 60 columns, at 120, or unbounded (`PhaseOneOptions.cs` ~2015) | — |
| `resharper_labeled_statement_style` | `line_break` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size` moved | — |
| `resharper_max_lambda_and_anonymous_function_parameters_on_line` | `10000` | one of five lambda-parameter keys re-asked in T6 with controls: at 120 and at 60 columns each returns the margin's own bytes, while `csharp_max_formal_parameters_on_line = 1` and `csharp_wrap_parameters_style = chop_always` both chop the lambda's list. Ten runs, five keys, two margins, four controls (`PhaseOneOptions.cs` ~2155) | — |
| `resharper_new_line_before_enumerators` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_parentheses_same_type_operations` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_parentheses_non_obvious_operations`, under **cleanup**` moved` | — |
| `resharper_place_namespace_definitions_on_same_line` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size` moved | — |
| `resharper_prefer_line_break_after_multiline_lparen` | `true` | hunted: a call whose argument is a two-statement lambda, one whose argument is a wide object initializer, and a nested call, at 120 and at 80, with and without `place_single_method_argument_lambda_on_same_line` and with `wrap_arguments_style = chop_always`. Inert at every one; the C#-prefixed spelling too (`PhaseOneOptions.cs` ~2202) | — |
| `resharper_prefer_roslyn_rules_for_parentheses_clarity` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_remove_spaces_on_blank_lines` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size` moved | — |
| `resharper_treat_case_statement_with_break_as_simple` | `true` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_place_simple_case_statement_on_same_line` moved | — |
| `resharper_use_continuous_line_indent_in_method_pars` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_wrap_parameters_style`, `indent_method_decl_pars` moved | — |
| `resharper_wrap_after_lambda_and_anonymous_function_declaration_lpar` | `false` | one of the five lambda keys above; same ten-run measurement | — |
| `resharper_wrap_before_lambda_and_anonymous_function_declaration_lpar` | `false` | one of the five lambda keys above; `csharp_wrap_before_declaration_lpar` is the key that moves a `delegate(…)`'s parenthesis | — |
| `resharper_wrap_before_lambda_and_anonymous_function_declaration_rpar` | `false` | one of the five lambda keys above; the declaration keys govern | — |
| `resharper_wrap_comments` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `same` moved | — |
| `resharper_wrap_enumeration_style` | `chop_if_long` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `resharper_csharp_keep_existing_enum_arrangement` moved | — |
| `resharper_wrap_lambda_and_anonymous_function_parameters_style` | `wrap_if_long` | one of the five lambda keys above; `csharp_wrap_parameters_style` chops that very list in the same batch | — |
| `resharper_xmldoc_insert_final_newline` | `false` | a `///` comment has no file end to put a newline at, and JetBrains' key index does not list XMLDOC among the languages that accept the key at all (docs/divergences.md § "The refusals that stand") | — |
| `resharper_xmldoc_tab_width` | `4` | it only changes how wide a tab is when measuring, and the only tab a re-wrap can meet is inside a `<code>` block, which is verbatim and never measured (docs/divergences.md § "The refusals that stand") | — |
| `resharper_xmldoc_wrap_around_elements` | `true` | ⚠ refused for a measured reason: with the doc-comment task enabled, at both values, over prose containing inline `<see/>`, `<c>` and `<b>` elements both long enough to wrap and short enough not to, the oracle's output is byte-identical (docs/divergences.md § "The refusals that stand") | — |
| `tab_width` | `4` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_style`, on a tab-indented fixture — see the note below` moved` | — |
| `trim_trailing_whitespace` | `false` | docs/plan/12 § "The third phase, part one": flat at every value on a fixture the control `indent_size` moved | — |

### Wrong subsystem — 10

The arranger’s or the cleanup profile’s, not `CSReformatCode`’s. The key-flip sweep excludes these by
name (`SweepPlan.Build`, `ArrangementOptions.Implemented`).

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_preferred_modifier_order` | `public, private, protected, internal, fil…` | docs/plan/12 § "The third phase, part one": moves only under `OracleProfile.Cleanup`, task `SortModifiers` — the arranger's, not `CSReformatCode`'s | — |
| `resharper_csharp_allow_alias` | `true` | the cleanup profile's `CSOptimizeUsings`/`CSArrangeQualifiers`, not `CSReformatCode`'s — ⚠ **by inspection**: no probe has been run on this key, only on its family sibling `prefer_qualified_reference` | — |
| `resharper_csharp_builtin_type_apply_to_native_integer` | `false` | docs/plan/12 § "The third phase, part one": moves only under `OracleProfile.Cleanup`, task `CSFixBuiltinTypeReferences` — the arranger's, not `CSReformatCode`'s | — |
| `resharper_csharp_can_use_global_alias` | `true` | same family and same status — ⚠ **by inspection**, unprobed | — |
| `resharper_csharp_force_attribute_style` | `separate` | docs/plan/12 § "The third phase, part one": moves only under `OracleProfile.Cleanup`, task `ArrangeAttributes` — the arranger's, not `CSReformatCode`'s | — |
| `resharper_csharp_prefer_qualified_reference` | `false` | measured and decisive, and it is a *qualification rewrite*: at `true` the oracle fully qualifies every simple name and drops the usings that become redundant; at the export's `false` it shortens an already-qualified reference. `CSArrangeQualifiers` under `OracleProfile.Cleanup` (registry `summary`) | M — an arranger rule, in the cleanup phase's scope, not the formatter's |
| `resharper_csharp_prefer_separate_deconstructed_variables_declaration` | `false` | docs/plan/12 § "The third phase, part one": moves only under `OracleProfile.Cleanup`, task `ArrangeVarStyle` — the arranger's, not `CSReformatCode`'s | — |
| `resharper_csharp_qualified_using_at_nested_scope` | `false` | same family and same status — ⚠ **by inspection**, unprobed | — |
| `resharper_parentheses_non_obvious_operations` | `none, shift, bitwise_and, bitwise_exclusi…` | docs/plan/12 § "The third phase, part one": moves only under `OracleProfile.Cleanup`, task `RemoveRedundantParentheses` — the arranger's, not `CSReformatCode`'s | — |
| `resharper_remove_unused_only_aliases` | `false` | the cleanup profile's `CSOptimizeUsings` — ⚠ **by inspection**, unprobed; the registry carries no summary beyond the vocabulary classification | — |

### Not a formatter option at all — 9

Code generation, naming, file templates, the IDE, or the file’s encoding. No formatter reads them.

| key | export value | evidence | size |
|---|---|---|---|
| `charset` | `utf-8` | docs/plan/12 § "The third phase, part one": flat under both profiles: `cleanupcode` does not re-encode a file | — |
| `file_header_template` | *(unset)* | docs/plan/12 § "The third phase, part one": ⚠ flat **by construction** — both oracle profiles set `CSUpdateFileHeader` to `False`, so no fixture in this repository can ever exercise it | — |
| `resharper_apply_on_completion` | `false` | docs/plan/12 § "The third phase, part one": the IDE's completion, not a file transformation at all | — |
| `resharper_configure_await_analysis_mode` | `disabled` | docs/plan/12 § "The third phase, part one": the analyser — it selects an inspection, not a layout | — |
| `resharper_default_exception_variable_name` | `e` | docs/plan/12 § "The third phase, part one": code generation | — |
| `resharper_event_handler_pattern_long` | `$object$On$event$` | docs/plan/12 § "The third phase, part one": code generation / naming | — |
| `resharper_event_handler_pattern_short` | `On$event$` | docs/plan/12 § "The third phase, part one": code generation / naming | — |
| `resharper_nullable_enable_for_new_files` | `false` | docs/plan/12 § "The third phase, part one": file templates | — |
| `resharper_support_vs_event_naming_pattern` | `true` | docs/plan/12 § "The third phase, part one": code generation / naming | — |

### Unresolved — 5

⚠ Reported rather than guessed. Four of the five were flat **and so was the control**, which proves
nothing about the key; the fifth has no second value known to be legal, so it cannot be flipped at
all. Each row says which.

| key | export value | evidence | size |
|---|---|---|---|
| `csharp_prefer_braces` | `true:none` | docs/plan/12 § "The third phase, part one": flat under both profiles, and no control on its own cleanup fixture moved | ? |
| `resharper_csharp_indent_braces_inside_statement_conditions` | `true` | docs/plan/12 § "The third phase, part one": the paired control `align_multiline_statement_conditions` was flat too; the fixture never chopped the condition | ? |
| `resharper_csharp_space_between_keyword_and_type` | `true` | docs/plan/12 § "The third phase, part one": its Tier A sibling `space_between_keyword_and_expression` was flat on the same fixture; the oracle closed `typeof (int)` up at **both** values, so something else owns that gap. The `OfInert` reason on it — a type after a keyword is word-like, so the separation is mandatory — is consistent with everything seen and is not *established* by it | ? |
| `resharper_prefer_wrap_around_eq` | `default` | docs/plan/12 § "The third phase, part one": a `string` option with no documented domain — `default`, `true` and `false` were tried and nothing is known to be legal | ? |
| `resharper_use_indents_from_main_language_in_file` | `true` | docs/plan/12 § "The third phase, part one": no control moved on any fixture tried; the name suggests a mixed-language (Razor) key and **that is a guess, which is why it is here** | ? |

## Method, and what would strengthen it

The evidence behind every row is one of five instruments, in descending order of strength:

1. **The committed key-flip sweep** — the option at every legal value, Skala against `jb cleanupcode`
   under the same configuration, on the fixture the registry’s `oracle` field names. Reaches 81 of the 230.
2. **A directed probe with a named control on the same fixture** — the technique docs/plan/12
   § “The third phase, part one” describes: a directory per probe, the export plus one overridden key, and
   in every batch both a positive control known to move the fixture and a negative control with no override.
   Reaches 91 more.
3. **A two-flip probe**, for a key the export masks. The per-option unit makes one flip, so these can never
   appear in the sweep and are recorded at the declaration instead.
4. **A profile probe** — the same key asked under `OracleProfile.Cleanup` or `OracleProfile.DocComments`,
   which is how the arranger’s keys and the doc-comment keys were separated from flat ones.
5. **Inspection**, which four rows rest on and say so.

### Re-deriving the 58 without trusting this document

The export-value column is a join of two committed files and nothing else, so it can be checked in a few
lines rather than believed. For each row of `conformance-sweep.json` whose `Key` is Tier D in
`options.json`: take that entry’s `default` — which is the export’s own value, per the registry’s own
`defaultSource` note — strip a trailing `:severity` for anything that is not a `string` option, resolve it
through the enum’s `valueAliases` (`false` → `never` for `PlacementStyle`, and so on), then find the
member of `Values` whose `Value` equals it and read its `Agree`. 58 are `true`, 23 are `false`, and every
value the export sets is in the probe set — there is no third bucket. ⚠ Do this before quoting the number:
the alias resolution is load-bearing (five rows move without it) and so is the severity strip.

⚠ **The same aliasing bug bit the first draft of this document.** Eight Tier D keys are registered in
`PhaseOneOptions.cs` under an *alias* spelling — `Of("resharper_keep_existing_list_patterns_arrangement")`
registers the canonical `resharper_csharp_keep_existing_list_patterns_arrangement` — so a scan that greps
the literal string reports them unregistered. Anything counting what the formatter reads has to resolve
through `OptionRegistry.TryResolve`, not through the spelling in the source.

What would make this document stronger, in order of value:

- **`oracle` fixtures for the 149 unswept Tier D keys.** The sweep is the only instrument here that reruns
  itself; everything measured by a directed probe is a one-off that goes stale silently. The 58
  enforced-at-export rows are the ones that would rot first, and they are the load-bearing half of the
  headline.
- **An int probe set of three legal values.** `max_line_length` is `DIVERGENT` partly because the probe set
  offers `0` and `1`, two degenerate margins the two engines already disagree about. That defect is shared
  with the pairwise pass and is named there.
- **A probe for the four inspection-only rows**, which is one cleanup-profile batch.
- **A polarity-aware `expands`**, which is one mechanism and unblocks three of the 63 at once.

