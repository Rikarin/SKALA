// The ordinary correct shape: a get-only property the constructor fills in.
sealed class Window {
    public int Width { get; }

    public Window(int width) => Width = width;

    public bool IsWide => Width > 800;
}
