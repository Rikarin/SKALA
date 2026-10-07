// ⚠ #424: `nameof` is a contextual keyword. With a method called `nameof` in scope, `nameof(count)`
// calls it, compiles, and hands the exception "user-method:…" as its parameter name (#412's audit).
using System;

public static class Probe {
    static string nameof(object o) => "user-method:" + o;

    static void Check(string count) {
        if (count.Length == 0) {
            throw new ArgumentException("bad", "cont");
        }
    }

    public static string? Run() {
        try {
            Check("");
            return "none";
        } catch (ArgumentException e) {
            return e.ParamName;
        }
    }
}
