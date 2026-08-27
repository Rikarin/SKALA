using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (Exception outer) {
            try {
                Recover(path);
            } catch (FormatException) {
                // ⚠ `throw;` here would re-throw the *inner* exception, which is a different
                // program. The rule must not touch it.
                throw outer;
            }
        }
    }

    static void Parse(string path) { }

    static void Recover(string path) { }
}
