// The whole point of the parameter, working. Anti-vacuity for the positives: if the rule were
// keyed on the attribute rather than on the argument, this would fire too.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Quiet {
        public void Run() {
            Trace("started");
        }

        static void Trace(string message, [CallerMemberName] string? member = null) { }
    }
}
