using System.Diagnostics;

public sealed class Tracker {
    int count;

    public void Record() {
        Debug.Assert(++count > 0);
    }
}
