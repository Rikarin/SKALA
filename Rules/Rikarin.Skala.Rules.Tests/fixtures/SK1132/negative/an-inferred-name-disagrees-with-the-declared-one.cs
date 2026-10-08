public sealed class Viewport {
    (int X, int Y, int Width, int Height) viewport;

    // Vixen's `GlStateCache.SetViewport`: the literal's names are inferred from the expressions that
    // fill it, so the second slot is called `flipped` here and `Y` in the field it is stored into, and
    // the other three have no name at all.
    public int Set(float x, float width, float height, int flipped) {
        var value = ((int)x, flipped, (int)width, (int)height);
        viewport = value;
        return value.Item1 + value.Item2 + value.Item3 + value.Item4;
    }
}
