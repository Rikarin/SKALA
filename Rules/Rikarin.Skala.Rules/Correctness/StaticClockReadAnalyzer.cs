using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.TestQuality;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2160</c> — the clock is read from a static, so no test can give this code a different time.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This ships disabled and the reason is measurement, not taste — see the rule's
///         <c>falsePositives</c>.
///     </b> <c>TimeProvider</c> is the .NET 8 answer and a repository that has not
///     adopted it lights up on every line that asks what time it is, which is the shape that gets an
///     analyzer switched off rather than adopted. It is opt-in exactly the way <c>SK7010</c>,
///     <c>SK7101</c> and <c>SK6053</c> are.
///     <para>
///         ⚠ <b>The whole analyzer withdraws when <c>System.TimeProvider</c> does not resolve.</b> Below
///         .NET 8 the repair this rule names does not exist, and a finding whose advice cannot be taken
///         is noise with a citation. That is one type lookup, done once per compilation.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK8007</c> by construction.</b> <c>SK8007</c> reports a live clock
///         consumed by an xUnit assertion; this rule excludes test code entirely. The two cannot both
///         fire on one read, so neither declares <c>supersedes</c> over the other and a shared span never
///         carries two findings.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticClockReadAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.StaticClockRead);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ The repair is `TimeProvider`. Where the type does not exist there is nothing to
                // advise, so the rule does not run at all rather than reporting advice that cannot be
                // taken.
                //
                // ⚠ **`GetTypesByMetadataName`, plural, because a repository may declare its own
                // `System.TimeProvider` — and the reason first written here was wrong.** Serilog ships
                // `namespace System; abstract class TimeProvider` under `#if !NET8_0_OR_GREATER`, a
                // shim so the library can use the shape where the framework lacks it. The hypothesis
                // was that this makes the name ambiguous, that the singular
                // `GetTypeByMetadataName` returns null for it, and that the rule therefore withdrew
                // from Serilog entirely. **Measured, that is false**: on a compilation containing the
                // shim the singular form returns a symbol — the *source* one — and the plural form
                // returns two, `[serilog, System.Private.CoreLib]`. Nothing withdrew.
                //
                // The plural form is kept anyway, for the exclusion below rather than for this guard:
                // where both a shim and the framework type exist, a body deriving from either is the
                // designated place to read the real clock, and the singular form would recognise only
                // whichever one it happened to return.
                var providers = start.Compilation.GetTypesByMetadataName("System.TimeProvider");
                if (providers.Length == 0) {
                    return;
                }

                var frameworks = TestFrameworks.Resolve(start.Compilation);
                var testClasses = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, providers, frameworks, testClasses),
                    SyntaxKind.SimpleMemberAccessExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> providers,
        TestFrameworks frameworks,
        ConcurrentDictionary<ISymbol, bool> testClasses
    ) {
        var access = (MemberAccessExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (!Clock.IsStaticRead(model.GetOperation(access, cancellation), context.Compilation)) {
            return;
        }

        // ⚠ `DateTime.Now.Date` is one finding rather than two, and nothing here has to arrange
        // that: `Date` is an instance property, so the outer access is not a static clock read and
        // the test above declines it. The inner node carries the finding on its own.
        if (model.GetEnclosingSymbol(access.SpanStart, cancellation) is not { } enclosing
            || IsTest(enclosing, frameworks, testClasses)
            || ImplementsClock(enclosing, providers)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                access.GetLocation(),
                "`"
                + Clock.NameOf(model.GetOperation(access, cancellation)!)
                + "` reads the machine clock from a static, so no caller and no test can give this code "
                + "a different time; take a `TimeProvider` instead"
            )
        );
    }

    /// <summary>
    ///     ⚠ Test code is excluded outright, by method attribute, by containing type, and — for xUnit —
    ///     by the containing type holding a test case.
    /// </summary>
    /// <remarks>
    ///     A test that pins a clock is doing the thing this rule asks for, and a test that reads the real
    ///     one has made that choice deliberately — <c>SK8007</c> is the rule that has an opinion there.
    ///     The containing type is checked as well as the method, because a fixture's helper and its
    ///     constructor are test code that carries no attribute of its own.
    ///     <para>
    ///         ⚠ <b>The attribute walk alone cannot see an xUnit fixture</b> (#303). MSTest has
    ///         <c>[TestClass]</c> and NUnit has <c>[TestFixture]</c>; xUnit has nothing at class level at
    ///         all, so a helper in an xUnit test class carries nothing and its class carries nothing
    ///         either, and the helper was reported. That was <b>22 of the 38 findings</b> this rule makes
    ///         on the reference tree, every one of them a settle loop polling a wall-clock deadline —
    ///         code that reads the real clock because reading the real clock is the point.
    ///     </para>
    ///     <para>
    ///         ⚠ <see cref="TestFrameworks.HoldsATestCase" /> is xUnit's own discovery rule: a class is a
    ///         test class when one of its methods carries a test attribute. It is decidable from
    ///         attributes alone — no naming convention and no reference sniffing — and its cost is that a
    ///         class holding one <c>[Fact]</c> beside production helpers has all of them excluded.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>What it still does not reach</b>, stated rather than left to be rediscovered: a
    ///         helper in a separate class that holds no test case of its own. Only "the project
    ///         references a test framework" would cover that, and it would exclude a repository's own
    ///         test-helper *library* along with it — a coarser claim this rule does not make. A
    ///         repository wanting it has <c>.editorconfig</c> path scoping, which is what this rule's
    ///         <c>none</c> default already assumes.
    ///     </para>
    ///     <para>
    ///         The per-type answer is cached because a syntax-node action asks it once per clock read and
    ///         <c>GetMembers</c> is not free on a large fixture.
    ///     </para>
    /// </remarks>
    static bool IsTest(ISymbol symbol, TestFrameworks frameworks, ConcurrentDictionary<ISymbol, bool> cache) {
        for (var current = symbol; current is not null; current = current.ContainingSymbol) {
            if (TestFrameworks.Carries(current, frameworks.TestMethodAttributes)
                || TestFrameworks.Carries(current, frameworks.LifecycleAttributes)
                || TestFrameworks.Carries(current, frameworks.MsTestClassAttribute)
                || TestFrameworks.Carries(current, frameworks.NUnitFixtureAttribute)) {
                return true;
            }

            // ⚠ `TryGetValue` and an assignment rather than `GetOrAdd`: netstandard2.0's
            // `ConcurrentDictionary` has no state-carrying overload, and the closure form would
            // allocate a delegate on every clock read the cache exists to make cheap.
            if (current is INamedTypeSymbol type) {
                if (!cache.TryGetValue(type, out var holds)) {
                    cache[type] = holds = TestFrameworks.HoldsATestCase(type, frameworks);
                }

                if (holds) {
                    return true;
                }
            }

            if (current is INamespaceSymbol) {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Somebody has to read the real clock, and <c>TimeProvider</c> is where the framework says it
    ///     happens.
    /// </summary>
    /// <remarks>
    ///     A type deriving from <c>System.TimeProvider</c> exists precisely to turn the machine clock
    ///     into an injectable dependency; reporting the one read inside it would be reporting the repair
    ///     this rule asks for. The base chain is walked, so an intermediate abstract provider is covered
    ///     too — and every candidate the name resolves to is checked, so a repository shipping its own
    ///     <c>System.TimeProvider</c> shim excludes its shim's body rather than the whole repository.
    /// </remarks>
    static bool ImplementsClock(ISymbol symbol, ImmutableArray<INamedTypeSymbol> providers) {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var provider in providers) {
                if (SymbolEqualityComparer.Default.Equals(current, provider)) {
                    return true;
                }
            }
        }

        return false;
    }
}
