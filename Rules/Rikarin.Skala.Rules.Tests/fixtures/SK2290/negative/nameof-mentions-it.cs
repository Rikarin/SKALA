using System;

// A `nameof` is the name in a non-callee position, and the withdrawal set is deliberately spelled in
// identifier text rather than in symbols: the cost of being over-broad here is a missed finding, and
// the cost of being under-broad is a false one.
class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public void Run(string line) {
        if (TryParseHeader(line, out _)) {
            Console.WriteLine(nameof(TryParseHeader));
        }
    }
}
