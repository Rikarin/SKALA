using System;

public sealed class Wiring {
    public void Wire() {
        Action callback = delegate {
            Console.WriteLine("done");
        };

        callback();
    }
}
