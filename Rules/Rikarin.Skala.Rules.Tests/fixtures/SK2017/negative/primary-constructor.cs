using System;

// A primary-constructor parameter is in scope in every member body of the type that declares it,
// and naming it in a `paramName` is correct.
public sealed class Window(int width) {
    public int Width => width;

    public void Grow(int by) {
        if (width + by < 0) {
            throw new ArgumentOutOfRangeException("width", width, "must not go negative");
        }
    }
}
