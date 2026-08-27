using System;

public sealed class Registration : IDisposable {
    public Registration() {
        // Constructors that register themselves are why a local nobody reads is not reported: the
        // object is doing its work through a side effect the rule cannot see.
    }

    public void Dispose() { }
}

public sealed class Host {
    public void Install() {
        var registration = new Registration();
    }
}
