using System;

// ⚠ ReSharper's `.Global` half, and it is out of reach rather than unbuilt. A public method's callers
// live in assemblies that do not exist yet, so "every caller discards it" is not a fact this
// compilation holds — however unanimous the callers it can see happen to be.
public class Reader {
    public static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public void Run(string line) {
        if (TryParseHeader(line, out _)) {
            Console.WriteLine("ok");
        }
    }
}
