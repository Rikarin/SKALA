public static class Writer {
    static int Write(string text, bool flush = false, int retries = 0) => text.Length + retries;

    // Both go in one edit: one at a time would make the rule fire on its own output.
    public static int Emit(string text) => Write(text, false, 0);
}
