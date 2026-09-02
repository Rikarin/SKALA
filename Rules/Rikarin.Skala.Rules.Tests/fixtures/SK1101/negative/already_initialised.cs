public sealed class Reset {
    static int Read(string source) => source.Length;

    // The declaration already carries a value, so the assignment below is an overwrite rather than
    // the initialisation this rule joins.
    public static int Count(string source) {
        int count = 0;
        count = Read(source);
        return count;
    }
}
