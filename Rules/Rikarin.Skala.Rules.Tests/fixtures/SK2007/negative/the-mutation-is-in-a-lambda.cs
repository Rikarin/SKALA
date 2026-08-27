using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Deferred {
    // The delegate is built inside the loop and run somewhere else entirely. When it runs — if it
    // runs — is not a question this rule can answer, so it does not answer it.
    public List<Action> Plan(List<int> items) {
        var actions = new List<Action>();
        foreach (var item in items) {
            actions.Add(() => items.Remove(item));
        }

        return actions;
    }
}
