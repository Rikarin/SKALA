using System;

// `value` is the delegate the caller handed in, not one created here.
public sealed class Relay {
    EventHandler? handlers;

    public event EventHandler Forwarded {
        add => handlers += value;
        remove => handlers -= value;
    }

    public void Raise() => handlers?.Invoke(this, EventArgs.Empty);
}
