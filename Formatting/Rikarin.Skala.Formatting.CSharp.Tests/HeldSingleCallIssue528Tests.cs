namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #528: an <c>=</c> whose value is a single call on a receiver (Newtonsoft's
///     <c>JsonConvert.DeserializeObject&lt;T&gt;(json)</c>) breaks by the measured table in
///     <c>Fitter.HeldValueBreaks</c>, and otherwise the call's own dot breaks. Every expected string is
///     <c>jb cleanupcode</c>'s own output, measured 2026-10-09 with <c>Testing ask</c>;
///     <c>constructs/breaks/held-single-call.cs</c> holds the wider set.
/// </summary>
public sealed class HeldSingleCallIssue528Tests {
    /// <summary>
    ///     A typed local keeps its <c>=</c> and breaks before the call. Behind <c>var y</c> the same call breaks
    ///     the <c>=</c> while it fits below, breaks the <c>=</c> and the dot when it overflows below by one
    ///     column, chops its argument while its <c>(</c> lands three short of the margin below, and past that
    ///     keeps the <c>=</c> and breaks the dot. Before the fix every row chopped <c>(json)</c> beside the
    ///     <c>=</c>.
    /// </summary>
    [Fact]
    public void ASingleCallValue_FollowsTheMeasuredTable() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    Taaaa c = JsonConvert.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y = Jjjjjjjjjjj.DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y = Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                }
            }
            """,
            """
            class T {
                void M() {
                    Taaaa c = JsonConvert
                        .DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y =
                        Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y =
                        Jjjjjjjjjjj
                            .DeserializeObject<Ggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                    var y =
                        Jjjjjjjjjjj.DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(
                            json
                        );
                    var y = Jjjjjjjjjjj
                        .DeserializeObject<Gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg>(json);
                }
            }
            """
        );
}
