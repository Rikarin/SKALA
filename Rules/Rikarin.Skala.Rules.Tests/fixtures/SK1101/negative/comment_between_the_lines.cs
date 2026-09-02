public sealed class Explained {
    static int Read(string source) => source.Length;

    public static int Count(string source) {
        int count;

        // Deliberately not folded into the declaration: the reader is meant to notice the order.
        count = Read(source);
        return count;
    }
}
