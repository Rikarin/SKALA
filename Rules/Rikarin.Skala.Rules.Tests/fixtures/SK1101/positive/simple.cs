public sealed class Reading {
    static int Read(string source) => source.Length;

    public static int Count(string source) {
        int count;
        count = Read(source);
        return count * 2;
    }
}
