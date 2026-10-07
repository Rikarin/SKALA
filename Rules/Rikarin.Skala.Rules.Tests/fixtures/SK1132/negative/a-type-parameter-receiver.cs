// A type parameter is never a tuple type: `T.Item1` here is an interface property with one name.
public interface IPair {
    int Item1 { get; }
}

public static class Generic {
    public static int First<T>(T pair) where T : IPair => pair.Item1;
}
