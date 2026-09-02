public static class Registry {
    private static int Version { get; set; }

    public static void Bump() {
        Version++;
    }

    public static int Current() => Version;
}
