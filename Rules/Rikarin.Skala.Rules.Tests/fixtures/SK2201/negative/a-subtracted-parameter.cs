using System;

// ⚠ Ordinary, correct unsubscription, and the reason issue #165's general form does not ship:
// `d -= h` removes exactly `h` unless `h` is itself multicast, and whether it is cannot be decided
// from here.
public sealed class Hub {
    Action? subscribers;

    public void Add(Action handler) => subscribers += handler;

    public void Remove(Action handler) => subscribers -= handler;

    public void Fire() => subscribers?.Invoke();
}
