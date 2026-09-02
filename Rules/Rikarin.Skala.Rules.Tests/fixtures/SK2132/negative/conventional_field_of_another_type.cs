using System.Collections.Generic;

// ⚠ Rewritten after a sabotage stayed green: the first version of this fixture declined for the
// wrong reason, so removing the type test changed nothing in it. This one isolates the test. Drop
// the type equality and `names` becomes `Names`'s "own" field, `label` is `Label`'s, and the rule
// reports a crossed pair — then proposes `names` as the repair for a `string` accessor, which does
// not compile. With the test, `Names` has no field of its own type and the rule never starts.
sealed class Roster {
    readonly List<string> names = new();
    string label = "";

    public string Names {
        get => label;
        set => label = value;
    }

    public string Label {
        get => label;
        set => label = value;
    }

    public int Size => names.Count;
}
