using System;
using System.Collections.Generic;

class C {
    readonly List<Action> actions = new();

    void M() {
        foreach (var i in new[] { 1, 2, 3 }) {
            actions.Add(() => Console.WriteLine(i));
        }
    }
}
