# What the corpus does not contain

⚠ **This is the complement of `fidelity constructs`, and it is the one with a deadline on it.**
`ConstructReport` answers *of the constructs the corpus contains, which diverge*. It cannot answer the
other half, and the other half is the half `jb cleanupcode`'s retirement makes permanent: a construct
that appears nowhere in `Testing/corpus/` has no fidelity number, no fixture, no divergence entry and
no sweep row. It is invisible to every instrument this repository has, and once ReSharper is
uninstalled **no authoritative `.expected.cs` for it can ever be authored**, so it stays invisible for
good.

Re-measure with `fidelity coverage [set…] [--table=<path>]`. It needs no oracle.

## The headline

Measured 2026-08-31 over every parseable input in `constructs/`, `real/` and `pathological/`, at
`4c27ab65` plus the extension-cref addition:

| | before this audit | after |
|---|---:|---:|
| node kinds the pinned Roslyn declares | 290 | 290 |
| exercised | 253 | **282** |
| absent | 37 | **8** |
| thin (1–50 occurrences) | 120 | 147 |
| over the R1 threshold (>50) | 133 | 135 |
| token-level constructs probed | 14 | 14 |
| of those, absent | **4** | **0** |

The audit closed 29 of the 37 absent node kinds and all four absent token-level constructs. The eight
that remain are argued below; every one of them is a judgement, not an oversight.

⚠ **"Thin" rose, and that is the expected direction.** A fixture that introduces a construct moves it
from *absent* to *present once*, which is a real improvement and still not coverage: docs/plan/16 § R1
sets the bar at 50 occurrences, and a construct pinned by one file is pinned at one configuration by
one author's choices. The thin band is the next reader's queue, not a finished result.

## Two corrections to the probe, made before any conclusion was drawn from it

⚠ **The node predicate is a range check, and three tokens sit above the boundary.**
`NodeLayouts.IsNodeKind` is `(int)kind >= 8598`, and `InterpolatedSingleLineRawStringStartToken`,
`InterpolatedMultiLineRawStringStartToken` and `InterpolatedRawStringEndToken` are above it. They are
the `$"""` and `"""` delimiters of an interpolated raw string — **tokens, never nodes** — so no corpus
can ever exercise them, and counting them made the absent list three rows longer than the real gap.
They are excluded in `SyntaxCoverage.IsNodeKind` rather than corrected in `corpus/syntax-kinds.txt`,
because that inventory is asserted against `NodeLayouts.Classify` and does faithfully record what
`Classify` returns. The denominator here is therefore 290, not 293.

⚠ **A `checked` keyword is not a checked operator.** The first version of the token probe counted
`CheckedKeyword` and reported three checked *operator declarations* where the answer was one — the
other two were `checked(…)` expressions, which have their own node kind and their own coverage. The
probe now matches on `OperatorDeclarationSyntax`/`ConversionOperatorDeclarationSyntax` with a
`CheckedKeyword`.

## The blind spot a kind census structurally has

⚠ **The `SyntaxKind` enumeration is exhaustive over *nodes*, and several of the newest and most
format-sensitive constructs in C# are not nodes.** A raw string literal is a `StringLiteralExpression`
like any other and differs only in its token; `required`, `scoped`, `file` and `ref readonly` are
modifier tokens; a generic attribute is an `Attribute` whose name happens to be a `GenericName`; a
primary constructor is a `ParameterList` hanging off a type declaration. Reading "282 of 290 node
kinds" as "almost every construct" would be exactly the mistake this file exists to prevent.

`SyntaxCoverage.Probes` therefore asks fourteen shape questions directly. **All four of the zeros it
found were modern C#, and none of them was visible to the kind census:**

| construct | before | after |
|---|---:|---:|
| generic attribute (C# 11) | **0** | 12 |
| alias to a non-name type (C# 12) | **0** | 8 |
| `ref readonly` parameter (C# 12) | **0** | 3 |
| `scoped` modifier (C# 11) | **0** | 11 |
| interpolated raw string | 2 | 8 |
| UTF-8 string literal | 1 | 6 |
| `file`-local type | 1 | 4 |
| static abstract interface member | 1 | 5 |
| checked operator (C# 11) | 1 | 3 |
| `required` member | 8 | 17 |
| raw string literal | 58 | 59 |
| primary constructor on a class/struct | 30 | 35 |
| nested collection expression | 28 | 28 |
| collection expression as an argument | 309 | 309 |

## What was added

Nineteen fixtures, all in `constructs/syntax/`.

| fixture | the kinds and shapes it introduces |
|---|---|
| `syntax/event-accessors.cs` | `EventDeclaration`, `AddAccessorDeclaration`, `RemoveAccessorDeclaration` |
| `syntax/field-keyword.cs` | `FieldExpression` — the C# 14 `field` keyword |
| `syntax/generic-constraints.cs` | `AllowsConstraintClause`, `RefStructConstraint`, `DefaultConstraint` |
| `syntax/checked-and-unchecked.cs` | `CheckedStatement`, checked operator and conversion declarations |
| `syntax/unsafe-and-function-pointers.cs` | `UnsafeStatement`, `FunctionPointerCallingConvention`, `FunctionPointerUnmanagedCallingConvention(List)` |
| `syntax/null-conditional-element-access.cs` | `ElementBindingExpression` |
| `syntax/compound-assignments.cs` | `ModuloAssignmentExpression`, `UnsignedRightShiftAssignmentExpression` |
| `syntax/generic-attributes.cs` | generic attributes, in every attribute position the language has |
| `syntax/alias-any-type.cs` | alias-any-type: tuple, array, jagged, rank-2, delegate and generic aliases |
| `syntax/scoped-and-ref-readonly.cs` | `ScopedType`, `scoped` parameters, `ref readonly` parameters |
| `syntax/stackalloc-initializers.cs` | `ImplicitStackAllocArrayCreationExpression` |
| `syntax/extension-blocks.cs` | `ExtensionBlockDeclaration` outside `pathological/` |
| `syntax/raw-and-utf8-strings.cs` | interpolated raw strings, `u8` literals |
| `syntax/goto-and-labels.cs` | `GotoDefaultStatement`, labels on blocks |
| `syntax/required-and-file-local.cs` | `required`, `file`, static abstract interface members |
| `syntax/query-joins.cs` | `JoinClause`, `JoinIntoClause` beyond one instance each |
| `syntax/varargs-and-typed-references.cs` | `ArgListExpression`, `MakeRefExpression`, `RefTypeExpression`, `RefValueExpression` |
| `syntax/line-directives.cs` | `LineSpanDirectiveTrivia`, `LineDirectivePosition` |
| `syntax/cref-member-forms.cs` | `IndexerMemberCref`, `OperatorMemberCref`, `ConversionOperatorMemberCref`, `ExtensionMemberCref`, `CrefBracketedParameterList` |

⚠ **Six of the nineteen diverge**, recorded as SK-DIV-0095 through SK-DIV-0099 in
[`divergences.md`](divergences.md). That is the result the audit was for; a coverage pass that added
only fixtures Skala already passes would have been a pass that selected for comfort. The other
thirteen are pins rather than findings — after retirement they are the only thing that would notice a
regression in those constructs — and they are worth having on that ground alone.

### The cref forms are pinned under one profile of two

⚠ **A gap this audit could not close, named rather than left implicit.** `cref-member-forms.cs` sits
in `constructs/syntax/`, so the fixture beside it is the **format-only** answer. The doc-comment
profile is the one that actually walks a cref, and it is only ever run over `constructs/xmldoc/` —
which `XmlDocOracleTests` asserts is **one file per option key**, because the doc-comment verdict is
attributed by file name. A fixture named after a construct cannot live there without attributing its
measurement to nothing, and `TheSplit_IsTwentyTwoAgainstNone` pins the subtree's size besides.

What the doc-comment profile does to these forms *was* measured by hand while the oracle was
installed, on a deliberately mis-spaced probe:
`<see    cref="Ext.extension(string).IsBlank"   />` comes back
`<see cref="Ext.extension(string).IsBlank" />` at the standard indent, identically to an ordinary
`<see cref="Ext.Ordinary(int)" />`. So the sub-formatter treats an extension cref exactly as it
treats a name cref. **That measurement is written down here and pinned nowhere**, and pinning it needs
`XmlDocOracle`'s row model to admit a file that is not named after a key — a design change, not an
addition, and out of scope for a coverage pass.

## The eight that were not filled, and why

| kind | why not |
|---|---|
| `UnionDeclaration` | ⚠ **Measured, not assumed.** ReSharper 2025.2.6 does not parse `union`: on a deliberately mis-formatted one it normalised the member whitespace but left the opening brace where it was written, while applying the brace rule to the `class` below it in the same file. A fixture would pin the oracle's *error recovery*, not its formatting of a construct, and error recovery is not a standard. Revisit when the oracle supports it — which, for this oracle, is never. |
| `WithElement` | Roslyn declares the kind; no syntax the pinned parser accepts produces it. A probe of the obvious spellings inside a collection expression yielded none. |
| `UnsafeExpression` | No C# syntax produces it at all. The kind exists in the enum and is unreachable from any source text. |
| `IncompleteMember` | Only ever produced by error recovery on source that does not parse. `pathological/` already holds a file that does not parse; adding a second whose point is a recovery node pins the parser, not the formatter. |
| `UnknownAccessorDeclaration` | Same: an accessor whose keyword is none of `get`/`set`/`init`/`add`/`remove`, which only error recovery produces. |
| `ShebangDirectiveTrivia` | `#!` is recognised only under script parse options. A `.cs` in a project cannot carry one, and `cleanupcode` does not format `.csx`, so no authoritative fixture is possible. |
| `LoadDirectiveTrivia` | `#load`, same reason. |
| `IgnoredDirectiveTrivia` | `#:sdk` and friends are recognised only in file-based-program parse mode, which neither the corpus's parse options nor the oracle's project ever uses. |

⚠ The last three are **permanently unfillable**, not deferred: the oracle formats projects, and these
three kinds exist only outside one. That is worth stating rather than leaving as an empty row, because
the next reader would otherwise spend an afternoon rediscovering it.

## ⚠ One thing this pass could not leave green: `skala check --gate=ci`

Adding the `coverage` command to `Testing/Rikarin.Skala.Testing/Program.cs` inserted 26 lines above
line 901, and that turns the `ci` gate red on **exactly one pre-existing finding**:

```
Testing/Rikarin.Skala.Testing/Program.cs:901:1: warning SK7020: duplicated block of 128 tokens
  (40 lines), also at Testing/Rikarin.Skala.Testing/Program.cs:977-1009      ← master
Testing/Rikarin.Skala.Testing/Program.cs:927:1: warning SK7020: duplicated block of 128 tokens
  (40 lines), also at Testing/Rikarin.Skala.Testing/Program.cs:1003-1035     ← here
```

⚠ **Measured against master rather than assumed.** A clean `git archive master` checkout, built and
gated the same way, exits 0 with 234 findings; this branch exits 1 with the same 234, and `diff` over
the two outputs is the single line above. `--gate=pr --since master` — the branch-scoped question —
exits **0**.

⚠ **The cause is a defect in SK7020, not in this branch.** `Fingerprints` is documented as carrying
"**no line numbers, and no file path** … a fingerprint that moves when a line moves is a baseline that
expires every commit". SK7020 defeats that: it puts the *paired* location, line numbers and all, into
its **message**, and the message is one of the fingerprint's four terms. So every SK7020 entry's
fingerprint moves whenever anything above either half of the clone moves, and no edit to a file
containing a clone can leave the `ci` gate green.

⚠ **Not re-recorded here, deliberately.** `skala baseline update --apply` accepts the moved entry —
"477 accepted before · 477 firing now · 1 newly accepted" is exactly the one-entry delta — but it
re-serialises the whole file: **2 618 insertions, 2 588 deletions**, and it rewrites
`configurationFingerprint` (`2d506f1f…` → `5b91d1bd…`) because it was recorded from a different
checkout. Committing a 2 600-line artefact rewrite from an agent worktree to launder a one-line
relocation is the trade this repository refuses everywhere else, so the red gate is reported instead
of hidden. Master's own baseline commit is the right place to absorb it.

## What is still thin, ranked

The four kinds pinned by a single file, and the ones a reader should treat as the next queue:

| kind | occurrences | files | note |
|---|---:|---:|---|
| `DefaultConstraint` | 1 | 1 | `where T : default` is legal only on an override; one instance is close to all the language allows in one file |
| `ExternAliasDirective` | 1 | 1 | needs a second assembly to be meaningful; unlikely to grow |
| `GotoDefaultStatement` | 1 | 1 | one legal rendering |
| `RefTypeExpression` | 1 | 1 | `__reftype`, and see SK-DIV-0095 |

⚠ **"Present once" is not covered.** The R1 threshold is 50 and every row above is at 1. Each is
pinned at one configuration by one author's choices, and a rule that only fires at a width or a
nesting depth this fixture does not reach is still unmeasured. The same caveat applies with less force
to the 143 other kinds in the 2–50 band, which the full table below carries.

## Every kind, with its counts

Machine-written by `fidelity coverage --table=…`; do not hand-edit. `occurrences` is the per-kind
maximum of two parses — bare, and with `Corpus.PropertySymbols` supplied — because a construct that
lives only inside a `#if` body is disabled text under one parse and a node under the other, and
neither parse alone answers "does the corpus contain it".

| kind | layout | occurrences | files | sets |
|---|---|---:|---:|---|
| `IdentifierName` | Transparent | 85673 | 743 | constructs, pathological, real |
| `Argument` | Transparent | 26861 | 459 | constructs, pathological, real |
| `SimpleMemberAccessExpression` | Continuation | 21980 | 423 | constructs, pathological, real |
| `ArgumentList` | Parens | 16448 | 527 | constructs, pathological, real |
| `InvocationExpression` | Transparent | 13505 | 504 | constructs, pathological, real |
| `NumericLiteralExpression` | Transparent | 9533 | 444 | constructs, pathological, real |
| `ExpressionStatement` | Transparent | 8547 | 500 | constructs, pathological, real |
| `PredefinedType` | Transparent | 8540 | 806 | constructs, pathological, real |
| `Block` | BracedBlock | 5686 | 647 | constructs, pathological, real |
| `EqualsValueClause` | Continuation | 5681 | 430 | constructs, pathological, real |
| `VariableDeclarator` | Transparent | 5654 | 490 | constructs, pathological, real |
| `VariableDeclaration` | Transparent | 5620 | 490 | constructs, pathological, real |
| `Parameter` | Transparent | 5171 | 533 | constructs, pathological, real |
| `LocalDeclarationStatement` | Transparent | 4124 | 345 | constructs, pathological, real |
| `ParameterList` | Parens | 3994 | 756 | constructs, pathological, real |
| `StringLiteralExpression` | Transparent | 3582 | 278 | constructs, pathological, real |
| `MethodDeclaration` | Transparent | 3433 | 719 | constructs, pathological, real |
| `QualifiedName` | Transparent | 3346 | 475 | constructs, pathological, real |
| `SimpleAssignmentExpression` | Continuation | 3134 | 315 | constructs, pathological, real |
| `ObjectCreationExpression` | Transparent | 2074 | 275 | constructs, real |
| `GenericName` | Transparent | 1732 | 306 | constructs, pathological, real |
| `TypeArgumentList` | Angles | 1732 | 306 | constructs, pathological, real |
| `BracketedArgumentList` | Brackets | 1722 | 190 | constructs, pathological, real |
| `ReturnStatement` | Transparent | 1712 | 303 | constructs, pathological, real |
| `ElementAccessExpression` | Transparent | 1652 | 188 | constructs, pathological, real |
| `Attribute` | Transparent | 1594 | 206 | constructs, pathological, real |
| `AttributeList` | Brackets | 1593 | 206 | constructs, pathological, real |
| `IfStatement` | Embedded | 1541 | 249 | constructs, pathological, real |
| `NameMemberCref` | Transparent | 1369 | 206 | constructs, real |
| `UsingDirective` | Transparent | 1269 | 362 | constructs, pathological, real |
| `PropertyDeclaration` | Transparent | 1257 | 254 | constructs, pathological, real |
| `ArrowExpressionClause` | Continuation | 1102 | 317 | constructs, pathological, real |
| `FieldDeclaration` | Transparent | 1052 | 314 | constructs, pathological, real |
| `ImplicitObjectCreationExpression` | Transparent | 1050 | 153 | constructs, pathological, real |
| `ClassDeclaration` | BracedBlock | 1022 | 827 | constructs, pathological, real |
| `AccessorList` | BracedBlock | 955 | 218 | constructs, pathological, real |
| `GetAccessorDeclaration` | Transparent | 951 | 217 | constructs, pathological, real |
| `ExpressionElement` | Transparent | 949 | 78 | constructs, pathological, real |
| `AddExpression` | Continuation | 920 | 191 | constructs, pathological, real |
| `CompilationUnit` | Transparent | 884 | 884 | constructs, pathological, real |
| `InterpolatedStringText` | Transparent | 856 | 100 | constructs, pathological, real |
| `ParenthesizedExpression` | Parens | 792 | 155 | constructs, pathological, real |
| `NullableType` | Transparent | 769 | 173 | constructs, pathological, real |
| `SingleVariableDesignation` | Transparent | 757 | 147 | constructs, pathological, real |
| `Interpolation` | Transparent | 726 | 100 | constructs, pathological, real |
| `AttributeArgument` | Transparent | 697 | 106 | constructs, pathological, real |
| `NullLiteralExpression` | Transparent | 682 | 171 | constructs, pathological, real |
| `MultiplyExpression` | Continuation | 665 | 99 | constructs, pathological, real |
| `CastExpression` | Transparent | 640 | 119 | constructs, real |
| `ArrayRankSpecifier` | Brackets | 638 | 182 | constructs, pathological, real |
| `ArrayType` | Transparent | 629 | 182 | constructs, pathological, real |
| `ConstantPattern` | Transparent | 608 | 86 | constructs, pathological, real |
| `CollectionExpression` | Brackets | 606 | 137 | constructs, pathological, real |
| `SetAccessorDeclaration` | Transparent | 600 | 143 | constructs, pathological, real |
| `EqualsExpression` | Continuation | 593 | 171 | constructs, pathological, real |
| `LessThanExpression` | Continuation | 592 | 150 | constructs, pathological, real |
| `AttributeArgumentList` | Parens | 558 | 106 | constructs, pathological, real |
| `PostIncrementExpression` | Transparent | 465 | 136 | constructs, real |
| `SimpleLambdaExpression` | Transparent | 465 | 104 | constructs, pathological, real |
| `DeclarationExpression` | Transparent | 443 | 110 | constructs, real |
| `GreaterThanExpression` | Continuation | 435 | 132 | constructs, pathological, real |
| `TrueLiteralExpression` | Transparent | 421 | 156 | constructs, pathological, real |
| `InterpolatedStringExpression` | Verbatim | 420 | 100 | constructs, pathological, real |
| `OmittedArraySizeExpression` | Transparent | 416 | 159 | constructs, pathological, real |
| `FalseLiteralExpression` | Transparent | 413 | 136 | constructs, real |
| `ForStatement` | Embedded | 396 | 128 | constructs, real |
| `SubtractExpression` | Continuation | 380 | 94 | constructs, real |
| `LogicalNotExpression` | Transparent | 373 | 135 | constructs, pathological, real |
| `ForEachStatement` | Embedded | 359 | 135 | constructs, real |
| `ConditionalExpression` | Continuation | 353 | 119 | constructs, pathological, real |
| `LogicalAndExpression` | Continuation | 352 | 122 | constructs, real |
| `NameColon` | Transparent | 339 | 83 | constructs, pathological, real |
| `SwitchExpressionArm` | Transparent | 336 | 29 | constructs, pathological, real |
| `ObjectInitializerExpression` | BracedInitializer | 335 | 97 | constructs, pathological, real |
| `FileScopedNamespaceDeclaration` | Transparent | 316 | 316 | constructs, pathological, real |
| `IsPatternExpression` | Continuation | 302 | 112 | constructs, pathological, real |
| `LogicalOrExpression` | Continuation | 300 | 109 | constructs, real |
| `SimpleBaseType` | Transparent | 299 | 190 | constructs, pathological, real |
| `QualifiedCref` | Transparent | 295 | 105 | constructs, real |
| `BaseList` | Continuation | 270 | 191 | constructs, pathological, real |
| `NotEqualsExpression` | Continuation | 267 | 98 | constructs, real |
| `UnaryMinusExpression` | Transparent | 255 | 71 | constructs, pathological, real |
| `SwitchSection` | SwitchSection | 249 | 41 | constructs, real |
| `EnumMemberDeclaration` | Transparent | 231 | 34 | constructs, real |
| `AliasQualifiedName` | Transparent | 224 | 1 | real |
| `DivideExpression` | Continuation | 221 | 63 | constructs, pathological, real |
| `ArrayCreationExpression` | Transparent | 220 | 72 | constructs, real |
| `TypeOfExpression` | Transparent | 214 | 52 | constructs, real |
| `ConstructorDeclaration` | Transparent | 210 | 147 | constructs, real |
| `CaseSwitchLabel` | Transparent | 202 | 35 | constructs, real |
| `CharacterLiteralExpression` | Transparent | 190 | 37 | constructs, real |
| `TypeParameter` | Transparent | 188 | 39 | constructs, pathological, real |
| `ParenthesizedLambdaExpression` | Transparent | 177 | 63 | constructs, real |
| `ThrowStatement` | Transparent | 175 | 74 | constructs, real |
| `ThisExpression` | Transparent | 173 | 65 | constructs, real |
| `OrPattern` | Continuation | 166 | 33 | constructs, real |
| `AddAssignmentExpression` | Continuation | 151 | 58 | constructs, real |
| `SuppressNullableWarningExpression` | Transparent | 139 | 56 | pathological, real |
| `TupleExpression` | Parens | 134 | 40 | constructs, real |
| `CoalesceExpression` | Continuation | 131 | 71 | constructs, real |
| `ElseClause` | Embedded | 129 | 58 | constructs, pathological, real |
| `TupleElement` | Transparent | 127 | 33 | constructs, pathological, real |
| `BreakStatement` | Transparent | 124 | 32 | constructs, real |
| `ParenthesizedVariableDesignation` | Parens | 124 | 41 | constructs, real |
| `GreaterThanOrEqualExpression` | Continuation | 123 | 58 | constructs, real |
| `WithExpression` | Transparent | 122 | 25 | real |
| `WithInitializerExpression` | BracedInitializer | 122 | 25 | real |
| `LessThanOrEqualExpression` | Continuation | 120 | 57 | constructs, real |
| `ConditionalAccessExpression` | Continuation | 119 | 50 | constructs, pathological, real |
| `NamespaceDeclaration` | BracedBlock | 117 | 116 | constructs, pathological, real |
| `DeclarationPattern` | Transparent | 115 | 36 | constructs, pathological, real |
| `TypeParameterList` | Angles | 115 | 39 | constructs, pathological, real |
| `ContinueStatement` | Transparent | 113 | 48 | constructs, real |
| `RangeExpression` | Transparent | 112 | 29 | real |
| `RecursivePattern` | Transparent | 111 | 45 | constructs, pathological, real |
| `InitAccessorDeclaration` | Transparent | 108 | 25 | constructs, real |
| `MemberBindingExpression` | Transparent | 108 | 50 | constructs, pathological, real |
| `PropertyPatternClause` | BracedInitializer | 96 | 44 | constructs, pathological, real |
| `DefaultLiteralExpression` | Transparent | 95 | 44 | pathological, real |
| `NameEquals` | Transparent | 92 | 33 | constructs, real |
| `Subpattern` | Transparent | 82 | 27 | constructs, pathological, real |
| `ArrayInitializerExpression` | BracedInitializer | 81 | 40 | constructs, pathological, real |
| `NotPattern` | Transparent | 76 | 39 | constructs, real |
| `BitwiseOrExpression` | Continuation | 67 | 30 | constructs, pathological, real |
| `OmittedTypeArgument` | Transparent | 67 | 6 | constructs, real |
| `SwitchStatement` | SwitchBody | 63 | 41 | constructs, real |
| `CrefParameter` | Transparent | 62 | 11 | constructs, real |
| `DiscardPattern` | Transparent | 61 | 28 | constructs, pathological, real |
| `SwitchExpression` | BracedInitializer | 61 | 29 | constructs, pathological, real |
| `RecordStructDeclaration` | BracedBlock | 60 | 41 | pathological, real |
| `ImplicitElementAccess` | Brackets | 59 | 5 | real |
| `TupleType` | Parens | 58 | 33 | constructs, pathological, real |
| `WhileStatement` | Embedded | 54 | 39 | constructs, real |
| `ImplicitArrayCreationExpression` | Transparent | 51 | 29 | constructs, pathological, real |
| `TypeParameterConstraintClause` | Continuation | 51 | 21 | constructs, real |
| `SpreadElement` | Transparent | 50 | 28 | constructs, pathological, real |
| `ModuloExpression` | Continuation | 49 | 22 | constructs, pathological, real |
| `AwaitExpression` | Transparent | 47 | 10 | constructs, real |
| `BitwiseAndExpression` | Continuation | 47 | 19 | constructs, pathological, real |
| `DiscardDesignation` | Transparent | 47 | 12 | constructs, real |
| `TryStatement` | Transparent | 47 | 34 | constructs, real |
| `RecordDeclaration` | BracedBlock | 46 | 29 | constructs, real |
| `DefaultSwitchLabel` | Transparent | 45 | 30 | constructs, real |
| `EnumDeclaration` | BracedBlock | 45 | 34 | constructs, real |
| `TypeConstraint` | Transparent | 45 | 15 | constructs, real |
| `UsingStatement` | Embedded | 41 | 22 | constructs, real |
| `CatchClause` | Transparent | 40 | 31 | constructs, real |
| `CatchDeclaration` | Transparent | 40 | 31 | constructs, real |
| `BaseExpression` | Transparent | 39 | 14 | constructs, real |
| `InterpolationFormatClause` | Transparent | 39 | 16 | constructs, real |
| `AnonymousObjectMemberDeclarator` | Transparent | 37 | 7 | constructs, real |
| `PreIncrementExpression` | Transparent | 36 | 14 | constructs, real |
| `CollectionInitializerExpression` | BracedInitializer | 35 | 25 | constructs, real |
| `ThrowExpression` | Transparent | 35 | 21 | constructs, real |
| `InterfaceDeclaration` | BracedBlock | 34 | 27 | constructs, pathological, real |
| `CrefParameterList` | Parens | 33 | 12 | constructs, real |
| `StackAllocArrayCreationExpression` | Transparent | 32 | 13 | constructs, pathological, real |
| `YieldReturnStatement` | Transparent | 32 | 12 | constructs, real |
| `CasePatternSwitchLabel` | Transparent | 30 | 7 | real |
| `LeftShiftExpression` | Continuation | 28 | 13 | constructs, pathological, real |
| `QueryBody` | Continuation | 28 | 9 | constructs, pathological, real |
| `StructDeclaration` | BracedBlock | 28 | 21 | constructs, pathological, real |
| `TypePattern` | Transparent | 27 | 4 | constructs, real |
| `FromClause` | Transparent | 25 | 9 | constructs, pathological, real |
| `RightShiftExpression` | Continuation | 25 | 11 | constructs, pathological, real |
| `SizeOfExpression` | Transparent | 25 | 10 | constructs, real |
| `ExplicitInterfaceSpecifier` | Transparent | 24 | 10 | constructs, real |
| `AsExpression` | Continuation | 23 | 19 | constructs, real |
| `ForEachVariableStatement` | Embedded | 23 | 14 | real |
| `LocalFunctionStatement` | Transparent | 23 | 20 | constructs, real |
| `QueryExpression` | Continuation | 23 | 9 | constructs, pathological, real |
| `RelationalPattern` | Transparent | 23 | 16 | constructs, pathological, real |
| `SelectClause` | Transparent | 23 | 9 | constructs, pathological, real |
| `FunctionPointerParameter` | Transparent | 22 | 2 | constructs, pathological |
| `EventFieldDeclaration` | Transparent | 21 | 15 | constructs, real |
| `BaseConstructorInitializer` | Continuation | 19 | 11 | constructs, real |
| `PointerType` | Transparent | 19 | 10 | constructs, pathological |
| `PostDecrementExpression` | Transparent | 19 | 16 | constructs, real |
| `DefaultExpression` | Transparent | 18 | 7 | constructs, real |
| `StructConstraint` | Transparent | 18 | 9 | constructs, real |
| `ComplexElementInitializerExpression` | BracedInitializer | 17 | 8 | real |
| `CoalesceAssignmentExpression` | Continuation | 16 | 14 | constructs, real |
| `LockStatement` | Embedded | 15 | 11 | constructs, real |
| `PositionalPatternClause` | Parens | 15 | 2 | real |
| `SubtractAssignmentExpression` | Continuation | 15 | 9 | constructs, real |
| `AnonymousObjectCreationExpression` | BracedInitializer | 14 | 7 | constructs, real |
| `InterpolationAlignmentClause` | Transparent | 14 | 4 | constructs, real |
| `OperatorDeclaration` | Transparent | 14 | 7 | constructs, pathological |
| `OrAssignmentExpression` | Continuation | 14 | 10 | constructs, real |
| `IsExpression` | Continuation | 13 | 10 | constructs, real |
| `ThisConstructorInitializer` | Continuation | 13 | 9 | real |
| `FieldExpression` | Transparent | 12 | 1 | constructs |
| `IndexExpression` | Transparent | 12 | 9 | constructs, real |
| `OrderByClause` | Transparent | 12 | 6 | constructs, pathological, real |
| `WhereClause` | Transparent | 12 | 6 | constructs, pathological, real |
| `AndPattern` | Continuation | 11 | 5 | constructs, pathological, real |
| `CatchFilterClause` | Transparent | 11 | 9 | constructs, real |
| `ClassConstraint` | Transparent | 11 | 5 | constructs, real |
| `ElementBindingExpression` | Transparent | 11 | 1 | constructs |
| `GlobalStatement` | Transparent | 11 | 2 | constructs, real |
| `PointerMemberAccessExpression` | Continuation | 11 | 2 | constructs |
| `BitwiseNotExpression` | Transparent | 10 | 8 | constructs, pathological, real |
| `BracketedParameterList` | Brackets | 10 | 8 | constructs, real |
| `ConstructorConstraint` | Transparent | 10 | 8 | constructs, real |
| `ExclusiveOrExpression` | Continuation | 10 | 6 | constructs, pathological, real |
| `FinallyClause` | Transparent | 10 | 5 | constructs, real |
| `IndexerDeclaration` | Transparent | 10 | 8 | constructs, real |
| `WhenClause` | Transparent | 10 | 4 | real |
| `ExclusiveOrAssignmentExpression` | Continuation | 9 | 4 | constructs, real |
| `ListPattern` | Brackets | 9 | 6 | constructs, pathological |
| `RefExpression` | Transparent | 9 | 6 | constructs, real |
| `RefType` | Transparent | 9 | 6 | constructs, real |
| `MultiplyAssignmentExpression` | Continuation | 8 | 6 | constructs, real |
| `AttributeTargetSpecifier` | Transparent | 7 | 7 | constructs, pathological |
| `DescendingOrdering` | Transparent | 7 | 5 | constructs, pathological, real |
| `DoStatement` | Embedded | 7 | 6 | constructs, real |
| `FunctionPointerParameterList` | Parens | 7 | 2 | constructs, pathological |
| `FunctionPointerType` | Transparent | 7 | 2 | constructs, pathological |
| `NullableDirectiveTrivia` | DirectiveNode | 7 | 7 | constructs, pathological, real |
| `AndAssignmentExpression` | Continuation | 6 | 5 | constructs, real |
| `AscendingOrdering` | Transparent | 6 | 4 | constructs, real |
| `CheckedExpression` | Transparent | 6 | 3 | constructs |
| `ConversionOperatorDeclaration` | Transparent | 6 | 4 | constructs, real |
| `ExtensionBlockDeclaration` | BracedBlock | 6 | 3 | constructs, pathological |
| `FixedStatement` | Embedded | 6 | 5 | constructs, pathological |
| `FunctionPointerCallingConvention` | Transparent | 6 | 1 | constructs |
| `FunctionPointerUnmanagedCallingConvention` | Transparent | 6 | 1 | constructs |
| `LineDirectivePosition` | Transparent | 6 | 1 | constructs |
| `PointerIndirectionExpression` | Transparent | 6 | 6 | constructs, pathological |
| `Utf8StringLiteralExpression` | Transparent | 6 | 2 | constructs, pathological |
| `YieldBreakStatement` | Transparent | 6 | 3 | real |
| `GotoStatement` | Transparent | 5 | 3 | constructs, pathological |
| `GroupClause` | Transparent | 5 | 5 | constructs, pathological, real |
| `JoinClause` | Transparent | 5 | 2 | constructs |
| `LabeledStatement` | Embedded | 5 | 3 | constructs, pathological |
| `LetClause` | Transparent | 5 | 3 | constructs, real |
| `QueryContinuation` | Transparent | 5 | 5 | constructs, pathological, real |
| `AddAccessorDeclaration` | Transparent | 4 | 1 | constructs |
| `AddressOfExpression` | Transparent | 4 | 4 | constructs |
| `AnonymousMethodExpression` | Transparent | 4 | 4 | constructs |
| `ArgListExpression` | Transparent | 4 | 1 | constructs |
| `DelegateDeclaration` | Transparent | 4 | 4 | constructs, real |
| `DivideAssignmentExpression` | Continuation | 4 | 3 | constructs, real |
| `EventDeclaration` | Transparent | 4 | 1 | constructs |
| `ExpressionColon` | Transparent | 4 | 4 | real |
| `ImplicitStackAllocArrayCreationExpression` | Transparent | 4 | 1 | constructs |
| `LeftShiftAssignmentExpression` | Continuation | 4 | 3 | constructs |
| `ModuloAssignmentExpression` | Continuation | 4 | 1 | constructs |
| `ParenthesizedPattern` | Parens | 4 | 3 | real |
| `RemoveAccessorDeclaration` | Transparent | 4 | 1 | constructs |
| `UnsignedRightShiftAssignmentExpression` | Continuation | 4 | 1 | constructs |
| `AllowsConstraintClause` | Transparent | 3 | 1 | constructs |
| `CheckedStatement` | Transparent | 3 | 1 | constructs |
| `ExtensionMemberCref` | Transparent | 3 | 1 | constructs |
| `FunctionPointerUnmanagedCallingConventionList` | Angles | 3 | 1 | constructs |
| `GotoCaseStatement` | Transparent | 3 | 2 | constructs, real |
| `JoinIntoClause` | Transparent | 3 | 2 | constructs |
| `LineSpanDirectiveTrivia` | DirectiveNode | 3 | 1 | constructs |
| `MakeRefExpression` | Transparent | 3 | 1 | constructs |
| `OperatorMemberCref` | Transparent | 3 | 1 | constructs |
| `PreDecrementExpression` | Transparent | 3 | 3 | constructs, real |
| `RefStructConstraint` | Transparent | 3 | 1 | constructs |
| `RefValueExpression` | Transparent | 3 | 1 | constructs |
| `ScopedType` | Transparent | 3 | 1 | constructs |
| `UnaryPlusExpression` | Transparent | 3 | 2 | constructs |
| `UncheckedExpression` | Transparent | 3 | 3 | constructs, real |
| `UncheckedStatement` | Transparent | 3 | 2 | constructs, real |
| `UnsignedRightShiftExpression` | Continuation | 3 | 2 | constructs, real |
| `VarPattern` | Transparent | 3 | 3 | constructs, real |
| `ConversionOperatorMemberCref` | Transparent | 2 | 1 | constructs |
| `CrefBracketedParameterList` | Brackets | 2 | 1 | constructs |
| `DestructorDeclaration` | Transparent | 2 | 2 | constructs, real |
| `EmptyStatement` | Transparent | 2 | 1 | real |
| `IndexerMemberCref` | Transparent | 2 | 1 | constructs |
| `PrimaryConstructorBaseType` | Transparent | 2 | 2 | constructs, real |
| `RightShiftAssignmentExpression` | Continuation | 2 | 2 | constructs |
| `SlicePattern` | Transparent | 2 | 2 | constructs, pathological |
| `UnsafeStatement` | Transparent | 2 | 1 | constructs |
| `DefaultConstraint` | Transparent | 1 | 1 | constructs |
| `ExternAliasDirective` | Transparent | 1 | 1 | constructs |
| `GotoDefaultStatement` | Transparent | 1 | 1 | constructs |
| `RefTypeExpression` | Transparent | 1 | 1 | constructs |
| `IgnoredDirectiveTrivia` | DirectiveNode | 0 | 0 | — |
| `IncompleteMember` | Transparent | 0 | 0 | — |
| `LoadDirectiveTrivia` | DirectiveNode | 0 | 0 | — |
| `ShebangDirectiveTrivia` | DirectiveNode | 0 | 0 | — |
| `UnionDeclaration` | BracedBlock | 0 | 0 | — |
| `UnknownAccessorDeclaration` | Transparent | 0 | 0 | — |
| `UnsafeExpression` | Transparent | 0 | 0 | — |
| `WithElement` | Transparent | 0 | 0 | — |
