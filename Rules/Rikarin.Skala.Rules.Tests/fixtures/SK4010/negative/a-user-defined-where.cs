using System.Collections.Generic;

public sealed class Bag {
    public Bag Where(System.Func<int, bool> predicate) => this;

    public int First() => 0;
}

public sealed class Registry {
    public static int Ready(Bag bag) => bag.Where(value => value > 0).First();
}
