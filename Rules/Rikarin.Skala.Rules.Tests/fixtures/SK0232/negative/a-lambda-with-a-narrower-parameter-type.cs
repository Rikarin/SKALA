using System;

public static class Narrowed {
    public static int Length(string? text) {
        // The written type is narrower than the delegate's. Dropping it would introduce the CS8602
        // the author wrote it to avoid.
        Func<string?, int> length = (string s) => s.Length;
        return length(text);
    }
}
