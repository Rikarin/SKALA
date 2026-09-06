using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

public sealed class RequiredBracesTests {
    [Fact]
    public void CancellationGuard_UsesABlockByDefault() {
        const string source =
            "class C { void M(System.Threading.CancellationTokenSource source) { "
            + "if (source.IsCancellationRequested) return; } }";
        var result = Format.Run(source);
        Assert.Contains("if (source.IsCancellationRequested) {\n            return;\n        }", result.Formatted);
        Assert.Equal(source, result.Original.ToString());
        Assert.Equal(result.Formatted, EditEmitter.Apply(source, result.Edits));
        AssertStable(result);
    }

    [Fact]
    public void RegistryDefaults_RequireBraces() {
        var result = CSharpFormatter.Format(
            "Test.cs",
            SourceText.From("if (true) return;"),
            FormattingOptions.Defaults
        );
        Assert.IsType<BlockSyntax>(Root(result).DescendantNodes().OfType<IfStatementSyntax>().Single().Statement);
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
    }

    [Fact]
    public void CrLf_IsPreservedWhenBracesAreInserted() {
        var result = Format.Run("class C {\r\n    void M() {\r\n        if (true) return;\r\n    }\r\n}\r\n");
        Assert.Contains("if (true) {\r\n            return;\r\n        }", result.Formatted);
        Assert.DoesNotContain("\n", result.Formatted.Replace("\r\n", "", StringComparison.Ordinal));
        AssertStable(result);
    }

    [Fact]
    public void RangeFormatting_KeepsTheBracePairTogether() {
        const string source =
            "class C {\n    void M(bool b) {\n        if (b)\n            return;\n\n        M( b );\n    }\n}\n";
        var result = Format.Run(source);
        var start = source.IndexOf("if (b)", StringComparison.Ordinal);
        var edits = EditEmitter.Restrict(result.Edits, SourceSpan.FromBounds(start, start + 6));
        var selected = EditEmitter.Apply(source, edits);
        var tree = CSharpSyntaxTree.ParseText(selected, cancellationToken: TestContext.Current.CancellationToken);
        Assert.DoesNotContain(
            tree.GetDiagnostics(TestContext.Current.CancellationToken),
            static d => d.Severity == DiagnosticSeverity.Error
        );
        Assert.Contains("if (b) {\n            return;\n        }", selected);
        Assert.Contains("M( b );", selected);
    }

    [Theory]
    [InlineData("false:none")]
    [InlineData("false:warning")]
    [InlineData("when_multiline:suggestion")]
    public void OptOut_PreservesASimpleUnbracedGuard(string preference) {
        var result = Run("class C { void M(bool b) { if (b) return; } }", preference);
        Assert.IsType<ReturnStatementSyntax>(
            Root(result).DescendantNodes().OfType<IfStatementSyntax>().Single().Statement
        );
        Assert.Equal(result.Formatted, Run(result.Formatted, preference).Formatted);
    }

    [Fact]
    public void MultilinePreference_UsesTheFormattedStatement() {
        var result = Run("class C { void M(bool a, bool b) { if (a) if (b) { return; } } }", "when_multiline");
        var statements = Root(result).DescendantNodes().OfType<IfStatementSyntax>().ToArray();
        Assert.IsType<BlockSyntax>(statements[0].Statement);
        Assert.IsType<BlockSyntax>(statements[1].Statement);
        Assert.Equal(result.Formatted, Run(result.Formatted, "when_multiline").Formatted);
    }

    [Fact]
    public void NestedIf_PreservesElseBindingAndElseIfChains() {
        var result = Format.Run(
            "class C { void M(bool a, bool b, bool c) { if (a) if (b) return; else if (c) return; else return; } }"
        );
        var statements = Root(result).DescendantNodes().OfType<IfStatementSyntax>().ToArray();
        Assert.Null(statements[0].Else);
        Assert.IsType<IfStatementSyntax>(statements[1].Else!.Statement);
        Assert.IsType<BlockSyntax>(statements[2].Else!.Statement);
        Assert.All(statements, static statement => Assert.IsType<BlockSyntax>(statement.Statement));
        AssertStable(result);
    }

    [Theory]
    [InlineData("if (true) return; // keep this comment\n")]
    [InlineData("if (true)\n// explain return\nreturn;\n")]
    [InlineData("if (true) /* explain */ return;")]
    public void Comments_SurviveBraceInsertion(string statement) {
        var source = "class C { void M() { " + statement + " } }";
        var result = Format.Run(source);
        var comments = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantTrivia()
            .Where(static t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
            )
            .Select(static t => t.ToString());
        foreach (var comment in comments) {
            Assert.Contains(comment, result.Formatted);
        }

        AssertStable(result);
    }

    [Theory]
    [InlineData("for (;;) break;")]
    [InlineData("foreach (var x in new int[0]) continue;")]
    [InlineData("while (true) break;")]
    [InlineData("do break; while (true);")]
    [InlineData("lock (this) return;")]
    [InlineData("using (var x = new System.IO.MemoryStream()) return;")]
    public void EmbeddedStatements_AlsoUseBlocks(string statement) {
        var result = Format.Run("class C { void M() { " + statement + " } }");
        Assert.Equal(2, Root(result).DescendantNodes().OfType<BlockSyntax>().Count());
        AssertStable(result);
    }

    [Fact]
    public void FormatterOff_ProtectsTheGuard() {
        const string protectedText = "if (true) return;";
        var result = Format.Run(
            "class C { void M() {\n// @formatter:off\n" + protectedText
            + "\n// @formatter:on\nif (false) return;\n} }"
        );
        var statements = Root(result).DescendantNodes().OfType<IfStatementSyntax>().ToArray();
        Assert.IsType<ReturnStatementSyntax>(statements[0].Statement);
        Assert.IsType<BlockSyntax>(statements[1].Statement);
        Assert.Contains(protectedText, result.Formatted);
        AssertStable(result);
    }

    [Fact]
    public void ConditionalDirectives_AreNotWrappedAcrossBranches() {
        var result = Format.Run("class C { void M() { if (true)\n#if DEBUG\nreturn;\n#else\nreturn;\n#endif\n} }");
        Assert.IsType<ReturnStatementSyntax>(
            Root(result).DescendantNodes().OfType<IfStatementSyntax>().Single().Statement
        );
        AssertStable(result);
    }

    [Theory]
    [InlineData("Test.g.cs", "if (true) return;", FormatOutcome.Generated)]
    [InlineData("Test.cs", "if (true", FormatOutcome.NotParseable)]
    public void SkippedFiles_AreUnchanged(string path, string source, FormatOutcome outcome) {
        var result = Format.Run(source, path);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(source, result.Formatted);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public void DisabledFormatter_DoesNotInsertBraces() {
        const string source = "if (true) return;";
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [new("skala_disable_formatter", "true")]
        ).Options;
        Assert.Equal(source, CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted);
    }

    static SyntaxNode Root(FormatResult result) =>
        CSharpSyntaxTree.ParseText(result.Formatted, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken);

    static void AssertStable(FormatResult result) {
        Assert.Equal(FormatOutcome.Formatted, result.Outcome);
        Assert.DoesNotContain(Root(result).GetDiagnostics(), static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(result.Formatted, Format.Text(result.Formatted));
    }

    static FormatResult Run(string source, string preference) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [new("csharp_prefer_braces", preference)]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options);
    }
}
