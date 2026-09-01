public sealed record Point(int X, int Y);

public static class Origin {
    // `with` invokes the record's copy constructor, so this allocates a whole Point in order to
    // produce one equal to the argument.
    public static Point Copy(Point source) => source with { };
}
