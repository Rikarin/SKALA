using System.Collections.Immutable;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The option subset the formatter implements, read once per file into fields.
/// </summary>
/// <remarks>
///     ⚠ Every value here is read out of <see cref="FormattingOptions" /> by <see cref="OptionId" />,
///     which is an array index and not a dictionary lookup (docs/plan/13 § "The fitting pass"). The
///     façade exists for two more reasons: the generated accessor names follow whichever spelling the
///     export happened to use, which is not a name a rule should be written against; and
///     <see cref="Implemented" /> is then a single honest list of what phase 1 consumes, which is what
///     the Tier A promotion and the per-option corpus test are checked against.
/// </remarks>
public readonly struct PhaseOneOptions {
    public PhaseOneOptions(in FormattingOptions options) {
        // ── Layout ───────────────────────────────────────────────────────────────────────────
        IndentSize = Math.Max(1, options.GetInt(Ids.IndentSize));
        TabWidth = Math.Max(1, options.GetInt(Ids.TabWidth));
        UseTabs = options.GetRaw(Ids.IndentStyle) == (int)IndentStyle.Tab;
        // ⚠ `wrap_lines = false` is exactly an unbounded margin, and that is measured rather than
        // reasoned. Asked with `wrap_lines = false` and asked with
        // `max_line_length = 2147483647`, `jb cleanupcode` returns byte-identical output — on source
        // written flat and on source already wrapped, over two files covering twenty constructs. It
        // is not "do not wrap": a construct that breaks for a reason other than width still breaks,
        // and the same measurement shows it. A `chop_if_long` argument list whose source is
        // multiline stays chopped at both settings, a LINQ query keeps the author's breaks, and a
        // hard break is a hard break — all of which Fits already expresses, because a subtree
        // holding a break has an Unbounded flat width and never fits at any margin.
        WrapLines = options.GetBool(Ids.WrapLines);
        MaxLineLength = !WrapLines
            ? Document.Unbounded
            : options.GetInt(Ids.MaxLineLength) is var w and > 0
            ? w
            : 120;
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
        SpaceAroundDot = options.GetBool(Ids.SpaceAroundDot);
        SpaceAroundArrowOp = options.GetBool(Ids.SpaceAroundArrowOp);
        SpaceAfterUnaryOperator = options.GetBool(Ids.SpaceAfterUnaryOperator);
        SpaceAfterLogicalNotOp = options.GetBool(Ids.SpaceAfterLogicalNotOp);
        SpaceAfterUnaryMinusOp = options.GetBool(Ids.SpaceAfterUnaryMinusOp);
        SpaceAfterUnaryPlusOp = options.GetBool(Ids.SpaceAfterUnaryPlusOp);
        SpaceAfterAmpersandOp = options.GetBool(Ids.SpaceAfterAmpersandOp);
        SpaceAfterAsterikOp = options.GetBool(Ids.SpaceAfterAsterikOp);
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

        SpaceBeforeIfParentheses = options.GetBool(Ids.SpaceBeforeIfParentheses);
        SpaceBeforeWhileParentheses = options.GetBool(Ids.SpaceBeforeWhileParentheses);
        SpaceBeforeForParentheses = options.GetBool(Ids.SpaceBeforeForParentheses);
        SpaceBeforeForeachParentheses = options.GetBool(Ids.SpaceBeforeForeachParentheses);
        SpaceBeforeSwitchParentheses = options.GetBool(Ids.SpaceBeforeSwitchParentheses);
        SpaceBeforeCatchParentheses = options.GetBool(Ids.SpaceBeforeCatchParentheses);
        SpaceBeforeLockParentheses = options.GetBool(Ids.SpaceBeforeLockParentheses);
        SpaceBeforeUsingParentheses = options.GetBool(Ids.SpaceBeforeUsingParentheses);
        SpaceBeforeFixedParentheses = options.GetBool(Ids.SpaceBeforeFixedParentheses);

        SpaceWithinIfParentheses = options.GetBool(Ids.SpaceWithinIfParentheses);
        SpaceWithinWhileParentheses = options.GetBool(Ids.SpaceWithinWhileParentheses);
        SpaceWithinForParentheses = options.GetBool(Ids.SpaceWithinForParentheses);
        SpaceWithinForeachParentheses = options.GetBool(Ids.SpaceWithinForeachParentheses);
        SpaceWithinSwitchParentheses = options.GetBool(Ids.SpaceWithinSwitchParentheses);
        SpaceWithinCatchParentheses = options.GetBool(Ids.SpaceWithinCatchParentheses);
        SpaceWithinLockParentheses = options.GetBool(Ids.SpaceWithinLockParentheses);
        SpaceWithinUsingParentheses = options.GetBool(Ids.SpaceWithinUsingParentheses);
        SpaceWithinFixedParentheses = options.GetBool(Ids.SpaceWithinFixedParentheses);
        SpaceWithinCheckedParentheses = options.GetBool(Ids.SpaceWithinCheckedParentheses);
        SpaceWithinDefaultParentheses = options.GetBool(Ids.SpaceWithinDefaultParentheses);
        SpaceWithinNameofParentheses = options.GetBool(Ids.SpaceWithinNameofParentheses);
        SpaceWithinNewParentheses = options.GetBool(Ids.SpaceWithinNewParentheses);
        SpaceWithinSizeofParentheses = options.GetBool(Ids.SpaceWithinSizeofParentheses);
        SpaceWithinTypeofParentheses = options.GetBool(Ids.SpaceWithinTypeofParentheses);

        SpaceWithinMethodCallParentheses = options.GetBool(Ids.SpaceWithinMethodCallParentheses);
        SpaceWithinEmptyMethodCallParentheses = options.GetBool(Ids.SpaceWithinEmptyMethodCallParentheses);
        SpaceWithinMethodDeclarationParentheses = options.GetBool(Ids.SpaceWithinMethodDeclarationParentheses);

        SpaceWithinEmptyMethodDeclarationParentheses =
            options.GetBool(Ids.SpaceWithinEmptyMethodDeclarationParentheses);

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
        SpaceWithinArrayRankBrackets = options.GetBool(Ids.SpaceWithinArrayRankBrackets);
        SpaceWithinArrayRankEmptyBrackets = options.GetBool(Ids.SpaceWithinArrayRankEmptyBrackets);
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
        AlignMultilineStatementConditions = options.GetBool(Ids.AlignMultilineStatementConditions);
        AlignMultilineArrayAndObjectInitializer = options.GetBool(Ids.AlignMultilineArrayAndObjectInitializer);
        AlignMultilineListPattern = options.GetBool(Ids.AlignMultilineListPattern);
        AlignMultilinePropertyPattern = options.GetBool(Ids.AlignMultilinePropertyPattern);
        AlignMultilineSwitchExpression = options.GetBool(Ids.AlignMultilineSwitchExpression);
        AlignMultilineBinaryExpressionsChain = options.GetBool(Ids.AlignMultilineBinaryExpressionsChain);
        AlignMultilineBinaryPatterns = options.GetBool(Ids.AlignMultilineBinaryPatterns);
        AlignLinqQuery = options.GetBool(Ids.AlignLinqQuery);
        AlignMultilineExtendsList = options.GetBool(Ids.AlignMultilineExtendsList);
        AlignTupleComponents = options.GetBool(Ids.AlignTupleComponents);

        IntAlignFields = options.GetBool(Ids.IntAlignFields);
        IntAlignVariables = options.GetBool(Ids.IntAlignVariables);
        IntAlignAssignments = options.GetBool(Ids.IntAlignAssignments);
        IntAlignProperties = options.GetBool(Ids.IntAlignProperties);
        IntAlignMethods = options.GetBool(Ids.IntAlignMethods);
        IntAlignComments = options.GetBool(Ids.IntAlignComments);
        IntAlignSwitchExpressions = options.GetBool(Ids.IntAlignSwitchExpressions);
        IntAlignSwitchSections = options.GetBool(Ids.IntAlignSwitchSections);
        IntAlignParameters = options.GetBool(Ids.IntAlignParameters);
        IntAlignInvocations = options.GetBool(Ids.IntAlignInvocations);
        IntAlignNestedTernary = options.GetBool(Ids.IntAlignNestedTernary);
        IntAlignBinaryExpressions = options.GetBool(Ids.IntAlignBinaryExpressions);
        IntAlignPropertyPatterns = options.GetBool(Ids.IntAlignPropertyPatterns);
        DisableIntAlign = options.GetBool(Ids.DisableIntAlign);
        IntAlignFixInAdjacent = options.GetBool(Ids.IntAlignFixInAdjacent);
        AllowFarAlignment = options.GetBool(Ids.AllowFarAlignment);
        IntAlign = options.GetBool(Ids.IntAlign);
        IntAlignEq = options.GetBool(Ids.IntAlignEq);
        IntAlignDeclarationNames = options.GetBool(Ids.IntAlignDeclarationNames);
        IntAlignEnumInitializers = options.GetBool(Ids.IntAlignEnumInitializers);
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
        OutdentStatementLabels = options.GetBool(Ids.OutdentStatementLabels);

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
        BlankLinesAfterStartComment = options.GetInt(Ids.BlankLinesAfterStartComment);

        BlankLinesBeforeControlTransferStatements =
            options.GetInt(Ids.BlankLinesBeforeControlTransferStatements);

        BlankLinesAfterControlTransferStatements = options.GetInt(Ids.BlankLinesAfterControlTransferStatements);
        BlankLinesBeforeMultilineStatements = options.GetInt(Ids.BlankLinesBeforeMultilineStatements);
        BlankLinesAfterMultilineStatements = options.GetInt(Ids.BlankLinesAfterMultilineStatements);
        BlankLinesBeforeBlockStatements = options.GetInt(Ids.BlankLinesBeforeBlockStatements);
        BlankLinesAroundBlockCaseSection = options.GetInt(Ids.BlankLinesAroundBlockCaseSection);
        BlankLinesAroundMultilineCaseSection = options.GetInt(Ids.BlankLinesAroundMultilineCaseSection);

        // ── Break presence and position (phase 2) ────────────────────────────────────────────
        KeepUserLinebreaks = options.GetBool(Ids.KeepUserLinebreaks);
        KeepUserWrapping = options.GetBool(Ids.KeepUserWrapping);
        KeepExistingInvocationParensArrangement = options.GetBool(Ids.KeepExistingInvocationParensArrangement);
        KeepExistingDeclarationParensArrangement = options.GetBool(Ids.KeepExistingDeclarationParensArrangement);
        KeepExistingLambdaParensArrangement = options.GetBool(Ids.KeepExistingLambdaParensArrangement);
        KeepExistingPrimaryConstructorParensArrangement =
            options.GetBool(Ids.KeepExistingPrimaryConstructorParensArrangement);
        KeepExistingExprMemberArrangement = options.GetBool(Ids.KeepExistingExprMemberArrangement);
        KeepExistingEmbeddedArrangement = options.GetBool(Ids.KeepExistingEmbeddedArrangement);
        KeepExistingAttributeArrangement = options.GetBool(Ids.KeepExistingAttributeArrangement);
        KeepExistingEnumArrangement = options.GetBool(Ids.KeepExistingEnumArrangement);
        KeepExistingDeclarationBlockArrangement = options.GetBool(Ids.KeepExistingDeclarationBlockArrangement);
        KeepExistingEmbeddedBlockArrangement = options.GetBool(Ids.KeepExistingEmbeddedBlockArrangement);
        KeepExistingLinebreaks = options.GetBool(Ids.KeepExistingLinebreaks);

        WrapEnumDeclaration = (WrapStyle)options.GetRaw(Ids.WrapEnumDeclaration);
        MaxEnumMembersOnLine = Math.Max(1, options.GetInt(Ids.MaxEnumMembersOnLine));
        WrapSwitchExpression = (WrapStyle)options.GetRaw(Ids.WrapSwitchExpression);
        WrapArgumentsStyle = (WrapStyle)options.GetRaw(Ids.WrapArgumentsStyle);
        WrapParametersStyle = (WrapStyle)options.GetRaw(Ids.WrapParametersStyle);
        WrapPrimaryConstructorParametersStyle = (WrapStyle)options.GetRaw(Ids.WrapPrimaryConstructorParametersStyle);
        WrapAfterPrimaryConstructorLpar = options.GetBool(Ids.WrapAfterPrimaryConstructorLpar);
        WrapBeforePrimaryConstructorRpar = options.GetBool(Ids.WrapBeforePrimaryConstructorRpar);

        WrapBeforeBinaryOpsign = options.GetBool(Ids.WrapBeforeBinaryOpsign);
        WrapBeforeBinaryPatternOp = options.GetBool(Ids.WrapBeforeBinaryPatternOp);
        WrapBeforeTernaryOpsigns = options.GetBool(Ids.WrapBeforeTernaryOpsigns);
        WrapBeforeEq = options.GetBool(Ids.WrapBeforeEq);
        WrapBeforeComma = options.GetBool(Ids.WrapBeforeComma);
        WrapAfterInvocationLpar = options.GetBool(Ids.WrapAfterInvocationLpar);
        WrapBeforeInvocationRpar = options.GetBool(Ids.WrapBeforeInvocationRpar);
        WrapAfterDeclarationLpar = options.GetBool(Ids.WrapAfterDeclarationLpar);
        WrapBeforeDeclarationRpar = options.GetBool(Ids.WrapBeforeDeclarationRpar);
        WrapBeforeDeclarationLpar = options.GetBool(Ids.WrapBeforeDeclarationLpar);
        WrapBeforeInvocationLpar = options.GetBool(Ids.WrapBeforeInvocationLpar);
        WrapBeforePrimaryConstructorLpar = options.GetBool(Ids.WrapBeforePrimaryConstructorLpar);
        WrapBeforeTypeParameterLangle = options.GetBool(Ids.WrapBeforeTypeParameterLangle);
        WrapBeforeLinqExpression = options.GetBool(Ids.WrapBeforeLinqExpression);
        WrapBeforeArrowWithExpressions = options.GetBool(Ids.WrapBeforeArrowWithExpressions);

        PlaceAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceAttributeOnSameLine);
        PlaceTypeAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceTypeAttributeOnSameLine);
        PlaceMethodAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceMethodAttributeOnSameLine);
        PlaceFieldAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceFieldAttributeOnSameLine);
        PlaceAccessorAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceAccessorAttributeOnSameLine);
        PlaceAccessorHolderAttributeOnSameLine =
            (PlacementStyle)options.GetRaw(Ids.PlaceAccessorHolderAttributeOnSameLine);
        PlaceRecordFieldAttributeOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceRecordFieldAttributeOnSameLine);
        MaxAttributeLengthForSameLine = options.GetInt(Ids.MaxAttributeLengthForSameLine);

        PlaceSingleMethodArgumentLambdaOnSameLine = options.GetBool(Ids.PlaceSingleMethodArgumentLambdaOnSameLine);
        PlaceExprMethodOnSingleLine = (PlacementStyle)options.GetRaw(Ids.PlaceExprMethodOnSingleLine);
        PlaceExprPropertyOnSingleLine = (PlacementStyle)options.GetRaw(Ids.PlaceExprPropertyOnSingleLine);
        PlaceExprAccessorOnSingleLine = (PlacementStyle)options.GetRaw(Ids.PlaceExprAccessorOnSingleLine);
        PlaceSimpleEmbeddedStatementOnSameLine =
            (PlacementStyle)options.GetRaw(Ids.PlaceSimpleEmbeddedStatementOnSameLine);
        PlaceSimpleCaseStatementOnSameLine = (PlacementStyle)options.GetRaw(Ids.PlaceSimpleCaseStatementOnSameLine);
        PlaceTypeConstraintsOnSameLine = options.GetBool(Ids.PlaceTypeConstraintsOnSameLine);
        PlaceConstructorInitializerOnSameLine = options.GetBool(Ids.PlaceConstructorInitializerOnSameLine);
        PlacePrimaryConstructorInitializerOnSameLine =
            options.GetBool(Ids.PlacePrimaryConstructorInitializerOnSameLine);
        PlaceLinqIntoOnNewLine = options.GetBool(Ids.PlaceLinqIntoOnNewLine);
        NewLineBetweenQueryExpressionClauses = options.GetBool(Ids.NewLineBetweenQueryExpressionClauses);

        // ── Wrapping (phase 3) ───────────────────────────────────────────────────────────────
        WrapArrayInitializerStyle = (WrapStyle)options.GetRaw(Ids.WrapArrayInitializerStyle);
        MaxInitializerElementsOnLine = Math.Max(1, options.GetInt(Ids.MaxInitializerElementsOnLine));
        MaxArrayInitializerElementsOnLine = Math.Max(1, options.GetInt(Ids.MaxArrayInitializerElementsOnLine));
        PlaceSimpleInitializerOnSingleLine = options.GetBool(Ids.PlaceSimpleInitializerOnSingleLine);
        WrapAfterExpressionLbrace = options.GetBool(Ids.WrapAfterExpressionLbrace);
        WrapBeforeExpressionRbrace = options.GetBool(Ids.WrapBeforeExpressionRbrace);

        WrapChainedMethodCalls = (WrapStyle)options.GetRaw(Ids.WrapChainedMethodCalls);
        WrapAfterDotInMethodCalls = options.GetBool(Ids.WrapAfterDotInMethodCalls);
        WrapBeforeFirstMethodCall = options.GetBool(Ids.WrapBeforeFirstMethodCall);
        WrapAfterPropertyInChainedMethodCalls = options.GetBool(Ids.WrapAfterPropertyInChainedMethodCalls);

        WrapChainedBinaryExpressions = (WrapStyle)options.GetRaw(Ids.WrapChainedBinaryExpressions);
        WrapChainedBinaryPatterns = (WrapStyle)options.GetRaw(Ids.WrapChainedBinaryPatterns);
        WrapTernaryExprStyle = (WrapStyle)options.GetRaw(Ids.WrapTernaryExprStyle);
        WrapMultipleDeclarationStyle = (WrapStyle)options.GetRaw(Ids.WrapMultipleDeclarationStyle);
        WrapExtendsListStyle = (WrapStyle)options.GetRaw(Ids.WrapExtendsListStyle);
        WrapForStmtHeaderStyle = (WrapStyle)options.GetRaw(Ids.WrapForStmtHeaderStyle);
        WrapBeforeExtendsColon = options.GetBool(Ids.WrapBeforeExtendsColon);
        WrapBeforeCommaInBaseClause = options.GetBool(Ids.WrapBeforeCommaInBaseClause);
        WrapPropertyPattern = (WrapStyle)options.GetRaw(Ids.WrapPropertyPattern);
        WrapListPattern = (WrapStyle)options.GetRaw(Ids.WrapListPattern);

        KeepExistingListPatternsArrangement = options.GetBool(Ids.KeepExistingListPatternsArrangement);
        KeepExistingPropertyPatternsArrangement = options.GetBool(Ids.KeepExistingPropertyPatternsArrangement);
        KeepExistingSwitchExpressionArrangement = options.GetBool(Ids.KeepExistingSwitchExpressionArrangement);
        PlaceSimpleListPatternOnSingleLine = options.GetBool(Ids.PlaceSimpleListPatternOnSingleLine);
        PlaceSimplePropertyPatternOnSingleLine = options.GetBool(Ids.PlaceSimplePropertyPatternOnSingleLine);
        PlaceSimpleSwitchExpressionOnSingleLine = options.GetBool(Ids.PlaceSimpleSwitchExpressionOnSingleLine);

        MaxInvocationArgumentsOnLine = Math.Max(1, options.GetInt(Ids.MaxInvocationArgumentsOnLine));
        MaxFormalParametersOnLine = Math.Max(1, options.GetInt(Ids.MaxFormalParametersOnLine));
        MaxPrimaryConstructorParametersOnLine = Math.Max(1, options.GetInt(Ids.MaxPrimaryConstructorParametersOnLine));
        PreferWrapAroundEq = options.GetString(Ids.PreferWrapAroundEq) ?? "default";

        // ── Escape hatch ─────────────────────────────────────────────────────────────────────
        IndentRawLiteralString = (RawStringIndentStyle)options.GetRaw(Ids.IndentRawLiteralString);
        FormatterTagsEnabled = options.GetBool(Ids.FormatterTagsEnabled);
        FormatterOffTag = options.GetString(Ids.FormatterOffTag) ?? "@formatter:off";
        FormatterOnTag = options.GetString(Ids.FormatterOnTag) ?? "@formatter:on";
        FormatterTagsAcceptRegexp = options.GetBool(Ids.FormatterTagsAcceptRegexp);

        // ── Documentation comments ───────────────────────────────────────────────────────────
        // ⚠ Read here, and therefore read on every path, because the sub-formatter is on by
        // default. It used to be built only where a caller passed `--xmldoc`, which meant a caller
        // that held a `PhaseOneOptions` and nothing else could not turn it on at all.
        XmlDoc = new XmlDocOptions(options);
    }

    public int IndentSize { get; }
    public int TabWidth { get; }
    public bool UseTabs { get; }

    /// <summary>
    ///     The column limit, or <see cref="Document.Unbounded" /> when <see cref="WrapLines" /> is off.
    /// </summary>
    /// <remarks>
    ///     ⚠ One number and not two, because the oracle answers them with one. Everything that reads a
    ///     margin therefore honours <c>wrap_lines</c> without knowing about it: the fitter, the fill
    ///     points in the layout writer, the single-line tests the blank-line rules run, and SK0002 —
    ///     which is right to go quiet, since with wrapping off an over-long line is what was asked for
    ///     rather than a line nothing could break.
    /// </remarks>
    public int MaxLineLength { get; }

    /// <summary>
    ///     <c>resharper_csharp_wrap_lines</c>: whether the formatter may break a line for width at all.
    /// </summary>
    public bool WrapLines { get; }

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

    /// <summary>
    ///     <c>space_around_dot</c>: the gap beside a <c>.</c> or a <c>?.</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read out of the specific key rather than out of the generalized
    ///     <c>space_around_member_access_operator</c> that used to supply it. The two agree in this
    ///     export, and the generalized one is still honoured — through
    ///     <see cref="Rikarin.Skala.Options.OptionInfo.Expands" />, applied by the resolver — but a
    ///     configuration that sets only <c>space_around_dot</c> is one the oracle answers and Skala
    ///     used to ignore.
    /// </remarks>
    public bool SpaceAroundDot { get; }

    /// <summary><c>space_around_arrow_op</c>: the gap beside a pointer member access <c>-&gt;</c>.</summary>
    public bool SpaceAroundArrowOp { get; }

    public bool SpaceAfterUnaryOperator { get; }

    /// <summary>
    ///     The five per-operator keys <c>space_after_unary_operator</c> generalizes.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read as well as the generalized key, not instead of it. <c>~</c> and the prefix
    ///     <c>++</c>/<c>--</c> have no key of their own, so they keep reading the generalized one; see
    ///     <c>SpaceRules.AfterPrefixOperator</c>.
    /// </remarks>
    public bool SpaceAfterLogicalNotOp { get; }

    public bool SpaceAfterUnaryMinusOp { get; }
    public bool SpaceAfterUnaryPlusOp { get; }
    public bool SpaceAfterAmpersandOp { get; }
    public bool SpaceAfterAsterikOp { get; }
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

    /// <summary>
    ///     The nine <c>space_before_&lt;keyword&gt;_parentheses</c> keys, one per control-flow keyword.
    /// </summary>
    /// <remarks>
    ///     ⚠ One key per keyword rather than the single generalized
    ///     <c>space_after_keywords_in_control_flow_statements</c> the export writes. The oracle answers
    ///     each of the nine separately — <c>space_before_if_parentheses = false</c> alone produces
    ///     <c>if(n &gt; 0)</c> and leaves every other keyword's space — so a rule written against the
    ///     generalized key silently ignores eight of the nine. The generalized key still reaches these
    ///     fields, through the resolver's expansion of
    ///     <see cref="Rikarin.Skala.Options.OptionInfo.Expands" />.
    /// </remarks>
    public bool SpaceBeforeIfParentheses { get; }

    public bool SpaceBeforeWhileParentheses { get; }
    public bool SpaceBeforeForParentheses { get; }
    public bool SpaceBeforeForeachParentheses { get; }
    public bool SpaceBeforeSwitchParentheses { get; }
    public bool SpaceBeforeCatchParentheses { get; }
    public bool SpaceBeforeLockParentheses { get; }
    public bool SpaceBeforeUsingParentheses { get; }
    public bool SpaceBeforeFixedParentheses { get; }

    /// <summary>
    ///     The <c>space_within_&lt;construct&gt;_parentheses</c> keys: the gap just inside a
    ///     parenthesis, by what the parenthesis belongs to.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="SpaceWithinParentheses" /> used to answer all of them, which made every one of
    ///     these fifteen keys inert. Each is observable on its own against the oracle:
    ///     <c>space_within_if_parentheses = true</c> produces <c>if ( n &gt; 0 )</c> and touches
    ///     nothing else.
    /// </remarks>
    public bool SpaceWithinIfParentheses { get; }

    public bool SpaceWithinWhileParentheses { get; }
    public bool SpaceWithinForParentheses { get; }
    public bool SpaceWithinForeachParentheses { get; }
    public bool SpaceWithinSwitchParentheses { get; }
    public bool SpaceWithinCatchParentheses { get; }
    public bool SpaceWithinLockParentheses { get; }
    public bool SpaceWithinUsingParentheses { get; }
    public bool SpaceWithinFixedParentheses { get; }
    public bool SpaceWithinCheckedParentheses { get; }
    public bool SpaceWithinDefaultParentheses { get; }
    public bool SpaceWithinNameofParentheses { get; }

    /// <summary>
    ///     ⚠ Read and never consulted. <c>space_within_new_parentheses</c> names the gap inside
    ///     <c>new T(…)</c>'s parentheses, and the oracle does not answer it at either value: asked with
    ///     <c>new List&lt;int&gt;(4)</c> and with <c>new object()</c>, the argument list comes back
    ///     governed by <c>space_between_method_call_parameter_list_parentheses</c> instead. It stays
    ///     Tier D with that measurement rather than being wired to a gap it does not own.
    /// </summary>
    public bool SpaceWithinNewParentheses { get; }

    public bool SpaceWithinSizeofParentheses { get; }
    public bool SpaceWithinTypeofParentheses { get; }

    public bool SpaceWithinMethodCallParentheses { get; }
    public bool SpaceWithinEmptyMethodCallParentheses { get; }
    public bool SpaceWithinMethodDeclarationParentheses { get; }
    public bool SpaceWithinEmptyMethodDeclarationParentheses { get; }

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

    /// <summary>
    ///     <c>space_within_array_rank_brackets</c>: <c>new int[ 2, 3 ]</c> and <c>int[ , ]</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ A rank specifier that is nothing but <c>[]</c> is <see cref="SpaceWithinArrayRankEmptyBrackets" />'s
    ///     instead, and <c>[,]</c> is not: the oracle answers <c>new[]</c> out of the empty key and
    ///     <c>int[,]</c> out of this one, so the line is one omitted size rather than "no sizes".
    /// </remarks>
    public bool SpaceWithinArrayRankBrackets { get; }

    public bool SpaceWithinArrayRankEmptyBrackets { get; }
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

    /// <summary>
    ///     <c>align_multiline_statement_conditions</c>: a condition broken across lines is laid out from
    ///     the column just after the statement's <c>(</c> rather than from an indent level.
    /// </summary>
    public bool AlignMultilineStatementConditions { get; }

    public bool AlignMultilineArrayAndObjectInitializer { get; }
    public bool AlignMultilineListPattern { get; }
    public bool AlignMultilinePropertyPattern { get; }
    public bool AlignMultilineSwitchExpression { get; }
    public bool AlignMultilineBinaryExpressionsChain { get; }
    public bool AlignMultilineBinaryPatterns { get; }
    public bool AlignLinqQuery { get; }
    public bool AlignMultilineExtendsList { get; }
    public bool AlignTupleComponents { get; }

    public bool IntAlignFields { get; }
    public bool IntAlignVariables { get; }
    public bool IntAlignAssignments { get; }
    public bool IntAlignProperties { get; }
    public bool IntAlignMethods { get; }
    public bool IntAlignComments { get; }
    public bool IntAlignSwitchExpressions { get; }
    public bool IntAlignSwitchSections { get; }
    public bool IntAlignParameters { get; }
    public bool IntAlignInvocations { get; }
    public bool IntAlignNestedTernary { get; }
    public bool IntAlignBinaryExpressions { get; }
    public bool IntAlignPropertyPatterns { get; }
    public bool DisableIntAlign { get; }
    public bool IntAlignFixInAdjacent { get; }
    public bool AllowFarAlignment { get; }
    public bool IntAlign { get; }
    public bool IntAlignEq { get; }
    public bool IntAlignDeclarationNames { get; }
    public bool IntAlignEnumInitializers { get; }

    /// <summary>
    ///     Whether any construct is column-aligned at all, and therefore whether <see cref="IntAlign" />
    ///     has to parse the output.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>disable_int_align</c> is honoured here and only here: it is the family's master switch
    ///     and it wins over every member, which the oracle confirms — with <c>int_align = true</c> and
    ///     <c>disable_int_align = true</c> together, the output is the unaligned one.
    /// </remarks>
    public bool IntAlignAnything =>
        !DisableIntAlign
        && (IntAlignFields
            || IntAlignVariables
            || IntAlignAssignments
            || IntAlignProperties
            || IntAlignMethods
            || IntAlignComments
            || IntAlignSwitchExpressions
            || IntAlignSwitchSections
            || IntAlignParameters
            || IntAlignInvocations
            || IntAlignNestedTernary
            || IntAlignBinaryExpressions
            || IntAlignPropertyPatterns);

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

    /// <summary><c>outdent_statement_labels</c>: <c>Finish:</c> one level out from what it labels.</summary>
    public bool OutdentStatementLabels { get; }

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

    /// <summary>The gap under the comment block a file opens with.</summary>
    /// <remarks>
    ///     ⚠ Not "any comment at the top of a member". Measured: a <c>///</c> run at position 0 is not a
    ///     start comment — it belongs to the type below it — and neither is a <c>//</c> that follows a
    ///     <c>#nullable</c>, because the directive has already started the file. Two <c>//</c> blocks
    ///     separated by a blank line are <em>one</em> start comment and the gap is the one under the
    ///     second.
    /// </remarks>
    public int BlankLinesAfterStartComment { get; }

    /// <summary>The six statement-level blank-line requirements.</summary>
    /// <remarks>
    ///     ⚠ Every boundary here was measured rather than read off the option name; see
    ///     <c>CSharpDocumentBuilder.BlankLines</c>, where each rule carries the shape that establishes it.
    /// </remarks>
    public int BlankLinesBeforeControlTransferStatements { get; }

    public int BlankLinesAfterControlTransferStatements { get; }
    public int BlankLinesBeforeMultilineStatements { get; }
    public int BlankLinesAfterMultilineStatements { get; }
    public int BlankLinesBeforeBlockStatements { get; }
    public int BlankLinesAroundBlockCaseSection { get; }
    public int BlankLinesAroundMultilineCaseSection { get; }

    public bool KeepUserLinebreaks { get; }
    public bool KeepUserWrapping { get; }
    public bool KeepExistingInvocationParensArrangement { get; }
    public bool KeepExistingDeclarationParensArrangement { get; }
    public bool KeepExistingLambdaParensArrangement { get; }
    public bool KeepExistingPrimaryConstructorParensArrangement { get; }
    public bool KeepExistingExprMemberArrangement { get; }
    public bool KeepExistingEmbeddedArrangement { get; }
    public bool KeepExistingAttributeArrangement { get; }
    public bool KeepExistingEnumArrangement { get; }
    public bool KeepExistingDeclarationBlockArrangement { get; }
    public bool KeepExistingEmbeddedBlockArrangement { get; }
    public bool KeepExistingLinebreaks { get; }

    /// <summary>
    ///     Whether a break the author put <em>between two items of a list</em> survives.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not the same question as whether a break next to the list's delimiters survives — that one
    ///     is the construct's own <c>keep_existing_*_arrangement</c>, gated by this. The four corners of
    ///     docs/plan/05's table are pinned by <c>constructs/preservation/*</c> under all four
    ///     configurations, and the corner people get wrong is
    ///     (<c>keep_user_linebreaks = true</c>, <c>keep_existing_X = false</c>): <c>Foo(\n a)</c> re-joins
    ///     there and <c>Foo(\n a,\n b)</c> does not.
    /// </remarks>
    public bool KeepsUserBreaksBetweenItems => KeepUserLinebreaks && KeepExistingLinebreaks;

    public WrapStyle WrapEnumDeclaration { get; }
    public int MaxEnumMembersOnLine { get; }
    public WrapStyle WrapSwitchExpression { get; }
    public WrapStyle WrapArgumentsStyle { get; }
    public WrapStyle WrapParametersStyle { get; }
    public WrapStyle WrapPrimaryConstructorParametersStyle { get; }
    public bool WrapAfterPrimaryConstructorLpar { get; }
    public bool WrapBeforePrimaryConstructorRpar { get; }

    public bool WrapBeforeBinaryOpsign { get; }
    public bool WrapBeforeBinaryPatternOp { get; }
    public bool WrapBeforeTernaryOpsigns { get; }
    public bool WrapBeforeEq { get; }
    public bool WrapBeforeComma { get; }
    public bool WrapAfterInvocationLpar { get; }
    public bool WrapBeforeInvocationRpar { get; }
    public bool WrapAfterDeclarationLpar { get; }
    public bool WrapBeforeDeclarationRpar { get; }
    public bool WrapBeforeDeclarationLpar { get; }
    public bool WrapBeforeInvocationLpar { get; }
    public bool WrapBeforePrimaryConstructorLpar { get; }
    public bool WrapBeforeTypeParameterLangle { get; }
    public bool WrapBeforeLinqExpression { get; }
    public bool WrapBeforeArrowWithExpressions { get; }

    public PlacementStyle PlaceAttributeOnSameLine { get; }
    public PlacementStyle PlaceTypeAttributeOnSameLine { get; }
    public PlacementStyle PlaceMethodAttributeOnSameLine { get; }
    public PlacementStyle PlaceFieldAttributeOnSameLine { get; }
    public PlacementStyle PlaceAccessorAttributeOnSameLine { get; }
    public PlacementStyle PlaceAccessorHolderAttributeOnSameLine { get; }
    public PlacementStyle PlaceRecordFieldAttributeOnSameLine { get; }
    public int MaxAttributeLengthForSameLine { get; }

    public bool PlaceSingleMethodArgumentLambdaOnSameLine { get; }
    public PlacementStyle PlaceExprMethodOnSingleLine { get; }
    public PlacementStyle PlaceExprPropertyOnSingleLine { get; }
    public PlacementStyle PlaceExprAccessorOnSingleLine { get; }
    public PlacementStyle PlaceSimpleEmbeddedStatementOnSameLine { get; }
    public PlacementStyle PlaceSimpleCaseStatementOnSameLine { get; }
    public bool PlaceTypeConstraintsOnSameLine { get; }
    public bool PlaceConstructorInitializerOnSameLine { get; }
    public bool PlacePrimaryConstructorInitializerOnSameLine { get; }
    public bool PlaceLinqIntoOnNewLine { get; }
    public bool NewLineBetweenQueryExpressionClauses { get; }

    public WrapStyle WrapArrayInitializerStyle { get; }
    public int MaxInitializerElementsOnLine { get; }
    public int MaxArrayInitializerElementsOnLine { get; }
    public bool PlaceSimpleInitializerOnSingleLine { get; }
    public bool WrapAfterExpressionLbrace { get; }
    public bool WrapBeforeExpressionRbrace { get; }

    public WrapStyle WrapChainedMethodCalls { get; }
    public bool WrapAfterDotInMethodCalls { get; }
    public bool WrapBeforeFirstMethodCall { get; }
    public bool WrapAfterPropertyInChainedMethodCalls { get; }

    public WrapStyle WrapChainedBinaryExpressions { get; }
    public WrapStyle WrapChainedBinaryPatterns { get; }
    public WrapStyle WrapTernaryExprStyle { get; }
    public WrapStyle WrapMultipleDeclarationStyle { get; }
    public WrapStyle WrapExtendsListStyle { get; }

    /// <summary>
    ///     <c>wrap_for_stmt_header_style</c>: what happens to a <c>for</c> header that does not fit.
    /// </summary>
    /// <remarks>
    ///     ⚠ The break is <em>after</em> each <c>;</c>, and the two styles differ on which of them break.
    ///     Measured at the export's 120-column margin on one header, one key flipped:
    ///     <code>
    /// chop_if_long                              wrap_if_long
    /// for (var i = 0;                           for (var i = 0; i &lt; xs.Count;
    ///      i &lt; xs.Count;                             i += 1) {
    ///      i += 1) {
    ///     </code>
    ///     So <c>chop_if_long</c> gives every clause a line and <c>wrap_if_long</c> is a fill, exactly as
    ///     the same two values mean for a delimited list.
    /// </remarks>
    public WrapStyle WrapForStmtHeaderStyle { get; }
    public bool WrapBeforeExtendsColon { get; }
    public bool WrapBeforeCommaInBaseClause { get; }
    public WrapStyle WrapPropertyPattern { get; }
    public WrapStyle WrapListPattern { get; }

    public bool KeepExistingListPatternsArrangement { get; }
    public bool KeepExistingPropertyPatternsArrangement { get; }
    public bool KeepExistingSwitchExpressionArrangement { get; }
    public bool PlaceSimpleListPatternOnSingleLine { get; }
    public bool PlaceSimplePropertyPatternOnSingleLine { get; }
    public bool PlaceSimpleSwitchExpressionOnSingleLine { get; }

    public int MaxInvocationArgumentsOnLine { get; }
    public int MaxFormalParametersOnLine { get; }
    public int MaxPrimaryConstructorParametersOnLine { get; }
    public string PreferWrapAroundEq { get; }

    public RawStringIndentStyle IndentRawLiteralString { get; }
    public bool FormatterTagsEnabled { get; }
    public string FormatterOffTag { get; }
    public string FormatterOnTag { get; }
    public bool FormatterTagsAcceptRegexp { get; }

    /// <summary>
    ///     The four keys as <see cref="FormatterTagGuard" /> wants them — for the passes that run
    ///     <em>outside</em> the document builder and would otherwise not see a tag at all.
    /// </summary>
    public FormatterTags Tags => new(FormatterTagsEnabled, FormatterOffTag, FormatterOnTag, FormatterTagsAcceptRegexp);

    /// <summary>
    ///     The <c>resharper_xmldoc_*</c> subset, resolved from the same configuration.
    /// </summary>
    /// <remarks>
    ///     ⚠ It is not part of <see cref="Implemented" /> and never can be. See
    ///     <see cref="XmlDocOptions" />: these keys govern real output and no oracle fixture can pin
    ///     them, which is a combination the tier table has no row for.
    /// </remarks>
    public XmlDocOptions XmlDoc { get; }

    /// <summary>
    ///     Every option milestone 1 reads, in registry order. The Tier A promotion and the per-option
    ///     corpus test are checked against this list, so an option that stops being read here stops
    ///     claiming to be implemented.
    /// </summary>
    public static ImmutableArray<OptionId> Implemented => Ids.All;
}

/// <summary>
///     The option ids phase 1 reads, resolved once through the registry.
/// </summary>
/// <remarks>
///     Written as <c>.editorconfig</c> spellings rather than as generated accessor names, because the
///     spelling is the thing the plan, the export and the ReSharper documentation all agree on, while
///     the accessor name depends on which spelling the importer happened to pick as canonical.
/// </remarks>
public static class Ids {
    static readonly List<OptionId> Collected = [];

    /// <summary>Ids <see cref="OfInert" /> marked; read but excluded from <see cref="All" />.</summary>
    static readonly List<OptionId> Inert = [];

    /// <summary>Ids <see cref="OfUnoracled" /> marked; observable, and excluded from <see cref="All" />.</summary>
    static readonly List<OptionId> Unoracled = [];

    public static readonly OptionId IndentSize = Of("resharper_csharp_indent_size");
    public static readonly OptionId TabWidth = OfInert("resharper_csharp_tab_width");
    public static readonly OptionId IndentStyle = Of("resharper_csharp_indent_style");
    // ⚠ No longer inert. Milestone 1 read it and could not act on it — nothing wrapped — and it was
    // Tier D for that reason (docs/plan/05 § "Phase 1"). Milestone 3 is the phase where the column
    // limit is the whole point, and constructs/wrapping/initializers.cs pins it.
    public static readonly OptionId MaxLineLength = Of("resharper_csharp_max_line_length");

    // ⚠ Beside MaxLineLength because it *is* MaxLineLength: at `false` the margin is unbounded, and
    // `jb cleanupcode` produces byte-identical output for `wrap_lines = false` and
    // `max_line_length = 2147483647` on every input tried. Recorded below as a "master switch with
    // nothing behind it" — "csharp_wrap_lines = false leaves an over-long line wrapped exactly as
    // before" — which was measured on input that was already wrapped, where most of what stays put
    // stays put under `keep_user_linebreaks` rather than under this key.
    public static readonly OptionId WrapLines = Of("resharper_csharp_wrap_lines");
    public static readonly OptionId InsertFinalNewline = Of("resharper_csharp_insert_final_newline");
    public static readonly OptionId RemoveSpacesOnBlankLines = OfInert("resharper_remove_spaces_on_blank_lines");
    public static readonly OptionId EnforceLineEndingStyle = Of("resharper_enforce_line_ending_style");
    public static readonly OptionId EndOfLine = OfInert("end_of_line");

    public static readonly OptionId SpaceAfterComma = Of("resharper_space_after_comma");
    public static readonly OptionId SpaceBeforeComma = Of("resharper_space_before_comma");
    public static readonly OptionId SpaceBeforeSemicolon = Of("resharper_csharp_space_before_semicolon");

    public static readonly OptionId SpaceAfterSemicolonInForStatement =
        Of("resharper_space_after_semicolon_in_for_statement");

    public static readonly OptionId SpaceBeforeSemicolonInForStatement =
        Of("resharper_space_before_semicolon_in_for_statement");

    public static readonly OptionId SpaceAfterCast = Of("resharper_space_after_cast");

    public static readonly OptionId SpaceAroundDot = Of("resharper_csharp_space_around_dot");
    public static readonly OptionId SpaceAroundArrowOp = Of("resharper_csharp_space_around_arrow_op");

    public static readonly OptionId SpaceAfterUnaryOperator = Of("resharper_csharp_space_after_unary_operator");

    // ⚠ The five keys `space_after_unary_operator` names, read on their own because the oracle
    // answers them on their own: `space_after_logical_not_op = true` alone writes `! b` and leaves
    // `-a`, `+a`, `&a` and `*p` where they were. Declared after the generalized key they belong to
    // so that `OfGeneralized`'s "at least one target is implemented" check can see them.
    public static readonly OptionId SpaceAfterLogicalNotOp = Of("resharper_csharp_space_after_logical_not_op");
    public static readonly OptionId SpaceAfterUnaryMinusOp = Of("resharper_csharp_space_after_unary_minus_op");
    public static readonly OptionId SpaceAfterUnaryPlusOp = Of("resharper_csharp_space_after_unary_plus_op");
    public static readonly OptionId SpaceAfterAmpersandOp = Of("resharper_csharp_space_after_ampersand_op");
    public static readonly OptionId SpaceAfterAsterikOp = Of("resharper_csharp_space_after_asterik_op");

    public static readonly OptionId SpaceNearPostfixAndPrefixOp =
        Of("resharper_csharp_space_near_postfix_and_prefix_op");

    public static readonly OptionId SpaceAroundAssignmentOp = Of("resharper_csharp_space_around_assignment_op");
    public static readonly OptionId SpaceAroundLambdaArrow = Of("resharper_csharp_space_around_lambda_arrow");
    public static readonly OptionId SpaceAroundAdditiveOp = Of("resharper_csharp_space_around_additive_op");
    public static readonly OptionId SpaceAroundRelationalOp = Of("resharper_csharp_space_around_relational_op");
    public static readonly OptionId SpaceAroundShiftOp = Of("resharper_csharp_space_around_shift_op");
    public static readonly OptionId SpaceAroundAliasEq = Of("resharper_csharp_space_around_alias_eq");
    public static readonly OptionId SpaceAfterOperatorKeyword = Of("resharper_csharp_space_after_operator_keyword");

    public static readonly OptionId SpaceBetweenKeywordAndExpression =
        Of("resharper_csharp_space_between_keyword_and_expression");

    public static readonly OptionId SpaceBetweenKeywordAndType =
        OfInert("resharper_csharp_space_between_keyword_and_type");

    public static readonly OptionId SpaceBeforeIfParentheses = Of("resharper_csharp_space_before_if_parentheses");

    public static readonly OptionId SpaceBeforeWhileParentheses =
        Of("resharper_csharp_space_before_while_parentheses");

    public static readonly OptionId SpaceBeforeForParentheses = Of("resharper_csharp_space_before_for_parentheses");

    public static readonly OptionId SpaceBeforeForeachParentheses =
        Of("resharper_csharp_space_before_foreach_parentheses");

    public static readonly OptionId SpaceBeforeSwitchParentheses =
        Of("resharper_csharp_space_before_switch_parentheses");

    public static readonly OptionId SpaceBeforeCatchParentheses =
        Of("resharper_csharp_space_before_catch_parentheses");

    public static readonly OptionId SpaceBeforeLockParentheses = Of("resharper_csharp_space_before_lock_parentheses");

    public static readonly OptionId SpaceBeforeUsingParentheses =
        Of("resharper_csharp_space_before_using_parentheses");

    public static readonly OptionId SpaceBeforeFixedParentheses =
        Of("resharper_csharp_space_before_fixed_parentheses");

    public static readonly OptionId SpaceWithinIfParentheses = Of("resharper_csharp_space_within_if_parentheses");

    public static readonly OptionId SpaceWithinWhileParentheses =
        Of("resharper_csharp_space_within_while_parentheses");

    public static readonly OptionId SpaceWithinForParentheses = Of("resharper_csharp_space_within_for_parentheses");

    public static readonly OptionId SpaceWithinForeachParentheses =
        Of("resharper_csharp_space_within_foreach_parentheses");

    public static readonly OptionId SpaceWithinSwitchParentheses =
        Of("resharper_csharp_space_within_switch_parentheses");

    public static readonly OptionId SpaceWithinCatchParentheses =
        Of("resharper_csharp_space_within_catch_parentheses");

    public static readonly OptionId SpaceWithinLockParentheses = Of("resharper_csharp_space_within_lock_parentheses");

    public static readonly OptionId SpaceWithinUsingParentheses =
        Of("resharper_csharp_space_within_using_parentheses");

    public static readonly OptionId SpaceWithinFixedParentheses =
        Of("resharper_csharp_space_within_fixed_parentheses");

    public static readonly OptionId SpaceWithinCheckedParentheses =
        Of("resharper_csharp_space_within_checked_parentheses");

    public static readonly OptionId SpaceWithinDefaultParentheses =
        Of("resharper_csharp_space_within_default_parentheses");

    public static readonly OptionId SpaceWithinNameofParentheses =
        Of("resharper_csharp_space_within_nameof_parentheses");

    // ⚠ Inert, and measured rather than assumed. Asked at both values with `new List<int>(4)` and
    // with `new object()`, the oracle returns the argument list exactly as
    // `space_between_method_call_parameter_list_parentheses` and its empty twin decide; nothing
    // distinguishes the two values of this key. `space_before_new_parentheses` — the gap in *front*
    // of the parenthesis — is the one `new` really does own, and it stays Tier A.
    public static readonly OptionId SpaceWithinNewParentheses =
        OfInert("resharper_csharp_space_within_new_parentheses");

    public static readonly OptionId SpaceWithinSizeofParentheses =
        Of("resharper_csharp_space_within_sizeof_parentheses");

    public static readonly OptionId SpaceWithinTypeofParentheses =
        Of("resharper_csharp_space_within_typeof_parentheses");

    public static readonly OptionId SpaceWithinMethodCallParentheses =
        Of("resharper_space_between_method_call_parameter_list_parentheses");

    public static readonly OptionId SpaceWithinEmptyMethodCallParentheses =
        Of("resharper_space_between_method_call_empty_parameter_list_parentheses");

    public static readonly OptionId SpaceWithinMethodDeclarationParentheses =
        Of("resharper_space_between_method_declaration_parameter_list_parentheses");

    public static readonly OptionId SpaceWithinEmptyMethodDeclarationParentheses =
        Of("resharper_space_between_method_declaration_empty_parameter_list_parentheses");

    public static readonly OptionId SpaceBeforeMethodParentheses =
        Of("resharper_csharp_space_before_method_parentheses");

    public static readonly OptionId SpaceBeforeMethodCallParentheses =
        Of("resharper_csharp_space_before_method_call_parentheses");

    public static readonly OptionId SpaceBeforeEmptyMethodParentheses =
        Of("resharper_csharp_space_before_empty_method_parentheses");

    public static readonly OptionId SpaceBeforeEmptyMethodCallParentheses =
        Of("resharper_csharp_space_before_empty_method_call_parentheses");

    public static readonly OptionId SpaceBeforeNewParentheses = Of("resharper_csharp_space_before_new_parentheses");

    public static readonly OptionId SpaceBeforeTypeofParentheses =
        Of("resharper_csharp_space_before_typeof_parentheses");

    public static readonly OptionId SpaceBeforeSizeofParentheses =
        Of("resharper_csharp_space_before_sizeof_parentheses");

    public static readonly OptionId SpaceBeforeDefaultParentheses =
        Of("resharper_csharp_space_before_default_parentheses");

    public static readonly OptionId SpaceBeforeCheckedParentheses =
        Of("resharper_csharp_space_before_checked_parentheses");

    public static readonly OptionId SpaceBeforeNameofParentheses =
        Of("resharper_csharp_space_before_nameof_parentheses");

    public static readonly OptionId SpaceWithinParentheses = Of("resharper_csharp_space_within_parentheses");

    public static readonly OptionId SpaceBetweenTypecastParentheses =
        Of("resharper_csharp_space_between_typecast_parentheses");

    public static readonly OptionId SpaceBeforeArrayAccessBrackets =
        Of("resharper_csharp_space_before_array_access_brackets");

    public static readonly OptionId SpaceBeforeArrayRankBrackets =
        Of("resharper_csharp_space_before_array_rank_brackets");

    public static readonly OptionId SpaceWithinArrayAccessBrackets =
        Of("resharper_csharp_space_within_array_access_brackets");

    public static readonly OptionId SpaceWithinArrayRankBrackets =
        Of("resharper_csharp_space_within_array_rank_brackets");

    public static readonly OptionId SpaceWithinArrayRankEmptyBrackets =
        Of("resharper_csharp_space_within_array_rank_empty_brackets");

    public static readonly OptionId SpaceWithinAttributeBrackets =
        Of("resharper_csharp_space_within_attribute_brackets");

    public static readonly OptionId SpaceWithinListPatternBrackets =
        Of("resharper_csharp_space_within_list_pattern_brackets");

    public static readonly OptionId SpaceBeforeTypeArgumentAngle =
        Of("resharper_csharp_space_before_type_argument_angle");

    public static readonly OptionId SpaceBeforeTypeParameterAngle =
        Of("resharper_csharp_space_before_type_parameter_angle");

    public static readonly OptionId SpaceWithinTypeArgumentAngles =
        Of("resharper_csharp_space_within_type_argument_angles");

    public static readonly OptionId SpaceWithinTypeParameterAngles =
        Of("resharper_csharp_space_within_type_parameter_angles");

    public static readonly OptionId SpaceAfterAttributes = Of("resharper_csharp_space_after_attributes");

    public static readonly OptionId SpaceBetweenAttributeSections =
        Of("resharper_csharp_space_between_attribute_sections");

    public static readonly OptionId SpaceBeforeAttributeColon = Of("resharper_csharp_space_before_attribute_colon");
    public static readonly OptionId SpaceAfterAttributeColon = Of("resharper_csharp_space_after_attribute_colon");

    public static readonly OptionId SpaceBeforeColonInInheritanceClause =
        Of("resharper_space_before_colon_in_inheritance_clause");

    public static readonly OptionId SpaceAfterColonInInheritanceClause =
        Of("resharper_space_after_colon_in_inheritance_clause");

    public static readonly OptionId SpaceBeforeColonInCase = Of("resharper_csharp_space_before_colon_in_case");
    public static readonly OptionId SpaceAfterColonInCase = Of("resharper_csharp_space_after_colon_in_case");

    public static readonly OptionId SpaceBeforeColonInCtorInitializer =
        Of("resharper_space_before_colon_in_ctor_initializer");

    public static readonly OptionId SpaceBeforeTypeParameterConstraintColon =
        Of("resharper_csharp_space_before_type_parameter_constraint_colon");

    public static readonly OptionId SpaceAfterTypeParameterConstraintColon =
        Of("resharper_csharp_space_after_type_parameter_constraint_colon");

    public static readonly OptionId SpaceBeforeTernaryQuest = Of("resharper_csharp_space_before_ternary_quest");
    public static readonly OptionId SpaceAfterTernaryQuest = Of("resharper_csharp_space_after_ternary_quest");
    public static readonly OptionId SpaceBeforeTernaryColon = Of("resharper_csharp_space_before_ternary_colon");
    public static readonly OptionId SpaceAfterTernaryColon = Of("resharper_csharp_space_after_ternary_colon");
    public static readonly OptionId SpaceBeforeNullableMark = Of("resharper_csharp_space_before_nullable_mark");

    public static readonly OptionId SpaceBeforePointerAsterikDeclaration =
        Of("resharper_csharp_space_before_pointer_asterik_declaration");

    public static readonly OptionId SpaceBeforeSinglelineAccessorholder =
        Of("resharper_csharp_space_before_singleline_accessorholder");

    public static readonly OptionId SpaceInSinglelineAccessorholder =
        Of("resharper_csharp_space_in_singleline_accessorholder");

    public static readonly OptionId SpaceBetweenAccessorsInSinglelineProperty =
        Of("resharper_csharp_space_between_accessors_in_singleline_property");

    // ⚠ Inert since milestone 3, because the shape it governs no longer exists. `space_in_singleline_method`
    // is the spacing of `{ M(); }` on a method's own line, and BreakPlan.PlanOnePerLine gives every
    // statement a line of its own — the oracle does the same, so no input produces a single-line
    // method body with anything in it. An empty one is `empty_block_style`'s.
    // ⚠ The one shape that *is* single-line, an accessor body, is not this key's. Measured, because
    // the reason above was true and the wiring was not: with `space_in_singleline_method = false`
    // the oracle returns `get { return _n; }` unchanged, and with
    // `space_in_singleline_accessorholder = false` it returns `get {return _n;}`. Skala read the
    // body out of this key until the inertness of every inert key started being checked.
    public static readonly OptionId SpaceInSinglelineMethod = OfInert("resharper_csharp_space_in_singleline_method");

    public static readonly OptionId SpaceInSinglelineAnonymousMethod =
        Of("resharper_csharp_space_in_singleline_anonymous_method");

    public static readonly OptionId SpaceWithinEmptyBraces = Of("resharper_csharp_space_within_empty_braces");

    public static readonly OptionId SpaceWithinSingleLineArrayInitializerBraces =
        Of("resharper_csharp_space_within_single_line_array_initializer_braces");

    public static readonly OptionId SpaceWithinSlicePattern = Of("resharper_csharp_space_within_slice_pattern");

    // ⚠ Inert since milestone 3.1, and it was Tier A before it — on a fixture that cannot tell the
    // two values apart. Asked directly at both values, the oracle returns `[1, .. xs, 2]` and
    // `[1, ..xs, 2]` exactly as written: the gap after a collection expression's `..` is not
    // governed by anything, and this key's name is the only reason anyone thought it was.
    // `space_within_slice_pattern` is the one that really does govern its own construct, and it
    // stays Tier A. SK-DIV-0009.
    public static readonly OptionId SpaceWithinSpreadPattern = OfInert("resharper_space_within_spread_pattern");

    public static readonly OptionId SpaceBeforeTrailingComment = Of("resharper_csharp_space_before_trailing_comment");
    public static readonly OptionId SpaceBeforeTrailingCommentText = Of("resharper_space_before_trailing_comment_text");
    // ⚠ Unoracled, not inert, and it has now been both. Milestone 1 had it Tier A; milestone 3
    // demoted it to inert because the oracle does not insert the space and doing it anyway cost 79
    // lines across 15 files of `corpus/real/`. That demotion rested on `jb cleanupcode` being the
    // definition of correct, and SK-DIV-0006 no longer says it is: Rider's editor formats
    // documentation comments and cleanup does not, so the 79 lines were the oracle's divergence
    // being charged to Skala. The space is inserted again, by the sub-formatter, on every
    // well-formed comment — and the key still cannot be Tier A, because no fixture can pin it.
    public static readonly OptionId SpaceAfterTripleSlash = OfUnoracled("resharper_space_after_triple_slash");
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

    public static readonly OptionId AlignMultilineStatementConditions =
        Of("resharper_csharp_align_multiline_statement_conditions");

    // ⚠ The seven `align_multiline_*` keys whose column the writer's scope stack can express, all
    // of them `false` in the export. The column is the construct's own first token, and the level
    // its contents take from that column is whatever the construct already spends — one indent for
    // a braced or bracketed body, none at all for a chain. See
    // CSharpDocumentBuilder.AlignsFromOwnColumn.
    public static readonly OptionId AlignMultilineArrayAndObjectInitializer =
        Of("resharper_csharp_align_multiline_array_and_object_initializer");

    public static readonly OptionId AlignMultilineListPattern =
        Of("resharper_csharp_align_multiline_list_pattern");

    public static readonly OptionId AlignMultilinePropertyPattern =
        Of("resharper_csharp_align_multiline_property_pattern");

    public static readonly OptionId AlignMultilineSwitchExpression =
        Of("resharper_csharp_align_multiline_switch_expression");

    public static readonly OptionId AlignMultilineBinaryExpressionsChain =
        Of("resharper_csharp_align_multiline_binary_expressions_chain");

    public static readonly OptionId AlignMultilineBinaryPatterns =
        Of("resharper_csharp_align_multiline_binary_patterns");

    // ⚠ Read, implemented, and Tier D — on the evidence and not on the wiring. The oracle aligns a
    // wrapped query's clauses to the column of its `from`, and this reads the key and opens that
    // scope. What is missing is the wrap: Skala does not break a query expression at its clauses at
    // all (a milestone-3 gap that has nothing to do with alignment), so the only continuation a
    // query has in Skala's output is one inside a single clause, and a fixture pinning that column
    // would be pinning a line the oracle does not write. Tier A when the query wraps at its
    // clauses, and not before.
    public static readonly OptionId AlignLinqQuery = OfInert("resharper_csharp_align_linq_query");

    // ⚠ The column of the *first base type*, two past the base list's own node, which is where
    // every other member of this family reads its column. That was recorded here as the reason the
    // key could not be implemented, and it is a reason to move the anchor rather than to stop:
    // AlignAnchor takes a position and not a node, so pointing it at `Types[0]` is enough. The `:`
    // and the gap after it are written by EmitLeadingGapAt before the scope opens, so the column
    // the scope reads is the one the first base type lands on. Measured:
    //
    //     public class Alpha : System.Collections.Generic.IReadOnlyCollection<int>,
    //                          System.IDisposable,        ← the first base type's column
    public static readonly OptionId AlignMultilineExtendsList =
        Of("resharper_csharp_align_multiline_extends_list");

    // ⚠ The anchor is the column *after* the `(`, re-measured rather than assumed, and it is a
    // different anchor from every key AlignsFromOwnColumn answers — so VisitDelimited opens the
    // scope for it after the `(` has been written:
    //
    //     var tuple = (FirstComponentName: a, SecondComponentName: b,
    //                  AThirdComponentName: c);
    //
    // ⚠ Inert until milestone 3.2, and twice wrongly so. The alignment was implemented and the
    // *break* was missing: Skala had no break point between a tuple's components at all, so a tuple
    // too wide for its line came back broken after the `=` with the components flat, and one too
    // wide even for the continuation line came back over-long. Before that it was filed as
    // "unmeasured — no probe found a shape where it changes the oracle's output", which was wrong
    // for a third reason: the probes used tuples that fit. BreakPlan.PlanTuple owns the gap at each
    // comma now and constructs/wrapping/tuple-alignment.cs pins the column.
    //
    // ⚠ TupleExpression only, measured. A tuple *type* too wide for its line is not broken at its
    // commas by the oracle at either value of this key — it breaks between an element's type and its
    // name, `bool AThird, double\n    FourthName` — so TupleTypeSyntax is deliberately not planned.
    public static readonly OptionId AlignTupleComponents = Of("resharper_csharp_align_tuple_components");

    // ⚠ The rest of the `align_*` family, read so the crash snapshot records them, and Tier D each
    // for a reason the oracle gave rather than for a gap in the wiring. All measured one key at a
    // time at a 70-column margin against `jb cleanupcode`.
    //
    // Never read by the C# formatter — the unprefixed spellings are the C++ and VB formatters'
    // keys, which this export writes without a language prefix. Each set to true (or, where the
    // export already says true, to false) on a file that wraps the construct it names returns
    // byte-identical oracle output, while the construct's real key changes it:
    //   align_multiline_array_initializer, align_multiline_ctor_init, align_multiline_expression_braces,
    //   align_multiline_implements_list, align_multiline_type_argument, align_multiline_type_parameter,
    //   align_multiline_type_parameter_constraints, align_multiline_type_parameter_list,
    //   align_ternary, alignment_tab_fill_style.
    //
    // Masked by another key at the export's own values, so the per-option unit — which flips one key
    // from the repository's configuration — cannot reach them:
    //   align_multiline_argument, align_multiline_parameter — the export sets
    //     wrap_after_{invocation,declaration}_lpar = true, which gives the first item a line of its
    //     own, and there is then no first item on the delimiter's line to align the rest to. With
    //     the lpar key off as well, both change the output.
    //   align_multiline_for_stmt — align_multiline_statement_conditions = true already aligns a
    //     `for` header by its `(`. Either key alone is enough and the export has the other one on.
    //     ⚠ Re-measured in milestone 3.2, once the header had a break point for a column to govern,
    //     because "masked" and "unreachable because nothing wraps" are easy to confuse and only one
    //     of them is still true. It is masked: at the export's values both `false` and `true` give
    //     `for (var i = 0;\n     i < xs.Count;` — the `(`'s column — and with
    //     align_multiline_statement_conditions = false as well, the two values separate, `false`
    //     taking one continuation indent and `true` the `(`'s column. Two flips, so the per-option
    //     unit still cannot reach it and it stays Tier D.
    //
    // Observable and not implemented, with the shape recorded so the next attempt starts from it:
    //   align_first_arg_by_paren — puts the arguments on the `(`'s column plus one and the closing
    //     parenthesis one column *left* of them. The writer's scope stack has one column per scope
    //     and no expression for "the closer is the column minus one".
    //   align_multiline_calls_chain — the anchor is the chain's first `.`, and a chain's
    //     continuation level is spent lazily at the first break, by which time the writer has
    //     written past that dot.
    //   align_multiline_expression — the union of four specific keys, except for binary patterns:
    //     it aligns a pattern chain one level from the *enclosing* expression where
    //     align_multiline_binary_patterns aligns it on the pattern's own column, one further right.
    //     An Align scope reads the column where it opens and cannot see the enclosing expression.
    //   align_multiple_declaration, align_multiline_comments — no probe found a shape where they
    //     change the oracle's output, which is weaker evidence than the rest of this list: they are
    //     unmeasured rather than measured inert. ⚠ `align_tuple_components` was in this group and
    //     is not any more: the probes that missed it used tuples that fit, and a tuple long enough
    //     to wrap moves the oracle's output at both values. Read the same warning into what is left
    //     of the group — "no probe found" is a statement about the probes.

    // ── Column alignment of adjacent constructs (int_align_*) ────────────────────────────────
    // ⚠ Every one of these is `false` in the export and every one of them is read here, so the
    // generalized `resharper_int_align` — which the registry expands into all thirteen — is
    // observable through them. See IntAlign, and Ids.DisableIntAlign below for the three keys of
    // the family that stay Tier D.
    public static readonly OptionId IntAlignFields = Of("resharper_csharp_int_align_fields");
    public static readonly OptionId IntAlignVariables = Of("resharper_csharp_int_align_variables");
    public static readonly OptionId IntAlignAssignments = Of("resharper_csharp_int_align_assignments");
    public static readonly OptionId IntAlignProperties = Of("resharper_csharp_int_align_properties");
    public static readonly OptionId IntAlignMethods = Of("resharper_csharp_int_align_methods");
    public static readonly OptionId IntAlignComments = Of("resharper_csharp_int_align_comments");

    public static readonly OptionId IntAlignSwitchExpressions =
        Of("resharper_csharp_int_align_switch_expressions");

    public static readonly OptionId IntAlignSwitchSections = Of("resharper_csharp_int_align_switch_sections");

    // ⚠ The five list-shaped members of the family, each measured one key at a time against
    // `jb cleanupcode` 2025.2.6 at config sha256:bd9791d3a6e6a087. The slot each pads is the
    // oracle's, not a guess from the option's name:
    //   int_align_parameters      — the parameter *name* of a chopped signature, so the types pad
    //                               out to a column: `int    first,` / `string secondName,`.
    //   int_align_invocations     — every argument of adjacent single-line calls *of the same
    //                               method*. `Take(1, 2, 3)` beside `Take(1000, 2000, 3000)` pads
    //                               both argument columns; an `Other2(…)` between two `Take(…)`
    //                               ends the run rather than joining it.
    //   int_align_nested_ternary  — the `?` of each member of a nested conditional chain.
    //   int_align_binary_expressions — the *operator* of each of those same conditions.
    //   int_align_property_patterns — the `:` of a chopped property pattern's subpatterns.
    //
    // ⚠ `int_align_binary_expressions` is narrower than its name, and the narrowness is measured
    // rather than assumed. Asked with the key on, the oracle moves nothing in: adjacent assignment
    // statements whose right-hand sides are binary; a binary chain chopped one operand per line;
    // adjacent `if` conditions; binary expressions as arguments, as initializer elements, or as
    // switch-expression arm results. The one shape that moves is the conditional chain, which is
    // why it is collected from the chain here and not from every binary expression in the file.
    public static readonly OptionId IntAlignParameters = Of("resharper_csharp_int_align_parameters");
    public static readonly OptionId IntAlignInvocations = Of("resharper_csharp_int_align_invocations");

    public static readonly OptionId IntAlignPropertyPatterns =
        Of("resharper_csharp_int_align_property_patterns");

    // ⚠ Implemented, measured, and Tier D — on Skala's own wrapping and not on this pass. Both keys
    // pad a conditional chain the oracle lays out with one member per line and the `?` on its
    // condition's own line:
    //
    //     var chain = flag > 10 ? "the first branch here" :
    //         flag > 5 ? "the second branch here" :
    //         flag > 1 ? "third" : "d";
    //
    // Skala does not write that layout. Asked with the chain on one source line it produces one
    // break and a flat tail — `flag > 10\n ? "…"\n : flag > 5 ? "…" : flag > 1 ? "third" : "d"` —
    // and asked with the oracle's own output as input it rewrites it into the same shape, so
    // `keep_user_linebreaks` does not reach it either. There is therefore no document Skala emits
    // that either key can pad, and a fixture would be pinning a shape Skala never produces.
    //
    // CollectConditionalChains is correct against the oracle's shape and stays: promotion is these
    // two lines becoming `Of`, plus a corpus file, once the chain wraps the way ReSharper wraps it.
    public static readonly OptionId IntAlignNestedTernary =
        OfInert("resharper_csharp_int_align_nested_ternary");

    public static readonly OptionId IntAlignBinaryExpressions =
        OfInert("resharper_csharp_int_align_binary_expressions");

    // ⚠ Read and Tier D, all three for the same reason and none of them for a missing
    // implementation: they refine an alignment that the export never asks for. `disable_int_align`
    // is a master switch over a family whose every member is already false, and
    // `int_align_fix_in_adjacent` and `allow_far_alignment` say which neighbours join a run and how
    // far a column may be dragged — questions with no answer while no run exists. Measured: with
    // `int_align = true` supplied alongside, `disable_int_align` turns the whole family off again
    // and the other two change nothing on any shape tried. The per-option unit flips one key from
    // the repository's configuration, so none of the three can be demonstrated there.
    public static readonly OptionId DisableIntAlign = OfInert("resharper_disable_int_align");
    public static readonly OptionId IntAlignFixInAdjacent = OfInert("resharper_csharp_int_align_fix_in_adjacent");
    public static readonly OptionId AllowFarAlignment = OfInert("resharper_csharp_allow_far_alignment");

    // ⚠ Read and Tier D on the measurement: the C# formatter does not consult the unprefixed
    // spellings at all. Asked directly with each set to true on a file that exercises it, the oracle
    // returns byte-identical output, while the `resharper_csharp_int_align_*` key covering the same
    // construct changes it — `int_align_fields` is what aligns an enum's initializers, not
    // `int_align_enum_initializers`. They are the C++ and VB formatters' keys, which this export
    // writes without a language prefix.
    // ⚠ Read and Tier D on the configuration model rather than on the formatter. The registry
    // records that `resharper_int_align` expands into the thirteen `resharper_csharp_int_align_*`
    // keys — that is what `expands` in options.json is for — and nothing applies the expansion:
    // OptionResolver resolves keys and aliases and leaves generalized properties alone. Setting it
    // therefore changes no value the formatter reads, whatever the formatter implements. Tier A when
    // the resolver expands it, and the fix belongs to docs/plan/03's configuration model.
    public static readonly OptionId IntAlign = OfInert("resharper_int_align");

    public static readonly OptionId IntAlignEq = OfInert("resharper_int_align_eq");
    public static readonly OptionId IntAlignDeclarationNames = OfInert("resharper_int_align_declaration_names");
    public static readonly OptionId IntAlignEnumInitializers = OfInert("resharper_int_align_enum_initializers");

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

    public static readonly OptionId UseContinuousIndentInsideParens =
        Of("resharper_csharp_use_continuous_indent_inside_parens");

    public static readonly OptionId UseContinuousIndentInsideInitializerBraces =
        Of("resharper_csharp_use_continuous_indent_inside_initializer_braces");

    public static readonly OptionId ContinuousIndentMultiplier = Of("resharper_csharp_continuous_indent_multiplier");
    public static readonly OptionId IndentPreprocessorIf = Of("resharper_csharp_indent_preprocessor_if");
    public static readonly OptionId IndentPreprocessorOther = Of("resharper_csharp_indent_preprocessor_other");
    public static readonly OptionId IndentPreprocessorRegion = Of("resharper_csharp_indent_preprocessor_region");

    public static readonly OptionId IndentAnonymousMethodBlock =
        OfInert("resharper_csharp_indent_anonymous_method_block");

    public static readonly OptionId OutdentStatementLabels = Of("resharper_csharp_outdent_statement_labels");

    public static readonly OptionId KeepBlankLinesInCode = Of("resharper_csharp_keep_blank_lines_in_code");

    public static readonly OptionId KeepBlankLinesInDeclarations =
        Of("resharper_csharp_keep_blank_lines_in_declarations");

    public static readonly OptionId RemoveBlankLinesNearBracesInCode =
        Of("resharper_csharp_remove_blank_lines_near_braces_in_code");

    public static readonly OptionId RemoveBlankLinesNearBracesInDeclarations =
        Of("resharper_csharp_remove_blank_lines_near_braces_in_declarations");

    public static readonly OptionId BlankLinesAroundType = Of("resharper_csharp_blank_lines_around_type");

    public static readonly OptionId BlankLinesAroundSingleLineType =
        Of("resharper_csharp_blank_lines_around_single_line_type");

    public static readonly OptionId BlankLinesAroundInvocable = Of("resharper_csharp_blank_lines_around_invocable");

    public static readonly OptionId BlankLinesAroundSingleLineInvocable =
        Of("resharper_csharp_blank_lines_around_single_line_invocable");

    public static readonly OptionId BlankLinesAroundField = Of("resharper_csharp_blank_lines_around_field");

    public static readonly OptionId BlankLinesAroundSingleLineField =
        Of("resharper_csharp_blank_lines_around_single_line_field");

    public static readonly OptionId BlankLinesAroundProperty = Of("resharper_csharp_blank_lines_around_property");

    public static readonly OptionId BlankLinesAroundSingleLineProperty =
        Of("resharper_csharp_blank_lines_around_single_line_property");

    public static readonly OptionId BlankLinesAroundAutoProperty =
        Of("resharper_csharp_blank_lines_around_auto_property");

    public static readonly OptionId BlankLinesAroundSingleLineAutoProperty =
        Of("resharper_csharp_blank_lines_around_single_line_auto_property");

    public static readonly OptionId BlankLinesAroundAccessor = Of("resharper_csharp_blank_lines_around_accessor");

    public static readonly OptionId BlankLinesAroundSingleLineAccessor =
        Of("resharper_csharp_blank_lines_around_single_line_accessor");

    public static readonly OptionId BlankLinesAroundLocalMethod =
        Of("resharper_csharp_blank_lines_around_local_method");

    public static readonly OptionId BlankLinesAroundSingleLineLocalMethod =
        Of("resharper_csharp_blank_lines_around_single_line_local_method");

    public static readonly OptionId BlankLinesAroundNamespace = Of("resharper_csharp_blank_lines_around_namespace");
    public static readonly OptionId BlankLinesAroundRegion = Of("resharper_csharp_blank_lines_around_region");
    public static readonly OptionId BlankLinesInsideRegion = Of("resharper_csharp_blank_lines_inside_region");
    public static readonly OptionId BlankLinesInsideType = OfInert("resharper_csharp_blank_lines_inside_type");

    public static readonly OptionId BlankLinesInsideNamespace =
        OfInert("resharper_csharp_blank_lines_inside_namespace");

    public static readonly OptionId BlankLinesAfterUsingList = Of("resharper_csharp_blank_lines_after_using_list");

    public static readonly OptionId BlankLinesAfterFileScopedNamespaceDirective =
        Of("resharper_csharp_blank_lines_after_file_scoped_namespace_directive");

    public static readonly OptionId BlankLinesAfterBlockStatements =
        Of("resharper_csharp_blank_lines_after_block_statements");

    public static readonly OptionId BlankLinesBeforeSingleLineComment =
        Of("resharper_csharp_blank_lines_before_single_line_comment");

    public static readonly OptionId BlankLinesAfterCase = Of("resharper_csharp_blank_lines_after_case");
    public static readonly OptionId BlankLinesBeforeCase = Of("resharper_csharp_blank_lines_before_case");

    public static readonly OptionId BlankLinesAfterStartComment =
        Of("resharper_csharp_blank_lines_after_start_comment");

    public static readonly OptionId BlankLinesBeforeControlTransferStatements =
        Of("resharper_csharp_blank_lines_before_control_transfer_statements");

    public static readonly OptionId BlankLinesAfterControlTransferStatements =
        Of("resharper_csharp_blank_lines_after_control_transfer_statements");

    public static readonly OptionId BlankLinesBeforeMultilineStatements =
        Of("resharper_csharp_blank_lines_before_multiline_statements");

    public static readonly OptionId BlankLinesAfterMultilineStatements =
        Of("resharper_csharp_blank_lines_after_multiline_statements");

    public static readonly OptionId BlankLinesBeforeBlockStatements =
        Of("resharper_csharp_blank_lines_before_block_statements");

    public static readonly OptionId BlankLinesAroundBlockCaseSection =
        Of("resharper_csharp_blank_lines_around_block_case_section");

    public static readonly OptionId BlankLinesAroundMultilineCaseSection =
        Of("resharper_csharp_blank_lines_around_multiline_case_section");

    // ── Break presence and position (phase 2) ────────────────────────────────────────────────
    public static readonly OptionId KeepUserLinebreaks = Of("resharper_keep_user_linebreaks");

    // ⚠ Inert, and established against the oracle rather than assumed: with
    // keep_user_linebreaks = true, setting keep_user_wrapping to false changes nothing on any shape
    // tried — broken argument lists, ternaries, binary chains and call chains all keep their breaks.
    // keep_user_linebreaks is the key that governs. See the M2 report.
    public static readonly OptionId KeepUserWrapping = OfInert("resharper_keep_user_wrapping");

    public static readonly OptionId KeepExistingInvocationParensArrangement =
        Of("resharper_csharp_keep_existing_invocation_parens_arrangement");

    public static readonly OptionId KeepExistingDeclarationParensArrangement =
        Of("resharper_csharp_keep_existing_declaration_parens_arrangement");

    public static readonly OptionId KeepExistingLambdaParensArrangement =
        Of("resharper_keep_existing_lambda_and_anonymous_function_parens_arrangement");

    public static readonly OptionId KeepExistingPrimaryConstructorParensArrangement =
        Of("resharper_csharp_keep_existing_primary_constructor_declaration_parens_arrangement");

    public static readonly OptionId KeepExistingExprMemberArrangement =
        Of("resharper_csharp_keep_existing_expr_member_arrangement");

    public static readonly OptionId KeepExistingEmbeddedArrangement =
        Of("resharper_csharp_keep_existing_embedded_arrangement");

    public static readonly OptionId KeepExistingAttributeArrangement =
        Of("resharper_csharp_keep_existing_attribute_arrangement");

    public static readonly OptionId KeepExistingDeclarationBlockArrangement =
        Of("resharper_keep_existing_declaration_block_arrangement");

    public static readonly OptionId KeepExistingEmbeddedBlockArrangement =
        Of("resharper_keep_existing_embedded_block_arrangement");

    public static readonly OptionId KeepExistingEnumArrangement = Of("resharper_csharp_keep_existing_enum_arrangement");
    public static readonly OptionId KeepExistingLinebreaks = Of("resharper_csharp_keep_existing_linebreaks");

    public static readonly OptionId WrapEnumDeclaration = Of("resharper_csharp_wrap_enum_declaration");
    public static readonly OptionId MaxEnumMembersOnLine = Of("resharper_csharp_max_enum_members_on_line");
    public static readonly OptionId WrapSwitchExpression = Of("resharper_csharp_wrap_switch_expression");
    public static readonly OptionId WrapArgumentsStyle = Of("resharper_csharp_wrap_arguments_style");
    public static readonly OptionId WrapParametersStyle = Of("resharper_csharp_wrap_parameters_style");

    public static readonly OptionId WrapPrimaryConstructorParametersStyle =
        Of("resharper_csharp_wrap_primary_constructor_parameters_style");

    public static readonly OptionId WrapAfterPrimaryConstructorLpar =
        Of("resharper_csharp_wrap_after_primary_constructor_declaration_lpar");

    public static readonly OptionId WrapBeforePrimaryConstructorRpar =
        Of("resharper_csharp_wrap_before_primary_constructor_declaration_rpar");

    public static readonly OptionId WrapBeforeBinaryOpsign = Of("resharper_csharp_wrap_before_binary_opsign");
    public static readonly OptionId WrapBeforeBinaryPatternOp = Of("resharper_csharp_wrap_before_binary_pattern_op");
    public static readonly OptionId WrapBeforeTernaryOpsigns = Of("resharper_csharp_wrap_before_ternary_opsigns");
    // ⚠ No longer inert. Milestone 2 recorded that no input could tell its two values apart,
    // because M2 never *added* a break at either side of an `=` and the key only chooses a side.
    // M3 added one — GroupFacts.PrefersOuterBreak — and the key became observable the moment it
    // did; the M2 note outlived the reason for it. Asked directly at a 70-column margin,
    // `target = a + b + c;` comes back broken after the `=` at false and before it at true.
    public static readonly OptionId WrapBeforeEq = Of("resharper_csharp_wrap_before_eq");
    public static readonly OptionId WrapBeforeComma = Of("resharper_csharp_wrap_before_comma");
    public static readonly OptionId WrapAfterInvocationLpar = Of("resharper_csharp_wrap_after_invocation_lpar");
    public static readonly OptionId WrapBeforeInvocationRpar = Of("resharper_csharp_wrap_before_invocation_rpar");
    public static readonly OptionId WrapAfterDeclarationLpar = Of("resharper_csharp_wrap_after_declaration_lpar");
    public static readonly OptionId WrapBeforeDeclarationRpar = Of("resharper_csharp_wrap_before_declaration_rpar");

    // ⚠ The opening half of the three `lpar` pairs. `wrap_after_X_lpar` says whether the first item
    // gets a line of its own; these say whether the *parenthesis itself* does, which is a break at
    // the gap before it and therefore a point of the same group:
    //     void Decl              SomeCall
    //     (                      (
    //         int a,                 argument
    //     ) { }                  );
    public static readonly OptionId WrapBeforeDeclarationLpar =
        Of("resharper_csharp_wrap_before_declaration_lpar");

    public static readonly OptionId WrapBeforeInvocationLpar = Of("resharper_csharp_wrap_before_invocation_lpar");

    public static readonly OptionId WrapBeforePrimaryConstructorLpar =
        Of("resharper_csharp_wrap_before_primary_constructor_declaration_lpar");

    // ⚠ Read, implemented, and Tier D — both of them blocked by a wrapping gap of their own rather
    // than by anything to do with the key.
    //
    //   wrap_before_type_parameter_langle — implemented in BreakPlan and verified: with the key on,
    //     Skala's output on a type parameter list too long for its line is byte-identical to the
    //     oracle's. With it off the two disagree, because Skala gives a type parameter list no group
    //     at all and the oracle wraps one — after the `<` when a single parameter overflows, at the
    //     last comma that fits when several do. A fixture pinning this key would be committing that
    //     unrelated divergence to the corpus. Tier A once a type parameter list wraps.
    //   wrap_before_linq_expression — implemented in PlanAroundEquals: with the key on, a query is
    //     taken out of the ordering rule and breaks whenever the whole query does not fit, which is
    //     what puts `from` on a line of its own. Blocked by the same gap as `align_linq_query`:
    //     Skala does not break a query at its clauses, so every query long enough to make the key
    //     matter is one Skala already lays out differently.
    public static readonly OptionId WrapBeforeTypeParameterLangle =
        OfInert("resharper_csharp_wrap_before_type_parameter_langle");

    public static readonly OptionId WrapBeforeLinqExpression =
        OfInert("resharper_csharp_wrap_before_linq_expression");

    // ⚠ The rest of the `wrap_*` family the export sets, measured the same way and Tier D.
    //
    // Never read by the C# formatter. Each is the unprefixed spelling of a key whose C# form is
    // elsewhere in this list, and setting it changes nothing in the oracle's output on a file that
    // exercises the construct: wrap_after_binary_opsign (the C# key is wrap_before_binary_opsign),
    // wrap_after_dot (wrap_after_dot_in_method_calls), wrap_arguments (csharp_wrap_arguments_style),
    // wrap_base_clause_style (csharp_wrap_extends_list_style), wrap_braced_init_list_style
    // (wrap_array_initializer_style), wrap_ctor_initializer_style, wrap_enumeration_style
    // (wrap_enum_declaration), wrap_before_colon, wrap_comments.
    //
    // ⚠ The four lambda keys belong here too, and the measurement is the interesting one: with
    // wrap_{before,after}_lambda_and_anonymous_function_declaration_{lpar,rpar} and
    // wrap_lambda_and_anonymous_function_parameters_style at any value, the oracle's layout of a
    // lambda's parameter list does not move — and it *does* move when
    // wrap_before_declaration_lpar changes. A lambda's parameter list is governed by the method
    // declaration keys; the five keys named for it are not read.
    //
    // Master switches with nothing behind them: enable_wrapping = true changes nothing, and
    // keep_user_wrapping has no observable effect in this export (BreakPlan records the same, from
    // M2). ⚠ csharp_wrap_lines was in this list and is not any more — it is implemented above, as
    // the margin itself. The measurement that put it here was taken on already-wrapped input, where
    // what stays put stays put under keep_user_linebreaks; on flat input it joins the file.
    //
    // Not reached by any probe: wrap_before_first_type_parameter_constraint — no shape tried put
    // two constraint clauses in a position where it could decide anything.
    //
    // ⚠ wrap_multiple_type_parameter_constraints_style was beside it and is not any more: it is
    // reached, and the shape that reaches it is a declaration with four constraint clauses on one
    // source line. At `wrap_if_long` the oracle fills them two to a line and at `chop_always` it
    // gives a two-clause method one `where` per line, both against the export's `chop_if_long`. Not
    // implemented, and not for want of the key: Skala has no break point before a `where` at all —
    // asked with the same declaration it leaves the constraints on a 200-column line rather than
    // choosing between the three styles. Tier A once the constraint list wraps.
    //
    // ⚠ wrap_for_stmt_header_style was in this list and is not any more. The shape was right — a
    // `for` whose three clauses do not fit, where `wrap_if_long` keeps the initializer and the
    // condition together and the export's `chop_if_long` gives each clause a line — and so was the
    // reason it could not be implemented: Skala had no break point at the header's `;` and broke
    // inside the incrementor expression instead. That is a gap to fill, not a wall; see
    // WrapForStmtHeaderStyle and BreakPlan.PlanForHeader.
    //
    // wrap_verbatim_interpolated_strings is observable — chop_if_long breaks the oracle's output
    // *inside* the interpolation holes of a verbatim string — and is not implemented: Skala emits an
    // interpolated string as one piece and has no break point inside one.

    public static readonly OptionId WrapBeforeArrowWithExpressions =
        Of("resharper_csharp_wrap_before_arrow_with_expressions");

    public static readonly OptionId PlaceTypeAttributeOnSameLine =
        Of("resharper_csharp_place_type_attribute_on_same_line");

    public static readonly OptionId PlaceMethodAttributeOnSameLine =
        Of("resharper_csharp_place_method_attribute_on_same_line");

    public static readonly OptionId PlaceFieldAttributeOnSameLine =
        Of("resharper_csharp_place_field_attribute_on_same_line");

    public static readonly OptionId PlaceAccessorAttributeOnSameLine =
        Of("resharper_csharp_place_accessor_attribute_on_same_line");

    public static readonly OptionId PlaceAccessorHolderAttributeOnSameLine =
        Of("resharper_csharp_place_accessorholder_attribute_on_same_line");

    public static readonly OptionId PlaceRecordFieldAttributeOnSameLine =
        Of("resharper_csharp_place_record_field_attribute_on_same_line");


    /// <summary>
    ///     ⚠ Generalized, not inert, and the difference is a Tier D that was understating the tool.
    ///     docs/plan/17 counted <c>ArrangeAttributes</c> among fifteen "declared and not performed"
    ///     arrangement options on the strength of this key's tier. It is in fact honoured in full: the
    ///     resolver expands it into the six <c>place_*_attribute_on_same_line</c> keys below, every one
    ///     of which is implemented and Tier A, and flipping it moves the formatter's output. The key
    ///     needed no rewrite — it needed to be claimed by the mechanism that already exists for exactly
    ///     this shape.
    /// </summary>
    public static readonly OptionId PlaceAttributeOnSameLine =
        OfGeneralized("resharper_place_attribute_on_same_line");

    // ⚠ Four keys read but never observable, and Tier D with the reason rather than Tier A:
    //   max_attribute_length_for_same_line — a length threshold for a placement that never happens.
    //   place_attribute_on_same_line — the six per-owner keys cover every C# attribute target, so
    //     the generalized key never gets to decide.
    //   new_line_between_query_expression_clauses and place_linq_into_on_new_line — measured against
    //     the oracle: `from x in xs where p select x` on one line comes back on one line with both
    //     set. They permit a break rather than requiring one, and permitting one is what
    //     keep_user_linebreaks already does.
    //   wrap_before_eq — it moves the break point from one side of the `=` to the other, and
    //     milestone 2 never adds a break at either side (that ordering is prefer_wrap_around_eq's,
    //     which is M3), so no input distinguishes the values.
    public static readonly OptionId MaxAttributeLengthForSameLine =
        OfInert("resharper_csharp_max_attribute_length_for_same_line");

    public static readonly OptionId PlaceSingleMethodArgumentLambdaOnSameLine =
        Of("resharper_place_single_method_argument_lambda_on_same_line");

    public static readonly OptionId PlaceExprMethodOnSingleLine =
        Of("resharper_csharp_place_expr_method_on_single_line");

    public static readonly OptionId PlaceExprPropertyOnSingleLine =
        Of("resharper_csharp_place_expr_property_on_single_line");

    public static readonly OptionId PlaceExprAccessorOnSingleLine =
        Of("resharper_csharp_place_expr_accessor_on_single_line");

    public static readonly OptionId PlaceSimpleEmbeddedStatementOnSameLine =
        Of("resharper_csharp_place_simple_embedded_statement_on_same_line");

    public static readonly OptionId PlaceSimpleCaseStatementOnSameLine =
        Of("resharper_csharp_place_simple_case_statement_on_same_line");

    public static readonly OptionId PlaceTypeConstraintsOnSameLine =
        Of("resharper_csharp_place_type_constraints_on_same_line");

    public static readonly OptionId PlaceConstructorInitializerOnSameLine =
        Of("resharper_csharp_place_constructor_initializer_on_same_line");

    public static readonly OptionId PlacePrimaryConstructorInitializerOnSameLine =
        Of("resharper_place_primary_constructor_initializer_on_same_line");

    public static readonly OptionId PlaceLinqIntoOnNewLine = OfInert("resharper_csharp_place_linq_into_on_new_line");

    public static readonly OptionId NewLineBetweenQueryExpressionClauses =
        OfInert("csharp_new_line_between_query_expression_clauses");

    // ── Wrapping (phase 3) ───────────────────────────────────────────────────────────────────
    public static readonly OptionId WrapArrayInitializerStyle = Of("resharper_csharp_wrap_array_initializer_style");
    public static readonly OptionId MaxInitializerElementsOnLine = Of("resharper_max_initializer_elements_on_line");

    public static readonly OptionId MaxArrayInitializerElementsOnLine =
        Of("resharper_max_array_initializer_elements_on_line");

    public static readonly OptionId PlaceSimpleInitializerOnSingleLine =
        Of("resharper_place_simple_initializer_on_single_line");

    public static readonly OptionId WrapAfterExpressionLbrace = Of("resharper_wrap_after_expression_lbrace");
    public static readonly OptionId WrapBeforeExpressionRbrace = Of("resharper_wrap_before_expression_rbrace");

    public static readonly OptionId WrapChainedMethodCalls = Of("resharper_csharp_wrap_chained_method_calls");
    public static readonly OptionId WrapAfterDotInMethodCalls = Of("resharper_wrap_after_dot_in_method_calls");
    public static readonly OptionId WrapBeforeFirstMethodCall = Of("resharper_wrap_before_first_method_call");

    public static readonly OptionId WrapAfterPropertyInChainedMethodCalls =
        Of("resharper_wrap_after_property_in_chained_method_calls");

    public static readonly OptionId WrapChainedBinaryExpressions =
        Of("resharper_csharp_wrap_chained_binary_expressions");

    public static readonly OptionId WrapChainedBinaryPatterns = Of("resharper_csharp_wrap_chained_binary_patterns");
    public static readonly OptionId WrapTernaryExprStyle = Of("resharper_csharp_wrap_ternary_expr_style");

    public static readonly OptionId WrapMultipleDeclarationStyle =
        Of("resharper_csharp_wrap_multiple_declaration_style");

    public static readonly OptionId WrapExtendsListStyle = Of("resharper_csharp_wrap_extends_list_style");

    // ⚠ No longer in the "reached and not implemented" list above. The reason recorded there was
    // right — Skala had no break point at the header's `;` and broke inside the incrementor
    // expression instead — and it was a reason to add the break rather than to stop. BreakPlan
    // .PlanForHeader now owns the two gaps after the semicolons; the alignment column the oracle
    // writes them on was already there, from align_multiline_statement_conditions.
    public static readonly OptionId WrapForStmtHeaderStyle = Of("resharper_csharp_wrap_for_stmt_header_style");
    public static readonly OptionId WrapBeforeExtendsColon = Of("resharper_wrap_before_extends_colon");
    public static readonly OptionId WrapBeforeCommaInBaseClause = Of("resharper_wrap_before_comma_in_base_clause");
    public static readonly OptionId WrapPropertyPattern = Of("resharper_csharp_wrap_property_pattern");
    public static readonly OptionId WrapListPattern = Of("resharper_csharp_wrap_list_pattern");

    public static readonly OptionId KeepExistingListPatternsArrangement =
        Of("resharper_keep_existing_list_patterns_arrangement");

    public static readonly OptionId KeepExistingPropertyPatternsArrangement =
        Of("resharper_keep_existing_property_patterns_arrangement");

    public static readonly OptionId KeepExistingSwitchExpressionArrangement =
        Of("resharper_keep_existing_switch_expression_arrangement");

    // ⚠ Read, implemented, and Tier D — because `keep_existing_list_patterns_arrangement` defaults
    // to true and outranks it in both directions. With keep on, the placement key neither joins a
    // list pattern the author broke nor forces a whole one apart, so no input can tell its two
    // values apart on ReSharper's own defaults. Verified against the oracle rather than assumed:
    // flipping it alone changes nothing; flipping it with keep off turns `xs is [1, 2, 3]` into
    // three lines. An option that cannot change behaviour must not claim a tier that says it was.
    public static readonly OptionId PlaceSimpleListPatternOnSingleLine =
        OfInert("resharper_place_simple_list_pattern_on_single_line");

    public static readonly OptionId PlaceSimplePropertyPatternOnSingleLine =
        Of("resharper_place_simple_property_pattern_on_single_line");

    public static readonly OptionId PlaceSimpleSwitchExpressionOnSingleLine =
        Of("resharper_place_simple_switch_expression_on_single_line");

    public static readonly OptionId MaxInvocationArgumentsOnLine = Of("resharper_max_invocation_arguments_on_line");
    public static readonly OptionId MaxFormalParametersOnLine = Of("resharper_max_formal_parameters_on_line");

    public static readonly OptionId MaxPrimaryConstructorParametersOnLine =
        Of("resharper_max_primary_constructor_parameters_on_line");

    // ⚠ Read, but Tier D on the evidence rather than on the wiring. `prefer_wrap_around_eq`'s
    // domain is not published and this export writes `default`; the ordering rule is implemented and
    // pinned by fixtures, but no second value is known to exist, so nothing can demonstrate the
    // option changing an output and Tier A would be a claim the corpus cannot support.
    public static readonly OptionId PreferWrapAroundEq = OfInert("resharper_prefer_wrap_around_eq");

    public static readonly OptionId IndentRawLiteralString = Of("resharper_csharp_indent_raw_literal_string");
    public static readonly OptionId FormatterTagsEnabled = Of("resharper_formatter_tags_enabled");
    public static readonly OptionId FormatterOffTag = Of("resharper_formatter_off_tag");
    public static readonly OptionId FormatterOnTag = Of("resharper_formatter_on_tag");
    public static readonly OptionId FormatterTagsAcceptRegexp = Of("resharper_formatter_tags_accept_regexp");

    // ── The xmldoc sub-formatter's subset ────────────────────────────────────────────────────
    // ⚠ Every one of these governs real output on the default path and every one of them stays
    // Tier D, which is a combination no other key in the registry has. No *committed* fixture can
    // show the oracle honouring any of them, because every fixture was generated under a profile
    // that leaves `CSharpFormatDocComments` off — not because `jb cleanupcode` cannot format a
    // documentation comment, which it does, and which four of these keys were measured against.
    // Rider's editor formats them too, so leaving them off would be the divergence rather than
    // turning them on (SK-DIV-0006). What pins them meanwhile is hand-written fixtures plus the
    // round-trip property in XmlDocFormatter. See XmlDocOptions for the full argument, and
    // `OfUnoracled` for what the mark means.
    public static readonly OptionId XmlDocWrapLines = OfUnoracled("resharper_xmldoc_wrap_lines");
    public static readonly OptionId XmlDocMaxLineLength = OfUnoracled("resharper_xmldoc_max_line_length");
    public static readonly OptionId XmlDocWrapText = OfUnoracled("resharper_xmldoc_wrap_text");
    public static readonly OptionId XmlDocWrapTagsAndPi = OfUnoracled("resharper_xmldoc_wrap_tags_and_pi");
    public static readonly OptionId XmlDocKeepUserLinebreaks = OfUnoracled("resharper_xmldoc_keep_user_linebreaks");

    public static readonly OptionId XmlDocMaxBlankLinesBetweenTags =
        OfUnoracled("resharper_xmldoc_max_blank_lines_between_tags");

    public static readonly OptionId XmlDocIndentChildElements = OfUnoracled("resharper_xmldoc_indent_child_elements");
    public static readonly OptionId XmlDocIndentText = OfUnoracled("resharper_xmldoc_indent_text");

    public static readonly OptionId XmlDocLinebreaksInsideTagsForElementsWithChildElements =
        OfUnoracled("resharper_xmldoc_linebreaks_inside_tags_for_elements_with_child_elements");

    public static readonly OptionId XmlDocLinebreaksInsideTagsForMultilineElements =
        OfUnoracled("resharper_xmldoc_linebreaks_inside_tags_for_multiline_elements");

    public static readonly OptionId XmlDocLinebreakBeforeMultilineElements =
        OfUnoracled("resharper_xmldoc_linebreak_before_multiline_elements");

    public static readonly OptionId XmlDocLinebreakBeforeSinglelineElements =
        OfUnoracled("resharper_xmldoc_linebreak_before_singleline_elements");

    public static readonly OptionId XmlDocSpacesInsideTags = OfUnoracled("resharper_xmldoc_spaces_inside_tags");

    public static readonly OptionId XmlDocSpaceBeforeSelfClosing =
        OfUnoracled("resharper_xmldoc_space_before_self_closing");

    public static readonly OptionId XmlDocIndentSize = OfUnoracled("resharper_xmldoc_indent_size");
    public static readonly OptionId XmlDocIndentStyle = OfUnoracled("resharper_xmldoc_indent_style");

    public static readonly OptionId XmlDocLinebreakBeforeElements =
        OfUnoracled("resharper_xmldoc_linebreak_before_elements");

    // ⚠ These four were in `XmlDocIds.Refused` until the tag-header behaviour was measured with
    // `CSharpFormatDocComments` switched on. The refusals were the SK-DIV-0006 mistake repeated at
    // option granularity: a profile that never asked the question, read as an answer.
    public static readonly OptionId XmlDocSpaceAfterLastAttribute =
        OfUnoracled("resharper_xmldoc_space_after_last_attribute");

    public static readonly OptionId XmlDocSpacesAroundEqInAttribute =
        OfUnoracled("resharper_xmldoc_spaces_around_eq_in_attribute");

    public static readonly OptionId XmlDocBlankLineAfterPi = OfUnoracled("resharper_xmldoc_blank_line_after_pi");

    public static readonly OptionId XmlDocLinebreaksInsideTagsForElementsLongerThan =
        OfUnoracled("resharper_xmldoc_linebreaks_inside_tags_for_elements_longer_than");

    // ── Generalized keys ─────────────────────────────────────────────────────────────────────
    // ⚠ These are not read by the formatter and never will be. A generalized key is a way of
    // writing several other keys at once, so the honest implementation is the resolver expanding it
    // into the keys it names (docs/plan/03 § "The option registry"); the formatter then reads only
    // the specific ones. They are listed here because Tier A is a claim about what the tool
    // honours, not about which field a value lands in, and because
    // `EveryImplementedOption_ChangesTheOutputOfItsCorpusFile` is exactly the right question to ask
    // of them: set the generalized key and the output has to move.
    public static readonly OptionId SpaceAfterKeywordsInControlFlowStatements =
        OfGeneralized("resharper_space_after_keywords_in_control_flow_statements");

    public static readonly OptionId SpaceAroundMemberAccessOperator =
        OfGeneralized("resharper_space_around_member_access_operator");

    public static readonly OptionId SpaceAroundTernaryOperator =
        OfGeneralized("resharper_space_around_ternary_operator");

    public static readonly OptionId SpaceBeforeOpenSquareBrackets =
        OfGeneralized("resharper_space_before_open_square_brackets");

    public static readonly OptionId SpaceBetweenSquareBrackets =
        OfGeneralized("resharper_space_between_square_brackets");

    public static readonly OptionId SpaceBetweenMethodCallNameAndOpeningParenthesis =
        OfGeneralized("resharper_space_between_method_call_name_and_opening_parenthesis");

    public static readonly OptionId SpaceBetweenMethodDeclarationNameAndOpenParenthesis =
        OfGeneralized("resharper_space_between_method_declaration_name_and_open_parenthesis");

    public static readonly OptionId GeneralizedIndentSize = OfGeneralized("indent_size");
    public static readonly OptionId GeneralizedIndentStyle = OfGeneralized("indent_style");

    /// <summary>
    ///     ⚠ A space <em>inside</em> the parentheses of all nine control-flow keywords at once, and
    ///     the one entry here that was on record as inert.
    /// </summary>
    /// <remarks>
    ///     ⚠ Its <c>expands</c> list was empty and the registry said "the oracle ignores it at both
    ///     values, while each of the nine <c>space_within_&lt;keyword&gt;_parentheses</c> keys it names
    ///     answers on its own". Asked again it answers on every one of the nine —
    ///     <c>if ( b )</c>, <c>while ( b )</c>, <c>for ( … )</c>, <c>foreach ( … )</c>,
    ///     <c>switch ( b )</c>, <c>catch ( … )</c>, <c>lock ( o )</c>, <c>using ( … )</c>,
    ///     <c>fixed ( … )</c> — and a <c>space_within_if_parentheses = false</c> written after it takes
    ///     the <c>if</c> back on its own, which is the expansion model and not a coincidence. An empty
    ///     <c>expands</c> is the shape a wrong "inert" verdict takes: nothing honours the key, so
    ///     nothing can disprove the verdict either.
    /// </remarks>
    public static readonly OptionId SpaceBetweenParenthesesOfControlFlowStatements =
        OfGeneralized("resharper_space_between_parentheses_of_control_flow_statements");

    // ── Microsoft-compatible spellings of the generalized keys ───────────────────────────────
    // ⚠ Separate registry entries rather than aliases, because they are separate lines in the
    // export and the oracle reads them as two assignments to one ReSharper property: whichever is
    // written later wins. `OptionResolver.Expand` orders them the same way — by position — so the
    // pair agrees under this export, where the `csharp_` spellings sit at lines 27–43 and the
    // `resharper_` ones at 890–978 carrying the same values.
    public static readonly OptionId MsSpaceAfterKeywordsInControlFlowStatements =
        OfGeneralized("csharp_space_after_keywords_in_control_flow_statements");

    public static readonly OptionId MsSpaceBeforeOpenSquareBrackets =
        OfGeneralized("csharp_space_before_open_square_brackets");

    public static readonly OptionId MsSpaceBetweenSquareBrackets =
        OfGeneralized("csharp_space_between_square_brackets");

    public static readonly OptionId MsSpaceBetweenMethodCallNameAndOpeningParenthesis =
        OfGeneralized("csharp_space_between_method_call_name_and_opening_parenthesis");

    public static readonly OptionId MsSpaceBetweenMethodDeclarationNameAndOpenParenthesis =
        OfGeneralized("csharp_space_between_method_declaration_name_and_open_parenthesis");

    /// <summary>Every id above that phase 1 can actually be observed to honour.</summary>
    /// <remarks>
    ///     ⚠ <see cref="Unoracled" /> is subtracted as well as <see cref="Inert" />, and for the opposite
    ///     reason. An inert id is excluded because it changes nothing; an unoracled id is excluded
    ///     because what it changes cannot be checked against the oracle, and this list is what the Tier
    ///     A promotion reads.
    /// </remarks>
    public static ImmutableArray<OptionId> All { get; } =
        [.. Collected.Distinct().Except(Inert).Except(Unoracled).Order()];

    /// <summary>
    ///     The ids phase 1 reads and cannot be observed to honour, each with a reason at its
    ///     declaration.
    /// </summary>
    /// <remarks>
    ///     ⚠ Exposed so that the reason can be checked rather than believed. "Inert" is the sentence a
    ///     key gets when it is honoured vacuously — another rule decides first, or the oracle ignores
    ///     it too — and it is also the sentence an unimplemented key gets when nobody looks. The
    ///     difference is measurable: an inert key produces one output across its whole domain, and a
    ///     key that has quietly become observable produces two.
    /// </remarks>
    public static ImmutableArray<OptionId> ReadButInert { get; } = [.. Inert.Distinct().Order()];

    /// <summary>
    ///     The ids phase 1 reads and honours, and that no oracle fixture can pin.
    /// </summary>
    /// <remarks>
    ///     ⚠ The third shape, and it exists because the second one stopped being true. Until the
    ///     documentation-comment sub-formatter became the default these were <see cref="OfInert" /> —
    ///     "read, and unable to change anything" — which was accurate only because nothing ran them.
    ///     They run on every file now, so "inert" would be a lie, and Tier A would be a different lie:
    ///     Tier A means "pinned by at least one oracle fixture" and <c>jb cleanupcode</c> returns every
    ///     documentation comment exactly as written, so no fixture can ever show it agreeing or
    ///     disagreeing (SK-DIV-0006).
    ///     <para>
    ///         So they stay Tier D and out of <see cref="All" />, and what is checked instead is the
    ///         opposite of the inert claim: an unoracled key must be <em>observable</em>, or it is an
    ///         unimplemented key hiding behind a reason. <c>OptionObservabilityTests</c> asserts it.
    ///     </para>
    /// </remarks>
    public static ImmutableArray<OptionId> ReadButUnoracled { get; } = [.. Unoracled.Distinct().Order()];

    /// <summary>
    ///     ⚠ An option phase 1 reads but whose value it cannot yet make a difference to. No fitting
    ///     pass means <c>max_line_length</c> changes nothing; no tabs in the output means
    ///     <c>tab_width</c> changes nothing; the removal rules win over <c>blank_lines_inside_type</c>
    ///     outright; <c>end_of_line</c> is inert while <c>enforce_line_ending_style</c> is false;
    ///     <c>remove_spaces_on_blank_lines</c> is inert because a blank line is a break followed by a
    ///     break and the writer never puts anything between them (the one place trailing whitespace
    ///     survives is inside a comment's own text, which is never a blank line); and
    ///     <c>space_between_keyword_and_type</c> is inert because a type after a
    ///     keyword is always word-like, so the separation is mandatory whatever the option says.
    ///     <para>
    ///         They are read so the plumbing exists and so the crash snapshot can record them, and they
    ///         stay Tier D, because Tier A is a claim about behaviour and not about wiring.
    ///     </para>
    /// </summary>
    static OptionId OfInert(string key) {
        var id = Of(key);
        Inert.Add(id);
        return id;
    }

    /// <summary>
    ///     A key the formatter honours and that the oracle cannot be asked about.
    /// </summary>
    /// <remarks>
    ///     ⚠ See <see cref="ReadButUnoracled" />. It is not a softer <see cref="Of" />: it is the mark
    ///     that says the evidence for this key is hand-written fixtures and a round-trip property
    ///     rather than a committed <c>.expected.cs</c>, and it keeps the key out of the Tier A claim so
    ///     that "Tier A" keeps meaning one thing.
    /// </remarks>
    static OptionId OfUnoracled(string key) {
        var id = Of(key);
        Unoracled.Add(id);
        return id;
    }

    /// <summary>
    ///     A key the formatter honours without reading: the resolver expands it into the specific keys
    ///     it names, and those are what the rules consult.
    /// </summary>
    /// <remarks>
    ///     ⚠ Declared after every id it expands to, and checked rather than trusted: a generalized key
    ///     none of whose targets is implemented would be a Tier A claim with nothing behind it, which
    ///     is the exact failure mode M3.1 found. At least one target must be implemented and not
    ///     <see cref="OfInert" />; the rest may belong to a component that does not exist yet —
    ///     <c>indent_size</c> also names <c>resharper_xmldoc_indent_size</c>, and Skala's honouring it
    ///     for C# is not made less true by the doc-comment target being pinned differently.
    ///     <para>
    ///         ⚠ <see cref="OfUnoracled" /> targets do not satisfy the requirement either, for the same
    ///         reason <see cref="OfInert" /> ones do not: a generalized key inherits the tier claim of what
    ///         it expands to, and an unoracled target carries no Tier A claim to inherit.
    ///     </para>
    /// </remarks>
    static OptionId OfGeneralized(string key) {
        var id = Of(key);
        var targets = OptionRegistry.Get(id).Expands;
        if (targets.Count == 0) {
            throw new InvalidOperationException(
                $"'{key}' is registered as generalized but expands to nothing. Nothing would honour it."
            );
        }

        if (!targets.Any(target => Collected.Contains(target) && !Inert.Contains(target) && !Unoracled.Contains(target)
            )) {
            throw new InvalidOperationException(
                $"'{key}' expands to [{string.Join(", ", targets.Select(static t => OptionRegistry.Get(t).Key))}] and phase 1 implements none of them. A generalized key is honoured through its targets or not at all."
            );
        }

        return id;
    }

    static OptionId Of(string key) {
        if (!OptionRegistry.TryResolve(key, out var id)) {
            throw new InvalidOperationException(
                $"'{key}' is not in options.json. The formatter may not read an option the registry does not know: the tier report, `skala config explain` and the per-option corpus test all key off the registry."
            );
        }

        Collected.Add(id);
        return id;
    }
}
