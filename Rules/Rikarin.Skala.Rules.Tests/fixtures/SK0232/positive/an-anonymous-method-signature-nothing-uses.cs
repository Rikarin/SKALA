using System;

public sealed class Ticker {
    public event EventHandler? Elapsed;

    public void Ignore() {
        // Neither parameter is mentioned, and the target type is written down, so `delegate { }`
        // converts to exactly the same delegate.
        EventHandler handler = delegate(object? sender, EventArgs e) { Console.WriteLine("tick"); };
        Elapsed += handler;
    }
}
