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

### `@formatter:off` binds arrangement

⚠ It did not, and that is the worst place for the escape hatch to leak. `format` moves whitespace and
`arrange` moves the tree, and the table above says the second is reversible only by `git revert` —
so the pass a person most needs the tag to bind is the one it did not.

Every rule of the catalogue derives from `GuardedRewriter`, whose `Visit` is sealed: twelve rewriters
each remembering to ask the guard is twelve chances to forget, and the one that forgets is the one
that eats somebody's table. `UsingsRule` rebuilds the using block by hand rather than through a
rewriter, so `Arranger` also checks the whole rule's output for region survival before keeping it.

⚠ **The oracle's cleanup profile does not do this**, measured under `SkalaCleanup`, and Skala diverges
on purpose — SK-DIV-0016, with the fixtures in `constructs/arrangement/formatter-tags/`. Doc 00's
non-negotiable 9 applies: the reference tool is a test subject, not a specification, and the user's
expectation is the requirement.

The boundary cases — the straddling node, the containing node, the unterminated `off`, which comments
count as tags — are in [04](04-formatting-engine.md) § "Formatter tags", because they are one set of
rules serving both halves and two copies of them is one too many.

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
resharper_empty_string                = string_empty          # `string.Empty`, not `""`
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
still applies.

⚠ **The `--aggressive` gate is lifted.** It was set for the first release with the condition for
revisiting written down — "when the corpus differential shows zero divergences" — and that condition
was the wrong test, because a gated rule contributes divergences by being gated. What settled it is
the price, measured both ways over 401 corpus files against the cleanup profile: **59.43 % agreed
with the gate on, 63.68 % with it off**, so the gate cost 4.25 points against an oracle whose own
profile removes these by default. [17](17-inspection-parity.md) then made it the largest single item
in the whole parity measurement. SK-DIV-0014 is retired.

⚠ **What actually justified lifting it is not the number — it is that the rule no longer guesses.**
The gated version carried a precedence table and was arithmetic-only, because a table is exactly as
trustworthy as whoever wrote it. The rule now removes the parentheses, prints the enclosing
expression, parses it back, and keeps the edit only if the re-parsed tree is structurally equivalent
to the one it built. "Redundant" *means* "deleting them re-parses to the same tree", so checking that
directly is both safer than a table and much broader: it covers casts, unary operators, invocations
and nesting that the table version declined, and it refuses `a - (b - c)` and `a / (b / c)` without
anyone having to remember that subtraction and division are not associative.

Which parentheses Skala is *willing* to drop stays a separate question, settled by the export and
measured against `jb cleanupcode` rather than read off the key names:

| | |
|---|---|
| Removed | arithmetic and relational operands, casts, unary operators, invocations, nested parentheses, and a parenthesized initializer |
| Kept | an operand that is itself a shift, bitwise, `&&`, `\|\|` or `??` expression |
| Kept | **any** operand of a shift or bitwise expression, whatever the operand is |
| Declined | assignments and conditionals inside parentheses, the operand of a cast, and anything the re-parse does not prove |

⚠ **The proof refuses one class the oracle removes, and that refusal is the better answer.** Equal
precedence is not associativity: `a * (x * y)` re-parses as `(a * x) * y`, a different tree, so the
parentheses stay. On `float` they are load-bearing — Vixen's `SphericalHarmonics` writes
`coefficients.L2m2 * (x * y)` and the grouping is the author's arithmetic, not decoration. This is
the whole residue of `ArrangeRedundantParentheses` after the rewrite: **6 findings, down from
1 231**, and every one of them is this shape.

⚠ **The second "kept" row is the one that was got wrong first.**
`resharper_parentheses_non_obvious_operations = shift, bitwise_*` reads like a statement about which
parenthesized expressions to keep, and it is a statement about which *enclosing* operations need
their operands clarified. `a & (b + 1)` and `a << (b + 1)` keep their parentheses even though the
inner expression is plain arithmetic. The first implementation keyed on the inner expression alone,
agreed with the oracle on every case in the fixture that had been written for it, and stripped those
two anyway — found by reading what it did to Vixen's `BitReader`, not by a test. A fixture containing
only the cases you thought of is a fixture that agrees with you.

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

## ⚠ The fifteen Tier D arrangement options, settled

[17](17-inspection-parity.md) measured fifteen arrangement options as **Tier D — declared, not
implemented** — and concluded that they, rather than the 586-inspection rule gap, are what mostly
stands between Skala and retiring ReSharper. Each was probed against `jb cleanupcode` 2025.2.6 before
anything was written. **Eleven moved to Tier A; three did not, and one of the fifteen turned out not
to be a rewrite at all.**

| Inspection | Key | Verdict |
|---|---|---|
| `ArrangeRedundantParentheses` | `parentheses_redundancy_style` | ✅ Tier A. Rewritten against a re-parse proof; the `--aggressive` gate lifted |
| `ArrangeNamespaceBody` | `csharp_style_namespace_declarations` | ✅ Tier A. `namespace N { … }` ⇒ `namespace N;` |
| `BuiltInTypeReferenceStyleForMemberAccess` | `dotnet_style_predefined_type_for_member_access` | ✅ Tier A — see below, it was implemented already |
| `ArrangeStaticMemberQualifier` | `static_members_qualify_members` | ✅ Tier A, both directions |
| `ArrangeTrailingCommaInMultilineLists` | `trailing_comma_in_multiline_lists` | ✅ Tier A |
| `ArrangeTrailingCommaInSinglelineLists` | `trailing_comma_in_singleline_lists` | ✅ Tier A |
| `ArgumentsStyleLiteral` | `arguments_literal` | ✅ Tier A |
| `ArgumentsStyleStringLiteral` | `arguments_string_literal` | ✅ Tier A |
| `ArgumentsStyleAnonymousFunction` | `arguments_anonymous_function` | ✅ Tier A |
| `ArgumentsStyleOther` | `arguments_other` | ✅ Tier A |
| `SuggestVarOrType_DeconstructionDeclarations` | `prefer_explicit_discard_declaration` | ✅ the **key** is Tier A; ⚠ the **inspection** is not — see below |
| `ArrangeAttributes` | `place_attribute_on_same_line` | ✅ Tier A **without a rewrite** — see below |
| `ArrangeThisQualifier` | `instance_members_qualify_declared_in` | ⚠ stays **D**, honoured vacuously |
| `SeparateControlTransferStatement` | `blank_lines_before_control_transfer_statements` | ⚠ stays **D**, wrong component |
| `UnnecessaryWhitespace` | `trim_trailing_whitespace` | ⚠ stays **D**, the oracle ignores the key |

### ⚠ Two of the fifteen were measurement artefacts, not missing work

`dotnet_style_predefined_type_for_member_access` was **already implemented** and credited to the key
beside it: `PredefinedTypeRule` read
`dotnet_style_predefined_type_for_locals_parameters_members` and applied it to both a declaration
(`Int32 x`) and a member-access receiver (`Int32.MaxValue`). The behaviour shipped; the key could not
be observed through its own value, so it read as Tier D. The fix is to read the right key in the
right position, not to write a rewrite.

`resharper_place_attribute_on_same_line` is a **generalized** key: the resolver expands it into the
six `place_*_attribute_on_same_line` keys, every one of which the formatter implements and pins.
`PhaseOneOptions` had it as `OfInert` when `OfGeneralized` — a mechanism that already existed for
exactly this shape, and which checks that at least one expansion target is really implemented — is
what it is. Also no rewrite.

⚠ **And five more were unmeasurable rather than unimplemented.** `ArrangeNamespaces` and
`ArrangeArgumentsStyle` are real cleanup tasks that the M4 profile sweep never probed, so the oracle
was running without them and declining five of the export's own settings. See
[`../oracle-cleanup-profile.md`](../oracle-cleanup-profile.md) § "Two tasks the first sweep missed".
**Seven of the fifteen were therefore artefacts of how the gap was measured**, which is worth more
than the seven: it is the reason to re-run a measurement rather than re-read it.

### ⚠ Twelve keys moved, and eleven inspections — the difference is one mismapping

`SuggestVarOrType_DeconstructionDeclarations` is the one place where promoting the option does *not*
retire the inspection, and saying so is the point of this paragraph.

The option `resharper_prefer_explicit_discard_declaration` is genuinely Tier A: it is implemented,
it is observable (at `true` the oracle turns `out _` into `out var _`, measured), and a cleanup
fixture pins it. But the inspection doc 17 attached to it reports something else — `var (a, b)`
against explicit types in a deconstruction — and *that* is governed by
`resharper_for_deconstruction_declarations`, which the author's export **does not set at all**. It
is therefore not in the option registry, so `gov.json` — which names it first, correctly — had no
registry entry to match and fell through to the second key in its list.

⚠ **Counting the inspection as retired because its fallback key moved would be exactly the
double-count [17](17-inspection-parity.md) warned about**, in the direction it warned about: crediting
Skala with arrangement it does not perform. Measured on Vixen, `SuggestVarOrType_DeconstructionDeclarations`
fires on `foreach (var (identity, instance) in instances)` and Skala leaves those alone, correctly,
because nothing in the configuration asks otherwise.

**So: twelve option keys moved D → A, and eleven of the fifteen inspections are retired.**

### ⚠ The three that stay Tier D, with the reason beside each

Doc 03's Tier A is a claim about *behaviour*, not about wiring, and "the option is read" is not
evidence. These three are read and cannot change a byte of output on this repository's
configuration, so promoting them would be adding to the 69-of-201 unsubstantiated Tier A claims the
conformance sweep already found.

1. **`resharper_instance_members_qualify_declared_in`** — the key scopes *which declaring types* get
   a `this.` added, and adding one is governed by `resharper_instance_members_qualify_members`, which
   **is not in the author's export and therefore not in the option registry at all**. A scope for an
   action that never happens cannot be observed. ⚠ The *inspection* `ArrangeThisQualifier` is
   nonetheless covered: removing `this.` is `ThisQualifierRule` under `resharper_remove_this_qualifier`,
   which is Tier A and pinned. Doc 17's `gov.json` maps the inspection to the scoping key, and that
   mapping is what put it on the list.
2. **`resharper_blank_lines_before_control_transfer_statements`** — measured observable: at `1` the
   oracle inserts a blank line before `return`, `continue`, `break` and `throw`. The export writes
   `0`, so it does nothing here, but that is not why it stays D. ⚠ **It is a blank-line option and
   blank lines belong to the formatter**, which is the component that owns that family; the arranger
   "never emits whitespace decisions of its own" is the first line of `Arranger`'s own contract.
   Implementing it here to move a count would put a whitespace rule in the tree-rewriting pass.
3. **`trim_trailing_whitespace`** — already recorded in the registry from an earlier probe, and
   re-confirmed: `jb cleanupcode` ignores the key outright. Skala matches the oracle rather than the
   key, so it is inert in both directions.

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
| `ArgumentNullException.ThrowIfNull` | — | `SK1020` ⚠ retired (#281), hosted by `CA1510` |
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

### The bar, re-measured with the new rewrites in

M4's bar was "arrangement over Vixen introduces zero compiler diagnostics, measured *independently*
of the safety layer that makes it true", and the seven rewrites added since then do not move it.
Over the ten projects [17](17-inspection-parity.md) measured, from a `git archive` scratch copy:

| | |
|---|---|
| files considered | 2 071 |
| files arranged | 1 236 |
| ⚠ **new compiler diagnostics** | **0** |
| reverted by the re-bind | 2 (0.10 %) |
| reverted by the symbol check | 0 |
| did not converge | 0 |

The two reverts are both `CS8754` — "there is no target type for `new(…)`" — from the
target-typed-`new` rule that predates this work, in one file. The new rewrites fired 472 times
(parentheses), 216 (argument style) and 5 (`this.` qualifier) without producing a diagnostic between
them.

⚠ **The first attempt at this measurement measured nothing, and the way it failed is worth keeping.**
The scratch tree came from `git archive`, which does not include `.git` — so
`FormatCommand.FindRepositoryRoot` walked to the filesystem root, found nothing, and the binlog was
never located. `arrange` fell back to the syntactic subset and reported "N files were in no loaded
compilation", which is a correct and quiet line, and every semantic rule silently did nothing. The
run looked successful: files changed, no errors. It was only visible because the *fire counts* for
the semantic rules came back identical before and after. A scratch tree needs a repository root
before it is a subject.

## Interaction with the formatter

Arrangement runs **before** the document is built (step 2 of the pipeline), and the rewritten tree is
then formatted normally. It never emits whitespace decisions of its own — a rewriter that also
formats is a rewriter whose output disagrees with the formatter. Every rewriter emits nodes with
elastic trivia and lets step 3 lay them out, which is exactly how Roslyn's own code fixes work and is
the reason arrangement + format is idempotent as a pair rather than only individually.
