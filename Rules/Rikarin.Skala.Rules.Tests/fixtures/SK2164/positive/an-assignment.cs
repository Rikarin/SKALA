using System.Diagnostics;

public sealed class Tracker {
    int count;

    public void Record(int value) {
        Debug.Assert((count = value) > 0);
    }
}
