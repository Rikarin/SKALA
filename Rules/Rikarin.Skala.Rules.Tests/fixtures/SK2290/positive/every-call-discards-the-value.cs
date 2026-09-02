using System;

// Two call sites, both `out _`. The length the body computes is thrown away at every point in the
// program that could have read it, so the assignment is dead across the whole call graph.
class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public void Accept(string line) {
        if (TryParseHeader(line, out _)) {
            Console.WriteLine("accepted");
        }
    }

    public void Reject(string line) {
        if (!TryParseHeader(line, out _)) {
            Console.WriteLine("rejected");
        }
    }
}
