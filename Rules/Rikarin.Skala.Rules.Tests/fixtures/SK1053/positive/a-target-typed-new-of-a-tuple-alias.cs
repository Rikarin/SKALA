using Range = (int Low, int High);

// ⚠ An alias to a tuple type is a name, and `new Range()` compiles — only the tuple *syntax*
// after `new` is CS8181. Measured, not assumed: the first draft of this fixture said the opposite.
public sealed class Pool {
    public void Warm() {
        Range range = new();
    }
}
