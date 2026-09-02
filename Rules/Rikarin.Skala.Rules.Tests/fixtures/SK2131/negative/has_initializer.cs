// An initializer is a value, so the property is not `default`.
sealed class Window {
    public int Width { get; } = 1024;

    public bool IsWide => Width > 800;
}
