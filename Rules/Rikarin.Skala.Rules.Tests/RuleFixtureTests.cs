using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Every shipped rule, against its positive and its "should not fire" fixture set.
/// </summary>
/// <remarks>
///     ⚠ The negative direction is the one that decides whether a rule ships. docs/plan/16 § R3: a 5 %
///     false-positive rate on a corpus producing 5 000 findings is 250 wrong findings, which is where
///     the analysis half gets switched off — and the rules most likely to over-fire are exactly the
///     ones with the most value.
/// </remarks>
public sealed class RuleFixtureTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
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
        new CertificateValidationAnalyzer(), new XmlExternalEntityAnalyzer(),
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
        new RedundantDeclarationAnalyzer(),
        new TestAndCastPatternAnalyzer(), new PatternSimplificationAnalyzer(),
        new MergedConditionalAccessAnalyzer(), new DiscardAssignmentAnalyzer(),
        new InlineOutVariableAnalyzer(),
        new LockOverSynchronizationPrimitiveAnalyzer(), new NonAtomicVolatileUpdateAnalyzer(),
        new DoubleCheckedLockingAnalyzer(), new LockOrderAnalyzer(),
        new InconsistentlySynchronizedFieldAnalyzer(),
        new InheritanceDepthAnalyzer(), new TypeCouplingAnalyzer(), new NestedConditionalAnalyzer(),
        new RepeatedStringLiteralAnalyzer(),
        new IneffectiveThreadStaticAnalyzer(), new PureAttributeOnVoidAnalyzer(),
        new DebuggerDisplayMissingMemberAnalyzer(),
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Rule_FiresExactlyWhereTheFixtureSaysItShould(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);

        // ⚠ A fixture that does not compile is a fixture that proves nothing: a rule reading an
        // error type answers "no finding" for the wrong reason, and the negative case passes.
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"{fixture}: the fixture does not compile, so it proves nothing: "
            + string.Join("; ", errors.Take(3).Select(static d => d.ToString()))
        );

        var produced = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        if (fixture.ShouldFire) {
            Assert.True(
                produced.Length > 0,
                $"{fixture}: {fixture.RuleId} did not fire on a positive fixture."
            );
        } else {
            Assert.True(
                produced.Length == 0,
                $"{fixture}: {fixture.RuleId} fired {produced.Length} time(s) on a fixture that documents why it must not:\n"
                + string.Join(
                    "\n",
                    produced.Select(static d => "  " + d.Location.GetLineSpan() + ": " + d.GetMessage())
                )
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFix_ProducesTextThatStillParses(RuleFixture fixture) {
        // ⚠ Only rules the catalogue says have a fix. docs/plan/08 § SK7000: the metric rules carry
        // `hasFix: false`, because there is no edit that makes a 300-statement method shorter — the
        // finding is a measurement and the fix is a design decision a person makes. Asserting a fix
        // on those would be asserting the catalogue is wrong.
        if (!fixture.ShouldFire || RuleCatalog.Find(fixture.RuleId) is not { HasFix: true }) {
            return;
        }

        var source = File.ReadAllText(fixture.Path);
        var compilation = RuleFixtures.Compile(source, fixture.Path);
        var produced = RuleFixtures
            .Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        foreach (var diagnostic in produced) {
            var edits = ReadEdits(diagnostic);
            Assert.True(
                edits.Count > 0,
                $"{fixture}: {diagnostic.Id} carries no fix, but the catalogue says it has one."
            );

            var text = source;
            foreach (var (start, length, replacement) in edits.OrderByDescending(static edit => edit.Start)) {
                text = text[..start] + replacement + text[(start + length)..];
            }

            var after = CSharpSyntaxTree.ParseText(
                text,
                new CSharpParseOptions(LanguageVersion.Preview),
                cancellationToken: TestContext.Current.CancellationToken
            );
            var errors = after.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.True(
                errors.Length == 0,
                $"{fixture}: applying {diagnostic.Id}'s fix produced text that does not parse:\n"
                + string.Join("\n", errors.Take(3).Select(static d => "  " + d))
                + "\n---\n"
                + text
            );
        }
    }

    /// <summary>
    ///     ⚠ The other half of "the fix works": apply every edit, re-bind, and the rule is quiet.
    /// </summary>
    /// <remarks>
    ///     <see cref="EveryFix_ProducesTextThatStillParses" /> asks only whether the result parses, and a
    ///     fix can parse, bind and still leave the finding standing — an edit in the wrong place, or one
    ///     that repairs the symptom the message names and not the shape the rule matches. That failure
    ///     looks exactly like a working fix in a report and turns <c>skala fix</c> into a loop.
    ///     <para>
    ///         ⚠ It re-binds rather than re-parsing, so it also catches the fix that compiles as text and
    ///         not as a program: a <c>.ToList()</c> where <c>System.Linq</c> is not imported, an
    ///         <c>async</c> added to a method holding a byref-like local. The comparison is against the
    ///         fixture's own diagnostics before the edit, because a fixture is allowed to carry warnings.
    ///     </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic(RuleFixture fixture) {
        if (!fixture.ShouldFire || RuleCatalog.Find(fixture.RuleId) is not { HasFix: true }) {
            return;
        }

        var source = File.ReadAllText(fixture.Path);
        var before = RuleFixtures.Compile(source, fixture.Path);
        var findings = RuleFixtures
            .Analyze(before, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        var edits = findings.SelectMany(ReadEdits).OrderByDescending(static edit => edit.Start).ToArray();
        if (edits.Length == 0) {
            return;
        }

        var text = source;
        foreach (var (start, length, replacement) in edits) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        var after = RuleFixtures.Compile(text, fixture.Path);

        // ⚠ Errors for every fix; warnings only for a *safe* one. `fixIsSafe` is the promise that
        // `--fix` may apply the edit without review, so a safe fix that leaves a warning behind
        // fails a `TreatWarningsAsErrors` build on the tool's advice. An unsafe fix is reviewed by
        // definition, and its new warning is often the point: SK3001 turns `async void` into
        // `async Task`, and the CS4014 that appears at every caller is the rule finishing its
        // sentence.
        var severities = RuleCatalog.Get(fixture.RuleId).FixIsSafe
            ? new[] { DiagnosticSeverity.Error, DiagnosticSeverity.Warning }
            : [DiagnosticSeverity.Error];

        var introduced = Signatures(after.GetDiagnostics(TestContext.Current.CancellationToken), severities)
            .Except(
                Signatures(before.GetDiagnostics(TestContext.Current.CancellationToken), severities),
                StringComparer.Ordinal
            )
            .ToArray();

        Assert.True(
            introduced.Length == 0,
            $"{fixture}: applying {fixture.RuleId}'s fix introduced {introduced.Length} diagnostic(s) the "
            + $"fixture did not have:\n  {string.Join("\n  ", introduced.Take(5))}\n---\n{text}"
        );

        var remaining = RuleFixtures
            .Analyze(after, Analyzers, TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.True(
            remaining.Length == 0,
            $"{fixture}: {fixture.RuleId} still fires {remaining.Length} time(s) after its own fix was "
            + $"applied, so `skala fix` would loop:\n  "
            + string.Join("\n  ", remaining.Select(static d => d.Location.GetLineSpan() + ": " + d.GetMessage()))
            + $"\n---\n{text}"
        );
    }

    /// <summary>
    ///     ⚠ Id and message, never the location. A fix that deletes a line moves every diagnostic below
    ///     it, and keyed on position an unchanged warning reads as a new one — the same shrug
    ///     <c>RuleAudit</c> keys per <c>(file, id)</c> to avoid.
    /// </summary>
    static IEnumerable<string> Signatures(
        IEnumerable<Diagnostic> diagnostics,
        IReadOnlyList<DiagnosticSeverity> severities
    ) =>
        diagnostics
            .Where(diagnostic => severities.Contains(diagnostic.Severity))
            .Select(static diagnostic => diagnostic.Id + ": " + diagnostic.GetMessage());

    /// <summary>
    ///     ⚠ docs/plan/08: every modernization rule declares its floor and is silent below it, checked
    ///     against the compilation's effective LangVersion and not the SDK's. A rule that suggests C# 12
    ///     syntax to a project pinned at C# 10 produces uncompilable fixes.
    /// </summary>
    [Fact]
    public void ARuleWithALanguageFloor_IsSilentBelowIt() {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK1005", "positive", "simple.cs"));

        var above = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "simple.cs", LanguageVersion.CSharp10),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        var below = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "simple.cs", LanguageVersion.CSharp9),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(above, diagnostic => diagnostic.Id == RuleIds.FileScopedNamespace);
        Assert.DoesNotContain(below, diagnostic => diagnostic.Id == RuleIds.FileScopedNamespace);
    }

    [Fact]
    public void EveryRule_HasMoreNegativeFixturesThanPositive() {
        var fixtures = RuleFixtures.All();
        var shipped = Analyzers.SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var ruleId in shipped) {
            var positive = fixtures.Count(f => f.RuleId == ruleId && f.ShouldFire);
            var negative = fixtures.Count(f => f.RuleId == ruleId && !f.ShouldFire);

            Assert.True(positive > 0, $"{ruleId} ships with no positive fixture.");
            Assert.True(
                negative >= positive,
                $"{ruleId} has {positive} positive fixture(s) and {negative} \"should not fire\" fixture(s). "
                + "docs/plan/16 § R3: the negative set must be at least as large as the positive one."
            );
        }
    }

    [Fact]
    public void EveryShippedAnalyzer_IsInTheCatalogue() {
        foreach (var descriptor in Analyzers.SelectMany(static analyzer => analyzer.SupportedDiagnostics)) {
            var rule = RuleCatalog.Find(descriptor.Id);
            Assert.True(rule is not null, $"{descriptor.Id} is reported by an analyzer and is not in rules.json.");
            Assert.Equal(rule!.Title, descriptor.Title.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    static List<(int Start, int Length, string Text)> ReadEdits(Diagnostic diagnostic) {
        var result = new List<(int, int, string)>();
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, out var count)) {
            return result;
        }

        for (var i = 0; i < count; i++) {
            result.Add(
                (
                    int.Parse(
                        diagnostic.Properties[FixEdits.StartKey(i)]!,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    int.Parse(
                        diagnostic.Properties[FixEdits.LengthKey(i)]!,
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                    diagnostic.Properties[FixEdits.TextKey(i)] ?? string.Empty
                )
            );
        }

        return result;
    }
}
