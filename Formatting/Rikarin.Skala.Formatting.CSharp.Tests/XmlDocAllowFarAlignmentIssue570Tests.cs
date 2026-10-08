namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #570: <c>skala_xmldoc_allow_far_alignment</c>. Every row is <c>jb cleanupcode</c> 2025.2.6's answer
///     under <c>OracleProfile.DocComments</c> at <c>skala_xmldoc_max_line_length = 90</c>, for a
///     <c>&lt;summary&gt;</c> followed by an element of 29 attributes whose first attribute sits at the
///     column given (counted after <c>/// </c>): how many comment lines the oracle wrote, and the one column
///     every continuation line starts at.
/// </summary>
/// <remarks>
///     ⚠ No key-named fixture can pin this key: the export's <c>attribute_indent = single_indent</c> masks
///     it, so it is Tier D and these rows are its evidence (see <c>XmlDocIds.Refused</c>).
/// </remarks>
public sealed class XmlDocAllowFarAlignmentIssue570Tests {
    [Theory]
    // `true`: under the first attribute wherever it fits beside the tag name …
    [InlineData("align_by_first_attribute", "true", 56, 10, 56)]
    [InlineData("align_by_first_attribute", "true", 60, 10, 60)]
    [InlineData("align_by_first_attribute", "true", 62, 10, 62)]
    [InlineData("align_by_first_attribute", "true", 70, 16, 70)]
    [InlineData("align_by_first_attribute", "true", 80, 30, 80)]
    [InlineData("align_by_first_attribute", "true", 83, 30, 83)]
    // … and one indent past the tag once it does not.
    [InlineData("align_by_first_attribute", "true", 84, 5, 4)]
    [InlineData("align_by_first_attribute", "true", 95, 5, 4)]
    // `false`, the export: two indents from two thirds of the margin, one once the first attribute moves.
    [InlineData("align_by_first_attribute", "false", 59, 10, 59)]
    [InlineData("align_by_first_attribute", "false", 60, 5, 8)]
    [InlineData("align_by_first_attribute", "false", 80, 5, 8)]
    [InlineData("align_by_first_attribute", "false", 84, 5, 4)]
    // The other two indents keep their own column whether or not the first attribute moves.
    [InlineData("double_indent", "true", 83, 5, 8)]
    [InlineData("double_indent", "true", 84, 5, 8)]
    [InlineData("single_indent", "true", 83, 5, 4)]
    [InlineData("single_indent", "true", 84, 5, 4)]
    public void TheContinuationColumn(string indent, string far, int firstAttribute, int lines, int column) =>
        Probe.Continues(
            firstAttribute,
            lines,
            column,
            ("skala_xmldoc_attribute_indent", indent),
            ("skala_xmldoc_allow_far_alignment", far)
        );

    /// <summary>
    ///     ⚠ <c>on_different_lines</c> puts the first attribute below the name too, so there is nothing to
    ///     align under at any column: one indent past the tag, measured with the first attribute at 20 and 56.
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(56)]
    public void OnDifferentLines_NothingIsBesideTheName(int firstAttribute) =>
        Probe.Continues(
            firstAttribute,
            31,
            4,
            ("skala_xmldoc_attribute_indent", "align_by_first_attribute"),
            ("skala_xmldoc_allow_far_alignment", "true"),
            ("skala_xmldoc_attribute_style", "on_different_lines")
        );
}

file static class Probe {
    public static void Continues(int firstAttribute, int lines, int column, params (string, string)[] keys) {
        var attributes = string.Join(" ", Enumerable.Range(1, 29).Select(static i => $"a{i}=\"{i}\""));
        (string, string)[] overrides = [..keys, ("skala_xmldoc_max_line_length", "90")];
        var once = XmlDoc.Text(
            XmlDoc.InClass(["/// <summary>Text.</summary><" + new string('n', firstAttribute - 2) + " " + attributes + " />"]),
            overrides
        );
        Assert.Equal(once, XmlDoc.Text(once, overrides));

        string[] doc = [..XmlDoc.DocLines(once)];
        Assert.Equal(lines, doc.Length);
        Assert.All(
            doc[2..],
            line => Assert.StartsWith("/// " + new string(' ', column) + "a", line, StringComparison.Ordinal)
        );
    }
}
