public sealed class Nested {
    static int Read(string source) => source.Length;

    public static int Count(string source, bool wanted) {
        if (wanted) {
            int count;
            count = Read(source);
            return count + 1;
        }

        return 0;
    }
}
