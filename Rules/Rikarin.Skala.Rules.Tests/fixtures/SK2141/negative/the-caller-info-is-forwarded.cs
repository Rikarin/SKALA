// A relay passes its own caller-info parameter down, which is the only way the value survives an
// extra frame. ⚠ No guard of its own is needed and none exists: an identifier is not a constant,
// so a forwarded value is never a candidate in the first place.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Relay {
        public void Trace(string message, [CallerMemberName] string? member = null) {
            Inner(message, member);
        }

        static void Inner(string message, [CallerMemberName] string? member = null) { }
    }
}
