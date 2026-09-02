public sealed class Registry {
    // The element does not carry the position, so `foreach` would lose it.
    public static void Render(string[] entries) {
        for (var i = 0; i < entries.Length; i++) {
            System.Console.WriteLine(i + ": " + entries[i]);
        }
    }
}
