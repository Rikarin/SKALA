// ⚠ The span where this rule and SK0232 must not disagree, pinned from this side.
//
// SK0232's own false-positive note records the case: passing `null` to a [CallerMemberName]
// parameter looks exactly like restating the default and is the opposite — omit it and the
// compiler substitutes a name, so the argument is the only thing keeping the value null. SK0232
// declines it by excluding caller-info parameters outright; this rule declines it by reporting
// only a restatement or a fabricated location, and `null` is neither. Both stay silent here, and
// a change to either that broke that would fail this fixture.
//
// ⚠ The [CallerFilePath] call is the one that pins the guard, and it was missing at first. A
// [CallerMemberName] argument of `null` is declined a second time anyway — by the `is string`
// test that follows — so deleting the null exclusion left a fixture holding only that call
// completely green. A *location* parameter is reported for any constant, so `null` reaches the
// `return true` unless the exclusion stops it. That is the sabotage this file has to fail.
using System.Runtime.CompilerServices;

namespace Fixtures {
    sealed class Anonymous {
        public void Run() {
            Trace("started", null);
            Locate("started", null);
        }

        static void Trace(string message, [CallerMemberName] string? member = null) { }

        static void Locate(string message, [CallerFilePath] string? file = null) { }
    }
}
