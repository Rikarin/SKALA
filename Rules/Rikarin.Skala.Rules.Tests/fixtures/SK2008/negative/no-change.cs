using System;
using System.Collections.Generic;

class C {
    readonly List<Action> actions = new();

    void M() {
        for (var i = 0; i < 3; i += 0) {
            actions.Add(() => Console.WriteLine(i));
            break;
        }
    }
}
