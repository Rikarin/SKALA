using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            try {
                Parse(path);
            } catch (FormatException inner) {
                throw inner;
            }
        } catch (Exception outer) {
            Log(outer);
        }
    }

    static void Parse(string path) { }

    static void Log(Exception exception) { }
}
