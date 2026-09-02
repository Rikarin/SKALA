public sealed class Explained {
    public static int Run() {
        int Work() => 7;

        // Deliberately the last thing in the method, so the reader meets the helper first.
        return Work();
    }
}
