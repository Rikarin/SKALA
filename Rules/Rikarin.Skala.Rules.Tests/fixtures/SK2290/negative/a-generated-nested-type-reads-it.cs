using System;
using System.CodeDom.Compiler;

// ⚠ Why the analyzer configures `GeneratedCodeAnalysisFlags.Analyze` rather than `None`. A call from
// generated code *is* a call. At `None` the syntax and operation actions never run inside the
// generated nested type, the reading call site below is invisible, and the rule reports a parameter
// somebody does read. The declaring type is not generated, so the finding's own location stays
// reportable and the difference is visible rather than swallowed by Roslyn's location filter.
class Reader {
    static bool TryParseHeader(string line, out int length) {
        length = line.Length;
        return line.Length > 0;
    }

    public void Run(string line) {
        if (TryParseHeader(line, out _)) {
            Console.WriteLine("ok");
        }
    }

    [GeneratedCode("fixture", "1.0")]
    public sealed class Generated {
        public void Run(string line) {
            if (TryParseHeader(line, out var length)) {
                Console.WriteLine(length);
            }
        }
    }
}
