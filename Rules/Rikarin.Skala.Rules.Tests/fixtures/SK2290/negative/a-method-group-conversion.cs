using System;

// ⚠ Through a delegate the arguments are invisible, so the call-site set stops being complete — and
// completeness is the rule's only claim. The direct call below discards the value; the delegate call
// does not, and nothing in the operation tree connects it back to the declaration.
delegate bool Attempt(string line, out int length);

class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public void Run(string line) {
        Attempt attempt = TryParseHeader;
        if (attempt(line, out var measured)) {
            Console.WriteLine(measured);
        }

        if (TryParseHeader(line, out _)) {
            Console.WriteLine("direct");
        }
    }
}
