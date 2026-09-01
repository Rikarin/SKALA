public sealed class Gate {
    bool open;

    // C# has no `&&=`. Rewriting to `open &= Check()` would evaluate a call the original skips.
    public void Close(bool other) {
        open = open && other;
    }

    public bool IsOpen => open;
}
