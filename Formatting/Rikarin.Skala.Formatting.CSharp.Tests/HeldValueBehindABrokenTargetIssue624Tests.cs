namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Issue #624: an assignment's held value behind a target that ends on a line of its own — <c>E.Find(e)</c> /
///     <c>.Value = World.Get(…);</c> — reads its column from that line, not through the whole target as if it were
///     flat. Every expected string is <c>jb cleanupcode</c>'s own output, measured 2026-10-14 with <c>Testing ask</c>.
/// </summary>
public sealed class HeldValueBehindABrokenTargetIssue624Tests {
    /// <summary>
    ///     Fuzz 15061394490375601826, minimised: the value broken inside <c>entity.</c> / <c>Index</c> is no held call,
    ///     so pass one kept the <c>=</c>; pass two, the value joined, measured the target flat through the <c>=</c>, put
    ///     the value at column 125 and broke the <c>=</c>. The oracle keeps it on both: the line is 93 columns.
    /// </summary>
    [Fact]
    public void ATargetBrokenBeforeItsLastDot_KeepsTheEquals() =>
        Oracle.Agrees(
            """
            class C {
              void M57() {
              {
               {
              {
               Entities_wwwwwwwwwwwwww.Find(entity)
            .Value = World.GetLinearVelocityOf(body.Handle, entity.
            Index, scale);
              }
               }
              }
              }
                void A() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwww.Find(entity)
                                    .Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                            }
                        }
                    }
                }
                void E() {
                    this.Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq
                        .Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
            """,
            """
            class C {
                void M57() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwww.Find(entity)
                                    .Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                            }
                        }
                    }
                }

                void A() {
                    {
                        {
                            {
                                Entities_wwwwwwwwwwwwww.Find(entity)
                                    .Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                            }
                        }
                    }
                }

                void E() {
                    this.Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq
                        .Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
            """
        );

    /// <summary>
    ///     A chopped chain breaks at every link once the author broke one, and once the target through its <c>=</c>
    ///     does not fit; either way the target ends on <c>.Velocity =</c>, and the value stays beside it. A target
    ///     that fits through its <c>=</c> stays whole, and the value moves down.
    /// </summary>
    [Fact]
    public void AChoppedChainTarget_KeepsTheEqualsOnItsLastLink() =>
        Oracle.Agrees(
            """
            class C {
                void B() {
                    Items_qqqqqqqqqqqqqqqqqqqqqq
                        .Find(entity).Velocity = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
                void D() {
                    Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq.Find(entity, Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq).Velocity = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
                void G() {
                    {
                        Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq.Find(entity, Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq).Velocity = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                    }
                }
            }
            """,
            """
            class C {
                void B() {
                    Items_qqqqqqqqqqqqqqqqqqqqqq
                        .Find(entity)
                        .Velocity = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }

                void D() {
                    Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq.Find(entity, Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq).Velocity =
                        World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }

                void G() {
                    {
                        Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq.Find(entity, Items_qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq)
                            .Velocity = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                    }
                }
            }
            """
        );
}
