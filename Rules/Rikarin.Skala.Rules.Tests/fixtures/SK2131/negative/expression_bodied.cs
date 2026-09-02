// An expression-bodied property computes its value; there is no storage to be left at `default`.
sealed class Window {
    public int Width => 1024;

    public bool IsWide => Width > 800;
}
