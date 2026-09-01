using System;

public sealed class Sink {
    public EventArgs Nothing() {
        EventArgs empty = new();
        return empty;
    }
}
