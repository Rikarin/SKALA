// #614: an assignment whose target holds a call of its own, before a single call on a receiver: the `=` decision
// reads the value's column from the target through the `=`, not from the target's own dot.
class C {
    void M1() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M2() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M3() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M4() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M5() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M6() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
    }

    void M7() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
    }

    void M8() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
    }

    void M9() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
    }

    void M10() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
    }

    void M11() {
        Entities_wwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M12() {
        Entities_wwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M13() {
        Entities_wwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M14() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M15() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M16() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M17() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M18() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M19() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M20() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
    }

    void M21() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
    }

    void M22() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
    }

    void M23() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
    }

    void M24() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
    }

    void M25() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
    }

    void M26() {
        Entities_wwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M27() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M28() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M29() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M30() {
        Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
    }

    void M31() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M32() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M33() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M34() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M35() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M36() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M37() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M38() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M39() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M40() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M41() {
        {
            {
                {
                    Entities_wwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M42() {
        {
            {
                {
                    Entities_wwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M43() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwww.Get<LinearVelocity>(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M44() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M45() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M46() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M47() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M48() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocity(body.Handle);
                }
            }
        }
    }

    void M49() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M50() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M51() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M52() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M53() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.Combine(body.Handle, other);
                }
            }
        }
    }

    void M54() {
        {
            {
                {
                    Entities_wwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M55() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M56() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M57() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }

    void M58() {
        {
            {
                {
                    Entities_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww.Find(entity).Value = World.GetLinearVelocityOf(body.Handle, entity.Index, scale);
                }
            }
        }
    }
}
