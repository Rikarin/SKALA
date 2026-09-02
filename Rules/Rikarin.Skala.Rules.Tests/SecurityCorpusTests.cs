using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Security;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The <c>SK5xxx</c> corpus: known-vulnerable code on one side, known-safe code on the other.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This is the only measurement that means anything for the security range, and it exists
///         because the reference corpus cannot make it.
///     </b> <c>Testing/corpus/real</c> and the vendored
///     trees are a logging library, a JSON serialiser and a game engine: between them they contain no
///     SQL reaching a request, no disabled certificate validation, no broken cipher and no XXE. A
///     <c>SK5xxx</c> run over them returns zero, and zero there proves only that the trees have none of
///     the shape — not that a rule works, and not that it is safe. So both halves of the evidence are
///     written here deliberately.
///     <para>
///         ⚠
///         <b>
///             The safe half is the half that decides whether a rule ships, and it is not "code with no
///             security in it".
///         </b> Every file under <c>corpus/safe</c> is the <em>same shape</em> as its twin
///         under <c>corpus/vulnerable</c> — the same request read, the same builder, the same loop, the
///         same callback, the same XML settings — with the vulnerability removed the way a reviewer would
///         remove it: a bound parameter, an <c>ArgumentList</c>, a parsed integer, an allow-list, a pinned
///         thumbprint, a null resolver. A rule that is really a keyword search passes the vulnerable half
///         and fails here, which is the entire point of writing the pair.
///     </para>
///     <para>
///         ⚠ <b>It is kept out of <c>Testing/corpus</c> on purpose.</b> That tree is the formatting
///         fidelity measurement, and these files are deliberately-shaped inputs rather than a sample of how
///         anyone writes code. Mixing them would let hand-written fixtures move a number that is supposed
///         to be about real code.
///     </para>
/// </remarks>
public sealed class SecurityCorpusTests {
    /// <summary>The security analyzers, and only those — the corpus is about this range.</summary>
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new SqlInjectionAnalyzer(), new ProcessArgumentInjectionAnalyzer(), new WeakCipherAnalyzer(),
        new CertificateValidationAnalyzer(), new XmlExternalEntityAnalyzer(), new RegexTimeoutAnalyzer(),
        new PredictableInitializationVectorAnalyzer(), new AsymmetricKeySizeAnalyzer()
    ];

    static string Root { get; } = Path.Combine(
        Path.GetDirectoryName(RuleFixtures.Root)!,
        "corpus"
    );

    /// <summary>
    ///     ⚠ 100 %, no exceptions. A single <c>SK5xxx</c> finding here is a false positive at
    ///     <c>error</c> severity, which is a build somebody cannot fix by fixing their code.
    /// </summary>
    [Fact]
    public void TheSafeHalf_ProducesNoFindingAtAll() {
        var findings = Analyze("safe");

        Assert.True(
            findings.Length == 0,
            $"{findings.Length} finding(s) on code that is not vulnerable. Every one is a false "
            + "positive at `error` severity:\n"
            + string.Join("\n", findings.Select(Describe))
        );
    }

    /// <summary>Every rule that ships has at least one file here that it catches.</summary>
    /// <remarks>
    ///     ⚠ Without this, <see cref="TheSafeHalf_ProducesNoFindingAtAll" /> passes perfectly for an
    ///     analyzer that reports nothing ever — which is the failure mode a "nothing is wrong"
    ///     assertion always has, and the one <c>ToolDiagnosticIdTests</c> shipped with for a whole
    ///     milestone.
    /// </remarks>
    [Fact]
    public void EverySecurityRule_CatchesSomethingInTheVulnerableHalf() {
        var findings = Analyze("vulnerable");
        var caught = findings.Select(static finding => finding.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Analyzers.SelectMany(static analyzer => analyzer.SupportedDiagnostics)) {
            Assert.True(
                caught.Contains(descriptor.Id),
                $"{descriptor.Id} found nothing in corpus/vulnerable. Either the corpus has no example "
                + "of it, or the rule stopped working — and the safe-half assertion cannot tell "
                + "those apart from a rule that is switched off."
            );
        }
    }

    /// <summary>
    ///     The per-rule counts, pinned, so a change in what the engine sees is visible in a diff.
    /// </summary>
    /// <remarks>
    ///     ⚠ These are not a target, they are a ratchet. A number that moves is not necessarily wrong —
    ///     it means the corpus or the engine changed — but it must never move without somebody
    ///     having decided that it should, which is what an assertion on a literal buys.
    /// </remarks>
    [Theory]
    [InlineData(RuleIds.SqlFromRequestConcatenation, 6)]
    [InlineData(RuleIds.ProcessStartFromRequest, 4)]
    [InlineData(RuleIds.WeakCipherAlgorithm, 6)]
    [InlineData(RuleIds.CertificateValidationDisabled, 5)]
    [InlineData(RuleIds.XmlExternalEntityResolution, 2)]
    [InlineData(RuleIds.RegexWithoutTimeout, 2)]
    [InlineData(RuleIds.PredictableCipherInitializationVector, 6)]
    [InlineData(RuleIds.UndersizedAsymmetricKey, 4)]
    public void TheVulnerableHalf_ProducesExactlyTheKnownCount(string ruleId, int expected) {
        var findings = Analyze("vulnerable").Where(finding => finding.Id == ruleId).ToArray();

        Assert.True(
            findings.Length == expected,
            $"{ruleId}: expected {expected} finding(s) in corpus/vulnerable, got {findings.Length}:\n"
            + string.Join("\n", findings.Select(Describe))
        );
    }

    /// <summary>
    ///     ⚠ No security analyzer threw on either half of the corpus.
    /// </summary>
    /// <remarks>
    ///     Roslyn swallows an analyzer exception as <c>AD0001</c> and lets the analyzer produce nothing
    ///     for the rest of the compilation — so a crash makes
    ///     <see cref="TheSafeHalf_ProducesNoFindingAtAll" /> pass perfectly and turns
    ///     <see cref="TheVulnerableHalf_ProducesExactlyTheKnownCount" /> into a count of zero that
    ///     somebody has to notice. Nothing else in this class can tell a clean run from a dead one, and
    ///     the fixture harness filters diagnostics down to the rule under test, which drops
    ///     <c>AD0001</c> before anybody sees it.
    /// </remarks>
    [Theory]
    [InlineData("safe")]
    [InlineData("vulnerable")]
    public void NoSecurityAnalyzerCrashed_OnEitherHalf(string half) {
        var crashes = Analyze(half).Where(static finding => finding.Id == "AD0001").ToArray();

        Assert.True(
            crashes.Length == 0,
            $"an analyzer threw on corpus/{half} and Roslyn swallowed it as AD0001, so every count in "
            + "this class is measuring a rule that stopped running:\n"
            + string.Join("\n", crashes.Select(static finding => "  " + finding.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ Asserts the harness read the corpus. Every other test in this class is a count, and a
    ///     count over an empty directory is a very convincing zero.
    /// </summary>
    [Fact]
    public void TheCorpus_IsWhereTheHarnessLooks() {
        foreach (var half in new[] { "safe", "vulnerable" }) {
            var directory = Path.Combine(Root, half);
            Assert.True(Directory.Exists(directory), $"{directory} does not exist.");
            Assert.True(
                Directory.GetFiles(directory, "*.cs").Length >= 6,
                $"{directory} holds {Directory.GetFiles(directory, "*.cs").Length} file(s); the "
                + "corpus is supposed to be a corpus."
            );
        }
    }

    /// <summary>
    ///     One compilation per half, out of every file in it.
    /// </summary>
    /// <remarks>
    ///     ⚠ All the files together rather than one at a time, which is what <c>skala check</c> does
    ///     and what the fixture harness deliberately does not. The fixtures are units; this is the
    ///     closest thing to the product that a test can be.
    /// </remarks>
    static ImmutableArray<Diagnostic> Analyze(string half) {
        var directory = Path.Combine(Root, half);
        var trees = Directory.GetFiles(directory, "*.cs")
            .OrderBy(static file => file, StringComparer.Ordinal)
            .Select(static file => CSharpSyntaxTree.ParseText(
                    SourceText.From(File.ReadAllText(file)),
                    new CSharpParseOptions(LanguageVersion.Preview),
                    file
                )
            )
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "security-corpus-" + half,
            trees,
            RuleFixtures.References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        // ⚠ The same guard the fixture harness has, for the same reason: a rule reading an error
        // type answers "no finding" for the wrong reason, and the safe half then passes on nothing.
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            $"corpus/{half} does not compile, so it proves nothing:\n"
            + string.Join("\n", errors.Take(5).Select(static d => "  " + d))
        );

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
    }

    static string Describe(Diagnostic diagnostic) {
        var span = diagnostic.Location.GetLineSpan();
        return "  "
            + diagnostic.Id
            + " "
            + Path.GetFileName(span.Path)
            + ":"
            + (span.StartLinePosition.Line + 1)
            + " — "
            + diagnostic.GetMessage();
    }
}
