namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #554: a using directive's name the author broke keeps the break with the rest one level in. Every
///     expected string is <c>jb cleanupcode</c>'s own output for the input, and each is checked on a second pass;
///     <c>constructs/syntax/using-directive-broken-name.cs</c> holds the wider set.
/// </summary>
public sealed class UsingDirectiveBrokenNameIssue554Tests {
    [Fact]
    public void ABrokenUsingName_ContinuesOneLevelIn() =>
        Oracle.Agrees(
            """
            using System.
            Text;
            using static System.
            Math;
            global using System
            .Linq;

            class C { }
            """,
            """
            using System.
                Text;
            using static System.
                Math;
            global using System
                .Linq;

            class C { }
            """
        );
}
