using System;

public sealed class Overloads {
    // ⚠ `"second"` names no parameter of the overload it sits in, so it is the defect. It is still
    // not reported: nothing in scope resembles it, so the rule cannot say whether `first` was meant
    // or the argument was copied from somewhere else entirely, and a finding it cannot repair is
    // one `skala fix` would leave standing. rules.json § falsePositives records the trade.
    public void Write(string first) {
        if (first is null) {
            throw new ArgumentNullException("second");
        }
    }

    public void Write(int second) {
        if (second < 0) {
            throw new ArgumentOutOfRangeException("second", second, "must not be negative");
        }
    }
}
