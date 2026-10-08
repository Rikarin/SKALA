namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #545: a parameter's name moves below its type when the parameter's line passes the margin, one
///     level past the parameter. Every expected string is <c>jb cleanupcode</c> 2025.2.6's output for the
///     input under <c>SkalaFormatOnly</c>; <c>constructs/wrapping/parameter-type-and-name.cs</c> carries the
///     column-by-column probe.
/// </summary>
public sealed class ParameterTypeNameIssue545Tests {
    static readonly string A108 = new('a', 108);
    static readonly string A109 = new('a', 109);

    [Fact]
    public void AtOneHundredAndTwenty_TheParameterStaysWhole() =>
        Oracle.Agrees(
            "class P {\n    void M(int " + A108 + ") { }\n}\n",
            "class P {\n    void M(\n        int " + A108 + "\n    ) { }\n}\n"
        );

    [Fact]
    public void AtOneHundredAndTwentyOne_TheNameMovesBelowItsType() =>
        Oracle.Agrees(
            "class P {\n    void M(int " + A109 + ") { }\n}\n",
            "class P {\n    void M(\n        int\n            " + A109 + "\n    ) { }\n}\n"
        );

    [Fact]
    public void ARecordsPrimaryConstructor_Too() =>
        Oracle.Agrees(
            "record R(int " + new string('g', 120) + ");\n",
            "record R(\n    int\n        " + new string('g', 120) + ");\n"
        );
}
