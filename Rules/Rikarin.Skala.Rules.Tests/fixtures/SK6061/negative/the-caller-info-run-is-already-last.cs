using System.Runtime.CompilerServices;

public static class Logging {
    public static void Log(
        string message,
        int level = 0,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) { }
}
