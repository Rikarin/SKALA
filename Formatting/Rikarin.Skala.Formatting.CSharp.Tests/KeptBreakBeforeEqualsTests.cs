namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A break the author put before an <c>=</c> is the <c>=</c>'s break: the value joins it, and a value too wide for
///     that line breaks inside itself, never after the <c>=</c>. Every expected string is <c>jb cleanupcode</c>'s own
///     output, measured 2026-10-31 with <c>Testing ask</c>.
/// </summary>
public sealed class KeptBreakBeforeEqualsTests {
    /// <summary>
    ///     Nightly 2026-10-25, fuzz 16130008065409407106, minimised: the author broke before the <c>=</c> and after
    ///     <c>World.</c>. Broken inside, the value was no held call and pass one kept the line; pass two, the value
    ///     joined, measured it beside the target through the <c>=</c> as if the target's line held it, and broke
    ///     after the <c>=</c> as well. The oracle keeps <c>= World.…</c> on both.
    /// </summary>
    [Fact]
    public void AHeldCallBelowAKeptEqualsBreak_JoinsTheEquals() =>
        Oracle.Agrees(
            """
            class C {
                void M2() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
            = World.
            GetLinearVelocity(body.Handle);
                }

                void A1() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = World.GetLinearVelocity(body.Handle);
                }

                void B6() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        = World_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.GetLinearVelocity(body.Handle);
                }
            }
            """,
            """
            class C {
                void M2() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = World.GetLinearVelocity(body.Handle);
                }

                void A1() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = World.GetLinearVelocity(body.Handle);
                }

                void B6() {
                    var vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv
                        = World_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.GetLinearVelocity(
                            body.Handle
                        );
                }
            }
            """
        );

    /// <summary>
    ///     Fuzz 7310017 on held-value-behind-a-target-call, minimised: a block comment after an assignment's <c>=</c>
    ///     stays on the target's line, so the held call's table counts it. Uncounted, the value "fitted" at 120 columns
    ///     where the line is 128; pass one kept the <c>=</c> and chopped the arguments, and pass two, the value no held
    ///     call once broken, broke the <c>=</c>. The oracle breaks after the comment wherever the line overflows.
    /// </summary>
    [Fact]
    public void ABlockCommentAfterTheEquals_CountsTowardTheHeldValue() =>
        Oracle.Agrees(
            """
            class C {
                void M1() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */  World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }

                void M3() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */ World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }

                void M4() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */ World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }
            }
            """,
            """
            class C {
                void M1() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */
                                    World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }

                void M3() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */ World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }

                void M4() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = /* f */
                                    World.GetLinearVelocity(body.Handle);
                            }
                        }
                    }
                }
            }
            """
        );

    /// <summary>
    ///     A value too wide even beside the kept <c>=</c> chops its own arguments; one that is an initializer or a binary
    ///     breaks there too. And a break the author put after the <c>=</c> as well is kept, <c>=</c> alone on its line.
    /// </summary>
    [Fact]
    public void AWideValueBelowAKeptEqualsBreak_BreaksInsideItself() =>
        Oracle.Agrees(
            """
            class C {
                int G
                    =
                    5;

                void A7() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = World.GetLinearVelocity_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww(body.Handle);
                }

                void B3() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                }

                void B5() {
                    Entities.Value
                        = new List<int> { 1111111111, 2222222222, 3333333333, 4444444444, 5555555555, 6666666666, 77777777777 };
                }
            }
            """,
            """
            class C {
                int G
                    =
                    5;

                void A7() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = World.GetLinearVelocity_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww(
                            body.Handle
                        );
                }

                void B3() {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value
                        = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                        + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
                }

                void B5() {
                    Entities.Value
                        = new List<int> {
                            1111111111,
                            2222222222,
                            3333333333,
                            4444444444,
                            5555555555,
                            6666666666,
                            77777777777
                        };
                }
            }
            """
        );
}
