using System;

// A `[ThreadStatic]` field is one slot per thread, so the sharing the finding complains about is
// not there.
sealed class Scratch {
    [ThreadStatic] static string? buffer;

    public void Remember(string value) {
        buffer = value;
    }
}
