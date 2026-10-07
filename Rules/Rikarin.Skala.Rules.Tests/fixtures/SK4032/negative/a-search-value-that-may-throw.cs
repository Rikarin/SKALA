// ⚠ #423: `holder.Needle` throws when `holder` is null, and the rewrite moves it ahead of the
// `Substring` call — which, given an offset past the end, threw `ArgumentOutOfRangeException` first.
public sealed class Holder {
    public char Needle = 'c';
}

public static class Probe {
    static bool Found(string text, Holder holder) => text.Substring(9).IndexOf(holder.Needle) >= 0;

    public static bool Run() => Found("abc", null!);
}
