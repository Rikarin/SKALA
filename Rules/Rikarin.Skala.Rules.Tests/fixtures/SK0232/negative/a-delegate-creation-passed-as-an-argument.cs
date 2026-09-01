using System;

public static class Passing {
    static void Accept(Action action) => action();

    static void Accept(Func<int> factory) => factory();

    static void Work() { }

    // As an argument the creation is what picks the overload.
    public static void Go() => Accept(new Action(Work));
}
