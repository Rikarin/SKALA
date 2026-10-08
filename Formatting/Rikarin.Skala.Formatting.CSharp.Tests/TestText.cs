namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Pieces of test sources that are written shorter than they are, so that a test's own lines stay inside the
///     column limit while the source it formats does not (SK0002).
/// </summary>
/// <remarks>
///     A margin test needs a line one column either side of 120 at a nesting depth, and written into a raw string
///     at the test's own indentation that line is longer than 120 itself. A run of one repeated character is the
///     usual filler and is spelled <c>{{R('a', 41)}}</c> in an interpolated raw string; the value is the same
///     string either way.
/// </remarks>
static class TestText {
    /// <summary><paramref name="count" /> copies of <paramref name="character" />.</summary>
    public static string R(char character, int count) => new(character, count);
}
