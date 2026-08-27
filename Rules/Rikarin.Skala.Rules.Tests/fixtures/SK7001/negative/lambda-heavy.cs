// ⚠ A lambda is its own control-flow graph in Roslyn, so its branches are not the containing
// method's cyclomatic complexity. Eight branching lambdas leave this method at 1 — and the cost is
// not lost, because cognitive complexity charges each of them a nesting increment.
using System;

public sealed class LambdaHeavy {
    public static Func<int, int>[] Build() =>
        new Func<int, int>[] {
            x => x > 0 ? 1 : 0, x => x > 1 ? 1 : 0, x => x > 2 ? 1 : 0, x => x > 3 ? 1 : 0, x => x > 4 ? 1 : 0,
            x => x > 5 ? 1 : 0, x => x > 6 ? 1 : 0, x => x > 7 ? 1 : 0
        };
}
