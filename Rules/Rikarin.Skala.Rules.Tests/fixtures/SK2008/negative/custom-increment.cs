using System;
using System.Collections.Generic;

struct Counter {
    public static Counter operator ++(Counter value) => value;
}

class C {
    readonly List<Action> actions = new();

    void M() {
        for (var i = new Counter();; i++) {
            actions.Add(() => Console.WriteLine(i));
            break;
        }
    }
}
