// An `init` accessor gives a caller a way to supply the value, so the property is not unwritable.
sealed class Window {
    public int Width { get; init; }

    public bool IsWide => Width > 800;
}
