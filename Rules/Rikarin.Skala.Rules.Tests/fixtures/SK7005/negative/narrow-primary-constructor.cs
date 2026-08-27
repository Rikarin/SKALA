// A primary constructor is measured, and a reasonable one produces nothing.
public sealed class NarrowPrimaryConstructor(int width, int height, string name) {
    public int Area => width * height;

    public string Name => name;
}
