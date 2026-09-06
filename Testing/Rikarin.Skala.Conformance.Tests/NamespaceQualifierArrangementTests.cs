using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

public sealed class NamespaceQualifierArrangementTests {
    const string Definitions =
        "namespace Demo.Reporting { public static class Fingerprints { public static string Normalize(string text) => text; } }";

    const string Path = "/namespace-qualifier/Probe.cs";

    [Theory]
    [InlineData("Reporting.Fingerprints.Normalize(text)")]
    [InlineData("Demo.Reporting.Fingerprints.Normalize(text)")]
    public void ImportedNamespace_IsRemovedFromStaticReceiver(string expression) {
        var source = "using Demo.Reporting; "
            + Definitions
            + "namespace Demo.Analysis { class C { string M(string text) => "
            + expression
            + "; } }";
        var result = Arrange(source);
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("=> Fingerprints.Normalize(text);", result.Text);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(ArrangementOutcome.Unchanged, Arrange(result.Text).Outcome);
    }

    [Fact]
    public void ActualFingerprintsMetadata_IsShortenedByThePipeline() {
        const string source =
            "using Rikarin.Skala.Reporting; namespace Rikarin.Skala.Analysis.Hosting { class C { string M(string text) { return Reporting.Fingerprints.Normalize(text); } } }";
        var (text, compilation) = Compile(source);
        var options = Options();
        var result = ArrangementPipeline.Run(
            Path,
            text,
            new PhaseOneOptions(options),
            new ArrangementOptions(options),
            compilation,
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.True(result.Converged);
        Assert.Contains(ArrangeIds.StaticQualifier, result.Applied);
        Assert.Contains("Fingerprints.Normalize(text)", result.Text);
        Assert.DoesNotContain("Reporting.Fingerprints.Normalize", result.Text);
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id is ArrangeIds.Reverted or ArrangeIds.SymbolChanged or ArrangeIds.RuleThrew
        );
    }

    [Theory]
    [InlineData("", "", "", "Reporting.Fingerprints.Normalize(text)")]
    [InlineData("using Demo.Reporting;", "", ", object Fingerprints", "Reporting.Fingerprints.Normalize(text)")]
    [InlineData(
        "using Demo.Reporting;",
        "class Fingerprints { public static string Normalize(string text) => text; }",
        "",
        "Reporting.Fingerprints.Normalize(text)"
    )]
    [InlineData("using Demo.Reporting; using Other;", "", "", "Reporting.Fingerprints.Normalize(text)")]
    [InlineData("using Demo.Reporting;", "", "", "global::Demo.Reporting.Fingerprints.Normalize(text)")]
    [InlineData("using Demo.Reporting; using R = Demo.Reporting;", "", "", "R.Fingerprints.Normalize(text)")]
    [InlineData("using Demo.Reporting;", "", "", "Reporting /* preserve */ .Fingerprints.Normalize(text)")]
    public void RequiredOrExplicitQualifiers_ArePreserved(
        string imports,
        string extra,
        string parameter,
        string expression
    ) {
        var source = imports
            + Definitions
            + "namespace Other { public static class Fingerprints { public static string Normalize(string text) => text; } }"
            + "namespace Demo.Analysis { "
            + extra
            + " class C { string M(string text"
            + parameter
            + ") => "
            + expression
            + "; } }";
        AssertUnchanged(source);
    }

    [Fact]
    public void ALeadingComment_DoesNotDisableShortening() {
        var result = Arrange(
            "using Demo.Reporting; "
            + Definitions
            + "namespace Demo.Analysis { class C { string M(string text) {\n// Keep this explanation.\nreturn Reporting.Fingerprints.Normalize(text);\n} } }"
        );
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("// Keep this explanation.", result.Text);
        Assert.Contains("return Fingerprints.Normalize(text);", result.Text);
    }

    [Fact]
    public void FormatterOff_IsRespected() =>
        AssertUnchanged(
            "using Demo.Reporting; "
            + Definitions
            + "namespace Demo.Analysis { class C { string M(string text) {\n// @formatter:off\nreturn Reporting.Fingerprints.Normalize(text);\n// @formatter:on\n} } }"
        );

    [Fact]
    public void GenericStaticReceiver_PreservesItsTypeArguments() {
        const string source =
            "using Demo.Reporting; namespace Demo.Reporting { class Helpers<T> { public static T Echo(T value) => value; } } namespace Demo.Analysis { class C { string M(string text) => Reporting.Helpers<string>.Echo(text); } }";
        var result = Arrange(source);
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
        Assert.Contains("Helpers<string>.Echo(text)", result.Text);
        Assert.DoesNotContain("Reporting.Helpers<string>.Echo", result.Text);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void StaticQualifierPreference_StillKeepsTheType() {
        var source = "using Demo.Reporting; "
            + Definitions
            + "namespace Demo.Analysis { class C { string M(string text) => Reporting.Fingerprints.Normalize(text); } }";
        var result = Arrange(source, "all");
        Assert.Contains("=> Fingerprints.Normalize(text);", result.Text);
        Assert.Equal(ArrangementOutcome.Arranged, result.Outcome);
    }

    [Fact]
    public void LooseMode_DoesNotGuessWhichTypeTheNameMeans() {
        var source = "using Demo.Reporting; "
            + Definitions
            + "namespace Demo.Analysis { class C { string M(string text) => Reporting.Fingerprints.Normalize(text); } }";
        var result = Arranger.Arrange(
            Path,
            SourceText.From(source),
            new ArrangementOptions(Options()),
            filter: new([ArrangeIds.StaticQualifier], []),
            cancellation: TestContext.Current.CancellationToken
        );
        Assert.Equal(ArrangementOutcome.Unchanged, result.Outcome);
    }

    static void AssertUnchanged(string source) {
        var result = Arrange(source);
        Assert.Equal(ArrangementOutcome.Unchanged, result.Outcome);
        Assert.Equal(source, result.Text);
        Assert.Empty(result.Diagnostics);
    }

    static Rikarin.Skala.Options.FormattingOptions Options(string qualifier = "none") =>
        OptionResolver.Resolve(
            System.IO.Path.Combine(Corpus.RepositoryRoot, "Probe.cs"),
            [new("skala_static_members_qualify_members", qualifier)]
        ).Options;

    static (SourceText, CSharpCompilation) Compile(string source) {
        var text = SourceText.From(source);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            CSharpFormatter.ParseOptions,
            Path,
            TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            [
                .. SharedFrameworkReferences.Value,
                MetadataReference.CreateFromFile(typeof(Fingerprints).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            d => d.Severity == DiagnosticSeverity.Error
        );
        return (text, compilation);
    }

    static ArrangementResult Arrange(string source, string qualifier = "none") {
        var (text, compilation) = Compile(source);
        return Arranger.Arrange(
            Path,
            text,
            new ArrangementOptions(Options(qualifier)),
            compilation,
            filter: new([ArrangeIds.StaticQualifier], []),
            cancellation: TestContext.Current.CancellationToken
        );
    }
}
