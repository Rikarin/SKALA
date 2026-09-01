public sealed class Slice {
    public Slice Substring(int start) => this;

    public int IndexOf(char value) => -1;
}

public sealed class Paths {
    public static bool HasSeparator(Slice slice, int start) => slice.Substring(start).IndexOf('/') >= 0;
}
