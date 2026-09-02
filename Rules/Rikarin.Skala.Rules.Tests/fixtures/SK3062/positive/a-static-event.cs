using System;

// Shape C, first half: the event is `static`. Nothing about `Meter` bounds how long `Clock.Tick`
// keeps the handler, so the event goes on raising into this object after every reference to it has
// been dropped — and it may raise once before the constructor has finished.
public static class Clock {
    public static event EventHandler? Tick;

    public static void Advance() => Tick?.Invoke(null, EventArgs.Empty);
}

public sealed class Meter {
    int ticks;

    public Meter() {
        Clock.Tick += OnTick;
    }

    public int Ticks => ticks;

    void OnTick(object? sender, EventArgs e) => ticks++;
}
