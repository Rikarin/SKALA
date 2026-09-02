using System;

public sealed class Audit {
    int sequence;

    public void Record(Action<int>? sink) {
        sink?.Invoke(sequence++);
    }
}
