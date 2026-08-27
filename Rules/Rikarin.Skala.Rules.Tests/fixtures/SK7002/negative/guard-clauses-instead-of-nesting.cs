// rules.json's SK7002 `good` example, at full size: the same work as `deeply-nested.cs` written with
// early exits. Cognitive complexity 6 against that fixture's 21, and nothing else about it changed.
public sealed class GuardClausesInsteadOfNesting {
    public static int Walk(int[] values, bool a, bool b, bool c, bool d, bool e) {
        if (!a) {
            return 0;
        }

        var total = 0;
        foreach (var value in values) {
            if (!b || !c) {
                continue;
            }

            if (d && e) {
                total += value;
            }
        }

        return total;
    }
}
