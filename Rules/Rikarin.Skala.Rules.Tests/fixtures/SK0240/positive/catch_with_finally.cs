using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException) {
            throw;
        } finally {
            Close();
        }
    }

    static void Run() { }

    static void Close() { }
}
