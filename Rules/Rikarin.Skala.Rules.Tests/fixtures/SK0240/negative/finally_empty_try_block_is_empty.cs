class C {
    // ⚠ Both halves are empty, so the splice would replace the statement with nothing — and a
    // statement position that ends up holding no text is not something the fix may produce.
    public static void Save() {
        try {
        } finally {
        }
    }
}
