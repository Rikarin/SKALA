public sealed class Options {
    public int Depth { get; set; }
}

public static class Defaults {
    // The braces are deleted wholesale, so a note about why they are empty would go with them.
    public static Options Create() =>
        new Options {
            // Depth is deliberately left at its default until the loader has run.
        };
}
