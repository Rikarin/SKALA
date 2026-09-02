using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The checks <see cref="RuleFixtureTests" /> does not make, for <c>SK1090</c>–<c>SK1094</c>.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, turns
///     it into <c>AD0001</c> and returns no diagnostics from that analyzer — so every "should not
///     fire" fixture goes green, the positives go red, and the failure reads as a rule that does not
///     work rather than as one that threw. The shared harness does not look at <c>AD0001</c>
///     (issue #279) and <c>skala check</c> drops it (issue #295), which is how <c>SK0232</c> shipped
///     with a live crash in it (issue #298). This file looks.
/// </remarks>
public sealed class DeclarationBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Batch = [
        new ComputedPropertyAnalyzer(), new PrivateAutoPropertyAnalyzer(), new TupleLiteralAnalyzer(),
        new CastInDeclarationAnalyzer(), new NullableAnnotationSyntaxAnalyzer()
    ];

    static readonly string[] Ids = [
        RuleIds.ComputedProperty, RuleIds.PrivateAutoProperty, RuleIds.TupleLiteral,
        RuleIds.CastInDeclaration, RuleIds.NullableAnnotationSyntax
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Ids.Contains(fixture.RuleId)) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>⚠ Anti-vacuity: an empty theory is the shape of this file having stopped working.</summary>
    [Fact]
    public void TheBatch_HasFixturesToCheck() {
        var all = RuleFixtures.All();
        foreach (var id in Ids) {
            Assert.True(
                all.Count(fixture => fixture.RuleId == id) >= 8,
                $"{id} has fewer than eight fixtures; the checks below would be nearly vacuous."
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerThrows(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var produced = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, fixture.Path),
            Batch,
            TestContext.Current.CancellationToken
        );

        var crashes = produced.Where(static diagnostic => diagnostic.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, which silently passes every negative fixture:\n  "
            + string.Join("\n  ", crashes.Select(static d => d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ The floors, measured rather than declared.
    /// </summary>
    /// <remarks>
    ///     A rule that suggests syntax a pinned <c>LangVersion</c> cannot compile produces a fix that
    ///     breaks the build on the tool's own advice. <c>rules.json</c> carrying a floor proves
    ///     nothing on its own — <see cref="SkalaRule.MeetsLanguageVersion" /> has to be wired into the
    ///     analyzer, and the shape of forgetting it is a rule that fires everywhere.
    /// </remarks>
    [Theory]
    [InlineData("SK1090", "a-string-constant.cs", LanguageVersion.CSharp6, LanguageVersion.CSharp5)]
    [InlineData("SK1092", "a-var-declaration.cs", LanguageVersion.CSharp7, LanguageVersion.CSharp6)]
    [InlineData("SK1094", "can-be-null-on-a-property.cs", LanguageVersion.CSharp8, LanguageVersion.CSharp7_3)]
    public void ARuleWithALanguageFloor_IsSilentBelowIt(
        string ruleId,
        string fixture,
        LanguageVersion above,
        LanguageVersion below
    ) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, ruleId, "positive", fixture));

        Assert.Contains(
            RuleFixtures.Analyze(
                RuleFixtures.Compile(source, fixture, above),
                Batch,
                TestContext.Current.CancellationToken
            ),
            diagnostic => diagnostic.Id == ruleId
        );

        Assert.DoesNotContain(
            RuleFixtures.Analyze(
                RuleFixtures.Compile(source, fixture, below),
                Batch,
                TestContext.Current.CancellationToken
            ),
            diagnostic => diagnostic.Id == ruleId
        );
    }

    /// <summary>
    ///     ⚠ <c>SK1093</c> and <c>SK0202</c> over one span, asserted rather than argued.
    /// </summary>
    /// <remarks>
    ///     The claim in <c>SK1093</c>'s <c>falsePositives</c> is that the two rules cannot reach the
    ///     same declaration: <c>SK0202</c>'s <c>VarRule</c> converts explicit-to-<c>var</c> only, and
    ///     <c>SK1093</c> reports only declarations already written <c>var</c>. What is checkable from
    ///     this assembly — which may not reference the formatter — is the half that is <c>SK1093</c>'s
    ///     own: it is silent on every declaration carrying a written type, which is the entire input
    ///     set <c>VarRule</c> looks at.
    /// </remarks>
    [Fact]
    public void CastInDeclaration_IsSilentOnEveryWrittenType() {
        const string source = """
            using System.IO;

            public sealed class Written {
                public TextWriter A() {
                    TextWriter writer = (TextWriter)new StringWriter();
                    return writer;
                }

                public object B() {
                    object boxed = (object)42;
                    return boxed;
                }
            }
            """;

        Assert.DoesNotContain(
            RuleFixtures.Analyze(
                RuleFixtures.Compile(source, "written.cs"),
                Batch,
                TestContext.Current.CancellationToken
            ),
            static diagnostic => diagnostic.Id == RuleIds.CastInDeclaration
        );
    }
}
