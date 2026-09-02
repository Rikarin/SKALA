using System;

// A local function is bounded the same way and is deliberately out of scope: it is not a
// MethodDeclarationSyntax and has no declared accessibility, so nothing here reaches it. Named rather
// than implied, so the residue keeps being counted.
class Reader {
    public void Run(string line) {
        static bool TryParseHeader(string text, out int length) {
            length = text.Length;
            return text.Length > 0;
        }

        if (TryParseHeader(line, out _)) {
            Console.WriteLine("ok");
        }
    }
}
