using System;

// ⚠ #412's audit: the deconstruction writes `ex`, so `throw ex;` throws the replacement and a bare
// `throw;` would rethrow the original.
public static class Probe {
    static void Fail() {
        try {
            throw new InvalidOperationException("original");
        } catch (Exception ex) {
            (ex, _) = (new ArgumentException("replacement"), 0);
            throw ex;
        }
    }

    public static string Run() {
        Fail();
        return "none";
    }
}
