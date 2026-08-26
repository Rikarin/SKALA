namespace Rikarin.Skala.Formatting.Tests;

public sealed class DocumentBuilderTests {
    [Fact]
    public void Build_NestsChildren_InSourceOrder() {
        var builder = new DocumentBuilder();
        builder.Text("a", new SourceSpan(0, 1));
        builder.OpenIndent(IndentKind.Block);
        builder.Line(LineKind.Hard);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();
        builder.Line(LineKind.Hard);
        builder.Text("c", new SourceSpan(4, 1));

        var document = builder.Build();
        var layout = LayoutWriter.Write(document, Fitter.Resolve(document, 120), "    ", "\n");

        Assert.Equal("a\n    b\nc", layout.Text);
    }

    [Fact]
    public void Line_KeepsTheSourcesOwnEnding() {
        // ⚠ enforce_line_ending_style = false means mixed endings are preserved, not normalised.
        var builder = new DocumentBuilder();
        builder.Text("a", new SourceSpan(0, 1));
        builder.Line(LineKind.Hard, 0, "\r\n");
        builder.Text("b", new SourceSpan(3, 1));

        var document = builder.Build();
        Assert.Equal("a\r\nb", LayoutWriter.Write(document, Fitter.Resolve(document, 120), "    ", "\n").Text);
    }

    [Fact]
    public void Space_BeforeALine_IsNeverWritten() {
        // remove_spaces_on_blank_lines = true, and the writer never produces trailing whitespace.
        var builder = new DocumentBuilder();
        builder.Text("a", new SourceSpan(0, 1));
        builder.Space(SpaceKind.Required);
        builder.Line(LineKind.Hard, 1);
        builder.Text("b", new SourceSpan(4, 1));

        var document = builder.Build();
        Assert.Equal("a\n\nb", LayoutWriter.Write(document, Fitter.Resolve(document, 120), "    ", "\n").Text);
    }

    [Fact]
    public void ContinuousScopes_Nest() {
        // A continuation level is a scope, not a per-line adjustment: `=>` then `(` is two levels,
        // which is the only way the two compose (docs/plan/04 § "Indentation").
        Assert.Equal("a\nb", Render(open: 0));
        Assert.Equal("a\n    b", Render(open: 1));
        Assert.Equal("a\n        b", Render(open: 2));

        static string Render(int open) {
            var builder = new DocumentBuilder();
            builder.Text("a", new SourceSpan(0, 1));
            for (var i = 0; i < open; i++) {
                builder.OpenIndent(IndentKind.Continuous);
            }

            builder.Line(LineKind.Hard);
            builder.Text("b", new SourceSpan(2, 1));
            for (var i = 0; i < open; i++) {
                builder.Close();
            }

            var document = builder.Build();
            return LayoutWriter.Write(document, Fitter.Resolve(document, 120), "    ", "\n").Text;
        }
    }
}

public sealed class EditEmitterTests {
    [Fact]
    public void Emit_ProducesNothing_WhenTheOutputMatchesTheInput() {
        // ⚠ The property that makes a first run on a 1.35 M-line tree reviewable.
        const string input = "a b";
        var layout = new Layout("a b", [
            new AnchorPoint(new SourceSpan(0, 1), 0, 1, 0),
            new AnchorPoint(new SourceSpan(2, 1), 2, 3, 1)
        ]);

        Assert.Empty(EditEmitter.Emit(input, layout));
    }

    [Fact]
    public void Emit_SpansOnlyTheGapThatDiffers() {
        const string input = "a    b";
        var layout = new Layout("a b", [
            new AnchorPoint(new SourceSpan(0, 1), 0, 1, 0),
            new AnchorPoint(new SourceSpan(5, 1), 2, 3, 1)
        ]);

        var edit = Assert.Single(EditEmitter.Emit(input, layout));
        Assert.Equal(new SourceSpan(2, 3), edit.Span);
        Assert.Equal(string.Empty, edit.NewText);
        Assert.Equal("a b", EditEmitter.Apply(input, [edit]));
    }

    [Fact]
    public void Restrict_KeepsOnlyTheEditsThatIntersectTheRange() {
        TextEdit[] edits = [
            new(new SourceSpan(0, 2), "x"),
            new(new SourceSpan(10, 2), "y")
        ];

        var restricted = EditEmitter.Restrict(edits, new SourceSpan(9, 5));
        Assert.Equal("y", Assert.Single(restricted).NewText);
    }
}

public sealed class TextWidthTests {
    [Theory]
    [InlineData("abc", 3)]
    [InlineData("\t", 4)]
    [InlineData("日本語", 6)]
    [InlineData("é", 1)]
    public void Measure_CountsColumns_NotCharacters(string text, int expected) =>
        Assert.Equal(expected, TextWidth.Measure(text));
}
