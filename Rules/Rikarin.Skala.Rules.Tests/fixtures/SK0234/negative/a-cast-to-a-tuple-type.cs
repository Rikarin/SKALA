public static class Tuples {
    // Tuple casts differ in the names the result carries, so the cast branch never sees them.
    public static (int A, int B) Named((int, int) pair) => ((int A, int B))pair;
}
