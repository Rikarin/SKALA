using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (FormatException ex) {
            Log(ex);
            throw;
        }
    }

    static void Parse(string path) { }

    static void Log(Exception exception) { }
}
