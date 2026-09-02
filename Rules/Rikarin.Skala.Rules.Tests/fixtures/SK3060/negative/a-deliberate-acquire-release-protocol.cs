using System.Threading;

public sealed class Session {
    readonly object gate = new();

    int depth;

    // ⚠ The type-level escape. There is no `finally` that could span two members, so the pairing was
    // split across them on purpose and the caller owns it. Reporting this would be an argument about
    // a convention rather than a bug, and that is the finding that gets the analysis switched off.
    public void Acquire() {
        Monitor.Enter(gate);
        depth++;
    }

    public void Release() {
        depth--;
        Monitor.Exit(gate);
    }
}
