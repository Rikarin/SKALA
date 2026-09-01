public sealed class Flags {
    public bool Verbose { get; set; }
}

public static class FlagFactory {
    // Empty under this symbol set and not under another. Deleting the braces would not merely
    // lose text, it would stop the file compiling when VERBOSE is defined.
    public static Flags Create() =>
        new Flags {
#if VERBOSE
            Verbose = true
#endif
        };
}
