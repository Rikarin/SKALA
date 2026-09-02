// ⚠ [#314]. `Monitor.Enter` with no `try`/`finally` in a top-level program is the same defect as
// `monitor-enter-without-a-try.cs`, and the rule was silent on it: `Body` walked from the invocation
// to the compilation unit without meeting a function and returned null, so the enter was declined
// before any of the rule's own logic ran.
//
// ⚠ Its silence was deliberately never asserted, because pinning a recorded gap turns it into a
// promise. This file is the other half of that: the behaviour is asserted now that it is right.
//
// A top-level program is the shape a model writes first, which is what makes this worth more than
// one fixture.

using System.Threading;

var gate = new object();
var balance = 0;

Monitor.Enter(gate);
balance = checked(balance + 1);
Monitor.Exit(gate);

System.Console.WriteLine(balance);
