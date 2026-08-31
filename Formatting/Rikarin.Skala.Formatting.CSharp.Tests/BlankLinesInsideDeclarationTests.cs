using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>blank_lines_inside_type</c> and <c>blank_lines_inside_namespace</c>, at a value the export
///     does not set.
/// </summary>
/// <remarks>
///     ⚠ These exist because the corpus fixture cannot pin them and a sabotage test proved it. Both
///     keys are <c>0</c> in the Rider export, so <c>constructs/blank-lines/*_inside_*.expected.cs</c>
///     is the oracle's answer at <c>0</c> — a file that is byte-identical whether the rule reaches an
///     <c>enum</c> body, a <c>namespace</c> body, both or neither. Narrowing
///     <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax" /> to
///     <c>TypeDeclarationSyntax</c> — which drops <c>enum</c> — left all 687 conformance tests green.
///     <para>
///         ⚠ Everything asserted here was measured against <c>jb cleanupcode</c> 2025.2.6 under this
///         repository's own <c>.editorconfig</c>, which sets
///         <c>remove_blank_lines_near_braces_in_declarations = true</c> and
///         <c>keep_blank_lines_in_declarations = 2</c>. The oracle pads the braces anyway, and pads five
///         at <c>5</c>, so the requirement outranks both the removal and the cap. The shape of *which*
///         bodies have an inside was probed body kind by body kind rather than read off the option's
///         name.
///     </para>
/// </remarks>
public class BlankLinesInsideDeclarationTests {
    static string Format(string source, params (string Key, string Value)[] overrides) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    /// <summary>Class, struct, interface, record and enum all have an inside. An enum is a type.</summary>
    [Theory]
    [InlineData("class C {\n    int a;\n}\n")]
    [InlineData("struct S {\n    int a;\n}\n")]
    [InlineData("interface I {\n    void M();\n}\n")]
    [InlineData("record R {\n    int a;\n}\n")]
    [InlineData("enum E {\n    A\n}\n")]
    public void EveryTypeBodyIsPadded(string source) {
        var formatted = Format(source, ("resharper_csharp_blank_lines_inside_type", "2"));
        Assert.Contains("{\n\n\n", formatted, StringComparison.Ordinal);
        Assert.Contains("\n\n\n}", formatted, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A method body, an accessor list and an <c>if</c> block sit inside a type and are not the
    ///     type's own brace.
    /// </summary>
    [Fact]
    public void OnlyTheTypesOwnBracesArePadded() {
        var formatted = Format(
            "class C {\n"
            + "    int a;\n\n"
            + "    void M() {\n        if (a > 0) {\n            a = 1;\n        }\n    }\n\n"
            + "    int P {\n        get { return a; }\n    }\n"
            + "}\n",
            ("resharper_csharp_blank_lines_inside_type", "2")
        );

        Assert.StartsWith("class C {\n\n\n    int a;", formatted, StringComparison.Ordinal);
        Assert.EndsWith("\n\n\n}\n", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("void M() {\n\n", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("if (a > 0) {\n\n", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("int P {\n\n", formatted, StringComparison.Ordinal);
    }

    /// <summary>A block-bodied namespace has an inside; a file-scoped one has no braces to be inside of.</summary>
    [Fact]
    public void OnlyABlockBodiedNamespaceIsPadded() {
        var block = Format(
            "namespace N {\n    class C {\n        int a;\n    }\n}\n",
            ("resharper_csharp_blank_lines_inside_namespace", "2")
        );
        Assert.StartsWith("namespace N {\n\n\n", block, StringComparison.Ordinal);
        Assert.EndsWith("\n\n\n}\n", block, StringComparison.Ordinal);

        var scoped = Format(
            "namespace N;\n\nclass C {\n    int a;\n}\n",
            ("resharper_csharp_blank_lines_inside_namespace", "2")
        );
        Assert.Equal(Format("namespace N;\n\nclass C {\n    int a;\n}\n"), scoped);
    }

    /// <summary>
    ///     ⚠ The requirement outranks <c>remove_blank_lines_near_braces_in_declarations</c>, which the
    ///     export sets to <c>true</c>. Ordered like every other requirement it could never be observed.
    /// </summary>
    [Fact]
    public void ItOutranksTheRemovalAndTheCap() {
        var removal = Format(
            "class C {\n    int a;\n}\n",
            ("resharper_csharp_blank_lines_inside_type", "2"),
            ("resharper_csharp_remove_blank_lines_near_braces_in_declarations", "true")
        );
        Assert.StartsWith("class C {\n\n\n    int a;\n\n\n}", removal, StringComparison.Ordinal);

        // `keep_blank_lines_in_declarations` caps what the author wrote; it does not cap what the
        // requirement asks for. Measured at 5 against the oracle with the cap at 2.
        var cap = Format(
            "class C {\n    int a;\n}\n",
            ("resharper_csharp_blank_lines_inside_type", "5"),
            ("resharper_csharp_keep_blank_lines_in_declarations", "2")
        );
        Assert.StartsWith("class C {\n\n\n\n\n\n    int a;", cap, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The export sets both to 0, so honouring them moves nothing at this repository's own configuration.
    /// </summary>
    [Fact]
    public void AtTheExportsOwnValuesNothingMoves() {
        const string source =
            "namespace N {\n    class C {\n        int a;\n    }\n\n    enum E {\n        A\n    }\n}\n";
        Assert.Equal(source, Format(source));
    }
}
