# 06 — Arrangement and Syntax Styles

Formatting moves whitespace. **Arrangement rewrites the tree**: it turns a block body into an
expression body, `List<int> x = new List<int>()` into `List<int> x = new()`, `!= null` into
`is not null`, and it deletes redundant `this.`, redundant parentheses, redundant braces and unused
usings. ReSharper calls this Code Cleanup; the keys live in the same `.editorconfig` and the author's
export configures 40 of them.

This is the half of the tool that keeps AI-written code in the house style, so it matters more here
than it does for a human team: an agent that has read a million lines of 2015-era C# writes
`new List<string>()`, `String.Format`, `x == null`, and block-bodied one-line methods, every time,
forever. Arrangement is the mechanical answer.

## The line between `format` and `arrange`

| | `skala format` | `skala arrange` |
|---|---|---|
| Changes | whitespace only | the syntax tree |
| Needs | a file | a `Compilation` |
| Safety check | token-stream equivalence (exact) | re-bind + diagnostic delta (below) |
| Runs in | pre-commit hook, agent loop, LSP on save | pre-commit hook when a project is loadable; CI; agent loop |
| Reversible by | reformatting | `git revert` |

`skala format --arrange` runs both. `skala check` reports arrangement violations as diagnostics with
fixes but does not apply them. ⚠ The default for `skala format` is **whitespace only**, because it
must work with no project, in under a second, on a file an agent just wrote.

### A few arrangements need no semantics

Brace insertion/removal, `default` literal, empty-string style, trailing commas, modifier order and
accessor order are pure syntax. They run in `format` when `--arrange=syntactic` (the default when no
compilation is available) and are the subset an agent gets for free.

## The catalogue

### Body styles

```ini
resharper_method_or_operator_body        = expression_body
resharper_accessor_owner_body            = expression_body
resharper_local_function_body            = expression_body
resharper_constructor_or_destructor_body = block_body
resharper_use_heuristics_for_body_style  = true
```

⚠ `use_heuristics_for_body_style = true` is what makes this liveable and it is easy to miss. Without
it, *every* single-statement method becomes an expression body, including ones where the result is
120 columns of unreadable ternary.

⚠ **M4 measured the heuristic against the oracle and two of the five conditions written here were
wrong.** The sweep is `constructs/arrangement/body-style/heuristics.cs` and its cleanup fixture.
What `jb cleanupcode` 2025.2.6 actually does, with the heuristic on:

| Condition | Status |
|---|---|
| (a) one statement, and it is a `return` **with a value** | ✅ — but *not* "an expression, `return`, or `throw`". A `throw` stays a block, as this doc said; and a bare expression statement — a `void` method's whole body — **also** stays a block. `void Helper() { _shared = 1; }` is not converted. The exception is an **accessor**: `set { _n = value; }` does become `set => _n = value;`. |
| (b) no comment in the body | ✅ |
| (c) the converted form fits `max_line_length` | ❌ **not a condition.** A 190-column body converts and the reformat that follows wraps it after the `=>`. Implementing (c) would refuse a conversion the oracle performs on every long one-line method in the corpus. |
| (d) not `async void` | ✅ |
| (e) no `#if` inside the member | ✅ |

So Skala's heuristic, as implemented and pinned: convert iff (a′) the body is one statement that is
a `return` with a value — or, in an accessor, an expression statement; (b) it has no comments;
(d) it is not `async void`; (e) the member has no `#if` inside. A constructor is exempt from (a′)
because it has no return value at all, so `constructor_or_destructor_body = expression_body` would
otherwise be a setting that could never fire.

`accessor_owner_body = expression_body` has two shapes and the key names only one: a property whose
only accessor is a `get` collapses onto the **property** (`public int P => _n;`); a property with
more than one accessor keeps its accessor list and each accessor gets an expression body. An indexer
is an accessor owner too.

Corresponding severities in the export: `arrange_method_or_operator_body_highlighting = none`,
`arrange_accessor_owner_body_highlighting = suggestion`, `arrange_local_function_body = none`. So the
*inspection* is mostly off while the *cleanup* setting is on — meaning: don't nag, but do fix it when
running cleanup. Skala reproduces that split exactly: `skala check` respects the highlighting
severity, `skala arrange` respects the style key. Conflating them produces a wall of suggestions
nobody asked for.

### Type inference and target typing

```ini
csharp_style_var_for_built_in_types = true      csharp_style_var_when_type_is_apparent = true
csharp_style_var_elsewhere          = true      resharper_use_roslyn_logic_for_evident_types = false
resharper_object_creation_when_type_evident     = target_typed
resharper_object_creation_when_type_not_evident = target_typed
resharper_default_value_when_type_evident       = default_literal
resharper_default_value_when_type_not_evident   = default_literal
resharper_builtin_type_apply_to_native_integer  = false
dotnet_style_predefined_type_for_locals_parameters_members = true
```

`var` everywhere, `new()` everywhere, `default` everywhere, `int` not `Int32`, but `nint` stays
`nint`. All four need semantics: `var` requires the initializer's type to be inferable and identical;
`new()` requires a target type that is not `var`, not `dynamic`, and not an anonymous type; `default`
requires no ambiguity in overload resolution.

⚠ `object_creation_when_type_not_evident = target_typed` is aggressive: it produces
`SomeVeryLongGenericType<A, B> x = new();`. That is the author's choice and Skala applies it, but
note the interaction with `var`: `var x = new SomeType()` is *also* legal under these settings and
ReSharper prefers `var` when the type is apparent from the right-hand side. The precedence — `var`
wins when the RHS names the type; target-typed `new` wins when the LHS names it — is a fixture set
(`constructs/arrangement/type-inference/`, 40 files), not a paragraph.

### Null and pattern style

```ini
resharper_null_checking_pattern_style = not_null_pattern      # `is not null`, not `!= null`
resharper_empty_string                = empty_literal         # `""`, not `string.Empty`
resharper_prefer_explicit_discard_declaration = false         # `out _`, not `out var _`
resharper_prefer_separate_deconstructed_variables_declaration = false
resharper_arrange_var_keywords_in_deconstructing_declaration_highlighting = suggestion
```

`is not null` is a real semantic change when the operand's type overloads `==`. Skala checks for a
user-defined `operator ==` on the operand type — **and on every base class**, because an operator
declared on a base applies to a derived operand — and skips the rewrite when one exists. `string` is
excluded from the check: its `==` is value equality and the pattern form matches it, so treating it
as dangerous would refuse every string null check in the corpus. This is a case where "what ReSharper
does" and "what is safe" can diverge, and Skala takes the safe side and reports the divergence in
`skala config explain`.

⚠ **M4 found that the divergence is not the one this section anticipated.** `jb cleanupcode` 2025.2.6
does not perform this rewrite *at all* — nor `string.Empty` ⇒ `""`, nor redundant-brace removal —
under any cleanup profile, with the inspections at their exported severities or raised to `warning`.
The sweep is `docs/oracle-cleanup-profile.md`. The reading that fits is that
`null_checking_pattern_style` and `empty_string` govern the pattern ReSharper **generates** in a
quick-fix, not a cleanup of code that already exists. Skala performs all three because the export
asks for them and this catalogue lists them; they are pinned by hand-written fixtures
(`ArrangementRuleTests`) rather than by the oracle, and excluded from the changed-span agreement
number, because measuring against an oracle that never moves would score every correct rewrite as a
divergence. See `SK-DIV-0013`.

### Qualification and redundancy

```ini
resharper_remove_this_qualifier = true            dotnet_style_qualification_for_* = false:suggestion
resharper_instance_members_qualify_declared_in = this_class, base_class
resharper_static_members_qualify_with = declared_type
resharper_braces_redundant = true                 csharp_prefer_braces = true:none
dotnet_style_parentheses_in_arithmetic_binary_operators = never_if_unnecessary:none
dotnet_style_parentheses_in_other_binary_operators      = always_for_clarity:none
resharper_prefer_roslyn_rules_for_parentheses_clarity   = false
```

⚠ The braces pair is contradictory on its face: `csharp_prefer_braces = true` (Microsoft: always use
braces) and `resharper_braces_redundant = true` (ReSharper: remove braces that add nothing). They are
not actually in conflict — ReSharper's `braces_redundant` governs *nested* redundant blocks
(`{ { x; } }`), not the braces of an `if`. Skala implements both with that reading, and
`skala config explain` prints the disambiguation, because the next person to read that file will
have the same doubt.

Parenthesis removal is the highest-risk rewrite in the whole tool: `never_if_unnecessary` for
arithmetic means `a + (b * c)` → `a + b * c`, which is correct and which people find alarming. Both
parenthesis keys carry severity `none` in the export, so the *inspection* is silent; the cleanup
still applies. Skala gates parenthesis removal behind `arrange --aggressive` for the first release
regardless, and revisits when the corpus differential shows zero divergences.

### Usings

```ini
resharper_sort_usings = true            dotnet_sort_system_directives_first = false
csharp_using_directive_placement = outside_namespace:silent
dotnet_separate_import_directive_groups = false
resharper_can_use_global_alias = true   resharper_qualified_using_at_nested_scope = false
resharper_remove_unused_only_aliases = false
resharper_blank_lines_after_using_list = 1
```

Sort alphabetically with `System` *not* hoisted, no group separation, outside the namespace, one
blank line after. Plain usings first, then aliases, then `using static` — measured, and one swap away
from Roslyn's own organiser, which puts `using static` before aliases. Removing unused usings needs
semantics and is the one rewrite that must consider the whole compilation: a using that looks unused
in one file may be required by a `#if` branch, or by an extension method resolved only under a
different target framework. Skala removes a using only when it is unused in **every** compilation the
file participates in — multi-targeting is not an edge case in this ecosystem.

Skala's answer to "is this using unused" is the compiler's own `CS8019`, not a hand-rolled reference
walk. A using carrying a comment is never removed: the comment is the author saying something about
that line, and a cleanup that deletes prose to save a using has made the file worse. Aliases and
`global using` are never removed either — a `global using` is used by files this one cannot see, so a
per-file answer is the wrong shape.

⚠ **This rule is excluded from the M4 agreement number, and the reason is about the oracle rather
than about Skala.** "Is this using needed" is a question about the references a project has, and the
oracle's scratch project has none but the shared framework — so `cleanupcode` deletes
`using NUnit.Framework;` from a file full of `[Test]` attributes, because `NUnit.Framework` does not
resolve there. Skala keeps it, correctly: an unresolvable using is `CS0246`, not `CS8019`. Scoring
Skala against that would reward deleting usings whose packages are missing. The rule is pinned by
`constructs/arrangement/usings/`, where every namespace resolves inside the corpus itself.

### Modifiers, accessors, attributes

`csharp_preferred_modifier_order` (a 19-element list including `file`, `required`, and ReSharper's
`closed`/`safe` which C# does not have — parsed, ignored, Tier C),
`resharper_arrange_accessors_order_highlighting = hint`,
`resharper_attribute_style = do_not_touch`, `resharper_sort_attributes = false`,
`dotnet_style_require_accessibility_modifiers = omit_if_default:suggestion`.

`attribute_style = do_not_touch` means attribute *merging* (`[A][B]` ↔ `[A, B]`) is off. Good:
that rewrite interacts with attribute targets and is rarely worth it.

## The modernization set

Everything above is what the export explicitly configures. The rules in
[08](08-rule-catalogue.md) § "SK1000 — Modernization" go further, and they are the reason this tool
exists in an AI-heavy workflow. They are *rules with fixes*, not cleanup settings, because they need
a judgement call more often than a body-style conversion does:

| Rewrite | ReSharper inspection (severity in export) | Skala rule |
|---|---|---|
| Collection expression `[…]` | `dotnet_style_prefer_collection_expression = when_types_loosely_match:suggestion` | `SK1001` |
| Primary constructor | `convert_to_primary_constructor` (suggestion) | `SK1002` |
| `field` keyword (C# 14) | `convert_to_auto_property*` (suggestion/hint) | `SK1003` |
| Extension block (C# 14) | `convert_to_extension_block` (hint) | `SK1004` |
| File-scoped namespace | `csharp_style_namespace_declarations = file_scoped:suggestion` | `SK1005` |
| `using` declaration | `convert_to_using_declaration` (suggestion) | `SK1006` |
| Pattern matching over `if`/`is`/cast | several | `SK1010`–`SK1015` |
| `ArgumentNullException.ThrowIfNull` | — | `SK1020` |
| `[GeneratedRegex]` over `new Regex` | — | `SK1021` |
| `SearchValues<T>` over `IndexOfAny(char[])` | — | `SK1022` |
| `System.Threading.Lock` over `lock(object)` | — | `SK1023` |
| `TimeProvider` over `DateTime.Now` | — | `SK1024` |
| Frozen/Immutable collections for static lookup tables | — | `SK1025` |
| UTF-8 string literals `"…"u8` | `csharp_style_prefer_utf8_string_literals = true:suggestion` | `SK1026` |
| `??=`, compound assignment | `convert_to_null_coalescing_compound_assignment` (suggestion) | `SK1030` |
| Null-conditional assignment (C# 14) | — | `SK1031` |
| `params ReadOnlySpan<T>` | — | `SK1032` |

Where a ReSharper inspection exists, its `resharper_*_highlighting` key configures the Skala rule
directly ([03](03-configuration-model.md) § "Severities"). Where none exists, `dotnet_diagnostic.SK…`
does, and the rule ships at `suggestion` — the level at which an agent sees it and a human is not
nagged.

## Safety

Arrangement changes the tree, so [04](04-formatting-engine.md)'s token-equivalence check does not
apply. Three layers instead:

1. **Conservative rewriters.** Every rewrite has an explicit precondition list, checked against the
   `SemanticModel`, and bails out to "no change" on anything it does not understand. The bar is:
   a rewrite that cannot prove its precondition does not run. There is no "probably fine".
2. **Re-bind and diff.** After rewriting a document, re-parse and re-bind it inside the same
   compilation and compare the diagnostic set. Any new error or warning ⇒ revert the file, report
   `SK9098`, drop a crash artefact. This catches the entire class of "rewrite was legal in isolation
   but not in context" bugs — overload resolution changing under `var`, an ambiguity introduced by
   removing a qualifier.
3. **Symbol-identity check on touched nodes.** For rewrites that change a name's resolution surface
   (qualifier removal, using removal, `var`), compare `GetSymbolInfo` before and after for every
   identifier in the changed span. Different symbol, same name ⇒ revert. This is the check that
   catches the genuinely dangerous case where the code still compiles but now calls something else.

Layer 2 costs a re-bind per changed document, which is why `arrange` is minutes-scale on a large tree
and `format` is seconds-scale. That is the correct trade: whitespace is cheap and constant, tree
rewrites are rare and must be right.

## Interaction with the formatter

Arrangement runs **before** the document is built (step 2 of the pipeline), and the rewritten tree is
then formatted normally. It never emits whitespace decisions of its own — a rewriter that also
formats is a rewriter whose output disagrees with the formatter. Every rewriter emits nodes with
elastic trivia and lets step 3 lay them out, which is exactly how Roslyn's own code fixes work and is
the reason arrangement + format is idempotent as a pair rather than only individually.
