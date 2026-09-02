using System;

// The claim is per parameter, not per method. `head` is read at both call sites and `tail` at
// neither, so exactly one of the two is reported and the other is left alone.
class Splitter {
    static bool TrySplit(string text, out string head, out string tail) {
        var index = text.IndexOf(',');
        head = index < 0 ? text : text.Substring(0, index);
        tail = index < 0 ? string.Empty : text.Substring(index + 1);
        return index >= 0;
    }

    public void First(string text) {
        if (TrySplit(text, out var head, out _)) {
            Console.WriteLine(head);
        }
    }

    public void Second(string text) {
        if (TrySplit(text, out var start, out _)) {
            Console.WriteLine(start);
        }
    }
}
