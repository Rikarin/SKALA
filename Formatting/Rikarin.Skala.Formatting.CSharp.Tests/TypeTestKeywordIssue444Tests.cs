namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #444, SK-DIV-0210: an <c>is</c> or <c>as</c> breaks before its keyword exactly when the operand
///     fits on its line and the operand with the keyword does not. Every expected string is
///     <c>jb cleanupcode</c>'s own output for the input, and <see cref="Oracle.Agrees" /> asserts the second
///     pass too.
/// </summary>
public sealed class TypeTestKeywordIssue444Tests {
    const string Long1 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXX";

    const string Long2 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXX";

    const string Long3 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXX";

    const string Long4 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXX";

    const string Long5 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXX";

    const string Long6 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXX";

    const string Long7 = "receiver.PropertyXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"
        + "XXXXXXXXXXXXXXX";

    const string Long8 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYY";

    const string Long9 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYY";

    const string Long10 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYY";

    const string Long11 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYY";

    const string Long12 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYY";

    const string Long13 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYY";

    const string Long14 = "receiver.PropertyYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
        + "YYYYYYYYYY";

    /// <summary>
    ///     <c>return x as string</c> and <c>var value = x is T</c> widened a column at a time: the break goes after
    ///     the keyword while <c>x as</c> fits, before it while only <c>x</c> does, and an author's break before
    ///     the keyword is kept.
    /// </summary>
    [Fact]
    public void TheBreakGoesBeforeTheKeyword_WhenOnlyTheKeywordOverflows() =>
        Oracle.Agrees(
            $$"""
            class C {
                object M() {
                    _ = 0;
                    return {{Long1}} as string;
                    _ = 0;
                    return {{Long2}} as string;
                    _ = 0;
                    return {{Long3}} as string;
                    _ = 0;
                    return {{Long4}} as string;
                    _ = 0;
                    return {{Long5}} as string;
                    _ = 0;
                    return {{Long6}} as string;
                    _ = 0;
                    return {{Long7}} as string;
                }
                bool N() {
                    var value = {{Long8}} is SomeTypeName;
                    var value = {{Long9}} is SomeTypeName;
                    var value = {{Long10}} is SomeTypeName;
                    var value = {{Long11}} is SomeTypeName;
                    var value = {{Long12}} is SomeTypeName;
                    var value = {{Long13}} is SomeTypeName;
                    var value = {{Long14}} is SomeTypeName;
                    bool kept = receiver
                        is SomeTypeName;
                    return true;
                }
            }
            """,
            $$"""
            class C {
                object M() {
                    _ = 0;
                    return {{Long1}} as
                        string;
                    _ = 0;
                    return {{Long2}} as
                        string;
                    _ = 0;
                    return {{Long3}} as
                        string;
                    _ = 0;
                    return {{Long4}} as
                        string;
                    _ = 0;
                    return {{Long5}}
                        as string;
                    _ = 0;
                    return {{Long6}}
                        as string;
                    _ = 0;
                    return {{Long7}}
                        as string;
                }

                bool N() {
                    var value =
                        {{Long8}} is
                            SomeTypeName;
                    var value =
                        {{Long9}} is
                            SomeTypeName;
                    var value =
                        {{Long10}} is
                            SomeTypeName;
                    var value =
                        {{Long11}} is
                            SomeTypeName;
                    var value =
                        {{Long12}} is
                            SomeTypeName;
                    var value =
                        {{Long13}} is
                            SomeTypeName;
                    var value =
                        {{Long14}} is
                            SomeTypeName;
                    bool kept = receiver
                        is SomeTypeName;
                    return true;
                }
            }
            """
        );
}
