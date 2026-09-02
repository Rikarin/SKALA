using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>One fixture file: which rule it is about, and whether the rule should fire on it.</summary>
public sealed record RuleFixture(string RuleId, bool ShouldFire, string Path) {
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public override string ToString() => RuleId + (ShouldFire ? "/+" : "/−") + "/" + Name;
}

/// <summary>
///     The rule unit level: run one analyzer over one file and see what it says.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/16 § R3's shipping bar is not "the rule works". It is
///     <b>
///         zero false positives on the
///         reference corpus, a documented false-positive story, and a "should not fire" fixture set at
///         least as large as the positive one
///     </b> — because the rules most likely to over-fire are exactly
///     the ones with the most value, and a rule that fires 400 times and is right 390 is not ready.
///     <see cref="RuleFixtureTests.EveryRule_HasMoreNegativeFixturesThanPositive" /> is that bar as a
///     test.
/// </remarks>
public static class RuleFixtures {
    public static string Root { get; } = Path.Combine(
        Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(static attribute => attribute.Key == "SkalaRepositoryRoot")
        .Value!,
        "Rules",
        "Rikarin.Skala.Rules.Tests",
        "fixtures"
    );

    public static IReadOnlyList<RuleFixture> All() {
        if (!Directory.Exists(Root)) {
            return [];
        }

        var result = new List<RuleFixture>();
        foreach (var directory in Directory.GetDirectories(Root).OrderBy(static d => d, StringComparer.Ordinal)) {
            var ruleId = Path.GetFileName(directory);
            foreach (var (folder, shouldFire) in new[] { ("positive", true), ("negative", false) }) {
                var path = Path.Combine(directory, folder);
                if (!Directory.Exists(path)) {
                    continue;
                }

                foreach (var file in Directory.GetFiles(path, "*.cs").OrderBy(static f => f, StringComparer.Ordinal)) {
                    result.Add(new RuleFixture(ruleId, shouldFire, file));
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     A compilation over the running framework's reference set, which is what loose mode gives a
    ///     rule and therefore the least the rule may assume.
    /// </summary>
    public static CSharpCompilation Compile(
        string source,
        string path,
        LanguageVersion version = LanguageVersion.Preview
    ) {
        var tree = CSharpSyntaxTree.ParseText(
            SourceText.From(source),
            new CSharpParseOptions(version).WithDocumentationMode(DocumentationMode.Parse),
            path
        );

        return CSharpCompilation.Create(
            "fixtures",
            [tree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: OptIn
            )
        );
    }

    /// <summary>
    ///     ⚠ The rules that ship <c>defaultSeverity: none</c>, turned on for the fixture harness.
    /// </summary>
    /// <remarks>
    ///     A rule that is disabled by default is one Roslyn's severity filter drops before the analyzer's
    ///     diagnostic reaches anybody — so without this, its positive fixtures would prove that the
    ///     filter works and nothing at all about the rule. Turning it on here is the same thing a
    ///     repository does with <c>dotnet_diagnostic.SK7010.severity</c> per path, which is how
    ///     rules.json says the rule is meant to be used.
    /// </remarks>
    static ImmutableDictionary<string, ReportDiagnostic> OptIn { get; } = BuildOptIn();

    static ImmutableDictionary<string, ReportDiagnostic> BuildOptIn() {
        var builder = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>(StringComparer.Ordinal);
        foreach (var rule in RuleCatalog.All) {
            if (!rule.Retired && rule.DefaultSeverity == RuleSeverity.None) {
                builder[rule.Id] = ReportDiagnostic.Warn;
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    ///     Every analyzer this repository ships, as one list.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>One list, because a second one would go quiet rather than fail.</b> Both
    ///     <c>RuleFixtureTests</c> and <c>CorpusCrashTests</c> run this set and assert no
    ///     <c>AD0001</c>. An analyzer missing from a private copy of the list is an analyzer whose
    ///     crash that harness cannot see, and the harness still reports success — the same
    ///     "answers confidently when it did not run" shape as #279 and #295. Adding an analyzer here
    ///     enrols it in every crash sweep at once.
    /// </remarks>
    public static ImmutableArray<DiagnosticAnalyzer> AllAnalyzers { get; } = [
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
        new ExtensionMethodOnObjectAnalyzer(), new ThreadSleepInTestAnalyzer(),
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
        new EnumConstraintAnalyzer(), new ExceptionNameAnalyzer(), new TypeKindSuffixAnalyzer(),
        new EmptyTypeAnalyzer(),
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
        new XmlSignatureAnalyzer(),
        new PredictableInitializationVectorAnalyzer(), new AsymmetricKeySizeAnalyzer(),
        new SingleUseTemporaryAnalyzer(), new SplitDeclarationAndAssignmentAnalyzer(),
        new LocalFunctionBeforeJumpAnalyzer(), new SharedBranchTailAnalyzer(),
        new InvalidConstantIndexOrRangeAnalyzer(), new UnchangingLoopConditionAnalyzer(),
        new SingleIterationLoopAnalyzer(), new IndexOfComparedToPositiveAnalyzer(),
        new DeadConditionalCallAnalyzer(), new UnsafeAccessorTargetAnalyzer(),
        new PartiallyCheckedOperatorAnalyzer(),
        new WithExpressionRewritesAllAnalyzer(), new MalformedRegexPatternAnalyzer(),
        new DeferredArgumentCheckAnalyzer(),
        new UngroupedExtensionMethodsAnalyzer(), new ConstantForwardingOverloadAnalyzer(),
        new ReflectiveTypeTestAnalyzer(), new MergeableTryAnalyzer(),
        new ReorderedAnonymousTypeAnalyzer(), new MergedPropertyPatternAnalyzer(),
        new SqlFragmentsRunTogetherAnalyzer(), new CommandParameterNotSuppliedAnalyzer(),
        new AssemblyLoadedOutsideItsContextAnalyzer(), new MistakenTypeArgumentAnalyzer(),
    ];

    public static ImmutableArray<MetadataReference> References { get; } = Build();

    static ImmutableArray<MetadataReference> Build() {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string assemblies) {
            foreach (var path in assemblies.Split(Path.PathSeparator)) {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        builder.Add(MetadataReference.CreateFromFile(path));
                    } catch (BadImageFormatException) {
                        // ⚠ Deliberate: the trusted-platform list carries native and resource-only
                        // `.dll` files alongside the managed ones, and the only way to tell them apart
                        // is to try. A reference that will not load is one the fixtures do not need.
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Every diagnostic Skala's own analyzers produce for one compilation.</summary>
    public static ImmutableArray<Diagnostic> Analyze(
        CSharpCompilation compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellation
    ) =>
        compilation
            .WithAnalyzers(
                analyzers,
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([], new FixtureOptionsProvider()),
                    null,
                    false,
                    false,
                    true
                )
            )
            .GetAnalyzerDiagnosticsAsync(cancellation)
            .GetAwaiter()
            .GetResult();

    /// <summary>Fixture-local EditorConfig values, written as leading // analyzer-option: key = value comments.</summary>
    internal sealed class FixtureOptionsProvider : AnalyzerConfigOptionsProvider {
        public override AnalyzerConfigOptions GlobalOptions => new FixtureOptions(string.Empty);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            new FixtureOptions(tree.GetText().ToString());

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    sealed class FixtureOptions : AnalyzerConfigOptions {
        readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public FixtureOptions(string source) {
            foreach (var line in SourceText.From(source).Lines) {
                var trimmed = line.ToString().Trim();
                if (!trimmed.StartsWith("//", StringComparison.Ordinal)) {
                    break;
                }

                const string prefix = "// analyzer-option:";
                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) {
                    continue;
                }

                var assignment = trimmed[prefix.Length..];
                var separator = assignment.IndexOf('=');
                if (separator > 0) {
                    values[assignment[..separator].Trim()] = assignment[(separator + 1)..].Trim();
                }
            }
        }

        public override IEnumerable<string> Keys => values.Keys;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
