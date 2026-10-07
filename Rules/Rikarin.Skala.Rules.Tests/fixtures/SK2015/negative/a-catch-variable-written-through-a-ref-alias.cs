using System;

// ⚠ #412's audit: `alias` is `ex`, so the write through it replaces the exception `throw ex;` throws,
// without `ex` ever standing on the left of an assignment.
public static class Probe {
    static void Fail() {
        try {
            throw new InvalidOperationException("original");
        } catch (Exception ex) {
            ref Exception alias = ref ex;
            alias = new ArgumentException("via ref");
            throw ex;
        }
    }

    public static string Run() {
        Fail();
        return "none";
    }
}
