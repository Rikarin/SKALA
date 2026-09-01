using System;

public static class Choosing {
    static int Apply(Func<int, int> map) => map(1);

    static int Apply(Func<string, int> map) => map("a");

    public static int Go() => Apply((int n) => n);
}
