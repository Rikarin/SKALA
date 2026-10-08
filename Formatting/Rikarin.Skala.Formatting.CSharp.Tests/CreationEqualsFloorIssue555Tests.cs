using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #555, SK-DIV-0211: an object creation after an = by the call&apos;s floor. Every expected string is
///     <c>jb cleanupcode</c> 2025.2.6's own output for the input under
///     <c>SkalaFormatOnly</c>, and each test asserts the second pass too.
/// </summary>
public sealed class CreationEqualsFloorIssue555Tests {
    /// <summary>The oracle's answer under the repository's export with <paramref name="overrides" /> on top.</summary>
    static void Agrees(string source, string expected, params (string Key, string Value)[] overrides) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(
                    Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
                    [..overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
                )
                .Options
        );

        var once = CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
        Assert.True(
            expected.TrimEnd('\n') == once.TrimEnd('\n'),
            $"Skala's output is not the oracle's.\n--- Skala ---\n{once}\n--- oracle ---\n{expected}"
        );

        var twice = CSharpFormatter.Format("Test.cs", SourceText.From(once), options).Formatted;
        Assert.True(once == twice, $"took two passes to settle:\n{once}\n--- pass two ---\n{twice}");
    }

    /// <summary>
    ///     #555: new T(…) after an = is measured as a call whose callee is new T, so its arguments chop by EqualsFloor;
    ///     and under an attribute and a comment it is joined and chopped.
    /// </summary>
    [Fact]
    public void ACreationWithArguments_ChopsByTheCallFloor() {
        Agrees(
            """
            class C {
                public Foo F = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                public Foo F = new Foo(
                    alphaValue,
                    betaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                public Foo Ffffffffffffffff = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                public Foo Ffffffffffffffff = new Foo(
                    alphaValue,
                    betaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                public Foo Fffffffffffffffffffffffffffffff = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                public Foo Fffffffffffffffffffffffffffffff =
                    new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """
        );
        Agrees(
            """
            class C {
                public Foo Fffffffffffffffffffffffffffffff = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                public Foo Fffffffffffffffffffffffffffffff = new Foo(
                    alphaValue,
                    betaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    var v = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
                }
            }
            """,
            """
            class C {
                void M() {
                    var v = new Foo(
                        alphaValue,
                        betaValue,
                        zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                    );
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvv = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
                }
            }
            """,
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvv = new Foo(
                        alphaValue,
                        betaValue,
                        zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                    );
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzz);
                }
            }
            """,
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv =
                        new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzz);
                }
            }
            """
        );
        Agrees(
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
                }
            }
            """,
            """
            class C {
                void M() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv = new Foo(
                        alphaValue,
                        betaValue,
                        zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                    );
                }
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                [Obsolete] /* c */
                public Foo F = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(
                    alphaValue,
                    betaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public Foo Ffffffffffffffff = new Foo(alphaValue, betaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public Foo Ffffffffffffffff = new Foo(
                    alphaValue,
                    betaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(alphaValue, betaValue, gammaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(
                    alphaValue,
                    betaValue,
                    gammaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
        Agrees(
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(alphaValue, betaValue, gammaValue, zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz);
            }
            """,
            """
            class C {
                [Obsolete] /* c */ public Foo F = new Foo(
                    alphaValue,
                    betaValue,
                    gammaValue,
                    zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
                );
            }
            """
        );
    }
}
