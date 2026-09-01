using System;

public sealed class Ticker {
    public event EventHandler? Tick;

    public void Run(int times) {
        for (var i = 0; i < times; i++) {
            Tick?.Invoke(this, new EventArgs());
        }
    }
}
