using System;

public sealed class Reader {
    public static void Read(string path) {
        try {
            Parse(path);
        } catch (Exception ex) {
            // ⚠ A bare `throw;` inside the lambda is CS0156 — the lambda is not the handler.
            Run(() => throw ex);
        }
    }

    static void Parse(string path) { }

    static void Run(Action action) { }
}
