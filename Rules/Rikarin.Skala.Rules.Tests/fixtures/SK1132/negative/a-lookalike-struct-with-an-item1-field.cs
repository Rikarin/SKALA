// A struct that happens to have a field called `Item1` is not a tuple and has no other name for it.
public struct Pair {
    public int Item1;
    public int Count;
}

public static class Reader {
    public static int First(Pair pair) => pair.Item1 + pair.Count;
}
