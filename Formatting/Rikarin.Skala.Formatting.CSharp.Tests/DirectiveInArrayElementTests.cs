namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     ⚠ Nightly replay 12061030311543376894 (#471's collection fill): an array initializer's collection
///     element that holds a <c>#region</c>. The draft measure read the breaks beside the directive as an
///     author's kept breaks, one space each, so pass one measured the element at 104 flat columns, found it
///     fitting one level in and moved it down whole; pass two measured its own output as unbounded and kept
///     <c>}, [</c> on the line. A break beside a directive can never be joined, so it is no kept break.
/// </summary>
public sealed class DirectiveInArrayElementTests {
    const string Source = """
                          class T12 {
                              public Func<IReadOnlyDictionary<DateTime?, DateTime?>, IReadOnlyDictionary<DateTime?, DateTime?>> P13 { get; } = new[] { (from item in Source where 56664 orderby item.Length descending select null), new int() { P14 = null, P15 = "sssssssssssssssssssss", P16 = "sssssssssssssssssssssssss", P17 = false }, ["sssssssssssssssssssssssssssss"
                          #region fuzz
                             , false, 0xd49, "sssssssssssssssssssss", false, .. Source], new[] { 42613 } };
                          #endregion
                          }
                          """;

    /// <summary>The oracle keeps the bracket after the element before it, on the first pass as on the second.</summary>
    [Fact]
    public void ACollectionElementHoldingARegion_KeepsItsBracketOnTheLine() =>
        Assert.Contains(
            "            new int() { P14 = null, P15 = \"sssssssssssssssssssss\", P16 = \"sssssssssssssssssssssssss\", P17 = false }, [\n",
            Format.Text(Source).ReplaceLineEndings("\n")
        );

    [Fact]
    public void ACollectionElementHoldingARegion_IsStableOnTheSecondPass() {
        var once = Format.Text(Source);
        Assert.Equal(once, Format.Text(once));
    }
}
