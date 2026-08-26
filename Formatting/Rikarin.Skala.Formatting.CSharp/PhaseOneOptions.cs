using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// The option subset milestone 1 implements, read once per file into fields.
/// </summary>
/// <remarks>
/// ⚠ Every value here is read out of <see cref="FormattingOptions"/> by <see cref="OptionId"/>,
/// which is an array index and not a dictionary lookup (docs/plan/13 § "The fitting pass"). The
/// façade exists for two more reasons: the generated accessor names follow whichever spelling the
/// export happened to use, which is not a name a rule should be written against; and
/// <see cref="Implemented"/> is then a single honest list of what phase 1 consumes, which is what
/// the Tier A promotion and the per-option corpus test are checked against.
/// </remarks>
public readonly struct PhaseOneOptions {
    public PhaseOneOptions(in FormattingOptions options) {
        // ── Layout ───────────────────────────────────────────────────────────────────────────
        IndentSize = Math.Max(1, options.GetInt(Ids.IndentSize));
        TabWidth = Math.Max(1, options.GetInt(Ids.TabWidth));
        UseTabs = options.GetRaw(Ids.IndentStyle) == (int)IndentStyle.Tab;
        MaxLineLength = options.GetInt(Ids.MaxLineLength) is var w and > 0 ? w : 120;
        InsertFinalNewline = options.GetBool(Ids.InsertFinalNewline);
        RemoveSpacesOnBlankLines = options.GetBool(Ids.RemoveSpacesOnBlankLines);
        EnforceLineEndingStyle = options.GetBool(Ids.EnforceLineEndingStyle);
        LineEnding = (LineEnding)options.GetRaw(Ids.EndOfLine);

        // ── Spaces ───────────────────────────────────────────────────────────────────────────
        SpaceAfterComma = options.GetBool(Ids.SpaceAfterComma);
        SpaceBeforeComma = options.GetBool(Ids.SpaceBeforeComma);
        SpaceBeforeSemicolon = options.GetBool(Ids.SpaceBeforeSemicolon);
        SpaceAfterSemicolonInFor = options.GetBool(Ids.SpaceAfterSemicolonInForStatement);
        SpaceBeforeSemicolonInFor = options.GetBool(Ids.SpaceBeforeSemicolonInForStatement);
        SpaceAfterCast = options.GetBool(Ids.SpaceAfterCast);
        SpaceAroundMemberAccess = options.GetBool(Ids.SpaceAroundMemberAccessOperator);
        SpaceAfterUnaryOperator = options.GetBool(Ids.SpaceAfterUnaryOperator);
        SpaceNearPostfixAndPrefixOp = options.GetBool(Ids.SpaceNearPostfixAndPrefixOp);
        SpaceAroundAssignmentOp = options.GetBool(Ids.SpaceAroundAssignmentOp);
        SpaceAroundLambdaArrow = options.GetBool(Ids.SpaceAroundLambdaArrow);
        SpaceAroundAdditiveOp = options.GetBool(Ids.SpaceAroundAdditiveOp);
        SpaceAroundRelationalOp = options.GetBool(Ids.SpaceAroundRelationalOp);
        SpaceAroundShiftOp = options.GetBool(Ids.SpaceAroundShiftOp);
        SpaceAroundAliasEq = options.GetBool(Ids.SpaceAroundAliasEq);
        SpaceAfterOperatorKeyword = options.GetBool(Ids.SpaceAfterOperatorKeyword);
        SpaceBetweenKeywordAndExpression = options.GetBool(Ids.SpaceBetweenKeywordAndExpression);
        SpaceBetweenKeywordAndType = options.GetBool(Ids.SpaceBetweenKeywordAndType);

        SpaceAfterKeywordsInControlFlow = options.GetBool(Ids.SpaceAfterKeywordsInControlFlowStatements);
        SpaceBeforeMethodParentheses = options.GetBool(Ids.SpaceBeforeMethodParentheses);
        SpaceBeforeMethodCallParentheses = options.GetBool(Ids.SpaceBeforeMethodCallParentheses);
        SpaceBeforeEmptyMethodParentheses = options.GetBool(Ids.SpaceBeforeEmptyMethodParentheses);
        SpaceBeforeEmptyMethodCallParentheses = options.GetBool(Ids.SpaceBeforeEmptyMethodCallParentheses);
        SpaceBeforeNewParentheses = options.GetBool(Ids.SpaceBeforeNewParentheses);
        SpaceBeforeTypeofParentheses = options.GetBool(Ids.SpaceBeforeTypeofParentheses);
        SpaceBeforeSizeofParentheses = options.GetBool(Ids.SpaceBeforeSizeofParentheses);
        SpaceBeforeDefaultParentheses = options.GetBool(Ids.SpaceBeforeDefaultParentheses);
        SpaceBeforeCheckedParentheses = options.GetBool(Ids.SpaceBeforeCheckedParentheses);
        SpaceBeforeNameofParentheses = options.GetBool(Ids.SpaceBeforeNameofParentheses);
        SpaceWithinParentheses = options.GetBool(Ids.SpaceWithinParentheses);
        SpaceBetweenTypecastParentheses = options.GetBool(Ids.SpaceBetweenTypecastParentheses);

        SpaceBeforeOpenSquareBrackets = options.GetBool(Ids.SpaceBeforeOpenSquareBrackets);
        SpaceBeforeArrayAccessBrackets = options.GetBool(Ids.SpaceBeforeArrayAccessBrackets);
        SpaceBeforeArrayRankBrackets = options.GetBool(Ids.SpaceBeforeArrayRankBrackets);
        SpaceWithinArrayAccessBrackets = options.GetBool(Ids.SpaceWithinArrayAccessBrackets);
        SpaceWithinAttributeBrackets = options.GetBool(Ids.SpaceWithinAttributeBrackets);
        SpaceWithinListPatternBrackets = options.GetBool(Ids.SpaceWithinListPatternBrackets);

        SpaceBeforeTypeArgumentAngle = options.GetBool(Ids.SpaceBeforeTypeArgumentAngle);
        SpaceBeforeTypeParameterAngle = options.GetBool(Ids.SpaceBeforeTypeParameterAngle);
        SpaceWithinTypeArgumentAngles = options.GetBool(Ids.SpaceWithinTypeArgumentAngles);
        SpaceWithinTypeParameterAngles = options.GetBool(Ids.SpaceWithinTypeParameterAngles);

        SpaceAfterAttributes = options.GetBool(Ids.SpaceAfterAttributes);
        SpaceBetweenAttributeSections = options.GetBool(Ids.SpaceBetweenAttributeSections);
        SpaceBeforeAttributeColon = options.GetBool(Ids.SpaceBeforeAttributeColon);
        SpaceAfterAttributeColon = options.GetBool(Ids.SpaceAfterAttributeColon);

        SpaceBeforeColonInInheritance = options.GetBool(Ids.SpaceBeforeColonInInheritanceClause);
        SpaceAfterColonInInheritance = options.GetBool(Ids.SpaceAfterColonInInheritanceClause);
        SpaceBeforeColonInCase = options.GetBool(Ids.SpaceBeforeColonInCase);
        SpaceAfterColonInCase = options.GetBool(Ids.SpaceAfterColonInCase);
        SpaceBeforeColonInCtorInitializer = options.GetBool(Ids.SpaceBeforeColonInCtorInitializer);
        SpaceBeforeTypeParameterConstraintColon = options.GetBool(Ids.SpaceBeforeTypeParameterConstraintColon);
        SpaceAfterTypeParameterConstraintColon = options.GetBool(Ids.SpaceAfterTypeParameterConstraintColon);
        SpaceBeforeTernaryQuest = options.GetBool(Ids.SpaceBeforeTernaryQuest);
        SpaceAfterTernaryQuest = options.GetBool(Ids.SpaceAfterTernaryQuest);
        SpaceBeforeTernaryColon = options.GetBool(Ids.SpaceBeforeTernaryColon);
        SpaceAfterTernaryColon = options.GetBool(Ids.SpaceAfterTernaryColon);
        SpaceBeforeNullableMark = options.GetBool(Ids.SpaceBeforeNullableMark);
        SpaceBeforePointerAsterikDeclaration = options.GetBool(Ids.SpaceBeforePointerAsterikDeclaration);

        SpaceBeforeSinglelineAccessorholder = options.GetBool(Ids.SpaceBeforeSinglelineAccessorholder);
        SpaceInSinglelineAccessorholder = options.GetBool(Ids.SpaceInSinglelineAccessorholder);
        SpaceBetweenAccessorsInSinglelineProperty = options.GetBool(Ids.SpaceBetweenAccessorsInSinglelineProperty);
        SpaceInSinglelineMethod = options.GetBool(Ids.SpaceInSinglelineMethod);
        SpaceInSinglelineAnonymousMethod = options.GetBool(Ids.SpaceInSinglelineAnonymousMethod);
        SpaceWithinEmptyBraces = options.GetBool(Ids.SpaceWithinEmptyBraces);
        SpaceWithinSingleLineArrayInitializerBraces = options.GetBool(Ids.SpaceWithinSingleLineArrayInitializerBraces);
        SpaceWithinSlicePattern = options.GetBool(Ids.SpaceWithinSlicePattern);
        SpaceWithinSpreadPattern = options.GetBool(Ids.SpaceWithinSpreadPattern);

        // ── Comments ─────────────────────────────────────────────────────────────────────────
        SpaceBeforeTrailingComment = options.GetBool(Ids.SpaceBeforeTrailingComment);
        SpaceBeforeTrailingCommentText = options.GetBool(Ids.SpaceBeforeTrailingCommentText);
        SpaceAfterTripleSlash = options.GetBool(Ids.SpaceAfterTripleSlash);
        StickComment = options.GetBool(Ids.StickComment);
        PlaceCommentsAtFirstColumn = options.GetBool(Ids.PlaceCommentsAtFirstColumn);

        // ── Braces ───────────────────────────────────────────────────────────────────────────
        NewLineBeforeOpenBrace = options.GetString(Ids.NewLineBeforeOpenBrace) ?? "none";
        NewLineBeforeElse = options.GetBool(Ids.NewLineBeforeElse);
        NewLineBeforeCatch = options.GetBool(Ids.NewLineBeforeCatch);
        NewLineBeforeFinally = options.GetBool(Ids.NewLineBeforeFinally);
        NewLineBeforeWhile = options.GetBool(Ids.NewLineBeforeWhile);
        SpecialElseIfTreatment = options.GetBool(Ids.SpecialElseIfTreatment);
        EmptyBlockStyle = (EmptyBlockStyle)options.GetRaw(Ids.EmptyBlockStyle);
        AllowCommentAfterLbrace = options.GetBool(Ids.AllowCommentAfterLbrace);

        // ── Indentation ──────────────────────────────────────────────────────────────────────
        IndentBraces = options.GetBool(Ids.IndentBraces);
        IndentSwitchLabels = options.GetBool(Ids.IndentSwitchLabels);
        IndentBreakFromCase = options.GetBool(Ids.IndentBreakFromCase);
        IndentInsideNamespace = options.GetBool(Ids.IndentInsideNamespace);
        IndentTypeConstraints = options.GetBool(Ids.IndentTypeConstraints);
        IndentNestedForStmt = options.GetBool(Ids.IndentNestedForStmt);
        IndentNestedForeachStmt = options.GetBool(Ids.IndentNestedForeachStmt);
        IndentNestedWhileStmt = options.GetBool(Ids.IndentNestedWhileStmt);
        IndentNestedUsingsStmt = options.GetBool(Ids.IndentNestedUsingsStmt);
        IndentNestedLockStmt = options.GetBool(Ids.IndentNestedLockStmt);
        IndentNestedFixedStmt = options.GetBool(Ids.IndentNestedFixedStmt);
        UseContinuousIndentInsideParens = options.GetBool(Ids.UseContinuousIndentInsideParens);
        UseContinuousIndentInsideInitializerBraces = options.GetBool(Ids.UseContinuousIndentInsideInitializerBraces);
        ContinuousIndentMultiplier = Math.Max(1, options.GetInt(Ids.ContinuousIndentMultiplier));
        IndentPreprocessorIf = (PreprocessorIndentStyle)options.GetRaw(Ids.IndentPreprocessorIf);
        IndentPreprocessorOther = (PreprocessorIndentStyle)options.GetRaw(Ids.IndentPreprocessorOther);
        IndentPreprocessorRegion = (PreprocessorIndentStyle)options.GetRaw(Ids.IndentPreprocessorRegion);
        IndentAnonymousMethodBlock = options.GetBool(Ids.IndentAnonymousMethodBlock);

        // ── Blank lines ──────────────────────────────────────────────────────────────────────
        KeepBlankLinesInCode = options.GetInt(Ids.KeepBlankLinesInCode);
        KeepBlankLinesInDeclarations = options.GetInt(Ids.KeepBlankLinesInDeclarations);
        RemoveBlankLinesNearBracesInCode = options.GetBool(Ids.RemoveBlankLinesNearBracesInCode);
        RemoveBlankLinesNearBracesInDeclarations = options.GetBool(Ids.RemoveBlankLinesNearBracesInDeclarations);
        BlankLinesAroundType = options.GetInt(Ids.BlankLinesAroundType);
        BlankLinesAroundSingleLineType = options.GetInt(Ids.BlankLinesAroundSingleLineType);
        BlankLinesAroundInvocable = options.GetInt(Ids.BlankLinesAroundInvocable);
        BlankLinesAroundSingleLineInvocable = options.GetInt(Ids.BlankLinesAroundSingleLineInvocable);
        BlankLinesAroundField = options.GetInt(Ids.BlankLinesAroundField);
        BlankLinesAroundSingleLineField = options.GetInt(Ids.BlankLinesAroundSingleLineField);
        BlankLinesAroundProperty = options.GetInt(Ids.BlankLinesAroundProperty);
        BlankLinesAroundSingleLineProperty = options.GetInt(Ids.BlankLinesAroundSingleLineProperty);
        BlankLinesAroundAutoProperty = options.GetInt(Ids.BlankLinesAroundAutoProperty);
        BlankLinesAroundSingleLineAutoProperty = options.GetInt(Ids.BlankLinesAroundSingleLineAutoProperty);
        BlankLinesAroundAccessor = options.GetInt(Ids.BlankLinesAroundAccessor);
        BlankLinesAroundSingleLineAccessor = options.GetInt(Ids.BlankLinesAroundSingleLineAccessor);
        BlankLinesAroundLocalMethod = options.GetInt(Ids.BlankLinesAroundLocalMethod);
        BlankLinesAroundSingleLineLocalMethod = options.GetInt(Ids.BlankLinesAroundSingleLineLocalMethod);
        BlankLinesAroundNamespace = options.GetInt(Ids.BlankLinesAroundNamespace);
        BlankLinesAroundRegion = options.GetInt(Ids.BlankLinesAroundRegion);
        BlankLinesInsideRegion = options.GetInt(Ids.BlankLinesInsideRegion);
        BlankLinesInsideType = options.GetInt(Ids.BlankLinesInsideType);
        BlankLinesInsideNamespace = options.GetInt(Ids.BlankLinesInsideNamespace);
        BlankLinesAfterUsingList = options.GetInt(Ids.BlankLinesAfterUsingList);
        BlankLinesAfterFileScopedNamespaceDirective = options.GetInt(Ids.BlankLinesAfterFileScopedNamespaceDirective);
        BlankLinesAfterBlockStatements = options.GetInt(Ids.BlankLinesAfterBlockStatements);
        BlankLinesBeforeSingleLineComment = options.GetInt(Ids.BlankLinesBeforeSingleLineComment);
        BlankLinesAfterCase = options.GetInt(Ids.BlankLinesAfterCase);
        BlankLinesBeforeCase = options.GetInt(Ids.BlankLinesBeforeCase);

        // ── Escape hatch ─────────────────────────────────────────────────────────────────────
        FormatterTagsEnabled = options.GetBool(Ids.FormatterTagsEnabled);
        FormatterOffTag = options.GetString(Ids.FormatterOffTag) ?? "@formatter:off";
        FormatterOnTag = options.GetString(Ids.FormatterOnTag) ?? "@formatter:on";
        FormatterTagsAcceptRegexp = options.GetBool(Ids.FormatterTagsAcceptRegexp);
    }

    public int IndentSize { get; }
    public int TabWidth { get; }
    public bool UseTabs { get; }
    public int MaxLineLength { get; }
    public bool InsertFinalNewline { get; }
    public bool RemoveSpacesOnBlankLines { get; }
    public bool EnforceLineEndingStyle { get; }
    public LineEnding LineEnding { get; }

    public bool SpaceAfterComma { get; }
    public bool SpaceBeforeComma { get; }
    public bool SpaceBeforeSemicolon { get; }
    public bool SpaceAfterSemicolonInFor { get; }
    public bool SpaceBeforeSemicolonInFor { get; }
    public bool SpaceAfterCast { get; }
    public bool SpaceAroundMemberAccess { get; }
    public bool SpaceAfterUnaryOperator { get; }
    public bool SpaceNearPostfixAndPrefixOp { get; }
    public bool SpaceAroundAssignmentOp { get; }
    public bool SpaceAroundLambdaArrow { get; }
    public bool SpaceAroundAdditiveOp { get; }
    public bool SpaceAroundRelationalOp { get; }
    public bool SpaceAroundShiftOp { get; }
    public bool SpaceAroundAliasEq { get; }
    public bool SpaceAfterOperatorKeyword { get; }
    public bool SpaceBetweenKeywordAndExpression { get; }
    public bool SpaceBetweenKeywordAndType { get; }

    public bool SpaceAfterKeywordsInControlFlow { get; }
    public bool SpaceBeforeMethodParentheses { get; }
    public bool SpaceBeforeMethodCallParentheses { get; }
    public bool SpaceBeforeEmptyMethodParentheses { get; }
    public bool SpaceBeforeEmptyMethodCallParentheses { get; }
    public bool SpaceBeforeNewParentheses { get; }
    public bool SpaceBeforeTypeofParentheses { get; }
    public bool SpaceBeforeSizeofParentheses { get; }
    public bool SpaceBeforeDefaultParentheses { get; }
    public bool SpaceBeforeCheckedParentheses { get; }
    public bool SpaceBeforeNameofParentheses { get; }
    public bool SpaceWithinParentheses { get; }
    public bool SpaceBetweenTypecastParentheses { get; }

    public bool SpaceBeforeOpenSquareBrackets { get; }
    public bool SpaceBeforeArrayAccessBrackets { get; }
    public bool SpaceBeforeArrayRankBrackets { get; }
    public bool SpaceWithinArrayAccessBrackets { get; }
    public bool SpaceWithinAttributeBrackets { get; }
    public bool SpaceWithinListPatternBrackets { get; }

    public bool SpaceBeforeTypeArgumentAngle { get; }
    public bool SpaceBeforeTypeParameterAngle { get; }
    public bool SpaceWithinTypeArgumentAngles { get; }
    public bool SpaceWithinTypeParameterAngles { get; }

    public bool SpaceAfterAttributes { get; }
    public bool SpaceBetweenAttributeSections { get; }
    public bool SpaceBeforeAttributeColon { get; }
    public bool SpaceAfterAttributeColon { get; }

    public bool SpaceBeforeColonInInheritance { get; }
    public bool SpaceAfterColonInInheritance { get; }
    public bool SpaceBeforeColonInCase { get; }
    public bool SpaceAfterColonInCase { get; }
    public bool SpaceBeforeColonInCtorInitializer { get; }
    public bool SpaceBeforeTypeParameterConstraintColon { get; }
    public bool SpaceAfterTypeParameterConstraintColon { get; }
    public bool SpaceBeforeTernaryQuest { get; }
    public bool SpaceAfterTernaryQuest { get; }
    public bool SpaceBeforeTernaryColon { get; }
    public bool SpaceAfterTernaryColon { get; }
    public bool SpaceBeforeNullableMark { get; }
    public bool SpaceBeforePointerAsterikDeclaration { get; }

    public bool SpaceBeforeSinglelineAccessorholder { get; }
    public bool SpaceInSinglelineAccessorholder { get; }
    public bool SpaceBetweenAccessorsInSinglelineProperty { get; }
    public bool SpaceInSinglelineMethod { get; }
    public bool SpaceInSinglelineAnonymousMethod { get; }
    public bool SpaceWithinEmptyBraces { get; }
    public bool SpaceWithinSingleLineArrayInitializerBraces { get; }
    public bool SpaceWithinSlicePattern { get; }
    public bool SpaceWithinSpreadPattern { get; }

    public bool SpaceBeforeTrailingComment { get; }
    public bool SpaceBeforeTrailingCommentText { get; }
    public bool SpaceAfterTripleSlash { get; }
    public bool StickComment { get; }
    public bool PlaceCommentsAtFirstColumn { get; }

    public string NewLineBeforeOpenBrace { get; }
    public bool NewLineBeforeElse { get; }
    public bool NewLineBeforeCatch { get; }
    public bool NewLineBeforeFinally { get; }
    public bool NewLineBeforeWhile { get; }
    public bool SpecialElseIfTreatment { get; }
    public EmptyBlockStyle EmptyBlockStyle { get; }
    public bool AllowCommentAfterLbrace { get; }

    public bool IndentBraces { get; }
    public bool IndentSwitchLabels { get; }
    public bool IndentBreakFromCase { get; }
    public bool IndentInsideNamespace { get; }
    public bool IndentTypeConstraints { get; }
    public bool IndentNestedForStmt { get; }
    public bool IndentNestedForeachStmt { get; }
    public bool IndentNestedWhileStmt { get; }
    public bool IndentNestedUsingsStmt { get; }
    public bool IndentNestedLockStmt { get; }
    public bool IndentNestedFixedStmt { get; }
    public bool UseContinuousIndentInsideParens { get; }
    public bool UseContinuousIndentInsideInitializerBraces { get; }
    public int ContinuousIndentMultiplier { get; }
    public PreprocessorIndentStyle IndentPreprocessorIf { get; }
    public PreprocessorIndentStyle IndentPreprocessorOther { get; }
    public PreprocessorIndentStyle IndentPreprocessorRegion { get; }
    public bool IndentAnonymousMethodBlock { get; }

    public int KeepBlankLinesInCode { get; }
    public int KeepBlankLinesInDeclarations { get; }
    public bool RemoveBlankLinesNearBracesInCode { get; }
    public bool RemoveBlankLinesNearBracesInDeclarations { get; }
    public int BlankLinesAroundType { get; }
    public int BlankLinesAroundSingleLineType { get; }
    public int BlankLinesAroundInvocable { get; }
    public int BlankLinesAroundSingleLineInvocable { get; }
    public int BlankLinesAroundField { get; }
    public int BlankLinesAroundSingleLineField { get; }
    public int BlankLinesAroundProperty { get; }
    public int BlankLinesAroundSingleLineProperty { get; }
    public int BlankLinesAroundAutoProperty { get; }
    public int BlankLinesAroundSingleLineAutoProperty { get; }
    public int BlankLinesAroundAccessor { get; }
    public int BlankLinesAroundSingleLineAccessor { get; }
    public int BlankLinesAroundLocalMethod { get; }
    public int BlankLinesAroundSingleLineLocalMethod { get; }
    public int BlankLinesAroundNamespace { get; }
    public int BlankLinesAroundRegion { get; }
    public int BlankLinesInsideRegion { get; }
    public int BlankLinesInsideType { get; }
    public int BlankLinesInsideNamespace { get; }
    public int BlankLinesAfterUsingList { get; }
    public int BlankLinesAfterFileScopedNamespaceDirective { get; }
    public int BlankLinesAfterBlockStatements { get; }
    public int BlankLinesBeforeSingleLineComment { get; }
    public int BlankLinesAfterCase { get; }
    public int BlankLinesBeforeCase { get; }

    public bool FormatterTagsEnabled { get; }
    public string FormatterOffTag { get; }
    public string FormatterOnTag { get; }
    public bool FormatterTagsAcceptRegexp { get; }

    /// <summary>
    /// Every option milestone 1 reads, in registry order. The Tier A promotion and the per-option
    /// corpus test are checked against this list, so an option that stops being read here stops
    /// claiming to be implemented.
    /// </summary>
    public static ImmutableArray<OptionId> Implemented => Ids.All;
}

/// <summary>
/// The option ids phase 1 reads, resolved once through the registry.
/// </summary>
/// <remarks>
/// Written as <c>.editorconfig</c> spellings rather than as generated accessor names, because the
/// spelling is the thing the plan, the export and the ReSharper documentation all agree on, while
/// the accessor name depends on which spelling the importer happened to pick as canonical.
/// </remarks>
public static class Ids {
    static readonly List<OptionId> Collected = [];

    /// <summary>Ids <see cref="OfInert"/> marked; read but excluded from <see cref="All"/>.</summary>
    static readonly List<OptionId> Inert = [];

    public static readonly OptionId IndentSize = Of("resharper_csharp_indent_size");
    public static readonly OptionId TabWidth = OfInert("resharper_csharp_tab_width");
    public static readonly OptionId IndentStyle = Of("resharper_csharp_indent_style");
    public static readonly OptionId MaxLineLength = OfInert("resharper_csharp_max_line_length");
    public static readonly OptionId InsertFinalNewline = Of("resharper_csharp_insert_final_newline");
    public static readonly OptionId RemoveSpacesOnBlankLines = OfInert("resharper_remove_spaces_on_blank_lines");
    public static readonly OptionId EnforceLineEndingStyle = Of("resharper_enforce_line_ending_style");
    public static readonly OptionId EndOfLine = OfInert("end_of_line");

    public static readonly OptionId SpaceAfterComma = Of("resharper_space_after_comma");
    public static readonly OptionId SpaceBeforeComma = Of("resharper_space_before_comma");
    public static readonly OptionId SpaceBeforeSemicolon = Of("resharper_csharp_space_before_semicolon");
    public static readonly OptionId SpaceAfterSemicolonInForStatement = Of("resharper_space_after_semicolon_in_for_statement");
    public static readonly OptionId SpaceBeforeSemicolonInForStatement = Of("resharper_space_before_semicolon_in_for_statement");
    public static readonly OptionId SpaceAfterCast = Of("resharper_space_after_cast");
    public static readonly OptionId SpaceAroundMemberAccessOperator = Of("resharper_space_around_member_access_operator");
    public static readonly OptionId SpaceAfterUnaryOperator = Of("resharper_csharp_space_after_unary_operator");
    public static readonly OptionId SpaceNearPostfixAndPrefixOp = Of("resharper_csharp_space_near_postfix_and_prefix_op");
    public static readonly OptionId SpaceAroundAssignmentOp = Of("resharper_csharp_space_around_assignment_op");
    public static readonly OptionId SpaceAroundLambdaArrow = Of("resharper_csharp_space_around_lambda_arrow");
    public static readonly OptionId SpaceAroundAdditiveOp = Of("resharper_csharp_space_around_additive_op");
    public static readonly OptionId SpaceAroundRelationalOp = Of("resharper_csharp_space_around_relational_op");
    public static readonly OptionId SpaceAroundShiftOp = Of("resharper_csharp_space_around_shift_op");
    public static readonly OptionId SpaceAroundAliasEq = Of("resharper_csharp_space_around_alias_eq");
    public static readonly OptionId SpaceAfterOperatorKeyword = Of("resharper_csharp_space_after_operator_keyword");
    public static readonly OptionId SpaceBetweenKeywordAndExpression = Of("resharper_csharp_space_between_keyword_and_expression");
    public static readonly OptionId SpaceBetweenKeywordAndType = OfInert("resharper_csharp_space_between_keyword_and_type");

    public static readonly OptionId SpaceAfterKeywordsInControlFlowStatements = Of("resharper_space_after_keywords_in_control_flow_statements");
    public static readonly OptionId SpaceBeforeMethodParentheses = Of("resharper_csharp_space_before_method_parentheses");
    public static readonly OptionId SpaceBeforeMethodCallParentheses = Of("resharper_csharp_space_before_method_call_parentheses");
    public static readonly OptionId SpaceBeforeEmptyMethodParentheses = Of("resharper_csharp_space_before_empty_method_parentheses");
    public static readonly OptionId SpaceBeforeEmptyMethodCallParentheses = Of("resharper_csharp_space_before_empty_method_call_parentheses");
    public static readonly OptionId SpaceBeforeNewParentheses = Of("resharper_csharp_space_before_new_parentheses");
    public static readonly OptionId SpaceBeforeTypeofParentheses = Of("resharper_csharp_space_before_typeof_parentheses");
    public static readonly OptionId SpaceBeforeSizeofParentheses = Of("resharper_csharp_space_before_sizeof_parentheses");
    public static readonly OptionId SpaceBeforeDefaultParentheses = Of("resharper_csharp_space_before_default_parentheses");
    public static readonly OptionId SpaceBeforeCheckedParentheses = Of("resharper_csharp_space_before_checked_parentheses");
    public static readonly OptionId SpaceBeforeNameofParentheses = Of("resharper_csharp_space_before_nameof_parentheses");
    public static readonly OptionId SpaceWithinParentheses = Of("resharper_csharp_space_within_parentheses");
    public static readonly OptionId SpaceBetweenTypecastParentheses = Of("resharper_csharp_space_between_typecast_parentheses");

    public static readonly OptionId SpaceBeforeOpenSquareBrackets = OfInert("resharper_space_before_open_square_brackets");
    public static readonly OptionId SpaceBeforeArrayAccessBrackets = Of("resharper_csharp_space_before_array_access_brackets");
    public static readonly OptionId SpaceBeforeArrayRankBrackets = Of("resharper_csharp_space_before_array_rank_brackets");
    public static readonly OptionId SpaceWithinArrayAccessBrackets = Of("resharper_csharp_space_within_array_access_brackets");
    public static readonly OptionId SpaceWithinAttributeBrackets = Of("resharper_csharp_space_within_attribute_brackets");
    public static readonly OptionId SpaceWithinListPatternBrackets = Of("resharper_csharp_space_within_list_pattern_brackets");

    public static readonly OptionId SpaceBeforeTypeArgumentAngle = Of("resharper_csharp_space_before_type_argument_angle");
    public static readonly OptionId SpaceBeforeTypeParameterAngle = Of("resharper_csharp_space_before_type_parameter_angle");
    public static readonly OptionId SpaceWithinTypeArgumentAngles = Of("resharper_csharp_space_within_type_argument_angles");
    public static readonly OptionId SpaceWithinTypeParameterAngles = Of("resharper_csharp_space_within_type_parameter_angles");

    public static readonly OptionId SpaceAfterAttributes = Of("resharper_csharp_space_after_attributes");
    public static readonly OptionId SpaceBetweenAttributeSections = Of("resharper_csharp_space_between_attribute_sections");
    public static readonly OptionId SpaceBeforeAttributeColon = Of("resharper_csharp_space_before_attribute_colon");
    public static readonly OptionId SpaceAfterAttributeColon = Of("resharper_csharp_space_after_attribute_colon");

    public static readonly OptionId SpaceBeforeColonInInheritanceClause = Of("resharper_space_before_colon_in_inheritance_clause");
    public static readonly OptionId SpaceAfterColonInInheritanceClause = Of("resharper_space_after_colon_in_inheritance_clause");
    public static readonly OptionId SpaceBeforeColonInCase = Of("resharper_csharp_space_before_colon_in_case");
    public static readonly OptionId SpaceAfterColonInCase = Of("resharper_csharp_space_after_colon_in_case");
    public static readonly OptionId SpaceBeforeColonInCtorInitializer = Of("resharper_space_before_colon_in_ctor_initializer");
    public static readonly OptionId SpaceBeforeTypeParameterConstraintColon = Of("resharper_csharp_space_before_type_parameter_constraint_colon");
    public static readonly OptionId SpaceAfterTypeParameterConstraintColon = Of("resharper_csharp_space_after_type_parameter_constraint_colon");
    public static readonly OptionId SpaceBeforeTernaryQuest = Of("resharper_csharp_space_before_ternary_quest");
    public static readonly OptionId SpaceAfterTernaryQuest = Of("resharper_csharp_space_after_ternary_quest");
    public static readonly OptionId SpaceBeforeTernaryColon = Of("resharper_csharp_space_before_ternary_colon");
    public static readonly OptionId SpaceAfterTernaryColon = Of("resharper_csharp_space_after_ternary_colon");
    public static readonly OptionId SpaceBeforeNullableMark = Of("resharper_csharp_space_before_nullable_mark");
    public static readonly OptionId SpaceBeforePointerAsterikDeclaration = Of("resharper_csharp_space_before_pointer_asterik_declaration");

    public static readonly OptionId SpaceBeforeSinglelineAccessorholder = Of("resharper_csharp_space_before_singleline_accessorholder");
    public static readonly OptionId SpaceInSinglelineAccessorholder = Of("resharper_csharp_space_in_singleline_accessorholder");
    public static readonly OptionId SpaceBetweenAccessorsInSinglelineProperty = Of("resharper_csharp_space_between_accessors_in_singleline_property");
    public static readonly OptionId SpaceInSinglelineMethod = Of("resharper_csharp_space_in_singleline_method");
    public static readonly OptionId SpaceInSinglelineAnonymousMethod = Of("resharper_csharp_space_in_singleline_anonymous_method");
    public static readonly OptionId SpaceWithinEmptyBraces = Of("resharper_csharp_space_within_empty_braces");
    public static readonly OptionId SpaceWithinSingleLineArrayInitializerBraces = Of("resharper_csharp_space_within_single_line_array_initializer_braces");
    public static readonly OptionId SpaceWithinSlicePattern = Of("resharper_csharp_space_within_slice_pattern");
    public static readonly OptionId SpaceWithinSpreadPattern = Of("resharper_space_within_spread_pattern");

    public static readonly OptionId SpaceBeforeTrailingComment = Of("resharper_csharp_space_before_trailing_comment");
    public static readonly OptionId SpaceBeforeTrailingCommentText = Of("resharper_space_before_trailing_comment_text");
    public static readonly OptionId SpaceAfterTripleSlash = Of("resharper_space_after_triple_slash");
    public static readonly OptionId StickComment = Of("resharper_csharp_stick_comment");
    public static readonly OptionId PlaceCommentsAtFirstColumn = Of("resharper_csharp_place_comments_at_first_column");

    public static readonly OptionId NewLineBeforeOpenBrace = Of("csharp_new_line_before_open_brace");
    public static readonly OptionId NewLineBeforeElse = Of("resharper_new_line_before_else");
    public static readonly OptionId NewLineBeforeCatch = Of("resharper_new_line_before_catch");
    public static readonly OptionId NewLineBeforeFinally = Of("resharper_new_line_before_finally");
    public static readonly OptionId NewLineBeforeWhile = Of("resharper_csharp_new_line_before_while");
    public static readonly OptionId SpecialElseIfTreatment = Of("resharper_csharp_special_else_if_treatment");
    public static readonly OptionId EmptyBlockStyle = Of("resharper_csharp_empty_block_style");
    public static readonly OptionId AllowCommentAfterLbrace = Of("resharper_csharp_allow_comment_after_lbrace");

    public static readonly OptionId IndentBraces = Of("csharp_indent_braces");
    public static readonly OptionId IndentSwitchLabels = Of("resharper_indent_switch_labels");
    public static readonly OptionId IndentBreakFromCase = Of("resharper_indent_break_from_case");
    public static readonly OptionId IndentInsideNamespace = Of("resharper_csharp_indent_inside_namespace");
    public static readonly OptionId IndentTypeConstraints = Of("resharper_csharp_indent_type_constraints");
    public static readonly OptionId IndentNestedForStmt = Of("resharper_csharp_indent_nested_for_stmt");
    public static readonly OptionId IndentNestedForeachStmt = Of("resharper_csharp_indent_nested_foreach_stmt");
    public static readonly OptionId IndentNestedWhileStmt = Of("resharper_csharp_indent_nested_while_stmt");
    public static readonly OptionId IndentNestedUsingsStmt = Of("resharper_csharp_indent_nested_usings_stmt");
    public static readonly OptionId IndentNestedLockStmt = Of("resharper_csharp_indent_nested_lock_stmt");
    public static readonly OptionId IndentNestedFixedStmt = Of("resharper_csharp_indent_nested_fixed_stmt");
    public static readonly OptionId UseContinuousIndentInsideParens = Of("resharper_csharp_use_continuous_indent_inside_parens");
    public static readonly OptionId UseContinuousIndentInsideInitializerBraces = Of("resharper_csharp_use_continuous_indent_inside_initializer_braces");
    public static readonly OptionId ContinuousIndentMultiplier = Of("resharper_csharp_continuous_indent_multiplier");
    public static readonly OptionId IndentPreprocessorIf = Of("resharper_csharp_indent_preprocessor_if");
    public static readonly OptionId IndentPreprocessorOther = Of("resharper_csharp_indent_preprocessor_other");
    public static readonly OptionId IndentPreprocessorRegion = Of("resharper_csharp_indent_preprocessor_region");
    public static readonly OptionId IndentAnonymousMethodBlock = OfInert("resharper_csharp_indent_anonymous_method_block");

    public static readonly OptionId KeepBlankLinesInCode = Of("resharper_csharp_keep_blank_lines_in_code");
    public static readonly OptionId KeepBlankLinesInDeclarations = Of("resharper_csharp_keep_blank_lines_in_declarations");
    public static readonly OptionId RemoveBlankLinesNearBracesInCode = Of("resharper_csharp_remove_blank_lines_near_braces_in_code");
    public static readonly OptionId RemoveBlankLinesNearBracesInDeclarations = Of("resharper_csharp_remove_blank_lines_near_braces_in_declarations");
    public static readonly OptionId BlankLinesAroundType = Of("resharper_csharp_blank_lines_around_type");
    public static readonly OptionId BlankLinesAroundSingleLineType = Of("resharper_csharp_blank_lines_around_single_line_type");
    public static readonly OptionId BlankLinesAroundInvocable = Of("resharper_csharp_blank_lines_around_invocable");
    public static readonly OptionId BlankLinesAroundSingleLineInvocable = Of("resharper_csharp_blank_lines_around_single_line_invocable");
    public static readonly OptionId BlankLinesAroundField = Of("resharper_csharp_blank_lines_around_field");
    public static readonly OptionId BlankLinesAroundSingleLineField = Of("resharper_csharp_blank_lines_around_single_line_field");
    public static readonly OptionId BlankLinesAroundProperty = Of("resharper_csharp_blank_lines_around_property");
    public static readonly OptionId BlankLinesAroundSingleLineProperty = Of("resharper_csharp_blank_lines_around_single_line_property");
    public static readonly OptionId BlankLinesAroundAutoProperty = Of("resharper_csharp_blank_lines_around_auto_property");
    public static readonly OptionId BlankLinesAroundSingleLineAutoProperty = Of("resharper_csharp_blank_lines_around_single_line_auto_property");
    public static readonly OptionId BlankLinesAroundAccessor = Of("resharper_csharp_blank_lines_around_accessor");
    public static readonly OptionId BlankLinesAroundSingleLineAccessor = Of("resharper_csharp_blank_lines_around_single_line_accessor");
    public static readonly OptionId BlankLinesAroundLocalMethod = Of("resharper_csharp_blank_lines_around_local_method");
    public static readonly OptionId BlankLinesAroundSingleLineLocalMethod = Of("resharper_csharp_blank_lines_around_single_line_local_method");
    public static readonly OptionId BlankLinesAroundNamespace = Of("resharper_csharp_blank_lines_around_namespace");
    public static readonly OptionId BlankLinesAroundRegion = Of("resharper_csharp_blank_lines_around_region");
    public static readonly OptionId BlankLinesInsideRegion = Of("resharper_csharp_blank_lines_inside_region");
    public static readonly OptionId BlankLinesInsideType = OfInert("resharper_csharp_blank_lines_inside_type");
    public static readonly OptionId BlankLinesInsideNamespace = OfInert("resharper_csharp_blank_lines_inside_namespace");
    public static readonly OptionId BlankLinesAfterUsingList = Of("resharper_csharp_blank_lines_after_using_list");
    public static readonly OptionId BlankLinesAfterFileScopedNamespaceDirective = Of("resharper_csharp_blank_lines_after_file_scoped_namespace_directive");
    public static readonly OptionId BlankLinesAfterBlockStatements = Of("resharper_csharp_blank_lines_after_block_statements");
    public static readonly OptionId BlankLinesBeforeSingleLineComment = Of("resharper_csharp_blank_lines_before_single_line_comment");
    public static readonly OptionId BlankLinesAfterCase = Of("resharper_csharp_blank_lines_after_case");
    public static readonly OptionId BlankLinesBeforeCase = Of("resharper_csharp_blank_lines_before_case");

    public static readonly OptionId FormatterTagsEnabled = Of("resharper_formatter_tags_enabled");
    public static readonly OptionId FormatterOffTag = Of("resharper_formatter_off_tag");
    public static readonly OptionId FormatterOnTag = Of("resharper_formatter_on_tag");
    public static readonly OptionId FormatterTagsAcceptRegexp = Of("resharper_formatter_tags_accept_regexp");

    /// <summary>Every id above that phase 1 can actually be observed to honour.</summary>
    public static ImmutableArray<OptionId> All { get; } = [.. Collected.Distinct().Except(Inert).Order()];

    /// <summary>
    /// ⚠ An option phase 1 reads but whose value it cannot yet make a difference to. No fitting
    /// pass means <c>max_line_length</c> changes nothing; no tabs in the output means
    /// <c>tab_width</c> changes nothing; the removal rules win over <c>blank_lines_inside_type</c>
    /// outright; <c>end_of_line</c> is inert while <c>enforce_line_ending_style</c> is false;
    /// <c>remove_spaces_on_blank_lines</c> is inert because the writer cannot produce trailing
    /// whitespace at all; and <c>space_between_keyword_and_type</c> is inert because a type after a
    /// keyword is always word-like, so the separation is mandatory whatever the option says.
    /// <para>
    /// They are read so the plumbing exists and so the crash snapshot can record them, and they
    /// stay Tier D, because Tier A is a claim about behaviour and not about wiring.
    /// </para>
    /// </summary>
    static OptionId OfInert(string key) {
        var id = Of(key);
        Inert.Add(id);
        return id;
    }

    static OptionId Of(string key) {
        if (!OptionRegistry.TryResolve(key, out var id)) {
            throw new InvalidOperationException(
                $"'{key}' is not in options.json. The formatter may not read an option the registry does not know: the tier report, `skala config explain` and the per-option corpus test all key off the registry.");
        }

        Collected.Add(id);
        return id;
    }
}
