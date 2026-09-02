using System;

public sealed class Wiring {
    public void Wire() {
        Action callback = () => Console.WriteLine("done");

        callback();
    }
}
