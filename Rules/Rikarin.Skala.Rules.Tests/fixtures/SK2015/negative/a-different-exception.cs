using System;

public sealed class Reader {
    static readonly Exception Fallback = new InvalidOperationException("fallback");

    public static void Read(string path) {
        try {
            Parse(path);
        } catch (FormatException ex) {
            Log(ex);
            throw Fallback;
        }
    }

    static void Parse(string path) { }

    static void Log(Exception exception) { }
}
