using System.Runtime.CompilerServices;

public static class OnlyCallerInfo {
    public static void Where([CallerFilePath] string file = "", [CallerLineNumber] int line = 0) { }
}
