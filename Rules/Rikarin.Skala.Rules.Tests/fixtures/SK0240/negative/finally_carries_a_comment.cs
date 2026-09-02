class C {
    // ⚠ Empty by statement count and not empty as text. The fix deletes the whole clause, and the
    // note inside it is the author saying why the guarantee is there — so the finding is withheld
    // rather than the prose being deleted under a fix marked safe.
    public static void Save() {
        try {
            Run();
        } finally {
            // The handle is closed by the caller; this frame only needs the region.
        }
    }

    static void Run() { }
}
