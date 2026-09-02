using System;

public static class Dispatching {
    static void Run(Action<int> work) => work(1);

    static void Run(Func<int, int> work) => work(1);

    // ⚠ The signature is what picks the overload. `delegate { }` converts to both, so removing it
    // is not a spelling change — it is an ambiguity. The target type has to be written down.
    public static void Go() => Run(delegate(int value) { Console.WriteLine("done"); });
}
