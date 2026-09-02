using System;

// ⚠ `Deconstruct` is the one out-parameter protocol the language reaches by *name* rather than by an
// invocation. `var (a, b) = this;` calls it and is not an IInvocationOperation, so the only visible
// call site is the explicit one below — which discards both parameters and would make the method look
// unanimously ignored while the deconstruction reads them.
class Point {
    readonly int x;
    readonly int y;

    public Point(int x, int y) {
        this.x = x;
        this.y = y;
    }

    void Deconstruct(out int first, out int second) {
        first = x;
        second = y;
    }

    public void Show() {
        var (a, b) = this;
        Console.WriteLine(a + b);
        Deconstruct(out _, out _);
    }
}
