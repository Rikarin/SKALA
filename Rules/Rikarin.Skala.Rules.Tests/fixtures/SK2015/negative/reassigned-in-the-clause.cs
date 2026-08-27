using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (Exception ex) {
            if (ex.InnerException is not null) {
                ex = ex.InnerException;
            }

            throw ex;
        }
    }

    static void Parse(string path) { }
}
