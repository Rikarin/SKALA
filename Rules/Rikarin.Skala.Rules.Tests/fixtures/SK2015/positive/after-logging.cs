using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (Exception ex) {
            Log(ex);
            throw ex;
        }
    }

    static void Parse(string path) { }

    static void Log(Exception exception) { }
}
