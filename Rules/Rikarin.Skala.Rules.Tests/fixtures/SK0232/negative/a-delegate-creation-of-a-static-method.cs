using System;

// ⚠ #412's audit: since C# 11 the conversion of a static method group is cached in a static field,
// so `Tick` hands out one delegate where `new Action(Tick)` made a fresh one each time.
public static class Ticks {
    static void Tick() { }

    public static Action Make() {
        Action action = new Action(Tick);
        return action;
    }
}

public static class Probe {
    public static bool Run() => ReferenceEquals(Ticks.Make(), Ticks.Make());
}
