using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (FormatException ex) {
            throw new InvalidOperationException("could not read " + path, ex);
        }
    }

    static void Parse(string path) { }
}
