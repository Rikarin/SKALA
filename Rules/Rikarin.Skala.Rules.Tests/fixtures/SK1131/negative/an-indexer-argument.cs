using System;

// An indexer's candidates are not a member group the rule enumerates, so it declines.
public sealed class Registry {
    public int this[Func<int, int> key] => key(1);
}

public static class Use {
    public static int Read(Registry registry) => registry[delegate(int x) { return x; }];
}
