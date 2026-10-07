// ⚠ #423: `a & Logged` runs the getter whatever `a` is, and `a && Logged` skips it when `a` is false.
// Measured for #412's audit: one call before the fix, none after. A getter with a body is work the
// author may have meant to run, exactly like a call.
public static class Probe {
    static int calls;

    static bool Logged {
        get {
            calls++;
            return true;
        }
    }

    public static int Run() {
        var a = calls < 0;
        var both = a & Logged;
        return both ? -1 : calls;
    }
}
