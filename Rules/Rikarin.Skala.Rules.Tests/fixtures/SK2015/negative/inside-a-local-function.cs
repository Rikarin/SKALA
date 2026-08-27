using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (Exception ex) {
            Retry();
            return;

            void Retry() => throw ex;
        }
    }

    static void Parse(string path) { }
}
