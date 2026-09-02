using System;

// One call site is still unanimity, and the message says so in the singular. What it is not is
// *zero* call sites, which is the vacuous case the rule declines.
class Reader {
    static bool TryMeasure(string line, out int width) {
        width = line.Length * 2;
        return line.Length > 0;
    }

    public void Run(string line) {
        if (TryMeasure(line, out _)) {
            Console.WriteLine("measured");
        }
    }
}
