using System;

// The claim is unanimity across the whole compilation, so one reader withdraws it. This is the
// fixture the discard test itself is measured against.
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

    public void Measure(string line) {
        if (TryParseHeader(line, out var length)) {
            Console.WriteLine(length);
        }
    }
}
