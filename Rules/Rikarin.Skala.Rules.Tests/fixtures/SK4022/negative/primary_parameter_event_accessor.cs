// ⚠ `+=` on a custom event calls its `add` accessor on the capture, and under `readonly struct` on a
// copy (#412).
using System;

public struct Signal {
    public int Listeners;

    public event Action Raised {
        add => Listeners++;
        remove => Listeners--;
    }
}

struct Relay(Signal signal) {
    public void Listen(Action handler) => signal.Raised += handler;

    public int Listeners => signal.Listeners;
}

public static class Probe {
    public static int Run() {
        var relay = new Relay(new Signal());
        relay.Listen(static () => { });
        return relay.Listeners;
    }
}
