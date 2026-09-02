using System;

// ⚠ The receiver is exactly the one shape C reports — a `static` event whose lifetime nothing
// bounds — and this still publishes nothing, because the handler is a static method (#306). The
// event holds `Meter.Report`, not a `Meter`.
//
// Shape C shipped without looking at the right-hand side at all, so every constructor subscribing
// a static handler to a static event drew a finding. `Reaches` is what declines it, and this
// fixture reaches that test: `Clock.Tick` binds to a static event symbol, so nothing before the
// handler check can cut it.
public static class Clock {
    public static event EventHandler? Tick;

    public static void Advance() => Tick?.Invoke(null, EventArgs.Empty);
}

public sealed class Meter {
    public Meter() {
        Clock.Tick += Report;
    }

    static void Report(object? sender, EventArgs e) => Console.WriteLine("tick");
}
