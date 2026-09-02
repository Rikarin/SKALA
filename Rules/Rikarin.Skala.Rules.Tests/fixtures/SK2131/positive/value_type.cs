// Nothing can write `Width`: no setter, no initializer, no constructor. It is 0 forever, and the
// compiler says nothing at all about it — CS8618 is a nullable-reference warning and this is an int.
sealed class Window {
    public int Width { get; }

    public bool IsWide => Width > 800;
}
