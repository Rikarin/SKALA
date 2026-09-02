using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The three things a fixture may say about how it is compiled, and the proof that saying them
///     changes the compilation.
/// </summary>
/// <remarks>
///     ⚠ Every assertion here is written so that it fails if the directive is <em>ignored</em>, not
///     merely if it is mis-parsed. A test that only asks "did the parser return C# 9" would still pass
///     on the day <c>Compile</c> stops passing the value to Roslyn, which is the failure this
///     mechanism is most likely to have.
/// </remarks>
public sealed class FixtureCompilationTests {
    [Fact]
    public void ASilentFixture_CompilesTheWayEveryFixtureAlwaysHas() {
        var options = FixtureCompilation.From("class C { }");

        Assert.Equal(LanguageVersion.Preview, options.LanguageVersion);
        Assert.Empty(options.PreprocessorSymbols);

        // ⚠ The one default that moved: #310. It can only make more code legal, never change the
        // meaning of code that already compiled, and it is what LooseLoader does in production.
        Assert.True(options.AllowUnsafe);
    }

    [Fact]
    public void ALanguageVersion_ReachesTheParserAndNotOnlyTheRecord() {
        const string source = """
            // fixture-option: LangVersion = 9
            namespace Sample;

            class C { }
            """;

        Assert.Equal(LanguageVersion.CSharp9, FixtureCompilation.From(source).LanguageVersion);

        // A file-scoped namespace is C# 10. If the directive did not reach Roslyn this would compile.
        Assert.Contains(
            RuleFixtures.Compile(source, "version.cs").GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    [Fact]
    public void AnExplicitVersionArgument_StillWinsOverTheDirective() {
        const string source = """
            // fixture-option: LangVersion = 9
            namespace Sample;

            class C { }
            """;

        Assert.Empty(
            RuleFixtures.Compile(source, "version.cs", LanguageVersion.Preview)
                .GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );
    }

    [Fact]
    public void ADefinedSymbol_TurnsDisabledTextIntoCode() {
        const string source = """
            // fixture-option: DefineConstants = FEATURE;EXTRA
            class C {
                int M() {
            #if FEATURE
                    return NotAType.Value;
            #else
                    return 0;
            #endif
                }
            }
            """;

        Assert.Equal(["FEATURE", "EXTRA"], FixtureCompilation.From(source).PreprocessorSymbols);

        // The `#if` branch does not bind. Without the symbol it is disabled text and the file is
        // clean, so this error is the directive reaching the parser.
        Assert.Contains(
            RuleFixtures.Compile(source, "define.cs").GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Id == "CS0103" || diagnostic.Id == "CS0246"
        );
    }

    [Fact]
    public void UnsafeCode_CompilesByDefaultAndStopsCompilingWhenAFixtureAsksItTo() {
        const string body = """
            class C {
                unsafe int M() {
                    int* buffer = stackalloc int[4];
                    return buffer[0];
                }
            }
            """;

        Assert.DoesNotContain(
            RuleFixtures.Compile(body, "unsafe.cs").GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Id == "CS0227"
        );

        Assert.Contains(
            RuleFixtures.Compile("// fixture-option: AllowUnsafe = false\n" + body, "unsafe.cs")
                .GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Id == "CS0227"
        );
    }

    [Fact]
    public void ADirectiveBelowTheHeader_IsNotADirective() {
        const string source = """
            class C { }
            // fixture-option: LangVersion = 9
            """;

        Assert.Equal(LanguageVersion.Preview, FixtureCompilation.From(source).LanguageVersion);
    }

    /// <summary>⚠ A dropped directive is worse than a rejected one: the fixture reads as a measurement it is not.</summary>
    [Theory]
    [InlineData("// fixture-option: LangVerison = 9")]
    [InlineData("// fixture-option: LangVersion = seventeen")]
    [InlineData("// fixture-option: AllowUnsafe = perhaps")]
    public void ADirectiveThisCannotHonour_Throws(string directive) =>
        Assert.Throws<InvalidOperationException>(() => FixtureCompilation.From(directive + "\nclass C { }"));

    /// <summary>
    ///     ⚠ The fixture corpus has to <em>use</em> the mechanism or it rots, and a setting nothing
    ///     depends on is removed by the next person tidying up (#310 says so in as many words). One
    ///     fixture per setting, each asserted to still depend on it.
    /// </summary>
    [Fact]
    public void TheLanguageVersion_HasAFixtureThatDependsOnIt() {
        var source = Fixture("SK1005", "negative", "below_the_language_floor.cs");

        Assert.Equal(LanguageVersion.CSharp9, FixtureCompilation.From(source).LanguageVersion);
    }

    [Fact]
    public void ThePreprocessorSymbols_HaveAFixtureThatDependsOnThem() {
        var source = Fixture("SK2220", "positive", "inside_a_defined_region.cs");

        Assert.Contains("RELEASE", FixtureCompilation.From(source).PreprocessorSymbols);
    }

    [Fact]
    public void TheUnsafeContext_HasAFixtureThatDependsOnIt() {
        var source = Fixture("SK2033", "negative", "an_unsafe_pointer_outside_a_loop.cs");

        Assert.Contains(
            RuleFixtures.Compile("// fixture-option: AllowUnsafe = false\n" + source, "unsafe-fixture.cs")
                .GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Id == "CS0227"
        );
    }

    static string Fixture(string rule, string folder, string name) {
        var path = Path.Combine(RuleFixtures.Root, rule, folder, name);

        Assert.True(File.Exists(path), path + " is the fixture that pins a fixture-option; it is gone.");

        return File.ReadAllText(path);
    }
}
