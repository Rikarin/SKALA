using System;

class C {
    public static void Save() {
        try {
            Run();
        } catch (InvalidOperationException e) {
            Log(e);
        } finally {
        }
    }

    static void Run() { }

    static void Log(Exception e) { }
}
