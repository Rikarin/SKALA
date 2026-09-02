// ⚠ [#314]. The compilation unit is the top-level program's body, but it is not the search space,
// and this fixture is what says so. `Helper` is a member of the same compilation unit and its
// `Monitor.Exit` sits in a `finally` — so a release scan over the unit's whole descendant set would
// walk from that call up to the unit, meet the `finally`, and withdraw the finding. Nothing in
// `Helper` runs on the entry point's path.
//
// The enter below still has no `try`/`finally` of its own, so the finding stands.

using System.Threading;

var gate = new object();
var balance = 0;

Monitor.Enter(gate);
balance = checked(balance + 1);
Monitor.Exit(gate);

System.Console.WriteLine(balance);

sealed class Helper {
    static readonly object other = new();

    public static void Unrelated() {
        Monitor.Enter(other);
        try {
            System.Console.WriteLine("work");
        } finally {
            Monitor.Exit(other);
        }
    }
}
