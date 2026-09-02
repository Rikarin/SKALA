// ⚠ The shape that crashes SK0232 (issue #298): more arguments than the method has parameters,
// so `arguments.Count - 1 - n` indexes past the end of the parameter array. This rule bounds the
// index rather than the counter and declines the call outright — for an expanded `params` call,
// "the parameter this argument fills" is not a fact at all.
//
// The fixture harness filters by rule id, so this file also has to exist under SK2141 for SK2141
// to be the analyzer that survives it.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Expanded {
        public void Run() {
            Take(1, 2, 3);
            Log("started", nameof(Run), 1, 2);
        }

        static void Take(int first, params int[] rest) { }

        static void Log(string message, [CallerMemberName] string? member = null, params int[] rest) { }
    }
}
