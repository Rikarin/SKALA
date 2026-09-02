using System;

// `out int _` is the third spelling of a discard — a declaration expression with an explicit type and
// a discard designation, where `out var _` infers and `out _` declares nothing at all. All three bind
// to the same operation, which is why the rule asks the operation and not the syntax.
class Reader {
    static bool TryMeasure(string line, out int width) {
        width = line.Length * 2;
        return line.Length > 0;
    }

    public void Run(string line) {
        if (TryMeasure(line, out int _)) {
            Console.WriteLine("measured");
        }
    }
}
