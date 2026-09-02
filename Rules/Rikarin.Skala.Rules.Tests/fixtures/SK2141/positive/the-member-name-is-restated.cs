// `nameof(Run)` is exactly what the compiler was going to substitute, so the argument buys
// nothing and costs the substitution the day the method is renamed.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Tracer {
        public void Run() {
            Trace("started", nameof(Run));
        }

        static void Trace(string message, [CallerMemberName] string? member = null) { }
    }
}
