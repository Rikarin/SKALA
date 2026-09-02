using System;

// Nothing is conditional here, so the increment happens on every run.
public sealed class Audit {
    int sequence;

    public void Record(Action<int> sink) {
        sink(sequence++);
    }
}
