using System.Collections.Generic;

public record Palette(string Name) {
    // A record's copy constructor can be written by hand, and `with` is what calls it: this one
    // detaches the copy's colours from the original's.
    protected Palette(Palette original) {
        Name = original.Name;
        Colours = new List<int>(original.Colours);
    }

    public List<int> Colours { get; init; } = [];
}

public static class Palettes {
    public static Palette Detached(Palette palette) => palette with { };
}
