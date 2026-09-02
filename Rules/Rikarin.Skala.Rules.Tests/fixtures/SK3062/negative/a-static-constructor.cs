using System;

// A static constructor has no `this` to publish. ⚠ The `ProcessExit` subscription below is
// character-for-character the shape `positive/the-process-exit-event.cs` reports, and it is correct
// here because there is no half-built object — which makes this the fixture that defends the
// `static` modifier test rather than an argument about events. Remove that test and it goes red.
public static class Bootstrap {
    static readonly int[] state;

    static Bootstrap() {
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        state = new int[4];
    }

    public static int Count => state.Length;

    static void OnExit(object? sender, EventArgs e) { }
}
