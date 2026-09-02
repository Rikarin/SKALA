using System.Threading;

// ⚠ #307 named `string` as the one overlap with `CA2002`; the re-probe found four more. `Thread`,
// `MemberInfo`, `ParameterInfo` and anything deriving from `MarshalByRefObject` are all weak
// identity, all reachable through shape 2's private mutable field, and `CA2002` fires on each —
// measured on a `Thread` field and a `MemberInfo` field in the same probe.
//
// This is what makes the exclusion a type test up the base chain rather than one `string` clause.
public sealed class Pump {
    Thread gate = new(static () => { });

    int hits;

    public void Touch() {
        lock (gate) {
            hits++;
        }
    }

    public void Rotate() => gate = new Thread(static () => { });
}
