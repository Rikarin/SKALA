using System;

// ⚠ Why the analysis starts at the compilation and not at the symbol. A `private` member is callable
// from a nested type, and a nested type is a *different* INamedTypeSymbol — so a per-type symbol
// start would see this declaration with none of the call sites that decide it.
class Outer {
    static bool TryCompute(int input, out int doubled) {
        doubled = input * 2;
        return input > 0;
    }

    public sealed class Inner {
        public void Check(int input) {
            if (TryCompute(input, out _)) {
                Console.WriteLine("positive");
            }
        }
    }
}
