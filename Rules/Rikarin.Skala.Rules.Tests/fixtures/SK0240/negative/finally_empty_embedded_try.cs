class C {
    // ⚠ The `try` is an `if`'s embedded statement rather than a statement in a block, so the splice
    // would have two statements and nowhere to put them.
    public static void Save(bool enabled) {
        if (enabled)
            try {
                Run();
                Close();
            } finally {
            }
    }

    static void Run() { }

    static void Close() { }
}
