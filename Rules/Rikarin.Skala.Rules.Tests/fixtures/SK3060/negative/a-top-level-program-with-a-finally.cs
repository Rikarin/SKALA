// ⚠ [#314]. The other half of `a-top-level-program.cs`: giving the compilation unit a body has to
// make the rule *right* about a top-level program, not merely loud about one. `Between<>` walks from
// the release up to the compilation unit, meets the `finally` on the way, and the finding is
// withdrawn exactly as it is inside a method.

using System.Threading;

var gate = new object();
var balance = 0;

Monitor.Enter(gate);
try {
    balance = checked(balance + 1);
} finally {
    Monitor.Exit(gate);
}

System.Console.WriteLine(balance);
