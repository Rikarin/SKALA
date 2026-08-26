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
        var layout = LayoutWriter.Write(document, 120, "    ", "\n");

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
        Assert.Equal("a\r\nb", LayoutWriter.Write(document, 120, "    ", "\n").Text);
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
        Assert.Equal("a\n\nb", LayoutWriter.Write(document, 120, "    ", "\n").Text);
    }

    [Fact]
    public void ContinuousScopes_CountOnePerOpeningLine() {
        // ⚠ A continuation level is a scope, not a per-line adjustment — `=>` then `(` is two
        // levels — but two scopes opened on the SAME line are one, which is what keeps
        // `Report(Create(` from indenting its arguments twice (docs/plan/04 § "Indentation").
        var builder = new DocumentBuilder();
        builder.Text("a", new SourceSpan(0, 1));
        builder.OpenIndent(IndentKind.Continuous);
        builder.OpenIndent(IndentKind.Continuous);
        builder.Line(LineKind.Hard);
        builder.Text("b", new SourceSpan(2, 1));
        builder.OpenIndent(IndentKind.Continuous);
        builder.Line(LineKind.Hard);
        builder.Text("c", new SourceSpan(4, 1));
        builder.Close();
        builder.Close();
        builder.Close();

        var document = builder.Build();
        Assert.Equal(
            "a\n    b\n        c",
            LayoutWriter.Write(document, 120, "    ", "\n").Text);
    }
}

/// <summary>
/// The three-state group model, and the fourth state <c>if_owner_is_single_line</c> needs.
/// </summary>
public sealed class FitterTests {
    [Fact]
    public void AutoGroup_BreaksOnlyWhenTheLineRunsOut() {
        Assert.Equal("a b", Call(GroupMode.Auto, new GroupFacts(), width: 10));
        Assert.Equal("a\nb", Call(GroupMode.Auto, new GroupFacts(), width: 2));
    }

    [Fact]
    public void PreserveGroup_KeepsTheAuthorsBreak_AndDoesNotAddOne() {
        // ⚠ The two halves of "subject to width" are separate facts, because the export wants a
        // different one per construct family. A group with neither may only reproduce the source.
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(SourceBroken: true), width: 80));
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(), width: 2));
    }

    [Fact]
    public void PreserveGroup_JoinsOnlyWhenAskedTo_AndOnlyWhenItFits() {
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(SourceBroken: true, JoinsIfFits: true), width: 80));
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(SourceBroken: true, JoinsIfFits: true), width: 2));
    }

    [Fact]
    public void PreserveGroup_BreaksOnlyWhenAskedTo_AndOnlyWhenItMust() {
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(BreaksIfTooLong: true), width: 2));
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(BreaksIfTooLong: true), width: 80));
    }

    [Fact]
    public void OwnerGroup_ReadsItsOwnersResolvedMode_AndOnlyEverBecomesMoreBroken() {
        // ⚠ The whole content of "two passes per group tree". The owner encloses the child, so a
        // depth-first walk resolves it first; the child reads the answer and may only move
        // Flat → Broken, which is why termination is a property of the walk order and not of a
        // convergence argument (docs/plan/04 § "The fitting algorithm").
        Assert.Equal("aaaa bbbb", OwnerAndChild(width: 80));
        Assert.Equal("aaaa\nbbbb", OwnerAndChild(width: 6));
    }

    [Fact]
    public void OwnerGroup_ThatIsReachedBeforeItsOwner_IsCountedRatherThanGuessedAt() {
        var builder = new DocumentBuilder();
        var child = builder.NextGroupId();
        var owner = builder.NextGroupId();
        builder.DescribeGroup(child, new GroupFacts(Owner: owner));
        builder.OpenGroup(GroupMode.Owner, child);
        builder.Text("a", new SourceSpan(0, 1));
        builder.Close();
        builder.OpenGroup(GroupMode.Auto, owner);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();

        var layout = LayoutWriter.Write(builder.Build(), 80, "    ", "\n");
        Assert.Equal(1, layout.OwnerUnresolved);
    }

    /// <summary>`a` then a break point then `b`, inside one group.</summary>
    static string Call(GroupMode mode, GroupFacts facts, int width) {
        var builder = new DocumentBuilder();
        var group = builder.NextGroupId();
        builder.DescribeGroup(group, facts);
        builder.OpenGroup(mode, group);
        builder.Text("a", new SourceSpan(0, 1));
        builder.BreakPoint(group, flatSpace: true);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();

        return LayoutWriter.Write(builder.Build(), width, "    ", "\n").Text;
    }

    /// <summary>An Auto owner with an Owner-mode child inside it, which is the export's shape.</summary>
    static string OwnerAndChild(int width) {
        var builder = new DocumentBuilder();
        var owner = builder.NextGroupId();
        var child = builder.NextGroupId();
        builder.DescribeGroup(owner, new GroupFacts());
        builder.DescribeGroup(child, new GroupFacts(Owner: owner));

        builder.OpenGroup(GroupMode.Auto, owner);
        builder.Text("aaaa", new SourceSpan(0, 4));
        builder.OpenGroup(GroupMode.Owner, child);
        builder.BreakPoint(child, flatSpace: true);
        builder.Text("bbbb", new SourceSpan(5, 4));
        builder.Close();
        builder.Close();

        return LayoutWriter.Write(builder.Build(), width, "    ", "\n").Text;
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
