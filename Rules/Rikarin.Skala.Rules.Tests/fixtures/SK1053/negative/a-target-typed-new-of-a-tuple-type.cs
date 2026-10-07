// `new (int, int)()` is CS8181: a tuple type cannot follow `new`, so `(int, int) range = new();` has
// no discard that keeps its meaning.
public sealed class Pool {
    public void Warm() {
        (int Low, int High) range = new();
    }
}
