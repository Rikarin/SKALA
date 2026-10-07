// #431: writing another field and declaring a local run no code, so they may come before the overwrite.
public sealed class Span {
    int low;
    readonly int high = 10;

    public Span(int a, int b) {
        low = a;
        var width = b - a;
        high = low + width;
    }

    public int Width => high - low;
}

public static class Probe {
    public static int Run() => new Span(2, 7).Width;
}
