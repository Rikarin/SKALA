public sealed class Deferred {
    static int Read(string source) => source.Length;

    static void Log() { }

    public static int Count(string source) {
        int count;
        Log();
        count = Read(source);
        return count;
    }
}
