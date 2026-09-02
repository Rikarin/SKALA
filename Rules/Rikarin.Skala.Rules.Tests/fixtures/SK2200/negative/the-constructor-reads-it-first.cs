// The declared value is read before it is replaced, so it is observable.
public sealed class Counter {
    readonly int seed = 7;

    public int Doubled { get; }

    public Counter(int given) {
        Doubled = seed * 2;
        seed = given;
    }
}
