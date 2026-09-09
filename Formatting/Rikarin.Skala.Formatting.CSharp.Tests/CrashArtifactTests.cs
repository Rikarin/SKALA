using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Reflection;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     What the dropped reproduction <em>says</em>, rather than that one was dropped.
/// </summary>
/// <remarks>
///     ⚠ These assert on artefact content, and that is the whole point of them. The artefact is the only
///     record of an arrangement refusal — nothing is written and the run continues — and it was wrong for
///     every refusal Skala had ever dropped: all three layers built a fresh <c>new PhaseOneOptions()</c>
///     at the moment they wrote the folder, so every <c>config.snapshot</c> read <c>indent_size = 0</c>,
///     <c>max_line_length = 0</c> and an empty <c>new_line_before_open_brace</c>. A test asserting that a
///     directory appeared passed throughout (#348).
///     <para>
///         ⚠ The overrides below are values nothing else in the repository uses — an indent of 7, a margin
///         of 43. Asserting the repository's own 4 and 120 would be indistinguishable from asserting a
///         default that happened to agree, and "the snapshot describes something other than the run" is
///         precisely the defect.
///     </para>
/// </remarks>
public sealed class CrashArtifactTests {
    const string ProbePath = "Probe.cs";

    /// <summary>A rewrite that leaves the code compiling and pointing at a different symbol.</summary>
    /// <remarks>
    ///     ⚠ A <em>type</em> rebinding, and it has to be. The first draft of this fixture shadowed a
    ///     field with a local of the same name — the shape <see cref="ArrangementSafety" />'s own
    ///     remarks give as the motivating example — and layer 3 did not fire on it. Layer 3 compares
    ///     <c>SymbolDisplayFormat.FullyQualifiedFormat</c>, whose <c>MemberOptions</c> is
    ///     <c>None</c>: a field <c>C.n</c> and a local <c>n</c> both render as the bare string
    ///     <c>"n"</c>, so they compare equal. Measured, not reasoned — see the report on #348. Only
    ///     types and namespaces are qualified by that format (<c>global::A.T</c> vs
    ///     <c>global::B.T</c>), so only a type change is observable to the layer as it stands.
    ///     <para>
    ///         ⚠ <c>CS0219</c> on <c>t</c> is present in both halves and therefore cancels out of layer
    ///         2's appeared-set, which is what keeps this fixture measuring layer 3 rather than layer 2.
    ///     </para>
    /// </remarks>
    const string SymbolBefore = """
                                namespace A { class T { } }
                                namespace B { class T { } }

                                namespace P {
                                    using A;

                                    class C {
                                        void M() {
                                            T t = null;
                                        }
                                    }
                                }
                                """;

    const string SymbolAfter = """
                               namespace A { class T { } }
                               namespace B { class T { } }

                               namespace P {
                                   using B;

                                   class C {
                                       void M() {
                                           T t = null;
                                       }
                                   }
                               }
                               """;

    const string Compiles = "class C { int M() { return 1; } }";
    const string DoesNot = "class C { int M() { return Missing(); } }";

    [Fact]
    public void ASymbolIdentityRefusal_SnapshotsTheRunsOwnOptions() {
        using var scope = new Scope();
        var artefact = scope.Refuse(SymbolBefore, SymbolAfter);

        Assert.Equal(ArrangeIds.SymbolChanged, artefact.Diagnostic.Id);
        AssertRunsOptions(artefact);
        Assert.Contains("# layer: arrange/symbol-identity", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains("# diagnostic: SK9096", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains("bound to", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains(artefact.Diagnostic.Message, artefact.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ADiagnosticDeltaRefusal_SnapshotsTheRunsOwnOptions() {
        using var scope = new Scope();
        var artefact = scope.Refuse(Compiles, DoesNot);

        Assert.Equal(ArrangeIds.Reverted, artefact.Diagnostic.Id);
        AssertRunsOptions(artefact);
        Assert.Contains("# layer: arrange/diagnostic-delta", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains("# diagnostic: SK9098", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains("CS0103", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains(artefact.Diagnostic.Message, artefact.Refusal, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Forced by handing <c>Check</c> a tree the compilation does not contain, so
    ///     <c>ReplaceSyntaxTree</c> throws out of <c>Evaluate</c>. A real shape rather than a contrived
    ///     one: SK-FUZZ-0012 is Roslyn's binder throwing <c>IndexOutOfRangeException</c> from inside the
    ///     same call chain. This is also the layer whose artefact mattered most and said least — it is the
    ///     one where Skala does not know what went wrong.
    /// </summary>
    [Fact]
    public void ARebindThatThrows_StillSnapshotsTheRunsOwnOptions() {
        using var scope = new Scope();
        var artefact = scope.Refuse(Compiles, Compiles, true);

        Assert.Equal(ArrangeIds.Reverted, artefact.Diagnostic.Id);
        AssertRunsOptions(artefact);
        Assert.Contains("# layer: arrange/rebind-threw", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains("the safety re-bind threw", artefact.Refusal, StringComparison.Ordinal);
        Assert.Contains(artefact.Diagnostic.Message, artefact.Refusal, StringComparison.Ordinal);
    }

    /// <summary>The three layers are told apart by the artefact, which is the point of naming them.</summary>
    /// <remarks>
    ///     ⚠ Two of the three carry the same diagnostic id (<c>SK9098</c>) and all three produce the same
    ///     folder shape, so before this the three were indistinguishable on disk — "three different bugs
    ///     with the same artefact shape".
    /// </remarks>
    [Fact]
    public void TheThreeLayers_AreDistinguishableOnDisk() {
        using var symbol = new Scope();
        using var delta = new Scope();
        using var threw = new Scope();

        var layers = new[] {
            Layer(symbol.Refuse(SymbolBefore, SymbolAfter).Refusal), Layer(delta.Refuse(Compiles, DoesNot).Refusal),
            Layer(threw.Refuse(Compiles, Compiles, true).Refusal)
        };

        Assert.Equal(3, layers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            ["arrange/diagnostic-delta", "arrange/rebind-threw", "arrange/symbol-identity"],
            layers.Order(StringComparer.Ordinal)
        );
    }

    /// <summary>
    ///     ⚠ The folder is keyed on a hash of the input alone, so a <c>format</c> failure on text an
    ///     <c>arrange</c> refusal already covered lands in the same directory. Without the removal in
    ///     <c>WriteRefusal</c> the arrange run's <c>refusal.txt</c> survives beside the format run's
    ///     <c>output.cs</c> and reads exactly like a fresh one.
    /// </summary>
    [Fact]
    public void AFormatArtefact_DoesNotInheritAnEarlierArrangeRefusal() {
        using var scope = new Scope();
        var arrange = scope.Refuse(SymbolBefore, SymbolAfter);
        Assert.Contains("arrange/symbol-identity", arrange.Refusal, StringComparison.Ordinal);

        var again = CrashArtifacts.Write(scope.Root, ProbePath, SymbolBefore, "class D { }", Format.Options);
        Assert.Equal(arrange.Folder, again);
        Assert.NotNull(again);
        Assert.False(File.Exists(Path.Combine(again, "refusal.txt")));

        // ⚠ And the snapshot loses the arrangement section with it: a `format` refusal ran no
        // arrangement rule, and restating the previous run's settings would be the same lie in a
        // different file.
        var snapshot = File.ReadAllText(Path.Combine(again, "config.snapshot"));
        Assert.DoesNotContain("# arrangement", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("arrange_", snapshot, StringComparison.Ordinal);
    }

    /// <summary>The settings that actually drive the rewrites reach the snapshot.</summary>
    /// <remarks>
    ///     ⚠ Named keys rather than "the section is non-empty". #348's second half is that five phase-one
    ///     keys under-specify an arrange failure even once they hold real values: the rewrites are driven
    ///     by the null-checking, object-creation and argument-style families, none of which
    ///     <see cref="PhaseOneOptions" /> carries at all.
    /// </remarks>
    [Fact]
    public void AnArrangeSnapshot_CarriesTheArrangementSettingsToo() {
        using var scope = new Scope();
        var snapshot = scope.Refuse(SymbolBefore, SymbolAfter).Snapshot;

        Assert.Contains("# arrangement", snapshot, StringComparison.Ordinal);
        Assert.Contains("arrange_scope = Full", snapshot, StringComparison.Ordinal);
        foreach (var key in (string[])[
                     "arrange_null_checking_pattern",
                     "arrange_object_creation_when_type_evident",
                     "arrange_arguments_literal",
                     "arrange_arguments_skip_single",
                     "arrange_var_when_type_is_apparent"
                 ]) {
            Assert.False(
                string.IsNullOrEmpty(Value(snapshot, key)),
                key + " is missing from the arrangement section, or is empty"
            );
        }

        // ⚠ The prefix is load-bearing, and this is what proves it. `indent_size` is one option: the
        // override below feeds both structs and both lines read 7. `max_line_length` is *two* —
        // PhaseOneOptions reads `skala_max_line_length` (43, from the override) and ArrangementOptions
        // reads the inert generic `max_line_length` (unset, so Math.Max(1, 0)). Unprefixed the
        // snapshot would state one key twice with two values and a reader would take the last.
        Assert.Equal("7", Value(snapshot, "arrange_indent_size"));
        Assert.Equal("43", Value(snapshot, "max_line_length"));
        Assert.Equal("1", Value(snapshot, "arrange_max_line_length"));
    }

    /// <summary>
    ///     ⚠ The snapshot is reflected off <see cref="ArrangementOptions" /> rather than hand-listed, so
    ///     an option added later cannot go missing silently. This is what asserts that: every public
    ///     instance property except the nested <c>PhaseOne</c> has a line of its own.
    /// </summary>
    [Fact]
    public void TheArrangementSection_NamesEveryArrangementProperty() {
        using var scope = new Scope();
        var snapshot = scope.Refuse(SymbolBefore, SymbolAfter).Snapshot;

        var expected = typeof(ArrangementOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static property => property.PropertyType != typeof(PhaseOneOptions))
            .ToArray();

        Assert.NotEmpty(expected);
        var missing = expected
            .Select(static property => SnakeCase(property.Name))
            .Where(key => !snapshot.Contains(key + " = ", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(missing);

        // The nested struct is excluded rather than dumped through its ToString; its five keys are
        // already the phase-one block above.
        Assert.DoesNotContain("arrange_phase_one", snapshot, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The zeroes #348 reported, asserted as absent, and the run's own values asserted as present.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the sabotage anchor. Point any of the three layers back at
    ///     <c>new PhaseOneOptions()</c> and this goes red first, on the exact strings from the issue's
    ///     paste.
    /// </remarks>
    static void AssertRunsOptions(Artefact artefact) {
        Assert.Equal("7", Value(artefact.Snapshot, "indent_size"));
        Assert.Equal("43", Value(artefact.Snapshot, "max_line_length"));

        // ⚠ Asserted as *non-empty* rather than as a literal. The issue's paste ends
        // `new_line_before_open_brace =` with nothing after it, which is what a default-constructed
        // struct's null string renders as; the repository's own value is a style name.
        Assert.False(
            string.IsNullOrEmpty(Value(artefact.Snapshot, "new_line_before_open_brace")),
            "new_line_before_open_brace is empty, which is what a default-constructed PhaseOneOptions prints"
        );
    }

    /// <summary>The value of one <c>key = value</c> line, or null when the key has no line.</summary>
    static string? Value(string snapshot, string key) {
        foreach (var line in snapshot.Split('\n')) {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith(key + " = ", StringComparison.Ordinal)) {
                return trimmed[(key.Length + 3)..];
            }
        }

        return null;
    }

    static string Layer(string refusal) => refusal.Split('\n')[0].TrimEnd('\r')["# layer: ".Length..];

    static string SnakeCase(string name) {
        var builder = new StringBuilder("arrange_");
        for (var index = 0; index < name.Length; index++) {
            if (index > 0 && char.IsUpper(name[index])) {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[index]));
        }

        return builder.ToString();
    }

    sealed record Artefact(string Folder, string Snapshot, string Refusal, SkalaDiagnostic Diagnostic);

    /// <summary>A throwaway <c>.skala</c> root, plus the one call that forces a layer to refuse.</summary>
    sealed class Scope : IDisposable {
        readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("skala-crash-348-");

        public string Root => _directory.FullName;

        public void Dispose() => _directory.Delete(true);

        /// <param name="detach">
        ///     Hand <c>Check</c> a tree the compilation does not hold, so its <c>ReplaceSyntaxTree</c>
        ///     throws and the outermost catch — the layer that could not answer — is the one that writes.
        /// </param>
        public Artefact Refuse(string original, string arranged, bool detach = false) {
            var tree = CSharpSyntaxTree.ParseText(original, CSharpFormatter.ParseOptions, ProbePath);
            var compilation = CSharpCompilation.Create(
                "probe",
                [tree],
                SharedFrameworkReferences.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            var resolved = OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, ProbePath),
                [new("indent_size", "7"), new("skala_max_line_length", "43")]
            ).Options;

            var passed = detach
                ? CSharpSyntaxTree.ParseText(original, CSharpFormatter.ParseOptions, ProbePath)
                : tree;

            var diagnostic = ArrangementSafety.Check(
                ProbePath,
                compilation,
                passed,
                passed.GetRoot(),
                arranged,
                compilation.GetSemanticModel(tree),
                Root,
                new ArrangementOptions(resolved),
                original
            );

            Assert.NotNull(diagnostic);
            var folder = Directory.EnumerateDirectories(Path.Combine(Root, "crash")).Single();
            return new(
                folder,
                File.ReadAllText(Path.Combine(folder, "config.snapshot")),
                File.ReadAllText(Path.Combine(folder, "refusal.txt")),
                diagnostic
            );
        }
    }
}
