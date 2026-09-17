using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     The two spaces #373 found <c>skala format</c> writing that the oracle does not: one between a
///     recursive pattern's type and its positional clause, one after the <c>&lt;</c> that opens a type
///     parameter list whose first parameter carries an attribute.
/// </summary>
/// <remarks>
///     ⚠ Every expected string here is the oracle's own answer, measured 2026-09-17 with
///     <c>Testing ask</c> under the repository's configuration and with each candidate key flipped
///     on its own. The two defects are different classes: the pattern gap is <em>ungoverned</em> — the
///     oracle keeps whatever the author wrote and collapses a run to one space — while the angle gap
///     is governed by <c>space_within_type_parameter_angles</c>, which the old rule never consulted.
/// </remarks>
public sealed class SpacingIssue373Tests {
    static string FormatWith(string source, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
            )
                .Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string Member(string member) => Format.Text($"record Point(int X, int Y);\nclass C {{ {member} }}");

    /// <summary>
    ///     A positional clause that follows its pattern's type keeps the gap the author wrote: none
    ///     stays none, one stays one, two collapse to one.
    /// </summary>
    [Theory]
    [InlineData("bool B(object o) => o is Point(2, 3);", "o is Point(2, 3);")]
    [InlineData("bool B(object o) => o is Point (2, 3);", "o is Point (2, 3);")]
    [InlineData("bool B(object o) => o is Point  (2, 3);", "o is Point (2, 3);")]
    [InlineData("bool A(object o) => o is (1, Point(2, 3));", "o is (1, Point(2, 3));")]
    [InlineData("bool A(object o) => o is (1, Point (2, 3));", "o is (1, Point (2, 3));")]
    [InlineData("bool C2(object o) => o is Point(2, 3) { X: 1 };", "o is Point(2, 3) { X: 1 };")]
    [InlineData("bool C2(object o) => o is Point (2, 3) { X: 1 };", "o is Point (2, 3) { X: 1 };")]
    [InlineData("bool C7(object o) => o is N.Point(2, 3);", "o is N.Point(2, 3);")]
    [InlineData("bool C9(object o) => o is Point(var x, var y) p;", "o is Point(var x, var y) p;")]
    [InlineData("bool C10(object o) => o is not Point(2, 3);", "o is not Point(2, 3);")]
    [InlineData("bool C10(object o) => o is not Point (2, 3);", "o is not Point (2, 3);")]
    [InlineData(
        "bool C5(object o) => o is System.Collections.Generic.KeyValuePair<int, int>(1, 2);",
        "KeyValuePair<int, int>(1, 2);"
    )]
    [InlineData(
        "bool C5(object o) => o is System.Collections.Generic.KeyValuePair<int, int> (1, 2);",
        "KeyValuePair<int, int> (1, 2);"
    )]
    [InlineData("bool C4(object o) => o switch { Point(2, 3) => true, _ => false };", "Point(2, 3) => true")]
    [InlineData("bool C4(object o) => o switch { Point (2, 3) => true, _ => false };", "Point (2, 3) => true")]
    [InlineData("void S(object o) { switch (o) { case Point(2, 3): break; } }", "case Point(2, 3):")]
    [InlineData("void S(object o) { switch (o) { case Point (2, 3): break; } }", "case Point (2, 3):")]
    public void ATypedPositionalClause_KeepsTheGapTheAuthorWrote(string member, string expected) =>
        Assert.Contains(expected, Member(member), StringComparison.Ordinal);

    /// <summary>
    ///     ⚠ The control: a clause with no type in front of it, and a <c>var</c> deconstruction, are
    ///     governed — the oracle inserts the space into <c>is(1, 2)</c> and <c>var(a, b)</c>.
    /// </summary>
    [Theory]
    [InlineData("bool C12(object o) => o is(1, 2);", "o is (1, 2);")]
    [InlineData("bool C13(object o) => o is not(1, 2);", "o is not (1, 2);")]
    [InlineData("bool C8(object o) => o is var(x, y);", "o is var (x, y);")]
    [InlineData("void C14() { var(a, b) = (1, 2); }", "var (a, b) = (1, 2);")]
    [InlineData("void S(object o) { switch (o) { case(1, 2): break; } }", "case (1, 2):")]
    [InlineData("bool C15(object o) => o is Point{ X: 1 };", "o is Point { X: 1 };")]
    [InlineData("bool C16(object o) => o is Point(2, 3){ X: 1 };", "o is Point(2, 3) { X: 1 };")]
    public void AnUntypedClause_IsGoverned(string member, string expected) =>
        Assert.Contains(expected, Member(member), StringComparison.Ordinal);

    /// <summary>
    ///     A leading attribute in a type parameter list sits against the angle at the export's
    ///     <c>space_within_type_parameter_angles = false</c>, wherever the list is declared.
    /// </summary>
    [Theory]
    [InlineData("class C { void D<[System.Obsolete] T>() { } }", "void D<[System.Obsolete] T>() { }")]
    [InlineData("class C { void D< [System.Obsolete] T>() { } }", "void D<[System.Obsolete] T>() { }")]
    [InlineData("class C { void E<T, [System.Obsolete] U>() { } }", "void E<T, [System.Obsolete] U>() { }")]
    [InlineData("class C { void F([System.Obsolete] int a) { } }", "void F([System.Obsolete] int a) { }")]
    [InlineData("class C { void G(int a, [System.Obsolete] int b) { } }", "void G(int a, [System.Obsolete] int b) { }")]
    [InlineData("public class H< [System.Obsolete] T> { }", "public class H<[System.Obsolete] T> { }")]
    [InlineData("public delegate void Del< [System.Obsolete] T>();", "public delegate void Del<[System.Obsolete] T>();")]
    public void ALeadingAttributeInATypeParameterList_SitsAgainstTheAngle(string source, string expected) =>
        Assert.Contains(expected, Format.Text(source), StringComparison.Ordinal);

    /// <summary>
    ///     ⚠ The gap is the angle's, not the attribute's: with the angle key on, the oracle writes
    ///     <c>D&lt; [Obsolete] T &gt;</c>, so the same key has to open it here.
    /// </summary>
    [Theory]
    [InlineData("class C { void D<[System.Obsolete] T>() { } }", "void D< [System.Obsolete] T >() { }")]
    [InlineData("class C { void E<T, [System.Obsolete] U>() { } }", "void E< T, [System.Obsolete] U >() { }")]
    [InlineData("public class H<[System.Obsolete] T> { }", "public class H< [System.Obsolete] T > { }")]
    public void ALeadingAttributeInATypeParameterList_FollowsTheAngleKey(string source, string expected) =>
        Assert.Contains(
            expected,
            FormatWith(source, ("skala_space_within_type_parameter_angles", "true")),
            StringComparison.Ordinal
        );
}
