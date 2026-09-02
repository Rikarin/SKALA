using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException e) {
            Log(e);
        } finally {
            Close();
        }
    }

    static void Run() { }

    static void Close() { }

    static void Log(Exception e) { }
}
