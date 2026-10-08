using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Analysis.Loading;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #514: one row per term of the diagnostic cache key, each one a unit that differs from the base in
///     that term alone.
/// </summary>
/// <remarks>
///     ⚠ The key is only as good as the list of things it hashes, and a missing term is silent: the
///     cache serves the previous answer and it looks exactly like a real one. The defect this file was
///     written for — <c>LangVersion</c> 14.0 → 9.0 in a <c>.csproj</c>, <c>SK1133</c> (floor C# 14)
///     still reported from the cache — was the language version missing from
///     <see cref="CacheKey.CompilationFingerprint" />. Each row was sabotaged by deleting its term from the
///     key, and each turned red on its own; the record is in the commit that added this file.
///     <para>
///         ⚠ <see cref="EveryTermIsDeterministic" /> is the control the rows rest on. A fingerprint that
///         moved on every call — a fresh MVID per emitted image, say — would make every row pass
///         vacuously, so the base is built twice, independently, and must hash the same.
///     </para>
/// </remarks>
public sealed class CacheKeyTermTests {
    const string Source = "namespace N { public class C { public int M(object o) => o.GetHashCode(); } }\n";

    static readonly CSharpParseOptions BaseParse = new(LanguageVersion.CSharp14, DocumentationMode.Parse);

    static readonly CSharpCompilationOptions BaseOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable
    );

    static readonly MetadataReference Corlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

    /// <summary>Two images with one identity — <c>Lib, Version=1.0.0.0</c> — and different bits.</summary>
    static readonly Lazy<(PortableExecutableReference First, PortableExecutableReference Second)> SameIdentityImages =
        new(static () => (
                Image("public class L { public void A() { } }"), Image("public class L { public void B() { } }"))
        );

    public static TheoryData<string> Terms => [..Variants.Keys];

    /// <summary>
    ///     Each variant is the base with exactly one term moved. The name is the term.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<CompilationUnit>> Variants =
        new Dictionary<string, Func<CompilationUnit>>(StringComparer.Ordinal) {
            ["targetFramework"] = static () => Base() with { TargetFramework = "net9.0" },
            ["loaderPreprocessorSymbols"] = static () => Base() with { PreprocessorSymbols = ["SKALA_DEFINE"] },
            // #517: SK1133 reads it, and a toolset pin moves nothing else in the key.
            ["compilerPath"] = static () => Base() with {
                CompilerPath = "/nuget/microsoft.net.compilers.toolset/4.11.0/tasks/netcore/bincore/csc.exe"
            },
            ["languageVersion"] = static () => Base(BaseParse.WithLanguageVersion(LanguageVersion.CSharp9)),
            ["specifiedLanguageVersion"] = static () => Base(BaseParse.WithLanguageVersion(LanguageVersion.Latest)),
            ["parsePreprocessorSymbols"] = static () => Base(BaseParse.WithPreprocessorSymbols("DEBUG")),
            ["features"] = static () => Base(BaseParse.WithFeatures([new("strict", "true")])),
            ["kind"] = static () => Base(BaseParse.WithKind(SourceCodeKind.Script)),
            ["documentationMode"] = static () => Base(BaseParse.WithDocumentationMode(DocumentationMode.None)),
            // ⚠ Roslyn throws on trees of two language versions in one compilation, but not on two
            // symbol sets: a loader that parses one file with an extra `#define` moves only that tree.
            ["everyTreeNotTheFirst"] = static () => Base(secondParse: BaseParse.WithPreprocessorSymbols("SECOND")),
            ["assemblyName"] = static () => Base(assemblyName: "Other"),
            ["outputKind"] = static () => Base(options: BaseOptions.WithOutputKind(OutputKind.ConsoleApplication)),
            ["nullableContext"] = static () => Base(
                options: BaseOptions.WithNullableContextOptions(NullableContextOptions.Disable)
            ),
            ["allowUnsafe"] = static () => Base(options: BaseOptions.WithAllowUnsafe(true)),
            ["checkOverflow"] = static () => Base(options: BaseOptions.WithOverflowChecks(true)),
            ["optimization"] = static () => Base(options: BaseOptions.WithOptimizationLevel(OptimizationLevel.Release)),
            ["platform"] = static () => Base(options: BaseOptions.WithPlatform(Platform.X64)),
            ["warningLevel"] = static () => Base(options: BaseOptions.WithWarningLevel(2)),
            ["generalDiagnosticOption"] =
                static () => Base(options: BaseOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Error)),
            ["specificDiagnosticOptions"] = static () => Base(
                options: BaseOptions.WithSpecificDiagnosticOptions([new("SK1005", ReportDiagnostic.Suppress)])
            ),
            ["mainTypeName"] = static () => Base(options: BaseOptions.WithMainTypeName("N.C")),
            ["moduleName"] = static () => Base(options: BaseOptions.WithModuleName("Other.dll")),
            ["scriptClassName"] = static () => Base(options: BaseOptions.WithScriptClassName("Other")),
            ["reportSuppressedDiagnostics"] =
                static () => Base(options: BaseOptions.WithReportSuppressedDiagnostics(true)),
            ["usings"] = static () => Base(options: BaseOptions.WithUsings("System")),
            ["metadataImportOptions"] =
                static () => Base(options: BaseOptions.WithMetadataImportOptions(MetadataImportOptions.All)),
            ["referenceMvid"] = static () => Base(library: SameIdentityImages.Value.Second),
            ["compilationReference"] = static () => Base(
                library: Library("public class L { public void B() { } }").ToMetadataReference()
            ),
            ["compilationReferenceOptions"] = static () => Base(
                library: Library("public class L { public void A() { } }", LanguageVersion.CSharp9)
                    .ToMetadataReference()
            ),
            ["siblingReferences"] = static () => Base() with {
                Siblings = [Sibling(BaseParse, [Corlib, SameIdentityImages.Value.Second])]
            },
            ["siblingLanguageVersion"] = static () => Base() with {
                Siblings = [
                    Sibling(
                        BaseParse.WithLanguageVersion(LanguageVersion.CSharp8),
                        [Corlib, SameIdentityImages.Value.First]
                    )
                ]
            }
        };

    /// <summary>
    ///     The control. Built twice with nothing moved, so every row below is measuring its term and not
    ///     a fingerprint that never repeats.
    /// </summary>
    [Fact]
    public void EveryTermIsDeterministic() {
        Assert.Equal(CacheKey.CompilationFingerprint(Base()), CacheKey.CompilationFingerprint(Base()));
        Assert.Equal(
            CacheKey.CompilationFingerprint(
                Base() with { Siblings = [Sibling(BaseParse, [Corlib, SameIdentityImages.Value.First])] }
            ),
            CacheKey.CompilationFingerprint(
                Base() with { Siblings = [Sibling(BaseParse, [Corlib, SameIdentityImages.Value.First])] }
            )
        );
        Assert.Equal(
            CacheKey.CompilationFingerprint(Base(library: Library("public class L { }").ToMetadataReference())),
            CacheKey.CompilationFingerprint(Base(library: Library("public class L { }").ToMetadataReference()))
        );
    }

    [Theory]
    [MemberData(nameof(Terms))]
    public void MovingOneTerm_MovesTheCompilationFingerprint(string term) {
        var variant = Variants[term]();
        // The rows that compare like with like need a base of their own kind: a sibling against a
        // sibling, a compilation reference against a compilation reference.
        var baseline = term.StartsWith("sibling", StringComparison.Ordinal)
            ? Base() with { Siblings = [Sibling(BaseParse, [Corlib, SameIdentityImages.Value.First])] }
            : term.StartsWith("compilationReference", StringComparison.Ordinal)
                ? Base(library: Library("public class L { public void A() { } }").ToMetadataReference())
                : Base();

        Assert.NotEqual(CacheKey.CompilationFingerprint(baseline), CacheKey.CompilationFingerprint(variant));
    }

    /// <summary>
    ///     ⚠ The row the issue named, with its precondition stated: <c>latest</c> and <c>14.0</c> are the
    ///     <em>same</em> effective version, so only the specified one can tell them apart — and
    ///     <c>SK1133</c> does.
    /// </summary>
    [Fact]
    public void LatestAndAWrittenFourteen_AreOneEffectiveVersionAndTwoKeys() {
        var latest = BaseParse.WithLanguageVersion(LanguageVersion.Latest);
        Assert.Equal(BaseParse.LanguageVersion, latest.LanguageVersion);
        Assert.NotEqual(BaseParse.SpecifiedLanguageVersion, latest.SpecifiedLanguageVersion);

        Assert.NotEqual(
            CacheKey.CompilationFingerprint(Base()),
            CacheKey.CompilationFingerprint(Base(latest))
        );
    }

    /// <summary>
    ///     The reference-MVID row's precondition: the two images really do share an identity, so it is
    ///     the MVID and nothing else that separates them.
    /// </summary>
    [Fact]
    public void TheTwoLibraryImages_ShareAnIdentity() {
        var (first, second) = SameIdentityImages.Value;
        var compilation = CSharpCompilation.Create("probe", [], [Corlib, first]);
        var other = CSharpCompilation.Create("probe", [], [Corlib, second]);

        Assert.Equal(
            ((IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(first)!).Identity,
            ((IAssemblySymbol)other.GetAssemblyOrModuleSymbol(second)!).Identity
        );
    }

    /// <summary>
    ///     The per-file terms: the file's text, the compilation, the rule set and the configuration.
    ///     How the path is normalised has its own file (<c>CacheKeyPathTests</c>).
    /// </summary>
    [Fact]
    public void EachPerFileTerm_MovesTheKey() {
        static string Key(
            string path = "/src/A.cs",
            string content = "class C { }",
            string compilation = "c",
            string rules = "r",
            string config = "e"
        ) =>
            CacheKey.For(path, System.Text.Encoding.UTF8.GetBytes(content), compilation, rules, config);

        Assert.Equal(Key(), Key());
        Assert.NotEqual(Key(), Key("/src/B.cs"));
        Assert.NotEqual(Key(), Key(content: "class D { }"));
        Assert.NotEqual(Key(), Key(compilation: "c2"));
        Assert.NotEqual(Key(), Key(rules: "r2"));
        Assert.NotEqual(Key(), Key(config: "e2"));
    }

    /// <summary>
    ///     #516: the semantic half's fingerprint moves with any tree's text — one the file being keyed is
    ///     not, a tree no reportable path names (a generator's output is one), and a sibling's — and with
    ///     nothing else that the compilation fingerprint does not already carry.
    /// </summary>
    [Fact]
    public void TheSemanticFingerprint_MovesWithEveryTreesText() {
        static string Of(CompilationUnit unit) =>
            CacheKey.SemanticFingerprint(unit, CacheKey.CompilationFingerprint(unit));

        var baseline = Base();
        Assert.Equal(Of(baseline), Of(Base()));

        // B.cs alone: the file A.cs's key would otherwise be blind to.
        var otherFile = baseline with {
            Compilation = baseline.Compilation.ReplaceSyntaxTree(
                baseline.Compilation.SyntaxTrees.Last(),
                CSharpSyntaxTree.ParseText(
                    "namespace N { class D { int x; } }\n",
                    BaseParse,
                    "/src/B.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        };
        Assert.NotEqual(Of(baseline), Of(otherFile));

        // A tree with a generator's kind of path, which no reportable path names.
        var generated = baseline with {
            Compilation = baseline.Compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "namespace N { partial class G { } }\n",
                    BaseParse,
                    "Gen/G.g.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        };
        Assert.NotEqual(Of(baseline), Of(generated));

        var withSibling = baseline with { Siblings = [Sibling(BaseParse, [Corlib, SameIdentityImages.Value.First])] };
        var siblingMoved = baseline with {
            Siblings = [
                CSharpCompilation.Create(
                    "Probe",
                    [
                        CSharpSyntaxTree.ParseText(
                            Source + "// moved\n",
                            BaseParse,
                            "/src/A.cs",
                            cancellationToken: TestContext.Current.CancellationToken
                        )
                    ],
                    [Corlib, SameIdentityImages.Value.First],
                    BaseOptions
                )
            ]
        };
        Assert.NotEqual(Of(withSibling), Of(siblingMoved));

        // ⚠ And it is not the compilation fingerprint: a semantic key equal to a Syntax key would let
        // either half be served as the other.
        Assert.NotEqual(CacheKey.CompilationFingerprint(baseline), Of(baseline));
    }

    static CompilationUnit Base(
        CSharpParseOptions? parse = null,
        CSharpParseOptions? secondParse = null,
        CSharpCompilationOptions? options = null,
        string assemblyName = "Probe",
        MetadataReference? library = null
    ) {
        var first = CSharpSyntaxTree.ParseText(Source, parse ?? BaseParse, "/src/A.cs");
        var second = CSharpSyntaxTree.ParseText(
            "namespace N { class D { } }\n",
            secondParse ?? parse ?? BaseParse,
            "/src/B.cs"
        );
        return new() {
            Name = assemblyName,
            TargetFramework = "net10.0",
            Compilation = CSharpCompilation.Create(
                assemblyName,
                [first, second],
                [Corlib, library ?? SameIdentityImages.Value.First],
                options ?? BaseOptions
            )
        };
    }

    static CSharpCompilation Sibling(CSharpParseOptions parse, ImmutableArray<MetadataReference> references) =>
        CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(Source, parse, "/src/A.cs")],
            references,
            BaseOptions
        );

    static CSharpCompilation Library(string source, LanguageVersion version = LanguageVersion.CSharp14) =>
        CSharpCompilation.Create(
            "Lib",
            [CSharpSyntaxTree.ParseText(source, BaseParse.WithLanguageVersion(version), "/lib/L.cs")],
            [Corlib],
            new(OutputKind.DynamicallyLinkedLibrary, deterministic: true)
        );

    static PortableExecutableReference Image(string source) {
        using var stream = new MemoryStream();
        var emitted = Library(source).Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
