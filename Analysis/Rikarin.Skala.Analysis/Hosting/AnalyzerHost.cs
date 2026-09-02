using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Cleanup;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using Rikarin.Skala.Rules.Security;
using Rikarin.Skala.Rules.TestQuality;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Analysis.Hosting;

/// <summary>What one analyzer cost, for <c>--profile</c>.</summary>
/// <remarks>
///     docs/plan/13 § "Analysis": "<c>--profile</c> surfaces <c>logAnalyzerExecutionTime</c> output
///     ranked by cost. This is how a rule that is accidentally O(n²) in a method's statement count gets
///     found, and every Skala rule's cost is reviewed against it before release."
/// </remarks>
public sealed record AnalyzerCost(string Analyzer, ImmutableArray<string> Rules, TimeSpan Elapsed);

/// <summary>What one compilation's analysis produced.</summary>
public sealed record AnalysisOutcome(
    ImmutableArray<Finding> Findings,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    bool Partial,
    ImmutableArray<AnalyzerCost> Costs = default) {
    /// <summary>⚠ A <c>default</c> ImmutableArray throws on enumeration; profiling is opt-in.</summary>
    public ImmutableArray<AnalyzerCost> Costs { get; init; } = Costs.IsDefault ? [] : Costs;
}

/// <summary>
///     <c>CompilationWithAnalyzers</c>, configured the way docs/plan/07 § "Running analyzers" says.
/// </summary>
/// <remarks>
///     Four settings, each of which is a decision:
///     <list type="bullet">
///         <item>
///             ⚠ <c>reportSuppressedDiagnostics: true</c> — Skala needs to distinguish "not found" from "found
///             and suppressed by <c>#pragma</c>", because a baseline has to see what was suppressed and because
///             a suppression audit is the SonarQube feature worth keeping.
///         </item>
///         <item>
///             ⚠ <c>onAnalyzerException</c> records <c>SK9030</c> and never aborts. A third-party analyzer that
///             throws on one syntax shape must not be able to turn a CI gate red for unrelated reasons — or,
///             worse, green by aborting the run early.
///         </item>
///         <item>
///             Compiler diagnostics are part of the report, so one command answers "does this build and is it clean".
///         </item>
///         <item>
///             <c>concurrentAnalysis: true</c>, with determinism restored by sorting afterwards, never by serialising.
///         </item>
///     </list>
/// </remarks>
public static class AnalyzerHost {
    /// <summary>Skala's own analyzers. One instance set, reused across compilations (ADR-006).</summary>
    public static ImmutableArray<DiagnosticAnalyzer> Own { get; } = [
        new FieldBackedPropertyAnalyzer(), new SearchValuesAnalyzer(), new FrozenDictionaryAnalyzer(),
        new ReadonlyStructMutationAnalyzer(), new ParamsSpanOverloadAnalyzer(),
        new DedicatedLockAnalyzer(), new FloatingPointEqualityAnalyzer(), new ConstrainedBoxingAnalyzer(),
        new LargeStructArgumentAnalyzer(), new CommentedCodeAnalyzer(),
        new SharedLazyAnalyzer(), new HotPathLinqAnalyzer(), new LoopClosureAnalyzer(),
        new ImmediateMaterializationAnalyzer(), new NondeterministicAssertionAnalyzer(),
        new ReturningSwitchExpressionAnalyzer(), new ListPatternAnalyzer(), new Utf8LiteralAnalyzer(),
        new ConstantRangeComparisonAnalyzer(), new SelfPropertyOperationAnalyzer(),
        new RelationalPatternAnalyzer(), new PropertyPatternAnalyzer(), new SpanDecodingAnalyzer(),
        new ConfigureAwaitAnalyzer(), new FileLengthAnalyzer(),
        new FileScopedNamespaceAnalyzer(), new NullPatternAnalyzer(), new ThrowIfNullAnalyzer(),
        new NullCoalescingAssignmentAnalyzer(),
        new CountPropertyAnalyzer(), new EnumGetValuesAnalyzer(), new DiscardedExceptionAnalyzer(),
        new RethrowAnalyzer(), new CollectionModifiedAnalyzer(), new EnumSwitchExhaustivenessAnalyzer(),
        new DiscardedPureResultAnalyzer(), new IncompleteEqualityContractAnalyzer(), new CapturedLoopVariableAnalyzer(),
        new ImplicitStringCultureAnalyzer(), new InheritedValueTypeEqualsAnalyzer(),
        new EmptyCatchAnalyzer(), new InterpolatedLoggerMessageAnalyzer(),
        new WrongArgumentNameAnalyzer(),
        new AsyncVoidAnalyzer(), new BlockingOnAsyncAnalyzer(), new CancellationTokenForwardingAnalyzer(),
        new FireAndForgetTaskAnalyzer(), new TaskReturnedFromUsingAnalyzer(), new UndisposedLocalAnalyzer(),
        new OwnedDisposableFieldAnalyzer(),
        new SynchronousAsyncDisposalAnalyzer(), new RedundantDisposeAnalyzer(), new UsingResourceInitializerAnalyzer(),
        new UsingVariableReturnedAnalyzer(), new NullTaskReturnAnalyzer(),
        new SpinLockInReadonlyFieldAnalyzer(), new MetricsAnalyzer(),
        new WhereBeforeOperatorAnalyzer(), new AbstractTypeConstructorAnalyzer(),
        new ExtensionMethodOnObjectAnalyzer(), new EnumConstraintAnalyzer(), new ExceptionNameAnalyzer(),
        new TypeKindSuffixAnalyzer(), new EmptyTypeAnalyzer(), new ThreadSleepInTestAnalyzer(),
        new TodoWithoutIssueAnalyzer(), new PragmaWithoutJustificationAnalyzer(),
        new SuppressMessageWithoutJustificationAnalyzer(), new ObsoleteWithoutMessageAnalyzer(),
        new ExcludeFromCodeCoverageWithoutJustificationAnalyzer(), new EmptySuppressionRegionAnalyzer(),
        new EmptyRegionAnalyzer(), new UnstructuredGotoAnalyzer(),
        new SkippedTestWithoutReasonAnalyzer(),
        new SqlInjectionAnalyzer(), new ProcessArgumentInjectionAnalyzer(), new WeakCipherAnalyzer(),
        new CertificateValidationAnalyzer(), new XmlExternalEntityAnalyzer(), new RegexTimeoutAnalyzer(),
        new CollectionExpressionAnalyzer(), new UsingDeclarationAnalyzer(), new TypePatternAnalyzer(),
        new NullConditionalAssignmentAnalyzer(), new DictionaryLookupAnalyzer(),
        new NanComparisonAnalyzer(), new UnusedValueParameterAnalyzer(),
        new RedundantSuppressFinalizeAnalyzer(), new StackAllocInLoopAnalyzer(),
        new EscapedKeywordAnalyzer(),
        new NullableShortFormAnalyzer(), new CompoundAssignmentAnalyzer(), new MergeableIfAnalyzer(),
        new ForAsWhileAnalyzer(), new NullOrEmptyCheckAnalyzer(),
        new UnusedOutVariableAnalyzer(), new WiderForeachVariableTypeAnalyzer(),
        new NoncapturingLambdaAnalyzer(), new StatelessPrivateMethodAnalyzer(), new ImmutableStructAnalyzer(),
        new RedundantCapacityArgumentAnalyzer(), new ForcedGarbageCollectionAnalyzer(),
        new NotImplementedMemberAnalyzer(), new ProcessExitAnalyzer(), new LoggedAndRethrownAnalyzer(),
        new ConsoleInsteadOfLoggerAnalyzer(),
        new CollectionOwnMethodAnalyzer(), new DictionaryKeyRelookupAnalyzer(),
        new SubstringBeforeSearchAnalyzer(), new ConcurrentDictionaryMemberAnalyzer(),
        new SortBeforeFilterAnalyzer(),
        new GlobalNamespaceTypeAnalyzer(),
        new ReadonlyMutableFieldAnalyzer(),
        new AbstractTypeWithoutAbstractionAnalyzer(),
        new PrivateConstructorOnlyAnalyzer(),
        new PublicConstantAnalyzer(),
        new UndisposedOwnedFieldAnalyzer(), new DisposeAsyncBaseCallAnalyzer(),
        new RefStructOwnedDisposableAnalyzer(), new AsyncIteratorNotEnumeratedAnalyzer(),
        new AsyncOnlyToAwaitAnalyzer(),
        new EmptyInitializerAnalyzer(), new RedundantStringCallAnalyzer(),
        new RedundantArgumentAnalyzer(), new RedundantSyntaxAnalyzer(),
        new RedundantCastAnalyzer(),
        new MissingTestClassAttributeAnalyzer(), new EmptyTestClassAnalyzer(),
        new SwappedAssertionArgumentsAnalyzer(),
        new DuplicatedBaseDocumentationAnalyzer(), new UndocumentedNonPublicMemberAnalyzer(),
        new RedundantControlFlowAnalyzer(), new IneffectiveModifierAnalyzer(),
        new RedundantNullableDirectiveAnalyzer(), new RedundantQualifierAnalyzer(),
        new RedundantDiscardDesignationAnalyzer(),
        new RedundantDeclarationAnalyzer(),
        new TestAndCastPatternAnalyzer(), new PatternSimplificationAnalyzer(),
        new MergedConditionalAccessAnalyzer(), new DiscardAssignmentAnalyzer(),
        new InlineOutVariableAnalyzer(),
        new LockOverSynchronizationPrimitiveAnalyzer(), new NonAtomicVolatileUpdateAnalyzer(),
        new DoubleCheckedLockingAnalyzer(), new LockOrderAnalyzer(),
        new InconsistentlySynchronizedFieldAnalyzer(),
        new InheritanceDepthAnalyzer(), new TypeCouplingAnalyzer(), new NestedConditionalAnalyzer(),
        new RepeatedStringLiteralAnalyzer(),
        new IntegerDivisionFractionAnalyzer(), new FixedResultArithmeticAnalyzer(), new MaskedShiftCountAnalyzer(),
        new NonnegativeSizeComparisonAnalyzer(), new SignedModulusEqualityAnalyzer(),
        new ThrowingFinalizerAnalyzer(), new ThrowInFinallyAnalyzer(),
        new CaughtNullReferenceAnalyzer(), new DiscardedCaughtExceptionAnalyzer(),
        new IneffectiveThreadStaticAnalyzer(), new PureAttributeOnVoidAnalyzer(),
        new DebuggerDisplayMissingMemberAnalyzer(), new DuplicatedAttributeAnalyzer(),
        new UnintendedReferenceComparisonAnalyzer(),
        new BaseEqualityCallAnalyzer(),
        new UncomparedHashMemberAnalyzer(),
        new MutableHashMemberAnalyzer(),
        new InconsistentEqualityMembersAnalyzer(),
        new AssignmentInConditionAnalyzer(), new IdenticalOperandsAnalyzer(), new RepeatedConditionAnalyzer(),
        new MisleadingOperatorSequenceAnalyzer(), new NonShortCircuitBooleanAnalyzer(),
        new ConstantReturningMethodAnalyzer(), new DerivedTypeTestOnThisAnalyzer(),
        new NullSequenceReturnAnalyzer(), new AsyncSuffixAnalyzer(),
        new DuplicateInitializerKeyAnalyzer(), new SelfCollectionArgumentAnalyzer(),
        new OverwrittenElementAnalyzer(), new EmptyCollectionLoopAnalyzer(),
        new IndexFromEndAnalyzer(), new NameofExpressionAnalyzer(), new EscapeFreeStringLiteralAnalyzer(),
        new InterpolatedStringFormAnalyzer(), new UnsignedRightShiftAnalyzer(),
        new TupleDeconstructionAnalyzer(), new WithExpressionCopyAnalyzer(),
        new RedundantSpreadElementAnalyzer(), new CachedEmptyInstanceAnalyzer(),
        new LogTemplateArgumentCountAnalyzer(), new LogTemplateDuplicatePropertyAnalyzer(),
        new InvisibleCharacterAnalyzer(), new CaughtExceptionNotLoggedAnalyzer(),
        new LoggerForAnotherTypeAnalyzer(),
        new PlainEnumBitwiseAnalyzer(), new AlwaysSucceedingAsAnalyzer(),
        new ImplicitStringSearchCultureAnalyzer(), new InvariantCultureComparisonAnalyzer(),
        new PlatformDependentPathComparisonAnalyzer(), new QueryableDegradedToEnumerableAnalyzer(),
        new SortWithoutOrderingAnalyzer(),
        new ToStringReturnsNullAnalyzer(), new InertNullSuppressionAnalyzer(),
        new NullableLocalNeverNullAnalyzer(), new NullForgivenServiceResolutionAnalyzer(),
        new ComputedPropertyAnalyzer(), new PrivateAutoPropertyAnalyzer(), new TupleLiteralAnalyzer(),
        new CastInDeclarationAnalyzer(), new NullableAnnotationSyntaxAnalyzer(),
        new AsyncVoidThrowAnalyzer(), new UncancellableAsyncMethodAnalyzer(),
        new AsyncVoidLambdaAnalyzer(),
        new OverriddenParameterDefaultAnalyzer(), new RestatedCallerInfoArgumentAnalyzer(),
        new OverwrittenParameterAnalyzer(), new CrosswiseArgumentOrderAnalyzer(),
        new StaticClockReadAnalyzer(), new UnspecifiedDateTimeKindAnalyzer(),
        new ImplicitDateParseCultureAnalyzer(), new WallClockElapsedAnalyzer(),
        new SideEffectInAssertionAnalyzer(),
        new OfTypeChainAnalyzer(), new RedundantSequenceCallAnalyzer(),
        new IndexerOverElementAtAnalyzer(), new ForeachOverIndexedForAnalyzer(),
        new LoopFilterAsQueryAnalyzer(),
        new ForwardStaticInitializerAnalyzer(), new UnassignedGetOnlyPropertyAnalyzer(),
        new MismatchedBackingFieldAnalyzer(), new UnimplementedPartialMethodAnalyzer(),
        new InstanceWriteToStaticAnalyzer(),
        new UnreleasedLockAnalyzer(), new IneffectiveLockTargetAnalyzer(),
        new ConstructorPublishesThisAnalyzer(),
        new ForeachElementDowncastAnalyzer(), new GetTypeOnATypeAnalyzer(),
        new TypeComparedByNameAnalyzer(), new StaticMemberViaDerivedTypeAnalyzer(),
        new HiddenBaseInterfaceOverloadAnalyzer(),
        new InvariantTypeParameterAnalyzer(), new CallerInfoParameterOrderAnalyzer(),
        new WriteOnlyLocalCollectionAnalyzer(),
        new StructKeyWithoutEqualityAnalyzer(), new ReadonlyReceiverMutationAnalyzer(),
        new SpanReferenceComparisonAnalyzer(), new ImmutableArrayCollectionInitializerAnalyzer(),
        new MutableCapturedPrimaryParameterAnalyzer(),
        new UndeclaredDisposeAnalyzer(), new ShortLivedHttpClientAnalyzer(), new DangerousHandleAnalyzer(),
        new CopyingPropertyAnalyzer(), new UnreadStringBuilderAnalyzer(),
        new OverwrittenFieldInitializerAnalyzer(), new AnonymousUnsubscriptionAnalyzer(),
        new ConditionalInvocationSideEffectAnalyzer(),
        new MisleadingBodyIndentationAnalyzer(), new VariableLengthHexEscapeAnalyzer(),
        new ForgivenIsOperandAnalyzer(), new NegatedEmptyPatternAnalyzer(),
        new UnparenthesisedPrecedenceMixAnalyzer(),
        new SingleUseTemporaryAnalyzer(), new SplitDeclarationAndAssignmentAnalyzer(),
        new LocalFunctionBeforeJumpAnalyzer(), new SharedBranchTailAnalyzer(),
    ];

    /// <summary>
    ///     The rules that cannot run under a given load mode, with the reason, for the SARIF.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/07 § loose: the mode "is honest, because the SARIF says <c>loadMode: loose</c>
    ///     and lists the rules that were skipped". A report that omits this is a report whose clean
    ///     result means something different from another clean result.
    /// </remarks>
    public static ImmutableArray<SkippedRule> SkippedFor(LoadMode mode) {
        if (mode != LoadMode.Loose) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<SkippedRule>();
        builder.Add(
            new SkippedRule(
                RoslynCodeStyle.NamingDiagnosticId,
                "requires a semantic model; --load=loose has no project (docs/plan/07 § loose)"
            )
        );
        foreach (var rule in RuleCatalog.All) {
            if (!rule.Retired && rule.RequiresSemantics) {
                builder.Add(
                    new SkippedRule(
                        rule.Id,
                        "requires a semantic model; --load=loose has no project (docs/plan/07 § loose)"
                    )
                );
            }
        }

        return builder.ToImmutable();
    }

    public static AnalysisOutcome Run(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        CancellationToken cancellation,
        bool profile = false
    ) =>
        Execute(unit, options, hosted, mode, null, profile, cancellation);

    /// <summary>
    ///     The warm path: run the analyzers over only the trees whose cache key moved.
    /// </summary>
    /// <remarks>
    ///     ⚠ Syntax <em>and</em> semantic actions, per tree. Running only
    ///     <c>GetAnalyzerSyntaxDiagnosticsAsync</c> would silently drop every semantic rule from a warm
    ///     run, so a file would produce different findings depending on whether the cache was cold —
    ///     which is the cache lying, in the direction that looks like progress.
    /// </remarks>
    public static AnalysisOutcome RunForTrees(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        IReadOnlyList<SyntaxTree> trees,
        CancellationToken cancellation,
        bool profile = false
    ) =>
        Execute(unit, options, hosted, mode, trees, profile, cancellation);

    /// <summary>The rule set a load mode allows, as instantiated analyzers.</summary>
    public static ImmutableArray<DiagnosticAnalyzer> EnabledFor(
        LoadMode mode,
        ImmutableArray<DiagnosticAnalyzer> hosted
    ) =>
        Select(mode, hosted);

    static AnalysisOutcome Execute(
        CompilationUnit unit,
        AnalyzerOptions options,
        ImmutableArray<DiagnosticAnalyzer> hosted,
        LoadMode mode,
        IReadOnlyList<SyntaxTree>? trees,
        bool profile,
        CancellationToken cancellation
    ) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var analyzers = Select(mode, hosted);
        if (analyzers.IsEmpty) {
            return new AnalysisOutcome([], diagnostics.ToImmutable(), false);
        }

        var withAnalyzers = unit.Compilation.WithAnalyzers(
            analyzers,
            new CompilationWithAnalyzersOptions(
                options,
                (exception, analyzer, diagnostic) => {
                    // ⚠ Recorded and continued, never rethrown. See the type's remarks.
                    var name = analyzer.GetType().FullName ?? analyzer.GetType().Name;
                    lock (failed) {
                        if (!failed.Add(name)) {
                            return;
                        }
                    }

                    lock (diagnostics) {
                        diagnostics.Add(
                            new SkalaDiagnostic(
                                RuleIds.AnalyzerThrew,
                                SkalaSeverity.Warning,
                                $"analyzer '{name}' threw on rule '{diagnostic.Id}' and was disabled for the rest of the run: {exception.Message}",
                                diagnostic.Location.SourceTree?.FilePath ?? unit.Name
                            )
                        );
                    }
                },
                true,
                true,
                true
            )
        );

        ImmutableArray<Diagnostic> produced;
        var partial = false;
        var costs = ImmutableArray<AnalyzerCost>.Empty;
        try {
            if (trees is not null) {
                // ⚠ The warm path is measured on the warm path. `ForTrees` already goes through
                // `GetAnalysisResultAsync`, so profiling it costs nothing and changes nothing --
                // which matters, because an instrument that quietly measured the *cold* path when
                // asked about a warm run would report the one number the budget is not about.
                produced = ForTrees(unit, withAnalyzers, trees, analyzers, profile, ref costs, cancellation);
            } else if (profile) {
                // ⚠ `GetAnalysisResultAsync` rather than `GetAllDiagnosticsAsync`, and the reason
                // is not style. Roslyn returns its analyzer driver to a pool when the run finishes,
                // and the execution times go back with it, so `GetAnalyzerTelemetryInfoAsync` called
                // afterwards reports 0.0 ms for every analyzer -- which looks exactly like a fast
                // run. `AnalysisResult` captures the telemetry before the driver is released. The
                // first `--profile` output ever produced was nineteen analyzers at 0.0 ms, and it
                // was entirely believable.
                var result = withAnalyzers.GetAnalysisResultAsync(cancellation).GetAwaiter().GetResult();
                costs = Measure(result, analyzers);

                // ⚠ `AnalysisResult` carries only analyzer diagnostics; `GetAllDiagnosticsAsync`
                // also folds in the compiler's, which the loop below expects to see.
                produced = [.. result.GetAllDiagnostics(), .. unit.Compilation.GetDiagnostics(cancellation)];
            } else {
                produced = withAnalyzers.GetAllDiagnosticsAsync(cancellation).GetAwaiter().GetResult();
            }
        } catch (OperationCanceledException) {
            // ⚠ Ctrl-C prints what was found so far, marked partial (docs/plan/07 § "Cancellation").
            return new AnalysisOutcome([], diagnostics.ToImmutable(), true);
        }

        var findings = ImmutableArray.CreateBuilder<Finding>();

        // ⚠ One semantic model per tree, reused. The enclosing symbol is the fingerprint's third
        // term and needs a model; building a fresh one per finding turns a file with forty findings
        // into forty binds of the same tree.
        var models = new Dictionary<SyntaxTree, SemanticModel>();

        foreach (var diagnostic in produced) {
            // ⚠ In loose mode the compiler's own diagnostics are dropped, and it is not a
            // convenience. There is no project, so half the references are missing and CS0246 is
            // the expected state rather than a finding; reporting them would bury the rules the
            // mode exists to run under a few hundred complaints about the user's own code being
            // broken. Roslyn will not let an *error* be suppressed through
            // specificDiagnosticOptions, so the filter has to be here.
            if (mode == LoadMode.Loose && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal)) {
                continue;
            }

            // ⚠ The per-descriptor half of the loose-mode filter; see `Select`. An analyzer that
            // reports both semantic and syntactic rules runs, and the semantic ones are dropped
            // here — so what a loose run reports is exactly what `SkippedFor` says it reports.
            if (mode == LoadMode.Loose && RuleCatalog.Find(diagnostic.Id) is { RequiresSemantics: true }) {
                continue;
            }

            if (Convert(diagnostic, unit, models) is { } finding) {
                findings.Add(finding);
            }
        }

        partial |= diagnostics.Count > 0;
        return new(
            findings.ToImmutable(),
            diagnostics.ToImmutable(),
            partial,
            costs
        );
    }

    /// <summary>
    ///     What each analyzer cost, taken off the result rather than asked for afterwards.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>logAnalyzerExecutionTime: true</c> had been set on every run since M5 and nothing
    ///     ever read it, so doc 13's promise that "every Skala rule's cost is reviewed against it
    ///     before release" had no instrument behind it. This is that instrument.
    /// </remarks>
    static ImmutableArray<AnalyzerCost> Measure(
        AnalysisResult result,
        ImmutableArray<DiagnosticAnalyzer> analyzers
    ) {
        var builder = ImmutableArray.CreateBuilder<AnalyzerCost>();
        foreach (var analyzer in analyzers) {
            if (!result.AnalyzerTelemetryInfo.TryGetValue(analyzer, out var telemetry)) {
                continue;
            }

            builder.Add(
                new AnalyzerCost(
                    analyzer.GetType().Name,
                    [.. analyzer.SupportedDiagnostics.Select(static descriptor => descriptor.Id)],
                    telemetry.ExecutionTime
                )
            );
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<Diagnostic> ForTrees(
        CompilationUnit unit,
        CompilationWithAnalyzers withAnalyzers,
        IReadOnlyList<SyntaxTree> trees,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        bool profile,
        ref ImmutableArray<AnalyzerCost> costs,
        CancellationToken cancellation
    ) {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        var measured = new List<AnalyzerCost>();
        foreach (var tree in trees) {
            var syntax = withAnalyzers.GetAnalysisResultAsync(tree, cancellation).GetAwaiter().GetResult();
            builder.AddRange(syntax.GetAllDiagnostics());

            var model = unit.Compilation.GetSemanticModel(tree);
            var semantic = withAnalyzers
                .GetAnalysisResultAsync(model, null, cancellation)
                .GetAwaiter()
                .GetResult();
            builder.AddRange(semantic.GetAllDiagnostics());

            if (profile) {
                // ⚠ Both halves. A warm run pays for the syntax actions and the semantic actions
                // separately, and a profile showing only one of them would understate every
                // semantic rule -- which is every rule this milestone added.
                measured.AddRange(Measure(syntax, analyzers));
                measured.AddRange(Measure(semantic, analyzers));
            }

            // ⚠ The compiler's own diagnostics for this tree, so that a warm run answers "does this
            // build and is it clean" the same way a cold one does.
            builder.AddRange(model.GetDiagnostics(null, cancellation));
        }

        costs = [.. measured];
        return builder.ToImmutable();
    }

    static ImmutableArray<DiagnosticAnalyzer> Select(LoadMode mode, ImmutableArray<DiagnosticAnalyzer> hosted) {
        if (mode != LoadMode.Loose) {
            return [.. Own, .. hosted];
        }

        // ⚠ In loose mode only the rules that declare no need for semantics run. A third-party
        // analyzer declares nothing Skala can read, so it does not run either: an analyzer answering
        // "no finding" because a symbol did not resolve is worse than an analyzer that did not run,
        // because only one of the two says so.
        //
        // ⚠ The filter is <b>per descriptor, not per analyzer</b>, and the difference is a whole
        // rule category. M6's metrics arrive as one analyzer reporting seven rules — one walk of the
        // member rather than seven — and only <c>SK7001</c> needs semantics, for the control-flow
        // graph. Dropping the analyzer because one of its seven descriptors needs a model would
        // silence the other six under <c>--load=loose</c> while <see cref="SkippedFor"/> named only
        // the one, which is precisely the "clean report that means two different things" that
        // docs/plan/07 § loose exists to prevent. So the analyzer runs and
        // <see cref="Execute"/> drops the findings of the rules that could not honestly answer.
        var builder = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var analyzer in Own) {
            foreach (var descriptor in analyzer.SupportedDiagnostics) {
                if (RuleCatalog.Find(descriptor.Id) is not { RequiresSemantics: true }) {
                    builder.Add(analyzer);
                    break;
                }
            }
        }

        return builder.ToImmutable();
    }

    static Finding? Convert(
        Diagnostic diagnostic,
        CompilationUnit unit,
        Dictionary<SyntaxTree, SemanticModel> models
    ) {
        var tree = diagnostic.Location.SourceTree;
        if (tree is null) {
            return null;
        }

        var path = Path.GetFullPath(tree.FilePath);

        // ⚠ Analysed, never reported on. A diagnostic in a file the user cannot edit is noise, and
        // the generated file is in the compilation because leaving it out changes what the semantic
        // model says about everything else.
        if (!unit.ReportablePaths.Contains(path)) {
            return null;
        }

        var span = diagnostic.Location.GetLineSpan();
        var textSpan = diagnostic.Location.SourceSpan;

        return new() {
            RuleId = diagnostic.Id,
            Severity = Severity(diagnostic),
            Message = diagnostic.GetMessage(CultureInfo.InvariantCulture),
            Path = path,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            EndLine = span.EndLinePosition.Line + 1,
            EndColumn = span.EndLinePosition.Character + 1,
            Start = textSpan.Start,
            Length = textSpan.Length,
            Fix = ReadFix(diagnostic, path),
            FixIsSafe = RuleCatalog.Find(diagnostic.Id) is { FixIsSafe: true },
            TargetFrameworks = unit.TargetFramework.Length == 0 ? [] : [unit.TargetFramework],
            Suppression = diagnostic.IsSuppressed ? SuppressionKind.Pragma : SuppressionKind.None,
            EnclosingSymbol = EnclosingSymbol(unit, tree, textSpan.Start, models),
            Snippet = Snippet(tree, textSpan)
        };
    }

    /// <summary>
    ///     The display string of the symbol a finding sits in — the fingerprint's third term.
    /// </summary>
    /// <remarks>
    ///     docs/plan/09 § "The fingerprint": <c>Vixen.Core.Foo.Bar(int, string)</c>, "stable across file
    ///     moves".
    ///     <para>
    ///         ⚠ A lambda or a local function reports its <em>containing</em> member instead of itself.
    ///         Roslyn's display string for an anonymous function contains its position in the file, so a
    ///         fingerprint built on it would move whenever anything above it moved — which is the one
    ///         failure this term exists to prevent, reintroduced through the back door.
    ///     </para>
    ///     <para>
    ///         ⚠ Empty rather than throwing when the model cannot be built. A finding with no enclosing
    ///         symbol still gets a fingerprint; it is simply a weaker one, which is better than no finding.
    ///     </para>
    /// </remarks>
    static string EnclosingSymbol(
        CompilationUnit unit,
        SyntaxTree tree,
        int position,
        Dictionary<SyntaxTree, SemanticModel> models
    ) {
        try {
            if (!models.TryGetValue(tree, out var model)) {
                model = unit.Compilation.GetSemanticModel(tree);
                models[tree] = model;
            }

            var symbol = model.GetEnclosingSymbol(position);
            while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }) {
                symbol = symbol.ContainingSymbol;
            }

            return symbol?.ToDisplayString() ?? string.Empty;
        } catch (ArgumentException) {
            // A position outside the tree, which can happen for a diagnostic whose location was
            // mapped through a #line directive. Not worth failing a run over.
            return string.Empty;
        }
    }

    /// <summary>
    ///     The finding's own span, whitespace collapsed — the fingerprint's second term.
    /// </summary>
    /// <remarks>
    ///     ⚠ Bounded. A finding whose span is a whole 4 000-line type would otherwise put 4 000 lines
    ///     into every fingerprint computation and into the baseline's memory; the leading window is
    ///     enough to identify it and the ordinal disambiguates what is left.
    /// </remarks>
    static string Snippet(SyntaxTree tree, TextSpan span) {
        const int limit = 400;
        var text = tree.GetText();
        if (span.Start < 0 || span.End > text.Length) {
            return string.Empty;
        }

        var bounded = span.Length <= limit ? span : new TextSpan(span.Start, limit);
        return Reporting.Fingerprints.Normalize(text.ToString(bounded));
    }

    /// <summary>Unpacks the text edits a Skala rule attached to its diagnostic.</summary>
    static ImmutableArray<FixEdit> ReadFix(Diagnostic diagnostic, string path) {
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || count <= 0) {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<FixEdit>(count);
        for (var i = 0; i < count; i++) {
            if (!diagnostic.Properties.TryGetValue(FixEdits.StartKey(i), out var startText)
                || !diagnostic.Properties.TryGetValue(FixEdits.LengthKey(i), out var lengthText)
                || !diagnostic.Properties.TryGetValue(FixEdits.TextKey(i), out var text)
                || !int.TryParse(startText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)) {
                return [];
            }

            builder.Add(new FixEdit(path, start, length, text ?? string.Empty));
        }

        return builder.ToImmutable();
    }

    static SkalaSeverity Severity(Diagnostic diagnostic) =>
        diagnostic.Severity switch {
            DiagnosticSeverity.Error => SkalaSeverity.Error,
            DiagnosticSeverity.Warning => SkalaSeverity.Warning,
            DiagnosticSeverity.Info => SkalaSeverity.Info,
            _ => SkalaSeverity.Hidden
        };

    /// <summary>
    ///     Merges near-duplicate findings from a multi-targeted build.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/07 § "Multi-targeting": merged on <c>(ruleId, file, line, column, message)</c>,
    ///     with the target-framework list carried as a property, "so a finding that only occurs under
    ///     one target is visibly a one-target finding". Dropping the list would make the two cases
    ///     indistinguishable, which is the whole reason the merge is allowed at all.
    /// </remarks>
    public static ImmutableArray<Finding> Merge(IEnumerable<Finding> findings) {
        var order = new List<(string, string, int, int, string)>();
        var merged = new Dictionary<(string, string, int, int, string), Finding>();

        foreach (var finding in findings) {
            var key = finding.MergeKey;
            if (merged.TryGetValue(key, out var existing)) {
                var frameworks = existing.TargetFrameworks;
                foreach (var framework in finding.TargetFrameworks) {
                    if (!frameworks.Contains(framework)) {
                        frameworks = frameworks.Add(framework);
                    }
                }

                merged[key] = existing with { TargetFrameworks = frameworks };
                continue;
            }

            order.Add(key);
            merged[key] = finding;
        }

        var builder = ImmutableArray.CreateBuilder<Finding>(order.Count);
        foreach (var key in order) {
            var finding = merged[key];
            builder.Add(finding with { TargetFrameworks = [.. finding.TargetFrameworks.Sort(StringComparer.Ordinal)] });
        }

        return builder.ToImmutable();
    }
}
