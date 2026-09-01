public sealed class Nested {
    int count;

    static void Use(int value) { }

    // Only whole expression statements are reported.
    public void Advance() {
        Use(count = count + 1);
    }
}
