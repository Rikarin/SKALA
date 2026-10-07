using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The documentation-comment rules that read the parsed comment structure: what their fixes leave
///     behind, and what they do on a tree with no structure to read.
/// </summary>
/// <remarks>
///     ⚠ The fixture harness asks only whether a fix parses and silences the rule. Both of these fixes
///     delete lines of a comment, and the question that matters to a reader — whether a bare <c>///</c>
///     or the neighbouring sentence survived — is invisible to both of those checks, so it is pinned here
///     as exact text.
/// </remarks>
public sealed class DocumentationCommentBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ReturnsOnVoidMemberAnalyzer(), new OrphanInheritdocAnalyzer()
    ];

    [Fact]
    public void TheReturnsFix_TakesTheWholeLine() {
        const string Source = """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                /// <returns>Nothing.</returns>
                public void Clear() { }
            }
            """;

        Assert.Equal(
            """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                public void Clear() { }
            }
            """,
            Apply(Source, RuleIds.ReturnsDocumentedOnVoidMember)
        );
    }

    [Fact]
    public void TheReturnsFix_TakesEveryLineOfAMultiLineElement() {
        const string Source = """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                /// <returns>
                ///     The number of entries removed.
                /// </returns>
                /// <remarks>Kept.</remarks>
                public void Clear() { }
            }
            """;

        Assert.Equal(
            """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                /// <remarks>Kept.</remarks>
                public void Clear() { }
            }
            """,
            Apply(Source, RuleIds.ReturnsDocumentedOnVoidMember)
        );
    }

    [Fact]
    public void TheReturnsFix_LeavesASentenceSharingTheLine() {
        const string Source = """
            public sealed class Store {
                /// <summary>Clears the store.</summary> <returns>Nothing.</returns>
                public void Clear() { }
            }
            """;

        // The space before the deleted element stays; that is the formatter's job, not the fix's.
        var lines = Apply(Source, RuleIds.ReturnsDocumentedOnVoidMember).Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("    /// <summary>Clears the store.</summary>", lines[1].TrimEnd());
        Assert.Equal("    public void Clear() { }", lines[2]);
    }

    [Fact]
    public void TheReturnsFix_TakesTheFirstLineOfTheComment() {
        const string Source = """
            public sealed class Store {
                /// <returns>Nothing.</returns>
                /// <summary>Clears the store.</summary>
                public void Clear() { }
            }
            """;

        Assert.Equal(
            """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                public void Clear() { }
            }
            """,
            Apply(Source, RuleIds.ReturnsDocumentedOnVoidMember)
        );
    }

    /// <summary>
    ///     ⚠ Under <c>DocumentationMode.None</c> a <c>///</c> line is an ordinary comment with no
    ///     structure, and the rule is silent — not wrong. That is what a binlog of a project built without
    ///     <c>GenerateDocumentationFile</c> hands the analyzers today (#388), so the rule's reach under
    ///     <c>--load=binlog</c> depends on how that issue is settled.
    /// </summary>
    [Fact]
    public void WithoutDocumentationParsing_TheRuleIsSilent() {
        const string Source = """
            public sealed class Store {
                /// <summary>Clears the store.</summary>
                /// <returns>Nothing.</returns>
                public void Clear() { }
            }
            """;

        Assert.Single(Run(Source, DocumentationMode.Parse), static d => d.Id == RuleIds.ReturnsDocumentedOnVoidMember);
        Assert.DoesNotContain(
            Run(Source, DocumentationMode.None),
            static d => d.Id == RuleIds.ReturnsDocumentedOnVoidMember
        );
    }

    [Fact]
    public void WithoutDocumentationParsing_TheInheritdocRuleIsSilent() {
        const string Source = """
            /// <summary>A cache.</summary>
            public sealed class Cache {
                /// <inheritdoc />
                public void Orphan() { }
            }
            """;

        Assert.Single(Run(Source, DocumentationMode.Parse), static d => d.Id == RuleIds.InheritdocWithNothingToInherit);
        Assert.DoesNotContain(
            Run(Source, DocumentationMode.None),
            static d => d.Id == RuleIds.InheritdocWithNothingToInherit
        );
    }

    /// <summary>
    ///     ⚠ The corpus sweep's whole first result: 150 findings, every one a member implementing an
    ///     interface the compilation could not resolve. A fixture cannot hold this shape — the harness
    ///     rejects a fixture that does not compile — so it is pinned here, in each of the three ways a
    ///     hierarchy fails to bind, beside a canary that the same compilation still reports a real orphan.
    /// </summary>
    [Theory]
    [InlineData("IMissing", "public void Emit() { }")]
    [InlineData("MissingBase", "public void Emit() { }")]
    [InlineData("System.IEquatable<Missing>", "public bool Equals(Missing? other) => false;")]
    public void AnUnboundHierarchy_IsNotJudged(string baseList, string member) {
        var source = $$"""
            /// <summary>A sink.</summary>
            public sealed class Sink : {{baseList}} {
                /// <inheritdoc />
                {{member}}
            }

            /// <summary>A canary in the same compilation.</summary>
            public sealed class Canary {
                /// <inheritdoc />
                public void Orphan() { }
            }
            """;

        var findings = Run(source, DocumentationMode.Parse)
            .Where(static d => d.Id == RuleIds.InheritdocWithNothingToInherit)
            .ToArray();

        var finding = Assert.Single(findings);
        Assert.Contains("`Orphan`", finding.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void TheInheritdocFix_RestoresThePartialDefinitionsProse() {
        const string Source = """
            /// <summary>A loader.</summary>
            public sealed partial class Loader {
                /// <summary>Loads everything.</summary>
                public partial void Load();

                /// <inheritdoc />
                public partial void Load() { }
            }
            """;

        Assert.Equal(
            """
            /// <summary>A loader.</summary>
            public sealed partial class Loader {
                /// <summary>Loads everything.</summary>
                public partial void Load();

                public partial void Load() { }
            }
            """,
            Apply(Source, RuleIds.InheritdocWithNothingToInherit)
        );
    }

    static ImmutableArray<Diagnostic> Run(string source, DocumentationMode mode) {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview).WithDocumentationMode(mode),
            "probe.cs",
            cancellationToken: TestContext.Current.CancellationToken
        );
        var compilation = CSharpCompilation.Create(
            "probe",
            [tree],
            RuleFixtures.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
    }

    /// <summary>Applies every edit the single finding of <paramref name="id" /> carries.</summary>
    static string Apply(string source, string id) {
        var diagnostic = Assert.Single(
            RuleFixtures.Analyze(
                    RuleFixtures.Compile(source, "probe.cs"),
                    Analyzers,
                    TestContext.Current.CancellationToken
                )
                .Where(d => d.Id == id)
        );

        var count = int.Parse(diagnostic.Properties[FixEdits.CountKey]!, CultureInfo.InvariantCulture);
        var edits = Enumerable.Range(0, count)
            .Select(index => new TextChange(
                    new TextSpan(
                        int.Parse(diagnostic.Properties[FixEdits.StartKey(index)]!, CultureInfo.InvariantCulture),
                        int.Parse(diagnostic.Properties[FixEdits.LengthKey(index)]!, CultureInfo.InvariantCulture)
                    ),
                    diagnostic.Properties[FixEdits.TextKey(index)]!
                )
            );

        return SourceText.From(source).WithChanges(edits).ToString();
    }
}
