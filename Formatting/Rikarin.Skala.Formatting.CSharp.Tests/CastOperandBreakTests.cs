namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A cast's <c>)</c> before an operand with no break point of its own is the line's last resort, and in a
///     switch arm it competes with the arrow by a measured table (#591, SK-DIV-0440).
/// </summary>
/// <remarks>
///     ⚠ Every expected string is the oracle's answer to its input, asked 2026-10-09 with <c>Testing ask</c>.
/// </remarks>
public sealed class CastOperandBreakTests {
    [Fact]
    public void AfterReturn_TheOperandMovesBelowTheCast() =>
        Oracle.Agrees(
            "class C {\n"
            + "    object M() {\n"
            + "        return (string)\"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss\";\n"
            + "    }\n"
            + "}\n",
            "class C {\n"
            + "    object M() {\n"
            + "        return (string)\n"
            + "            \"ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss\";\n"
            + "    }\n"
            + "}\n"
        );

    [Fact]
    public void BelowABrokenEquals_TheOperandStaysOnTheCastsColumn() =>
        Oracle.Agrees(
            "class C {\n"
            + "    void M() {\n"
            + "        var other = (string)xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx;\n"
            + "    }\n"
            + "}\n",
            "class C {\n"
            + "    void M() {\n"
            + "        var other =\n"
            + "            (string)\n"
            + "            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx;\n"
            + "    }\n"
            + "}\n"
        );

    /// <summary>
    ///     The table's two sides one column apart: a head of 15 through <c>=&gt;</c> before an eight-column cast keeps
    ///     the arrow while the line ends at 124 and breaks after the cast from 125.
    /// </summary>
    [Theory]
    [InlineData(87, "            (\"k\", var p) =>\n                (string)")]
    [InlineData(88, "            (\"k\", var p) => (string)\n                ")]
    public void InASwitchArm_TheArrowOrTheCastByTheTable(int operand, string expected) {
        var x = new string('x', operand);
        Oracle.Agrees(
            "class C {\n"
            + "    object M(object s) =>\n"
            + "        s switch {\n"
            + $"            (\"k\", var p) => (string){x},\n"
            + "            _ => null\n"
            + "        };\n"
            + "}\n",
            "class C {\n"
            + "    object M(object s) =>\n"
            + "        s switch {\n"
            + $"{expected}{x},\n"
            + "            _ => null\n"
            + "        };\n"
            + "}\n"
        );
    }

    [Fact]
    public void BeforeAPrefixOperator_TheGapIsTheCasts() =>
        Oracle.Agrees(
            "class C {\n"
            + "    void M(int x, bool b) {\n"
            + "        var a = (int) -1;\n"
            + "        var c = (bool) !b;\n"
            + "        var d = (int)~x;\n"
            + "    }\n"
            + "}\n",
            "class C {\n"
            + "    void M(int x, bool b) {\n"
            + "        var a = (int)-1;\n"
            + "        var c = (bool)!b;\n"
            + "        var d = (int)~x;\n"
            + "    }\n"
            + "}\n"
        );
}
