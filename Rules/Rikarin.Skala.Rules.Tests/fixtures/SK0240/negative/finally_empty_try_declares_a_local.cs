class C {
    // ⚠ The `finally` is the only clause, so the fix would have to splice the try block's contents
    // into the enclosing scope — and that moves `handle` out into a scope where it can collide with
    // a name that was never in conflict. Withheld rather than fixed badly.
    public static int Save() {
        try {
            var handle = Open();
            return handle;
        } finally {
        }
    }

    static int Open() => 1;
}
