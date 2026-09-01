using System;

public sealed class Flags {
    // ⚠ `"flar"` is one edit from both parameters. Two candidates equally close means the rule does
    // not know which name was meant, and guessing would write the wrong `nameof` into the fix.
    public void Set(string flag, string flat) {
        if (flag is null || flat is null) {
            throw new ArgumentNullException("flar");
        }
    }
}
