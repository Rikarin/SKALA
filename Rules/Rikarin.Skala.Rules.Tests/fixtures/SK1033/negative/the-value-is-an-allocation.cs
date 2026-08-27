using System.Collections.Generic;

// The same guard for the case that is not a correctness change but is still a regression: the queue
// is allocated on every call rather than only when the key is missing, and the missing case is the
// rare one.
public sealed class Demuxer {
    readonly Dictionary<int, Queue<int>> _pending = new();

    public void Follow(int track) {
        if (!_pending.ContainsKey(track)) {
            _pending[track] = new Queue<int>();
        }
    }
}
