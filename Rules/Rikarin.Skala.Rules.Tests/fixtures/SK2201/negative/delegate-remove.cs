using System;

// `Delegate.Remove` has the same problem and is a different shape; the rule reports the operator it
// can read, and does not guess at a call it cannot.
public sealed class Composer {
    Action? work;

    public void Detach(Action handler) {
        work = (Action?)Delegate.Remove(work, handler);
    }
}
