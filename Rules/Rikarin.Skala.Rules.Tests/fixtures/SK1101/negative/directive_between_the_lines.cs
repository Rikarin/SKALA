public sealed class Conditional {
    static int Read(string source) => source.Length;

    // Joining across the `#if` would give one symbol set a declaration with an initializer and the
    // other a declaration with none, which is two different files rather than one rewrite.
    public static int Count(string source) {
        int count;
#if DEBUG
        count = 0;
#else
        count = Read(source);
#endif
        return count;
    }
}
