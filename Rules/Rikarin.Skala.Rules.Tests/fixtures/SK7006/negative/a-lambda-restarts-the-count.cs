// ⚠ rules.json's SK7006: "A lambda body restarts the count, because a lambda is a separate reading
// context." Three levels, then a lambda, then five more — nine levels of indentation and a measured
// depth of five.
using System;

public sealed class ALambdaRestartsTheCount {
    public static int Count(bool[] flags, Func<Func<int>, int> run) {
        var total = 0;
        if (flags[0]) {
            if (flags[1]) {
                if (flags[2]) {
                    total = run(() => {
                        var inner = 0;
                        if (flags[0]) {
                            if (flags[1]) {
                                if (flags[2]) {
                                    if (flags[3]) {
                                        if (flags[4]) {
                                            inner++;
                                        }
                                    }
                                }
                            }
                        }
                        return inner;
                    });
                }
            }
        }

        return total;
    }
}
