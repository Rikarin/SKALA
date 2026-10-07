using System;

// `first + (int x) => { … }` does not parse either.
public static class Combining {
    public static Action<int> Both(Action<int> first) {
        return first + delegate(int x) { Console.WriteLine(x); };
    }
}
