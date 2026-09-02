// A primary constructor's assignments are not constructor statements the walk can read, so the
// whole type is declined rather than guessed at.
public sealed class Window(int given) {
    readonly int width = 800;

    public int Width => width + given;
}
