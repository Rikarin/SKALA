using System;

// `ref` carries a value *in* as well as out, so a caller that ignores the result still handed the
// method something to read. Only `RefKind.Out` — the parameter the callee is obliged to assign and
// the caller is obliged to receive — can be dead in the way this rule means.
class Accumulator {
    static void Add(ref int total, int amount) {
        total += amount;
    }

    public void Run() {
        var total = 0;
        Add(ref total, 3);
        Console.WriteLine(total);
    }
}
