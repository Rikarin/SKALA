using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using GeneratorDriver = Rikarin.Skala.Analysis.Loading.GeneratorDriver;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     #634: the binlog loader re-runs the build's generators, and some of them never ran.
/// </summary>
/// <remarks>
///     ⚠ Measured on Vixen before anything here was written. <c>Vixen.Net.Generators.dll</c> holds two
///     <c>IIncrementalGenerator</c>s, <c>ReplicationGenerator</c> and <c>RpcGenerator</c>; a
///     <c>--load=binlog</c> run saw the first one's output and reported 24 compiler errors about the
///     second's (<c>CS0103 RpcMethods</c>, <c>CS0117 RpcMethodTable</c>, <c>CS1061 Rpc</c>). The
///     assembly loaded, both types constructed, no exception was thrown and nothing was reported:
///     <c>AnalyzerFileReference.GetGeneratorsForAllLanguages()</c> returned one generator out of two.
///     <para>
///         ⚠ These go through <see cref="GeneratorDriver.Run" /> — the call the binlog loader makes for
///         every <c>csc</c> invocation — with a generator assembly emitted on the spot, rather than
///         through a real <c>dotnet build -bl</c>. A built fixture needs a generator compiled against a
///         Roslyn no newer than the SDK's own compiler (or <c>csc</c> refuses it with <c>CS9057</c>) and
///         no newer than the one Skala hosts, and no single reference satisfies both on every machine.
///         The emitted assembly is the same file shape the build hands <c>/analyzer:</c>.
///     </para>
/// </remarks>
public sealed class GeneratorDriverTests {
    const string TwoGenerators = """
                                 using Microsoft.CodeAnalysis;

                                 namespace Probe634;

                                 [Generator(LanguageNames.CSharp)]
                                 public sealed class FirstGenerator : IIncrementalGenerator {
                                     public void Initialize(IncrementalGeneratorInitializationContext context) =>
                                         context.RegisterPostInitializationOutput(static output => output.AddSource(
                                             "First.g.cs",
                                             "namespace Probe634.Generated { internal static class First { } }"));
                                 }

                                 [Generator(LanguageNames.CSharp)]
                                 public sealed class SecondGenerator : IIncrementalGenerator {
                                     public void Initialize(IncrementalGeneratorInitializationContext context) =>
                                         context.RegisterPostInitializationOutput(static output => output.AddSource(
                                             "Second.g.cs",
                                             "namespace Probe634.Generated { internal static class Second { } }"));
                                 }
                                 """;

    /// <summary>
    ///     ⚠ The defect itself: an assembly with two incremental generators contributes both.
    /// </summary>
    /// <remarks>
    ///     Sabotage: put <c>reference.GetGeneratorsForAllLanguages()</c> back in
    ///     <c>GeneratorDriver.Run</c> and <c>Second</c> is absent — the consumer's use of it is the
    ///     <c>CS0103</c> Vixen reported.
    /// </remarks>
    [Fact]
    public void EveryGeneratorInAnAssembly_Runs() {
        using var directory = new GeneratorDirectory();
        var assembly = directory.Emit("Probe634.Two", TwoGenerators);
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var outcome = Run(
            assembly,
            """
            using Probe634.Generated;

            internal static class Consumer {
                static void Use() {
                    _ = typeof(First);
                    _ = typeof(Second);
                }
            }
            """,
            diagnostics
        );

        Assert.NotNull(outcome.Compilation.GetTypeByMetadataName("Probe634.Generated.First"));
        Assert.NotNull(outcome.Compilation.GetTypeByMetadataName("Probe634.Generated.Second"));
        Assert.DoesNotContain(
            outcome.Compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        Assert.True(outcome.Complete);
        Assert.Empty(diagnostics);
    }

    /// <summary>
    ///     ⚠ A generator type that will not construct is reported and marks the compilation incomplete.
    /// </summary>
    /// <remarks>
    ///     <c>AnalyzerFileReference</c> reports this through its <c>AnalyzerLoadFailed</c> event and never
    ///     throws, so the <c>catch</c> that was meant to produce <c>SK9031</c> could not see it. Sabotage:
    ///     remove the event subscription and the run is silent and claims to be complete.
    /// </remarks>
    [Fact]
    public void AGeneratorThatWillNotConstruct_IsReported() {
        using var directory = new GeneratorDirectory();
        var assembly = directory.Emit(
            "Probe634.Unconstructible",
            """
            using Microsoft.CodeAnalysis;

            namespace Probe634;

            [Generator(LanguageNames.CSharp)]
            public sealed class BrokenGenerator : IIncrementalGenerator {
                public BrokenGenerator() => throw new System.InvalidOperationException("no");

                public void Initialize(IncrementalGeneratorInitializationContext context) { }
            }
            """
        );
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var outcome = Run(assembly, "internal static class Consumer;", diagnostics);

        Assert.False(outcome.Complete);
        var reported = Assert.Single(diagnostics);
        Assert.Equal(RuleIds.AnalyzerFailedToLoad, reported.Id);
        Assert.Equal(SkalaSeverity.Warning, reported.Severity);
        Assert.Contains("Probe634.BrokenGenerator", reported.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ A generator that throws while running is reported and marks the compilation incomplete.
    /// </summary>
    /// <remarks>
    ///     Roslyn's driver catches the exception and renders it as a <c>CS8784</c> <em>warning</em>, which
    ///     the error-only forwarding never passed on. Sabotage: drop the loop over
    ///     <c>GetRunResult().Results</c> and this goes red.
    /// </remarks>
    [Fact]
    public void AGeneratorThatThrows_IsReported() {
        using var directory = new GeneratorDirectory();
        var assembly = directory.Emit(
            "Probe634.Throwing",
            """
            using Microsoft.CodeAnalysis;

            namespace Probe634;

            [Generator(LanguageNames.CSharp)]
            public sealed class ThrowingGenerator : IIncrementalGenerator {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    throw new System.InvalidOperationException("no");
            }
            """
        );
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        var outcome = Run(assembly, "internal static class Consumer;", diagnostics);

        Assert.False(outcome.Complete);
        var reported = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == RuleIds.AnalyzerThrew);
        Assert.Equal(SkalaSeverity.Warning, reported.Severity);
        Assert.Contains("Probe634.ThrowingGenerator", reported.Message, StringComparison.Ordinal);
    }

    static GeneratorDriver.Outcome Run(
        string assembly,
        string consumer,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) =>
        GeneratorDriver.Run(
            CSharpCompilation.Create(
                "Consumer",
                [CSharpSyntaxTree.ParseText(consumer, cancellationToken: TestContext.Current.CancellationToken)],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                new(OutputKind.DynamicallyLinkedLibrary)
            ),
            [assembly],
            [],
            null,
            new(),
            diagnostics,
            TestContext.Current.CancellationToken
        );

    /// <summary>
    ///     A directory holding one emitted generator assembly.
    /// </summary>
    /// <remarks>
    ///     ⚠ One directory per test, because <see cref="GeneratorDriver" /> keys its load contexts on the
    ///     directory and keeps them for the life of the process. The assembly stays loaded and so stays
    ///     locked on Windows; a directory that will not delete is not a test result.
    /// </remarks>
    sealed class GeneratorDirectory : IDisposable {
        readonly string root = Directory.CreateTempSubdirectory("skala-generator-").FullName;

        public string Emit(string assemblyName, string source) {
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(static path => MetadataReference.CreateFromFile(path));

            var compilation = CSharpCompilation.Create(
                assemblyName,
                [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
                references,
                new(OutputKind.DynamicallyLinkedLibrary)
            );

            var path = Path.Combine(root, assemblyName + ".dll");
            var emitted = compilation.Emit(path, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));

            return path;
        }

        public void Dispose() {
            try {
                Directory.Delete(root, true);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                // The loaded assembly holds its file open; the OS reclaims the temp directory.
            }
        }
    }
}
