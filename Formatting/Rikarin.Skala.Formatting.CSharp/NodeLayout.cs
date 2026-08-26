using Microsoft.CodeAnalysis.CSharp;

// RSEXPERIMENTAL006: UnionDeclaration, UnsafeExpression and WithElement are experimental in Roslyn
// 5.9.0. Naming them is the point — the classifier is a total function over SyntaxKind (R5 in
// docs/plan/16) and an experimental kind is exactly the kind a formatter must not mangle.
#pragma warning disable RSEXPERIMENTAL006

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>How the document builder lays out one syntax node.</summary>
public enum NodeLayout {
    /// <summary>Children in order, no indentation scope of its own. Most kinds.</summary>
    Transparent,

    /// <summary>A <c>{ }</c> body whose contents take one block indent: blocks, types, namespaces, accessor lists.</summary>
    BracedBlock,

    /// <summary>An initializer <c>{ }</c>: block indent, and members go on their own lines.</summary>
    BracedInitializer,

    /// <summary>A <c>( )</c> group: continuation lines inside take one continuous indent.</summary>
    Parens,

    /// <summary>A <c>[ ]</c> group.</summary>
    Brackets,

    /// <summary>A <c>&lt; &gt;</c> type argument or type parameter list.</summary>
    Angles,

    /// <summary>A statement with an embedded statement that indents when it is not a block.</summary>
    Embedded,

    /// <summary>A <c>switch</c> statement: <c>indent_case_from_switch</c> governs the labels.</summary>
    SwitchBody,

    /// <summary>One <c>case</c> group: the statements indent one level from the label.</summary>
    SwitchSection,

    /// <summary>A continuation scope that is not delimited: a binary chain, a member-access chain, an initializer value.</summary>
    Continuation,

    /// <summary>
    /// Emitted byte-for-byte from its source span: a construct where a moved space changes the
    /// program's meaning.
    /// </summary>
    Verbatim,

    /// <summary>Directive trivia. A node in Roslyn, but the trivia model owns it and it never reaches the walker.</summary>
    DirectiveNode,

    /// <summary>
    /// ⚠ Not classified: a kind this Skala has never been told about.
    /// </summary>
    /// <remarks>
    /// The builder emits the node's original span verbatim rather than throwing or guessing. This
    /// is the run-time half of the R5 mitigation (docs/plan/16): a formatter that meets C# 15
    /// syntax must leave it alone, not mangle it. The build-time half is
    /// <c>SyntaxKindInventoryTests</c> against <c>Testing/corpus/syntax-kinds.txt</c>.
    /// </remarks>
    Unknown
}

/// <summary>
/// The total function over <see cref="SyntaxKind"/> that R5 requires: every node kind Roslyn 5.9.0
/// declares is named here, and the fallback is <see cref="NodeLayout.Unknown"/>.
/// </summary>
/// <remarks>
/// ⚠ Do not replace the explicit arms with a range check. The point of listing all 293 node kinds
/// is that adding a kind to Roslyn produces a kind that is <em>not</em> listed, which the inventory
/// test turns into a build failure. A range check would silently absorb it.
/// </remarks>
public static class NodeLayouts {
    /// <summary>The lowest <see cref="SyntaxKind"/> value that names a node rather than a token or trivia.</summary>
    public const int FirstNodeKind = 8598;

    public static NodeLayout Classify(SyntaxKind kind) =>
        kind switch {
            // ── Bodies whose contents take one block indent ─────────────────────────────────────────
            SyntaxKind.Block => NodeLayout.BracedBlock,
            SyntaxKind.NamespaceDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.ClassDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.StructDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.InterfaceDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.EnumDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.AccessorList => NodeLayout.BracedBlock,
            SyntaxKind.RecordDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.RecordStructDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.ExtensionBlockDeclaration => NodeLayout.BracedBlock,
            SyntaxKind.UnionDeclaration => NodeLayout.BracedBlock,

            // ── Initializer braces: block indent, members on their own lines ────────────────────────
            SyntaxKind.ObjectInitializerExpression => NodeLayout.BracedInitializer,
            SyntaxKind.CollectionInitializerExpression => NodeLayout.BracedInitializer,
            SyntaxKind.ArrayInitializerExpression => NodeLayout.BracedInitializer,
            SyntaxKind.ComplexElementInitializerExpression => NodeLayout.BracedInitializer,
            SyntaxKind.AnonymousObjectCreationExpression => NodeLayout.BracedInitializer,
            SyntaxKind.PropertyPatternClause => NodeLayout.BracedInitializer,
            SyntaxKind.SwitchExpression => NodeLayout.BracedInitializer,
            SyntaxKind.WithInitializerExpression => NodeLayout.BracedInitializer,

            // ── ( ) groups — one continuous indent inside (use_continuous_indent_inside_parens = true) ───
            SyntaxKind.CrefParameterList => NodeLayout.Parens,
            SyntaxKind.ParenthesizedExpression => NodeLayout.Parens,
            SyntaxKind.ArgumentList => NodeLayout.Parens,
            SyntaxKind.AttributeArgumentList => NodeLayout.Parens,
            SyntaxKind.ParameterList => NodeLayout.Parens,
            SyntaxKind.TupleType => NodeLayout.Parens,
            SyntaxKind.TupleExpression => NodeLayout.Parens,
            SyntaxKind.ParenthesizedVariableDesignation => NodeLayout.Parens,
            SyntaxKind.PositionalPatternClause => NodeLayout.Parens,
            SyntaxKind.ParenthesizedPattern => NodeLayout.Parens,
            SyntaxKind.FunctionPointerParameterList => NodeLayout.Parens,

            // ── [ ] groups ──────────────────────────────────────────────────────────────────────────
            SyntaxKind.CrefBracketedParameterList => NodeLayout.Brackets,
            SyntaxKind.ArrayRankSpecifier => NodeLayout.Brackets,
            SyntaxKind.BracketedArgumentList => NodeLayout.Brackets,
            SyntaxKind.ImplicitElementAccess => NodeLayout.Brackets,
            SyntaxKind.AttributeList => NodeLayout.Brackets,
            SyntaxKind.BracketedParameterList => NodeLayout.Brackets,
            SyntaxKind.ListPattern => NodeLayout.Brackets,
            SyntaxKind.CollectionExpression => NodeLayout.Brackets,

            // ── < > type argument and type parameter lists ──────────────────────────────────────────
            SyntaxKind.TypeArgumentList => NodeLayout.Angles,
            SyntaxKind.TypeParameterList => NodeLayout.Angles,
            SyntaxKind.FunctionPointerUnmanagedCallingConventionList => NodeLayout.Angles,

            // ── Statements with an embedded statement that indents when it is not a block ───────────
            SyntaxKind.LabeledStatement => NodeLayout.Embedded,
            SyntaxKind.WhileStatement => NodeLayout.Embedded,
            SyntaxKind.DoStatement => NodeLayout.Embedded,
            SyntaxKind.ForStatement => NodeLayout.Embedded,
            SyntaxKind.ForEachStatement => NodeLayout.Embedded,
            SyntaxKind.UsingStatement => NodeLayout.Embedded,
            SyntaxKind.FixedStatement => NodeLayout.Embedded,
            SyntaxKind.LockStatement => NodeLayout.Embedded,
            SyntaxKind.IfStatement => NodeLayout.Embedded,
            SyntaxKind.ElseClause => NodeLayout.Embedded,
            SyntaxKind.ForEachVariableStatement => NodeLayout.Embedded,

            // ── switch — indent_case_from_switch via csharp_indent_switch_labels = true ─────────────
            SyntaxKind.SwitchStatement => NodeLayout.SwitchBody,

            // ── One case group: statements indent one level from the label ──────────────────────────
            SyntaxKind.SwitchSection => NodeLayout.SwitchSection,

            // ── Undelimited continuation scopes: chains, initializer values, base lists ─────────────
            SyntaxKind.ConditionalExpression => NodeLayout.Continuation,
            SyntaxKind.IsPatternExpression => NodeLayout.Continuation,
            SyntaxKind.AddExpression => NodeLayout.Continuation,
            SyntaxKind.SubtractExpression => NodeLayout.Continuation,
            SyntaxKind.MultiplyExpression => NodeLayout.Continuation,
            SyntaxKind.DivideExpression => NodeLayout.Continuation,
            SyntaxKind.ModuloExpression => NodeLayout.Continuation,
            SyntaxKind.LeftShiftExpression => NodeLayout.Continuation,
            SyntaxKind.RightShiftExpression => NodeLayout.Continuation,
            SyntaxKind.LogicalOrExpression => NodeLayout.Continuation,
            SyntaxKind.LogicalAndExpression => NodeLayout.Continuation,
            SyntaxKind.BitwiseOrExpression => NodeLayout.Continuation,
            SyntaxKind.BitwiseAndExpression => NodeLayout.Continuation,
            SyntaxKind.ExclusiveOrExpression => NodeLayout.Continuation,
            SyntaxKind.EqualsExpression => NodeLayout.Continuation,
            SyntaxKind.NotEqualsExpression => NodeLayout.Continuation,
            SyntaxKind.LessThanExpression => NodeLayout.Continuation,
            SyntaxKind.LessThanOrEqualExpression => NodeLayout.Continuation,
            SyntaxKind.GreaterThanExpression => NodeLayout.Continuation,
            SyntaxKind.GreaterThanOrEqualExpression => NodeLayout.Continuation,
            SyntaxKind.IsExpression => NodeLayout.Continuation,
            SyntaxKind.AsExpression => NodeLayout.Continuation,
            SyntaxKind.CoalesceExpression => NodeLayout.Continuation,
            SyntaxKind.SimpleMemberAccessExpression => NodeLayout.Continuation,
            SyntaxKind.PointerMemberAccessExpression => NodeLayout.Continuation,
            SyntaxKind.ConditionalAccessExpression => NodeLayout.Continuation,
            SyntaxKind.UnsignedRightShiftExpression => NodeLayout.Continuation,
            SyntaxKind.SimpleAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.AddAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.SubtractAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.MultiplyAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.DivideAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.ModuloAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.AndAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.ExclusiveOrAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.OrAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.LeftShiftAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.RightShiftAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.CoalesceAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.UnsignedRightShiftAssignmentExpression => NodeLayout.Continuation,
            SyntaxKind.QueryExpression => NodeLayout.Continuation,
            SyntaxKind.QueryBody => NodeLayout.Continuation,
            SyntaxKind.EqualsValueClause => NodeLayout.Continuation,
            SyntaxKind.BaseList => NodeLayout.Continuation,
            SyntaxKind.TypeParameterConstraintClause => NodeLayout.Continuation,
            SyntaxKind.BaseConstructorInitializer => NodeLayout.Continuation,
            SyntaxKind.ThisConstructorInitializer => NodeLayout.Continuation,
            SyntaxKind.ArrowExpressionClause => NodeLayout.Continuation,
            SyntaxKind.OrPattern => NodeLayout.Continuation,
            SyntaxKind.AndPattern => NodeLayout.Continuation,

            // ── Emitted byte-for-byte: a moved space changes the value ──────────────────────────────
            SyntaxKind.InterpolatedStringExpression => NodeLayout.Verbatim,

            // ── Directive trivia. Nodes, but the trivia model owns them; they never reach the walker ───
            SyntaxKind.ShebangDirectiveTrivia => NodeLayout.DirectiveNode,
            SyntaxKind.LoadDirectiveTrivia => NodeLayout.DirectiveNode,
            SyntaxKind.NullableDirectiveTrivia => NodeLayout.DirectiveNode,
            SyntaxKind.LineSpanDirectiveTrivia => NodeLayout.DirectiveNode,
            SyntaxKind.IgnoredDirectiveTrivia => NodeLayout.DirectiveNode,

            // ── Children in order, no scope of this node's own ──────────────────────────────────────
            SyntaxKind.QualifiedCref => NodeLayout.Transparent,
            SyntaxKind.NameMemberCref => NodeLayout.Transparent,
            SyntaxKind.IndexerMemberCref => NodeLayout.Transparent,
            SyntaxKind.OperatorMemberCref => NodeLayout.Transparent,
            SyntaxKind.ConversionOperatorMemberCref => NodeLayout.Transparent,
            SyntaxKind.CrefParameter => NodeLayout.Transparent,
            SyntaxKind.ExtensionMemberCref => NodeLayout.Transparent,
            SyntaxKind.IdentifierName => NodeLayout.Transparent,
            SyntaxKind.QualifiedName => NodeLayout.Transparent,
            SyntaxKind.GenericName => NodeLayout.Transparent,
            SyntaxKind.AliasQualifiedName => NodeLayout.Transparent,
            SyntaxKind.PredefinedType => NodeLayout.Transparent,
            SyntaxKind.ArrayType => NodeLayout.Transparent,
            SyntaxKind.PointerType => NodeLayout.Transparent,
            SyntaxKind.NullableType => NodeLayout.Transparent,
            SyntaxKind.OmittedTypeArgument => NodeLayout.Transparent,
            SyntaxKind.InvocationExpression => NodeLayout.Transparent,
            SyntaxKind.ElementAccessExpression => NodeLayout.Transparent,
            SyntaxKind.Argument => NodeLayout.Transparent,
            SyntaxKind.NameColon => NodeLayout.Transparent,
            SyntaxKind.CastExpression => NodeLayout.Transparent,
            SyntaxKind.AnonymousMethodExpression => NodeLayout.Transparent,
            SyntaxKind.SimpleLambdaExpression => NodeLayout.Transparent,
            SyntaxKind.ParenthesizedLambdaExpression => NodeLayout.Transparent,
            SyntaxKind.AnonymousObjectMemberDeclarator => NodeLayout.Transparent,
            SyntaxKind.ObjectCreationExpression => NodeLayout.Transparent,
            SyntaxKind.ArrayCreationExpression => NodeLayout.Transparent,
            SyntaxKind.ImplicitArrayCreationExpression => NodeLayout.Transparent,
            SyntaxKind.StackAllocArrayCreationExpression => NodeLayout.Transparent,
            SyntaxKind.OmittedArraySizeExpression => NodeLayout.Transparent,
            SyntaxKind.RangeExpression => NodeLayout.Transparent,
            SyntaxKind.ImplicitObjectCreationExpression => NodeLayout.Transparent,
            SyntaxKind.MemberBindingExpression => NodeLayout.Transparent,
            SyntaxKind.ElementBindingExpression => NodeLayout.Transparent,
            SyntaxKind.UnaryPlusExpression => NodeLayout.Transparent,
            SyntaxKind.UnaryMinusExpression => NodeLayout.Transparent,
            SyntaxKind.BitwiseNotExpression => NodeLayout.Transparent,
            SyntaxKind.LogicalNotExpression => NodeLayout.Transparent,
            SyntaxKind.PreIncrementExpression => NodeLayout.Transparent,
            SyntaxKind.PreDecrementExpression => NodeLayout.Transparent,
            SyntaxKind.PointerIndirectionExpression => NodeLayout.Transparent,
            SyntaxKind.AddressOfExpression => NodeLayout.Transparent,
            SyntaxKind.PostIncrementExpression => NodeLayout.Transparent,
            SyntaxKind.PostDecrementExpression => NodeLayout.Transparent,
            SyntaxKind.AwaitExpression => NodeLayout.Transparent,
            SyntaxKind.IndexExpression => NodeLayout.Transparent,
            SyntaxKind.ThisExpression => NodeLayout.Transparent,
            SyntaxKind.BaseExpression => NodeLayout.Transparent,
            SyntaxKind.ArgListExpression => NodeLayout.Transparent,
            SyntaxKind.NumericLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.StringLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.CharacterLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.TrueLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.FalseLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.NullLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.DefaultLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.Utf8StringLiteralExpression => NodeLayout.Transparent,
            SyntaxKind.FieldExpression => NodeLayout.Transparent,
            SyntaxKind.TypeOfExpression => NodeLayout.Transparent,
            SyntaxKind.SizeOfExpression => NodeLayout.Transparent,
            SyntaxKind.CheckedExpression => NodeLayout.Transparent,
            SyntaxKind.UncheckedExpression => NodeLayout.Transparent,
            SyntaxKind.DefaultExpression => NodeLayout.Transparent,
            SyntaxKind.MakeRefExpression => NodeLayout.Transparent,
            SyntaxKind.RefValueExpression => NodeLayout.Transparent,
            SyntaxKind.RefTypeExpression => NodeLayout.Transparent,
            SyntaxKind.UnsafeExpression => NodeLayout.Transparent,
            SyntaxKind.FromClause => NodeLayout.Transparent,
            SyntaxKind.LetClause => NodeLayout.Transparent,
            SyntaxKind.JoinClause => NodeLayout.Transparent,
            SyntaxKind.JoinIntoClause => NodeLayout.Transparent,
            SyntaxKind.WhereClause => NodeLayout.Transparent,
            SyntaxKind.OrderByClause => NodeLayout.Transparent,
            SyntaxKind.AscendingOrdering => NodeLayout.Transparent,
            SyntaxKind.DescendingOrdering => NodeLayout.Transparent,
            SyntaxKind.SelectClause => NodeLayout.Transparent,
            SyntaxKind.GroupClause => NodeLayout.Transparent,
            SyntaxKind.QueryContinuation => NodeLayout.Transparent,
            SyntaxKind.LocalDeclarationStatement => NodeLayout.Transparent,
            SyntaxKind.VariableDeclaration => NodeLayout.Transparent,
            SyntaxKind.VariableDeclarator => NodeLayout.Transparent,
            SyntaxKind.ExpressionStatement => NodeLayout.Transparent,
            SyntaxKind.EmptyStatement => NodeLayout.Transparent,
            SyntaxKind.GotoStatement => NodeLayout.Transparent,
            SyntaxKind.GotoCaseStatement => NodeLayout.Transparent,
            SyntaxKind.GotoDefaultStatement => NodeLayout.Transparent,
            SyntaxKind.BreakStatement => NodeLayout.Transparent,
            SyntaxKind.ContinueStatement => NodeLayout.Transparent,
            SyntaxKind.ReturnStatement => NodeLayout.Transparent,
            SyntaxKind.YieldReturnStatement => NodeLayout.Transparent,
            SyntaxKind.YieldBreakStatement => NodeLayout.Transparent,
            SyntaxKind.ThrowStatement => NodeLayout.Transparent,
            SyntaxKind.CheckedStatement => NodeLayout.Transparent,
            SyntaxKind.UncheckedStatement => NodeLayout.Transparent,
            SyntaxKind.UnsafeStatement => NodeLayout.Transparent,
            SyntaxKind.CaseSwitchLabel => NodeLayout.Transparent,
            SyntaxKind.DefaultSwitchLabel => NodeLayout.Transparent,
            SyntaxKind.TryStatement => NodeLayout.Transparent,
            SyntaxKind.CatchClause => NodeLayout.Transparent,
            SyntaxKind.CatchDeclaration => NodeLayout.Transparent,
            SyntaxKind.CatchFilterClause => NodeLayout.Transparent,
            SyntaxKind.FinallyClause => NodeLayout.Transparent,
            SyntaxKind.LocalFunctionStatement => NodeLayout.Transparent,
            SyntaxKind.CompilationUnit => NodeLayout.Transparent,
            SyntaxKind.GlobalStatement => NodeLayout.Transparent,
            SyntaxKind.UsingDirective => NodeLayout.Transparent,
            SyntaxKind.ExternAliasDirective => NodeLayout.Transparent,
            SyntaxKind.FileScopedNamespaceDeclaration => NodeLayout.Transparent,
            SyntaxKind.AttributeTargetSpecifier => NodeLayout.Transparent,
            SyntaxKind.Attribute => NodeLayout.Transparent,
            SyntaxKind.AttributeArgument => NodeLayout.Transparent,
            SyntaxKind.NameEquals => NodeLayout.Transparent,
            SyntaxKind.DelegateDeclaration => NodeLayout.Transparent,
            SyntaxKind.SimpleBaseType => NodeLayout.Transparent,
            SyntaxKind.ConstructorConstraint => NodeLayout.Transparent,
            SyntaxKind.ClassConstraint => NodeLayout.Transparent,
            SyntaxKind.StructConstraint => NodeLayout.Transparent,
            SyntaxKind.TypeConstraint => NodeLayout.Transparent,
            SyntaxKind.ExplicitInterfaceSpecifier => NodeLayout.Transparent,
            SyntaxKind.EnumMemberDeclaration => NodeLayout.Transparent,
            SyntaxKind.FieldDeclaration => NodeLayout.Transparent,
            SyntaxKind.EventFieldDeclaration => NodeLayout.Transparent,
            SyntaxKind.MethodDeclaration => NodeLayout.Transparent,
            SyntaxKind.OperatorDeclaration => NodeLayout.Transparent,
            SyntaxKind.ConversionOperatorDeclaration => NodeLayout.Transparent,
            SyntaxKind.ConstructorDeclaration => NodeLayout.Transparent,
            SyntaxKind.AllowsConstraintClause => NodeLayout.Transparent,
            SyntaxKind.RefStructConstraint => NodeLayout.Transparent,
            SyntaxKind.DestructorDeclaration => NodeLayout.Transparent,
            SyntaxKind.PropertyDeclaration => NodeLayout.Transparent,
            SyntaxKind.EventDeclaration => NodeLayout.Transparent,
            SyntaxKind.IndexerDeclaration => NodeLayout.Transparent,
            SyntaxKind.GetAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.SetAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.AddAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.RemoveAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.UnknownAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.Parameter => NodeLayout.Transparent,
            SyntaxKind.TypeParameter => NodeLayout.Transparent,
            SyntaxKind.IncompleteMember => NodeLayout.Transparent,
            SyntaxKind.Interpolation => NodeLayout.Transparent,
            SyntaxKind.InterpolatedStringText => NodeLayout.Transparent,
            SyntaxKind.InterpolationAlignmentClause => NodeLayout.Transparent,
            SyntaxKind.InterpolationFormatClause => NodeLayout.Transparent,
            SyntaxKind.TupleElement => NodeLayout.Transparent,
            SyntaxKind.SingleVariableDesignation => NodeLayout.Transparent,
            SyntaxKind.DeclarationPattern => NodeLayout.Transparent,
            SyntaxKind.ConstantPattern => NodeLayout.Transparent,
            SyntaxKind.CasePatternSwitchLabel => NodeLayout.Transparent,
            SyntaxKind.WhenClause => NodeLayout.Transparent,
            SyntaxKind.DiscardDesignation => NodeLayout.Transparent,
            SyntaxKind.RecursivePattern => NodeLayout.Transparent,
            SyntaxKind.Subpattern => NodeLayout.Transparent,
            SyntaxKind.DiscardPattern => NodeLayout.Transparent,
            SyntaxKind.SwitchExpressionArm => NodeLayout.Transparent,
            SyntaxKind.VarPattern => NodeLayout.Transparent,
            SyntaxKind.RelationalPattern => NodeLayout.Transparent,
            SyntaxKind.TypePattern => NodeLayout.Transparent,
            SyntaxKind.NotPattern => NodeLayout.Transparent,
            SyntaxKind.SlicePattern => NodeLayout.Transparent,
            SyntaxKind.DeclarationExpression => NodeLayout.Transparent,
            SyntaxKind.RefExpression => NodeLayout.Transparent,
            SyntaxKind.RefType => NodeLayout.Transparent,
            SyntaxKind.ThrowExpression => NodeLayout.Transparent,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression => NodeLayout.Transparent,
            SyntaxKind.SuppressNullableWarningExpression => NodeLayout.Transparent,
            SyntaxKind.FunctionPointerType => NodeLayout.Transparent,
            SyntaxKind.FunctionPointerParameter => NodeLayout.Transparent,
            SyntaxKind.FunctionPointerCallingConvention => NodeLayout.Transparent,
            SyntaxKind.InitAccessorDeclaration => NodeLayout.Transparent,
            SyntaxKind.WithExpression => NodeLayout.Transparent,
            SyntaxKind.DefaultConstraint => NodeLayout.Transparent,
            SyntaxKind.PrimaryConstructorBaseType => NodeLayout.Transparent,
            SyntaxKind.FunctionPointerUnmanagedCallingConvention => NodeLayout.Transparent,
            SyntaxKind.ExpressionColon => NodeLayout.Transparent,
            SyntaxKind.LineDirectivePosition => NodeLayout.Transparent,
            SyntaxKind.InterpolatedSingleLineRawStringStartToken => NodeLayout.Transparent,
            SyntaxKind.InterpolatedMultiLineRawStringStartToken => NodeLayout.Transparent,
            SyntaxKind.InterpolatedRawStringEndToken => NodeLayout.Transparent,
            SyntaxKind.ScopedType => NodeLayout.Transparent,
            SyntaxKind.ExpressionElement => NodeLayout.Transparent,
            SyntaxKind.SpreadElement => NodeLayout.Transparent,
            SyntaxKind.WithElement => NodeLayout.Transparent,
            _ => NodeLayout.Unknown
        };

    /// <summary>True for the kinds that name a <see cref="Microsoft.CodeAnalysis.SyntaxNode"/>.</summary>
    public static bool IsNodeKind(SyntaxKind kind) => (int)kind >= FirstNodeKind;
}
