using System.Collections.Generic;

public sealed record Layer(string Name, List<int> Items);

public static class Layers {
    // #636, exactly as reported: `with` calls the copy constructor, so `Copy(layer)` is a different
    // object from `layer`. The fix used to write `layer`, and the copy was gone.
    public static Layer Copy(Layer layer) => layer with { };
}
