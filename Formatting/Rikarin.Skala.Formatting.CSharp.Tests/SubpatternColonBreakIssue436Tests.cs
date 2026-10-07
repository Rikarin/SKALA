using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #436: an author's break before a subpattern's or a named argument's colon is kept, and the colon
///     lands on its name's column.
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's own answer, measured 2026-10-07 with <c>Testing ask</c>
///     under the repository's configuration and again at <c>keep_user_linebreaks = false</c>. Every row
///     is also checked on a second pass.
/// </remarks>
public sealed class SubpatternColonBreakIssue436Tests {
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

    static string InMethod(string statement) =>
        "class P {\n    public int X;\n    public int Y;\n    public P Q;\n\n    void M(int a, int b) { }\n\n"
        + $"    void T(object o) {{\n        {statement}\n    }}\n}}\n";

    static void AssertFormats(string statement, string expected, params (string Key, string Value)[] overrides) {
        var once = FormatWith(InMethod(statement), overrides);
        Assert.Contains(expected, once, StringComparison.Ordinal);
        Assert.Equal(once, FormatWith(once, overrides));
    }

    [Theory]
    [InlineData("var p = o is P { X\n: 1 };", "        var p = o is P {\n            X\n            : 1\n        };\n")]
    [InlineData(
        "var p = o is P { X\n            : 1, Y: 2 };",
        "        var p = o is P {\n            X\n            : 1,\n            Y: 2\n        };\n"
    )]
    [InlineData(
        "var p = o is P { X: 1, Y\n            : 2 };",
        "        var p = o is P {\n            X: 1,\n            Y\n            : 2\n        };\n"
    )]
    [InlineData(
        "var p = o is P { Q.X\n            : 1 };",
        "        var p = o is P {\n            Q.X\n            : 1\n        };\n"
    )]
    [InlineData(
        "var p = o is P { Q: { X\n            : 1 } };",
        "        var p = o is P {\n            Q: {\n                X\n                : 1\n            }\n        };\n"
    )]
    [InlineData("var p = o is P (A\n            : 1, B: 2);", "        var p = o is P (A\n            : 1, B: 2);\n")]
    [InlineData("var p = (a\n            : 1, b: 2);", "        var p = (a\n            : 1, b: 2);\n")]
    [InlineData(
        "M(a\n            : 1, b: 2);",
        "        M(\n            a\n            : 1,\n            b: 2\n        );\n"
    )]
    public void AnAuthorsBreakBeforeTheColon_IsKept(string statement, string expected) =>
        AssertFormats(statement, expected);

    [Fact]
    public void InASwitchArmAndACaseLabel_TheBreakIsKeptToo() {
        AssertFormats(
            "var s = o switch {\n            P { X\n                : 1 } => 1,\n            _ => 0\n        };",
            "            P {\n                X\n                : 1\n            } => 1,\n"
        );
        AssertFormats(
            "switch (o) {\n            case P { X\n                : 1 }:\n                break;\n        }",
            "            case P {\n                X\n                : 1\n            }:\n"
        );
    }

    /// <summary>
    ///     At <c>keep_user_linebreaks = false</c> every one is joined, the gap taking its bit from the
    ///     indentation of the line it ended on.
    /// </summary>
    [Theory]
    [InlineData("var p = o is P { X\n: 1 };", "        var p = o is P { X: 1 };\n")]
    [InlineData("var p = o is P { X\n            : 1, Y: 2 };", "        var p = o is P { X : 1, Y: 2 };\n")]
    [InlineData("var p = o is P (A\n            : 1, B: 2);", "        var p = o is P (A : 1, B: 2);\n")]
    [InlineData("var p = (a\n            : 1, b: 2);", "        var p = (a: 1, b: 2);\n")]
    [InlineData("M(a\n            : 1, b: 2);", "        M(a: 1, b: 2);\n")]
    public void WithoutKeptLineBreaks_TheColonIsJoined(string statement, string expected) =>
        AssertFormats(statement, expected, ("skala_keep_user_linebreaks", "false"));

    /// <summary>A break after the colon was already the subpattern's own point, and is unchanged.</summary>
    [Fact]
    public void ABreakAfterTheColon_IsUnchanged() =>
        AssertFormats(
            "var p = o is P { X:\n            1 };",
            "        var p = o is P {\n            X:\n            1\n        };\n"
        );
}
