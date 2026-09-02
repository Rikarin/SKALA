using System;

// `out var _` is a declaration expression whose designation is a discard, and `out _` is a bare
// discard. Two syntaxes, one operation, and the rule asks the operation.
class Reader {
    static bool TryMeasure(string line, out int width) {
        width = line.Length * 2;
        return line.Length > 0;
    }

    public void First(string line) {
        if (TryMeasure(line, out var _)) {
            Console.WriteLine("first");
        }
    }

    public void Second(string line) {
        if (TryMeasure(line, out _)) {
            Console.WriteLine("second");
        }
    }
}
