// ⚠ The span where this rule and SK0232 must not disagree, pinned from this side.
//
// SK0232's own false-positive note records the case: passing `null` to a [CallerMemberName]
// parameter looks exactly like restating the default and is the opposite — omit it and the
// compiler substitutes a name, so the argument is the only thing keeping the value null. SK0232
// declines it by excluding caller-info parameters outright; this rule declines it by reporting
// only a restatement or a fabricated location, and `null` is neither. Both stay silent here, and
// a change to either that broke that would fail this fixture.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Anonymous {
        public void Run() {
            Trace("started", null);
        }

        static void Trace(string message, [CallerMemberName] string? member = null) { }
    }
}
