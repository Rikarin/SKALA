using System;

// ⚠ A `partial` method is two symbols for one method, and either part may be a generator's. The
// declaration the finding would land on is not necessarily the one the author edits, and the body
// holding the dead assignment is in the other part.
partial class Reader {
    private partial bool TryParseHeader(string line, out int length);

    public void Run(string line) {
        if (TryParseHeader(line, out _)) {
            Console.WriteLine("ok");
        }
    }
}

partial class Reader {
    private partial bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }
}
