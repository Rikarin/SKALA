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

    [Theory]
    [InlineData(false, "a, b\nc")]
    [InlineData(true, "a,\nb\nc")]
    public void Fill_DistinguishesItsOwnHardLineFromOneInsideAnItem(bool nested, string expected) {
        var builder = new DocumentBuilder();
        var group = builder.NextGroupId();
        builder.OpenGroup(GroupMode.Break, group);
        builder.Text("a,", new SourceSpan(0, 2));
        builder.BreakPoint(group, LineFlags.FlatSpace | LineFlags.FillPoint);
        if (nested) {
            builder.OpenGroup(GroupMode.Break, builder.NextGroupId());
        }

        builder.Text("b", new SourceSpan(3, 1));
        builder.Line(LineKind.Hard);
        builder.Text("c", new SourceSpan(5, 1));
        if (nested) {
            builder.Close();
        }

        builder.Close();
        Assert.Equal(expected, LayoutWriter.Write(builder.Build(), 80, "    ", "\n").Text);
    }

    [Theory]
    [InlineData(GroupMode.Break, false, "a,\nb\nc, d")]
    [InlineData(GroupMode.Preserve, true, "a,\nb\nc, d")]
    [InlineData(GroupMode.Preserve, false, "a, b c, d")]
    [InlineData(GroupMode.Auto, false, "a, b c, d")]
    public void Fill_HonorsNestedGroupsThatCannotFlatten(GroupMode mode, bool preserveBreak, string expected) {
        var builder = new DocumentBuilder();
        var outer = builder.NextGroupId();
        var inner = builder.NextGroupId();
        builder.DescribeGroup(inner, new GroupFacts(preserveBreak, HidesFlatWidthWhenBroken: true));
        builder.OpenGroup(GroupMode.Break, outer);
        builder.Text("a,", new SourceSpan(0, 2));
        builder.BreakPoint(outer, LineFlags.FlatSpace | LineFlags.FillPoint);
        builder.OpenConcat();
        builder.OpenGroup(mode, inner);
        builder.Text("b", new SourceSpan(3, 1));
        builder.BreakPoint(inner, LineFlags.FlatSpace);
        builder.Text("c", new SourceSpan(5, 1));
        builder.Close();
        builder.Close();
        builder.Text(",", new SourceSpan(6, 1));
        builder.BreakPoint(outer, LineFlags.FlatSpace | LineFlags.FillPoint);
        builder.Text("d", new SourceSpan(8, 1));
        builder.Close();

        Assert.Equal(expected, LayoutWriter.Write(builder.Build(), 80, "    ", "\n").Text);
    }

    /// <summary>
    ///     A last-resort point (<see cref="LineFlags.LastResort" />) does not end the rest-of-line
    ///     measure of a group before it, whether the point is that group's direct sibling or sits inside
    ///     a container: the inner group breaks first, and the point breaks only when what follows still
    ///     has no room. An ordinary fill point on the same gap ends the measure, so the group stays flat
    ///     and the point takes the break instead (SK-DIV-0106).
    /// </summary>
    [Theory]
    [InlineData(false, false, "a b\nc")]
    [InlineData(true, false, "a\nb c")]
    [InlineData(true, true, "a\nb c")]
    public void LastResortPoint_LetsTheGroupBeforeItBreakFirst(bool lastResort, bool nested, string expected) {
        var builder = new DocumentBuilder();
        var inner = builder.NextGroupId();
        var outer = builder.NextGroupId();
        builder.DescribeGroup(inner, new GroupFacts(BreaksIfTooLong: true));
        builder.OpenGroup(GroupMode.Break, outer);
        builder.OpenGroup(GroupMode.Preserve, inner);
        builder.Text("a", new SourceSpan(0, 1));
        builder.BreakPoint(inner, LineFlags.FlatSpace);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();
        if (nested) {
            builder.OpenConcat();
        }

        builder.BreakPoint(
            outer,
            LineFlags.FlatSpace | LineFlags.FillPoint | (lastResort ? LineFlags.LastResort : LineFlags.None)
        );
        builder.Text("c", new SourceSpan(4, 1));
        if (nested) {
            builder.Close();
        }

        builder.Close();

        // Four columns: `a b` fits, `a b c` does not.
        Assert.Equal(expected, LayoutWriter.Write(builder.Build(), 4, "    ", "\n").Text);
    }

    /// <summary>
    ///     A point that yields to its predecessors (<see cref="LineFlags.YieldsToPredecessors" />) is
    ///     the end of the line for a group <em>inside</em> its list: the inner group in the first item
    ///     stays flat and the list's own point breaks. A last-resort point on the same gap is read
    ///     through from inside as well, so the inner group breaks first (issue #377).
    /// </summary>
    [Theory]
    [InlineData(LineFlags.FillPoint, "a b\nc")]
    [InlineData(LineFlags.FillPoint | LineFlags.YieldsToPredecessors, "a b\nc")]
    [InlineData(LineFlags.FillPoint | LineFlags.LastResort, "a\nb c")]
    public void YieldingPoint_EndsTheLineForAGroupInsideItsList(LineFlags point, string expected) {
        var builder = new DocumentBuilder();
        var inner = builder.NextGroupId();
        var outer = builder.NextGroupId();
        builder.DescribeGroup(inner, new GroupFacts(BreaksIfTooLong: true));
        builder.OpenGroup(GroupMode.Break, outer);
        builder.OpenGroup(GroupMode.Preserve, inner);
        builder.Text("a", new SourceSpan(0, 1));
        builder.BreakPoint(inner, LineFlags.FlatSpace);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();
        builder.BreakPoint(outer, LineFlags.FlatSpace | point);
        builder.Text("c", new SourceSpan(4, 1));
        builder.Close();

        // Four columns: `a b` fits, `a b c` does not.
        Assert.Equal(expected, LayoutWriter.Write(builder.Build(), 4, "    ", "\n").Text);
    }

    /// <summary>
    ///     The other half: a group <em>before</em> the list is measured through a yielding point exactly
    ///     as through a last-resort one, so it wraps first and the list fills only what still has no
    ///     room. An ordinary fill point ends that group's measure at the list's gap.
    /// </summary>
    [Theory]
    [InlineData(LineFlags.FillPoint, "a b c\nd")]
    [InlineData(LineFlags.FillPoint | LineFlags.YieldsToPredecessors, "a\nb c d")]
    [InlineData(LineFlags.FillPoint | LineFlags.LastResort, "a\nb c d")]
    public void YieldingPoint_IsReadThroughByAGroupBeforeItsList(LineFlags point, string expected) {
        var builder = new DocumentBuilder();
        var inner = builder.NextGroupId();
        var list = builder.NextGroupId();
        builder.DescribeGroup(inner, new GroupFacts(BreaksIfTooLong: true));
        builder.OpenGroup(GroupMode.Preserve, inner);
        builder.Text("a", new SourceSpan(0, 1));
        builder.BreakPoint(inner, LineFlags.FlatSpace);
        builder.Text("b", new SourceSpan(2, 1));
        builder.Close();
        builder.OpenGroup(GroupMode.Break, list);
        builder.Text(" c", new SourceSpan(3, 2));
        builder.BreakPoint(list, LineFlags.FlatSpace | point);
        builder.Text("d", new SourceSpan(6, 1));
        builder.Close();

        // Six columns: `a b c` fits, `a b c d` does not; `b c d` fits after `a` has gone up.
        Assert.Equal(expected, LayoutWriter.Write(builder.Build(), 6, "    ", "\n").Text);
    }

    /// <summary>
    ///     A point taken only when the next line overflows (<see cref="LineFlags.BreaksOnlyIfNextLineOverflows" />):
    ///     the writer lays the next line out ahead, and joins when that line fits beside the point. The
    ///     list is too wide for the line either way; what decides is where its fill breaks first when
    ///     it stands alone — after <c>x,</c> the join fits, after <c>xxxxx,</c> it does not. A hard line
    ///     inside the point's own group takes the question away: the group spans lines, so the point
    ///     breaks (issue #377).
    /// </summary>
    [Theory]
    [InlineData("x,", false, "[A] T<x,\nyyyyyy>")]
    [InlineData("xxxxx,", false, "[A]\nT<xxxxx,\nyyyyyy>")]
    [InlineData("x,", true, "[A\n]\nT<x,\nyyyyyy>")]
    public void DeferredPoint_JoinsExactlyWhenTheNextLineFitsBesideIt(string first, bool multiLine, string expected) {
        var builder = new DocumentBuilder();
        var section = builder.NextGroupId();
        var list = builder.NextGroupId();
        builder.DescribeGroup(section, new GroupFacts(JoinsIfFits: true, BreaksIfTooLong: true));
        builder.DescribeGroup(list, new GroupFacts(BreaksIfTooLong: true));
        builder.OpenConcat();
        builder.OpenGroup(GroupMode.Preserve, section);
        builder.Text("[A", new SourceSpan(0, 2));
        if (multiLine) {
            builder.Line(LineKind.Hard);
        }

        builder.Text("]", new SourceSpan(2, 1));
        builder.Close();
        builder.BreakPoint(
            section,
            LineFlags.FlatSpace | LineFlags.LastResort | LineFlags.BreaksOnlyIfNextLineOverflows
        );
        builder.OpenGroup(GroupMode.Preserve, list);
        builder.Text("T<", new SourceSpan(4, 2));
        builder.Text(first, new SourceSpan(6, first.Length));
        builder.BreakPoint(list, LineFlags.FlatSpace | LineFlags.FillPoint | LineFlags.YieldsToPredecessors);
        builder.Anchor(new SourceSpan(12, 7), 1);
        builder.Text("yyyyyy>", new SourceSpan(12, 7));
        builder.Close();
        builder.Close();

        // Nine columns: `T<x, yyyyyy>` is twelve and never fits, `[A] T<x,` is eight, `[A] T<xxxxx,` twelve.
        var layout = LayoutWriter.Write(builder.Build(), 9, "    ", "\n");
        Assert.Equal(expected, layout.Text);

        // ⚠ The speculative line leaves nothing behind: the one anchor is the real walk's, at the
        // position the real walk wrote the token.
        Assert.Equal(expected.IndexOf("yyyyyy>", StringComparison.Ordinal), Assert.Single(layout.Anchors).OutputStart);
    }

    /// <summary>
    ///     A fill breaks before an item only when that makes the item fit whole (SK-DIV-0110). Item
    ///     <c>b…</c> holds a hard line of its own, so it fits nowhere: as a delimited item its head
    ///     stays after <c>a,</c> and it breaks inside. Item <c>c…</c> is whole and too wide for what is
    ///     left of the line but fits on a fresh one, so the fill breaks before it — the 104-column
    ///     initializer's case.
    /// </summary>
    [Fact]
    public void Fill_KeepsTheHeadOfAnItemThatFitsNowhere_AndMovesOneThatFitsMoved() {
        var builder = new DocumentBuilder();
        var group = builder.NextGroupId();
        builder.OpenGroup(GroupMode.Break, group);
        builder.Text("a,", new SourceSpan(0, 2));
        builder.BreakPoint(group, LineFlags.FlatSpace | LineFlags.FillPoint | LineFlags.DelimitedItem);

        // ⚠ A nested *group*: a hard line at the fill's own depth merely ends the segment, and it
        // is a line inside an item that made the item measure as unbounded (#337, #339).
        builder.OpenGroup(GroupMode.Break, builder.NextGroupId());
        builder.Text("b1", new SourceSpan(3, 2));
        builder.Line(LineKind.Hard);
        builder.Text("b2,", new SourceSpan(6, 3));
        builder.Close();
        builder.BreakPoint(group, LineFlags.FlatSpace | LineFlags.FillPoint);
        builder.Text("cccccc", new SourceSpan(10, 6));
        builder.Close();

        // Eight columns: `a, b1` fits, `b2, cccccc` does not, `cccccc` alone does.
        Assert.Equal("a, b1\nb2,\ncccccc", LayoutWriter.Write(builder.Build(), 8, "    ", "\n").Text);
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
            LayoutWriter.Write(document, 120, "    ", "\n").Text
        );
    }
}

/// <summary>
///     The three-state group model, and the fourth state <c>if_owner_is_single_line</c> needs.
/// </summary>
public sealed class FitterTests {
    [Fact]
    public void AutoGroup_BreaksOnlyWhenTheLineRunsOut() {
        Assert.Equal("a b", Call(GroupMode.Auto, new GroupFacts(), 10));
        Assert.Equal("a\nb", Call(GroupMode.Auto, new GroupFacts(), 2));
    }

    [Fact]
    public void PreserveGroup_KeepsTheAuthorsBreak_AndDoesNotAddOne() {
        // ⚠ The two halves of "subject to width" are separate facts, because the export wants a
        // different one per construct family. A group with neither may only reproduce the source.
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(true), 80));
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(), 2));
    }

    [Fact]
    public void PreserveGroup_JoinsOnlyWhenAskedTo_AndOnlyWhenItFits() {
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(true, true), 80));
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(true, true), 2));
    }

    [Fact]
    public void PreserveGroup_BreaksOnlyWhenAskedTo_AndOnlyWhenItMust() {
        Assert.Equal("a\nb", Call(GroupMode.Preserve, new GroupFacts(BreaksIfTooLong: true), 2));
        Assert.Equal("a b", Call(GroupMode.Preserve, new GroupFacts(BreaksIfTooLong: true), 80));
    }

    [Fact]
    public void OwnerGroup_ReadsItsOwnersResolvedMode_AndOnlyEverBecomesMoreBroken() {
        // ⚠ The whole content of "two passes per group tree". The owner encloses the child, so a
        // depth-first walk resolves it first; the child reads the answer and may only move
        // Flat → Broken, which is why termination is a property of the walk order and not of a
        // convergence argument (docs/plan/04 § "The fitting algorithm").
        Assert.Equal("aaaa bbbb", OwnerAndChild(80));
        Assert.Equal("aaaa\nbbbb", OwnerAndChild(6));
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
        builder.BreakPoint(group, LineFlags.FlatSpace);
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
        builder.BreakPoint(child, LineFlags.FlatSpace);
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
        var layout = new Layout(
            "a b",
            [
                new AnchorPoint(new SourceSpan(0, 1), 0, 1, 0), new AnchorPoint(new SourceSpan(2, 1), 2, 3, 1)
            ]
        );

        Assert.Empty(EditEmitter.Emit(input, layout));
    }

    [Fact]
    public void Emit_SpansOnlyTheGapThatDiffers() {
        const string input = "a    b";
        var layout = new Layout(
            "a b",
            [
                new AnchorPoint(new SourceSpan(0, 1), 0, 1, 0), new AnchorPoint(new SourceSpan(5, 1), 2, 3, 1)
            ]
        );

        var edit = Assert.Single(EditEmitter.Emit(input, layout));
        Assert.Equal(new SourceSpan(2, 3), edit.Span);
        Assert.Equal(string.Empty, edit.NewText);
        Assert.Equal("a b", EditEmitter.Apply(input, [edit]));
    }

    [Fact]
    public void Restrict_KeepsOnlyTheEditsThatIntersectTheRange() {
        TextEdit[] edits = [
            new(new SourceSpan(0, 2), "x"), new(new SourceSpan(10, 2), "y")
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
