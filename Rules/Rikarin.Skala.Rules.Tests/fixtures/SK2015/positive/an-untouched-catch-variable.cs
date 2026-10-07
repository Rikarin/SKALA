using System;

// Nothing in the clause writes `ex`, so `throw ex;` and `throw;` throw the same exception; only the
// stack trace differs, and that is the point of the fix.
public static class Probe {
    static void Fail() {
        try {
            throw new InvalidOperationException("original");
        } catch (InvalidOperationException ex) {
            throw ex;
        }
    }

    public static string Run() {
        Fail();
        return "none";
    }
}
