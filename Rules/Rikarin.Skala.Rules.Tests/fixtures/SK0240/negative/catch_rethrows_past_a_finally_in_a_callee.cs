using System;

// ⚠ #412's audit: the rethrowing `catch` ends the first pass of exception handling, so `Work`'s
// `finally` runs before the outer filter does. Unwrapped, the outer filter runs first.
public static class Probe {
    static string _log = "";

    static bool Note(string text) {
        _log += text + " ";
        return true;
    }

    static void Work() {
        try {
            throw new InvalidOperationException();
        } finally {
            _log += "finally ";
        }
    }

    static void Middle() {
        try {
            Work();
        } catch {
            throw;
        }
    }

    public static string Run() {
        try {
            Middle();
        } catch (InvalidOperationException) when (Note("filter")) {
            _log += "caught";
        }

        return _log;
    }
}
