using System;

// ⚠ The corpus sweep's finding, kept as a fixture. Newtonsoft.Json's `TryParseMicrosoftDate` has two
// call sites — one discarding `offset`, one reading it — and the reading one is inside
// `#if HAVE_DATE_TIME_OFFSET`. Disabled text is trivia: no nodes, no operations, no symbols, so the
// second call site is invisible to every part of the analysis and the finding is true of the
// configuration compiled and false of the library's own build. Every word in a disabled region is
// therefore treated as a reference to a name of that spelling.
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

#if HAVE_MEASUREMENT
    public void Measure(string line) {
        if (TryParseHeader(line, out var length)) {
            Console.WriteLine(length);
        }
    }
#endif
}
