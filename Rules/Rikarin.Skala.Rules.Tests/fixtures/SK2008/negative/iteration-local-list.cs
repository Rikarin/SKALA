using System;
using System.Collections.Generic;

class C {
    void M() {
        for (var i = 0; i < 3; i++) {
            var actions = new List<Action>();
            actions.Add(() => Console.WriteLine(i));
            actions[0]();
        }
    }
}
