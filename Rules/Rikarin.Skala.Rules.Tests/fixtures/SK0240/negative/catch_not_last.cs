using System;
using System.IO;

class C {
    static void Log(Exception error) { }

    public static void Save() {
        try {
            Run();
        } catch (IOException) {
            throw;
        } catch (Exception error) {
            Log(error);
        }
    }

    static void Run() { }
}
