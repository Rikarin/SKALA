namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #585: a chain that is the body of a sole lambda nested in another lambda's body takes one level
///     past its line for each argument list opened on it. Every expected string is <c>jb cleanupcode</c>'s
///     own output, measured 2026-10-09 with <c>Testing ask</c>.
/// </summary>
public sealed class NestedSoleLambdaChainIssue585Tests {
    /// <summary>
    ///     Depth one, two and three, a lambda among other arguments, a block body, an <c>=</c> and a member
    ///     call as controls. Before the fix the nested rows took one level too many.
    /// </summary>
    [Fact]
    public void ANestedSoleLambdasChain_TakesALevelPerArgumentList() =>
        Oracle.Agrees(
            """
            class T {
                void M() {
                    A(s => s
                        .X(1)
                        .Y(2));
                    A(() => B(s => s
                        .X(1)
                        .Y(2)));
                    A(() => B(() => C(s => s
                        .X(1)
                        .Y(2))));
                    A(() => B(first, s => s
                        .X(1)
                        .Y(2)));
                    A(() => { B(s => s
                        .X(1)
                        .Y(2)); });
                    var v = A(() => B(s => s
                        .X(1)
                        .Y(2)));
                    A(x => x.B(s => s
                        .X(1)
                        .Y(2)));
                    A(() => B(s => s.X(1)
                        .Y(2)
                        .Z(3)));
                }
            }
            """,
            """
            class T {
                void M() {
                    A(s => s
                        .X(1)
                        .Y(2)
                    );
                    A(() => B(s => s
                            .X(1)
                            .Y(2)
                        )
                    );
                    A(() => B(() => C(s => s
                                .X(1)
                                .Y(2)
                            )
                        )
                    );
                    A(() => B(
                            first,
                            s => s
                                .X(1)
                                .Y(2)
                        )
                    );
                    A(() => {
                            B(s => s
                                .X(1)
                                .Y(2)
                            );
                        }
                    );
                    var v = A(() => B(s => s
                            .X(1)
                            .Y(2)
                        )
                    );
                    A(x => x.B(s => s
                            .X(1)
                            .Y(2)
                        )
                    );
                    A(() => B(s => s.X(1)
                            .Y(2)
                            .Z(3)
                        )
                    );
                }
            }
            """
        );

    /// <summary>
    ///     Skala's own <c>build/Build.cs</c> shape: <c>.Executes(() =&gt; DotNetTest(settings =&gt; settings</c> /
    ///     <c>.SetProjectFile(…)</c> two levels past the <c>.Executes</c> line, as behind <c>Stamp(settings)</c>.
    /// </summary>
    [Fact]
    public void TheBuildScriptsShape_TakesTwoLevels() =>
        Oracle.Agrees(
            """
            class T {
                Target Test =>
                    definition => definition
                        .DependsOn(Compile)
                        .Executes(() => DotNetTest(settings => settings
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        ));

                Target Test2 =>
                    definition => definition
                        .DependsOn(Compile)
                        .Executes(() => DotNetTest(settings => Stamp(settings)
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        ));

                void M() {
                    Run(() => DotNetTest(settings => settings
                        .SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                    ));
                    definition
                        .Executes(() => DotNetTest(settings => settings
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        ));
                    definition.Executes(() => DotNetTest(settings => settings
                        .SetProjectFile(Solution)
                        .SetConfiguration(Configuration)
                    ));
                    definition
                        .Executes(x => DotNetTest(settings => settings
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        ));
                    definition
                        .Executes(() => DotNetTest(settings => settings.SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        ));
                }
            }
            """,
            """
            class T {
                Target Test =>
                    definition => definition
                        .DependsOn(Compile)
                        .Executes(() => DotNetTest(settings => settings
                                .SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                            )
                        );

                Target Test2 =>
                    definition => definition
                        .DependsOn(Compile)
                        .Executes(() => DotNetTest(settings => Stamp(settings)
                                .SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                            )
                        );

                void M() {
                    Run(() => DotNetTest(settings => settings
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        )
                    );
                    definition
                        .Executes(() => DotNetTest(settings => settings
                                .SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                            )
                        );
                    definition.Executes(() => DotNetTest(settings => settings
                            .SetProjectFile(Solution)
                            .SetConfiguration(Configuration)
                        )
                    );
                    definition
                        .Executes(x => DotNetTest(settings => settings
                                .SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                            )
                        );
                    definition
                        .Executes(() => DotNetTest(settings => settings.SetProjectFile(Solution)
                                .SetConfiguration(Configuration)
                            )
                        );
                }
            }
            """
        );
}
