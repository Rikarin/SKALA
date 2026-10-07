using System;

// A call through a delegate-typed field has one candidate, its `Invoke`, and no overloads to move.
public sealed class Pipeline {
    readonly Action<Func<int, int>> register;

    public Pipeline(Action<Func<int, int>> register) {
        this.register = register;
    }

    public void Configure() {
        register(delegate(int x) { return x * 2; });
    }
}
