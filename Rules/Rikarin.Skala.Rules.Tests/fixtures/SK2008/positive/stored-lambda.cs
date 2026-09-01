using System;
using System.Collections.Generic;

class C {
    List<Action> M() {
        var actions = new List<Action>();
        for (var i = 0; i < 3; i++) {
            actions.Add(() => Console.WriteLine(i));
        }

        return actions;
    }
}
