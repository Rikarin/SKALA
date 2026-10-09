namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A break the author kept between a positional element's type and its name stays (#559's fill point).
/// </summary>
/// <remarks>
///     ⚠ The expected string is the oracle's answer to this input, asked 2026-10-09: it comes back as written.
///     Without the pin, `fuzz --replay=9642682992700587520` was not idempotent.
/// </remarks>
public sealed class PositionalDesignationBreakTests {
    [Fact]
    public void AKeptBreakBetweenTypeAndName_Stays() {
        const string source = """
            class C {
                object A(object state) =>
                    state switch {
                        ("k", var
                            p) => 1,
                        ("k", int
                            q) => 2,
                        _ => default
                    };

                void B(object o) {
                    var x = o is (int
                        a, int b);
                    var (c,
                        d) = (1, 2);
                }
            }
            """;
        Oracle.Agrees(source, source);
    }
}
