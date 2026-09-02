using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Cleanup;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Modernization;
using Rikarin.Skala.Rules.Performance;
using Rikarin.Skala.Rules.Security;
using Rikarin.Skala.Rules.TestQuality;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules;

/// <summary>
///     Every analyzer this package ships, as one instance set, in one place.
/// </summary>
/// <remarks>
///     ⚠ <b>There used to be two of these lists</b>: <c>AnalyzerHost.Own</c>, which is what
///     <c>skala check</c> runs, and a second hand-written copy inside <c>RuleFixtureTests</c>, which is
///     what every fixture is measured against. They agreed — 290 entries each, no difference in
///     content — but nothing made them agree, and the comment on
///     <c>AnalyzerHost_OwnsEveryAnalyzerInTheRulesAssembly</c> claimed the fixture harness
///     <em>discovered</em> analyzers when in fact it was the copy. A rule added to one and not the
///     other is measured by a set that is not the set that ships, in whichever direction the omission
///     runs ([#297](https://github.com/Rikarin/SKALA/issues/297)).
///     <para>
///         ⚠ It is an explicit list rather than a reflection scan on purpose: the package is published
///         for NativeAOT and trimming, where a scan over <c>Assembly.GetTypes()</c> is exactly the
///         shape the trimmer cannot follow. Completeness is asserted instead — a declared analyzer
///         missing from this list fails a test rather than silently never running.
///     </para>
/// </remarks>
public static class SkalaAnalyzers {
    /// <summary>One instance set, reused across compilations (ADR-006).</summary>
    public static ImmutableArray<DiagnosticAnalyzer> All { get; } = [
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
        new FileScopedNamespaceAnalyzer(), new NullPatternAnalyzer(), new NullCoalescingAssignmentAnalyzer(),
        new EnumGetValuesAnalyzer(), new DiscardedExceptionAnalyzer(),
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
        new XmlSignatureAnalyzer(),
        new PredictableInitializationVectorAnalyzer(), new XmlResolverReenabledAnalyzer(),
        new RedundantBooleanExpressionAnalyzer(), new ConstantPatternOverSequenceEqualAnalyzer(),
        new RedundantAttributeDetailAnalyzer(), new RedundantBaseListEntryAnalyzer(),
        new RedundantRequiredMembersAttributeAnalyzer(), new RedundantPositionalPropertyAnalyzer(),
        new FixedKeyDerivationSaltAnalyzer(), new WorldWritableFileModeAnalyzer(), new AsymmetricKeySizeAnalyzer(),
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
}
